# M25: Screen-Partitioned Reactive Evasion (Lane Switcher: Half Taps)

## 1. Mechanics Overview
The **Lane Switcher Tap Halves** mechanic tests an agent's ability to navigate discrete runner corridors using binary screen partitioning (left-half vs. right-half click zones). The player controls an avatar situated across three parallel discrete vertical lanes: Left ($x = -140$ px, screen $x \approx 500$), Center ($x = 0$ px, screen $x \approx 640$), and Right ($x = +140$ px, screen $x \approx 780$). Collectible coins and hazard barriers scroll down continuously from $Y_{\text{start}} = +160$ to $Y_{\text{player}} = -90$ at velocity $V_y = 160$ px/s. The objective is to gather $N = 5$ gold coins while evading lethal spiked obstacles.

### Core Mechanics & Obstacles:
- **Binary Screen Partitioning**:
  - Center boundary: $X_{\text{center}} = 640$ px.
  - Left Zone ($X < 640$ px): Decrements current lane index ($\text{Lane} \leftarrow \max(0, \text{Lane} - 1)$).
  - Right Zone ($X > 640$ px): Increments current lane index ($\text{Lane} \leftarrow \min(2, \text{Lane} + 1)$).
- **Spawn Kinematics & Collision Geometry**:
  - Spawn cadence: $T_{\text{spawn}} = 1.6$ s.
  - Vertical travel duration from spawn to avatar contact: $\Delta t = (160 - (-90)) / 160 = 1.5625$ s.
  - Collision window: $|Y_{\text{item}} - Y_{\text{player}}| < 35$ px ($\approx 0.43$ s dwell window).
- **Score Dynamics**:
  - Coin Capture: $+1$ coin ($\Delta S = +1$).
  - Barrier Impact: $-1$ coin penalty ($\Delta S = -1$), clamped at $0$.
  - Win Condition: Reaching $S = 5$ net coins ($100\%$ progress).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M25_LaneSwitcherTapHalves` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ScoreText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Собрано: S / 5"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback |
| `SpawnContainer` | `Urdt2DSlotTarget` | ScreenCenter $(640, 330)$, parent container |
| Screen Tap Left | Device Click | Coordinate $(500, 320)$ (Left half of Canvas) |
| Screen Tap Right | Device Click | Coordinate $(780, 320)$ (Right half of Canvas) |

---

## 3. Mathematical State Transitions & Control Scheme

Let lane indices be $L \in \{0, 1, 2\}$, where $L=0$ is Left ($x = 500$), $L=1$ is Center ($x = 640$), and $L=2$ is Right ($x = 780$).
State update on click input:
$$\text{Click}(X, Y) \implies \begin{cases} L_{t+1} = \max(0, L_t - 1) & \text{if } X < 640 \\ L_{t+1} = \min(2, L_t + 1) & \text{if } X > 640 \end{cases}$$

### Lane Evasion & Convergence Dynamics:
At each spawn wave $k$:
- Exactly 1 coin spawns uniformly at random: $L_{\text{coin}} \in \{0, 1, 2\}$.
- With probability $P = 0.65$, exactly 1 barrier spawns in an alternative lane: $L_{\text{barrier}} \in \{0, 1, 2\} \setminus \{L_{\text{coin}}\}$.
- At least one lane is guaranteed free of hazards at all times.
- By tracking score delta and feedback text, the agent maintains an optimal lane occupation policy without needing direct vision.

---

## 4. General Gameplay Algorithm for AI Agents

1. **State Calibration & Baseline Lane Tracking**:
   - Initialize internal lane tracker $L_{\text{agent}} = 1$ (Center).
   - Target coin count $N_{\text{target}} = 5$.
   - Monitor `ScoreText` and `Urdt2DModuleTarget.ProgressNormalized`.

2. **Discrete Control Dispatch**:
   - To shift left: dispatch `click({ x: 500, y: 320 })`, decrement $L_{\text{agent}} \leftarrow \max(0, L_{\text{agent}} - 1)$.
   - To shift right: dispatch `click({ x: 780, y: 320 })`, increment $L_{\text{agent}} \leftarrow \min(2, L_{\text{agent}} + 1)$.
   - Maintain minimum cooldown between taps ($t_{\text{cool}} \ge 180$ ms) to prevent over-shooting.

3. **Autonomous Progression Verification**:
   - Continuously poll `Urdt2DModuleTarget` via URDT WebSocket.
   - Upon detecting $S \ge 5$ or `IsCompleted == true`:
     - Halt input.
     - Confirm module completion.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Tapping on UI Overlays**: Clicks dispatched near the bottom toolbar ($y < 70$) or top header ($y > 600$) can inadvertently trigger global navigation buttons (`BtnPrev`, `BtnNext`, `BtnPlayMainMenu`). Taps must be centered vertically around $Y \approx 320$.
- **Trap: Double-Tap Queueing**: Unlike continuous drag mechanics, discrete tap inputs buffer instantly. Sending rapid double-clicks shifts two full lanes in $< 100$ ms, potentially moving into an outer barrier.
- **Trap: Premature Advance**: Ensure `IsCompleted == true` and `ProgressNormalized == 1.0` are registered before shifting the suite window.
