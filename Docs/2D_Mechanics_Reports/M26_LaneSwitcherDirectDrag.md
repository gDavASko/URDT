# M26: Continuous Direct Drag Interception (Lane Switcher: Direct Drag)

## 1. Mechanics Overview
The **Lane Switcher Direct Drag** mechanic evaluates an AI agent's capacity for continuous horizontal guidance and direct spatial positioning under dynamic vertical hazard and reward flows. While built on the three-lane runner model ($x \in \{-140, 0, +140\}$ px; screen coordinates $X \in \{500, 640, 780\}$ px), `RunnerControlMode.DirectDrag` decouples avatar movement from stepped button clicks or discrete taps. Instead, the player avatar directly locks to the pointer's horizontal projection, moving continuously across the track width.

### Core Mechanics & Obstacles:
- **Continuous Pointer Tracking**:
  - Pointer down on the play area binds the avatar's horizontal center to the cursor $X$.
  - Position clamping: $X_{\text{avatar}} \in [500 - 25, 780 + 25] = [475, 805]$ px.
  - On release (`OnPointerUp`), the avatar magnetically snaps to the nearest discrete lane center ($L \in \{0, 1, 2\}$).
- **Collision Envelope**:
  - Direct Drag collision uses continuous Euclidean proximity:
    $$|X_{\text{item}} - X_{\text{player}}| < 45\text{ px} \quad \land \quad |Y_{\text{item}} - Y_{\text{player}}| < 35\text{ px}$$
- **Obstacle & Reward Streams**:
  - Collectible Coins: Spawn rate $1.6$ s, falling speed $V_y = 160$ px/s, value $+1$ coin.
  - Spiked Barriers: Spawn probability $P = 0.65$ in an alternate lane, penalty $-1$ coin.
  - Quota: Accumulate $S = 5$ net coins ($100\%$ completion).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M26_LaneSwitcherDirectDrag` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ScoreText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Собрано: S / 5"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback |
| `SpawnContainer` | `Urdt2DSlotTarget` | ScreenCenter $(640, 330)$, parent container |
| `PlayerAvatar` | Dynamic Entity | Transform tracked at $Y_{\text{player}} \approx 230$ |

---

## 3. Mathematical Drift & Continuous Positioning Dynamics

In contrast to discrete lane steps, direct dragging allows the agent to hold arbitrary intermediate positions or sweep horizontally across all three lanes during a single continuous drag interaction:
$$X(t) = \text{Lerp}(X_{\text{start}}, X_{\text{target}}, t)$$

### Expected Value Drift Analysis:
Let our avatar dwell at any chosen lane position $L$.
- Coin spawn: $P(\text{Coin in } L) = 1/3 \approx 0.333 \implies \Delta S = +1$.
- Barrier spawn: $P(\text{Barrier in } L) = \frac{2}{3} \times 0.65 \times \frac{1}{2} \approx 0.216 \implies \Delta S = -1$.
$$\mathbb{E}[\Delta S / \text{wave}] = 0.333 - 0.216 = \mathbf{+0.117} > 0$$

Because the expected score delta is strictly positive, dwelling in high-probability corridors while executing targeted horizontal drags upon coin arrival guarantees rapid convergence to the target quota $S = 5$.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Center Alignment**:
   - Establish initial anchor at center lane:
     `drag({ from: { x: 500, y: 240 }, to: { x: 640, y: 240 }, steps: 6 })`
   - Monitor `ScoreText` and `Urdt2DModuleTarget.ProgressNormalized`.

2. **Continuous Drag Repositioning**:
   - When shifting to adjacent lanes:
     - To Left Lane: `drag({ from: { x: 640, y: 240 }, to: { x: 500, y: 240 }, steps: 8 })`
     - To Right Lane: `drag({ from: { x: 640, y: 240 }, to: { x: 780, y: 240 }, steps: 8 })`
   - Releasing the pointer locks the avatar into the target lane through automatic magnetic snapping.

3. **Autonomous Quota Resolution**:
   - Poll progress via URDT inspect/query at $1.0$ s intervals.
   - Once `Score == 5 / 5` and `ProgressNormalized == 1.0`, confirm `IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Vertical Drag Wandering**: The mechanic only consumes horizontal displacement ($\Delta X$). If the agent accidentally introduces large $\Delta Y$ during drag, pointer raycasts may slip outside the parent container bounds, prematurely canceling the drag gesture.
- **Trap: Rapid Jitter Input**: Submitting sub-frame drag events can cause the player avatar to rapidly oscillate between lanes, intersecting both a coin and a hazard simultaneously. Smooth multi-step interpolation ($\text{steps} \ge 6$) is required.
- **Trap: Over-Correction After Hazard**: When a barrier hit is registered, the penalty text persists in `LocalInstruction` until a new event occurs. Do not interpret persistent text as continuous damage.
