# M12: Rotary Dial & Continuous Angular Delta Tracking (Rotary Encoder)

## 1. Mechanics Overview
The **Angular Delta Tracker** mechanic models rotational dials, steering wheels, knobs, or pipe valves where continuous rotational displacement around a central pivot must be accumulated until a specified angular revolution target (e.g., $720^\circ = 2.0$ full revolutions) is achieved.

### Core Mechanics & Obstacles:
- **Angular Integration Around Center**: Each input frame computes the angle between pointer position $\vec{P}$ and wheel center $\vec{C}$:
  $$\theta_t = \text{atan2}(P_y - C_y, P_x - C_x)$$
  $$\Delta \theta = \text{DeltaAngle}(\theta_{t-1}, \theta_t)$$
- **Cumulative Revolution Target**: Total rotation accumulates via $|\Delta \theta|$ until $\sum |\Delta \theta| \ge 720^\circ$.
- **Jammed / Decoy Knobs**: A decoy wheel (`Valve_Jammed_Junk`) marked with 'X' is locked in place and registers no rotational delta, triggering jam error warnings if touched.
- **Delta Clamping Filter**: Displacements exceeding $90^\circ$ per frame are discarded as pointer teleportation artifacts. Smooth continuous circular sampling is mandatory.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M12_AngularDeltaTracker` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Valve_Active` | `Urdt2DInteractiveAreaTarget` / Entity | Pivot center $(x_0, y_0) = (520, 320)$, interactable |
| `Valve_Jammed_Junk` | `Urdt2DInteractiveAreaTarget` / Entity | Decoy valve $(760, 320)$ (`IsJammed: true`, ignore) |

---

## 3. Parametric Circular Path Generation & Dispatch

To generate an unbroken rotary gesture around pivot $(x_0, y_0) = (520, 320)$ with radius $R = 60$ px over $N_{\text{rev}}$ revolutions:
$$\begin{cases}
X_k = x_0 + R \cos\left(\frac{2\pi \cdot N_{\text{rev}} \cdot k}{K}\right) \\
Y_k = y_0 + R \sin\left(\frac{2\pi \cdot N_{\text{rev}} \cdot k}{K}\right)
\end{cases} \quad \text{for } k = 0, 1, \dots, K$$

To ensure each intermediate delta $\Delta \theta < 90^\circ$ (staying safely within the $15^\circ - 20^\circ$ window):
$$\Delta \theta_{\text{segment}} = \frac{360^\circ \cdot N_{\text{rev}}}{K} \approx 12^\circ \implies \text{Set } K \ge 30 \times N_{\text{rev}}$$

For $N_{\text{rev}} = 3.0$ revolutions, $K = 96$ points guarantees smooth, loss-free accumulation of over $1000^\circ$, easily satisfying the $720^\circ$ requirement in a single continuous `press_move` command.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Active Pivot Identification**:
   - Query all rotary targets.
   - Select `Valve_Active` and read `ScreenCenter = (520, 320)`.
   - Strictly ignore `Valve_Jammed_Junk`.

2. **Circular Path Synthesis**:
   - Generate $K = 96$ circular points around `(520, 320)` with radius $R = 60$ px covering $3.0$ full rotations ($1080^\circ$).

3. **Motion Dispatch (`press_move`)**:
   - Issue `press_move(circlePath, stepsPerSegment=4)`.
   - The continuous drag invokes `IDragHandler` on each interpolated sub-frame, integrating the angular delta without pointer release.

4. **Status Confirmation**:
   - Wait $300$ ms for the final rotation threshold event.
   - Confirm `ProgressNormalized == 1.0` and `IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Linear Swiping Across the Dial**: Swiping straight left-to-right across the center yields chaotic or cancelling positive/negative deltas ($\Delta \theta \approx 0$). A true circular orbit enclosing the pivot is mathematically required.
- **Trap: Large Angular Jumps (>90°)**: Using too few points causes $\Delta \theta > 90^\circ$, triggering the wheel's anti-teleport filter (`if (Mathf.Abs(delta) < 90f)`). Always sample at least 24-32 points per full revolution.
