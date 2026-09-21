# M20: Harmonic Pendulum Release & Structural Stability (Vertical Stacking)

## 1. Mechanics Overview
The **Vertical Stacking** mechanic simulates crane-based payload placement, tower building, cargo container loading, or modular masonry assembly (e.g. Tower Bloxx, Stacker, crane hook drop). A suspension arm swings horizontally with sinusoidal harmonic motion across a fixed pivot:
$$X(t) = A \cdot \sin(\omega t)$$
Carrying a structural block at elevation $Y_{\text{arm}} = 65$. The player must trigger a release action (`click` on `BtnDrop`) precisely when the horizontal coordinate of the swinging block aligns with the top surface of the accumulated tower ($|X - X_{\text{top}}| \le \delta$). Upon release, the block falls under downward gravity ($V_{\text{fall}} = 450$ px/s) onto the base platform or preceding block. The objective is to assemble a stable vertical stack of $N = 3$ blocks.

### Core Mechanics & Obstacles:
- **Harmonic Motion Parameters**:
  - Amplitude $A = 140$ px.
  - Angular frequency $\omega = 2.4$ rad/s.
  - Oscillation period: $T = \frac{2\pi}{\omega} \approx 2.618$ s ($T/2 \approx 1309$ ms).
  - Maximum linear velocity at equilibrium: $V_{\max} = \omega A = 336$ px/s.
- **Variable Drop Elevations**:
  - Base platform $Y_0 = -85$. Block height $H = 45$ px.
  - Block 1 fall distance: $150$ px ($T_{\text{fall}} \approx 333$ ms).
  - Block 2 fall distance: $105$ px ($T_{\text{fall}} \approx 233$ ms).
  - Block 3 fall distance: $60$ px ($T_{\text{fall}} \approx 133$ ms).
- **Cracked / Defective Block Hazard (`Junk Block`)**: Block #2 is a cracked/unstable variant marked with `[X]`. Its stability acceptance window is tightened by $40\%$ ($\delta_{\text{junk}} = 21$ px vs standard $\delta = 35$ px).
- **Non-Destructive Retry State**: If a drop fails ($|X - X_{\text{top}}| > \delta$), the block falls off or skids away, but the existing stack is preserved ($S_{\text{count}}$ is not decremented). The crane immediately reloads, allowing incremental refinement.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M20_VerticalStacking` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnDrop` | `UrdtUiButtonTarget` | ScreenCenter $(640, 215)$, trigger command `click` |
| `StackText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Блоков в башне: S / 3"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, exposes numerical landing offset: `"Смещение Xpx превысило допуск (Tpx)"` or `"Точная посадка (смещение Xpx)!"` |

---

## 3. Mathematical Phase Sweep & ReAct Feedback Formulation

Because `LocalInstruction` returns the exact numerical landing error $|X_{\text{drop}} - X_{\text{top}}|$:
$$\Delta X = |140 \sin(\omega t_{\text{drop}}) - X_{\text{top}}|$$
An agent can exploit harmonic phase scanning.

### Phase Discretization:
The tolerance window for the narrowest block ($\delta = 21$ px) corresponds to an angular aperture:
$$\Delta \theta_{\text{tol}} = 2 \arcsin\left(\frac{21}{140}\right) = 2 \arcsin(0.15) \approx 0.301\text{ rad}$$
In terms of time:
$$\Delta t_{\text{window}} = \frac{\Delta \theta_{\text{tol}}}{\omega} = \frac{0.301}{2.4} \approx 0.125\text{ s (125 ms)}$$
For standard blocks ($\delta = 35$ px):
$$\Delta t_{\text{window}} = \frac{2 \arcsin(0.25)}{2.4} \approx 0.211\text{ s (211 ms)}$$

### Linear Phase Step Guarantee:
By stepping the release delay by $\Delta t_{\text{step}} = 85$ ms between consecutive attempts ($85\text{ ms} < 125\text{ ms}$):
$$\theta_{k+1} = \theta_k + \omega \Delta t_{\text{step}} = \theta_k + 0.204\text{ rad}$$
Because the step size is strictly smaller than the tolerance window ($85\text{ ms} < \Delta t_{\text{window}}$), the sequence of attempts is mathematically guaranteed to intersect the acceptance window within at most:
$$K_{\max} = \left\lceil \frac{T / 2}{\Delta t_{\text{step}}} \right\rceil = \left\lceil \frac{1309}{85} \right\rceil = 16\text{ attempts}$$
In practice, Block 2 was placed with an offset of only $6$ px at step 9, and Block 3 was placed at step 3.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Stack Height Initialization**:
   - Inspect `StackText` to determine current tower elevation $S \in \{0, 1, 2\}$.

2. **Phase Sweep Stacking Loop**:
   - For each block $S + 1 \le 3$:
     - Initialize phase shift accumulator: $\Delta \tau = 0$ ms.
     - While stack count has not incremented:
       1. Sleep baseline settling time: $500\text{ ms} + \Delta \tau$.
       2. Issue `click(BtnDrop)`.
       3. Sleep fall duration: $T_{\text{fall}}(S) = 380 - (S \times 100)$ ms.
       4. Inspect `StackText` and `LocalInstruction`.
       5. If `StackText` incremented:
          - Settle for $800$ ms.
          - Reset $\Delta \tau = 0$ ms.
          - Break to next block.
       6. Else (landing error):
          - Increment phase accumulator: $\Delta \tau = (\Delta \tau + 85\text{ ms}) \pmod{1300\text{ ms}}$.
          - Sleep $300$ ms before next attempt.

3. **Status Verification**:
   - Confirm `M20_VerticalStacking.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Fixed Delay Trap (Phase Synchronization)**: Retrying a drop with a constant inter-drop interval (e.g. exactly 500 ms) can easily lock into an anti-phase limit cycle where every attempt drops at the same non-zero offset (e.g. 98 px). Dynamically stepping the phase ($\Delta \tau = \Delta \tau + 85$ ms) is required to sweep across the full sinusoidal wave.
- **Trap: Double-Clicking during Fall**: Clicking `BtnDrop` while `_isFalling == true` is completely ignored by the game logic. The agent must respect the physics fall time before issuing subsequent drop commands.
- **Trap: Reduced Tolerance on Defect Layers**: Recognizing that specific layers (such as cracked block #2) exhibit smaller tolerances ($21$ px vs $35$ px) dictates choosing a fine phase step ($\le 85$ ms) rather than a coarse step.
