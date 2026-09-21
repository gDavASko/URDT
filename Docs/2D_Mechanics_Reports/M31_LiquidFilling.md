# M31: Sequential Volumetric Dispensing & Dwell Containment (Liquid Filling)

## 1. Mechanics Overview
The **Liquid Filling** mechanic models laboratory fluid pipetting, battery acid injection, chemical reagent dispensing, and precision container filling in manufacturing simulations. The interface contains three graduated reaction wells arranged in a horizontal bank: Well 1 $(505, 295)$, Well 2 $(640, 295)$, and Well 3 $(775, 295)$, each equipped with an annular glow collar and percentage readout. The player operates a mobile chemical dispenser (`DispenserTool`) that must be positioned directly above the active well throat ($R_{\text{well}} = 55\text{ px}$) and maintained within boundaries while pouring.

### Core Mechanics & Obstacles:
- **Strict Monotonic Sequence (1 $\to$ 2 $\to$ 3)**:
  - Well filling is strictly ordered. Attempting to pour into Well 2 or 3 before Well 1 is full triggers rejection: `"Заполняйте лунки строго по очереди!"`.
  - Attempting to fill an already completed well displays: `"Лунка N уже полностью заполнена. Перейдите к следующей!"`.
- **Dwell Containment & Volumetric Rate**:
  - Fill rate: $V_{\text{fill}} = 0.48\text{ s}^{-1}$ ($\approx 2.08$ seconds per well at continuous delivery).
  - Containment criterion: $|X_{\text{dispenser}} - X_{\text{well}}| \le 55\text{ px}$.
  - Fluid flow immediately ceases (`_isPouring = false`, stream hidden) if the pointer drifts outside the $55\text{ px}$ collar.
- **Visual & Numeric Telemetry**:
  - `_streamVisual` illuminates with procedural harmonic thickness modulation during active pouring.
  - Active well displays pulsing gold glow ring (`#FFDE33`); completed wells turn neon emerald (`#00FF8C`).
  - Readout displays real-time percent: $0\% \to \dots \to 100\%\ \checkmark$.
- **Win Condition**: Fill all three wells to $100\%$ ($1.0$ overall progress).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M31_LiquidFilling` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `DispenserTool` | Dispenser Transform | Screen position $(X_t, Y_t)$, initial $(505, 390)$ |
| `SocketBg` | Well 1 Base | ScreenCenter $(505, 295)$ |
| `SocketBg_1` | Well 2 Base | ScreenCenter $(640, 295)$ |
| `SocketBg_2` | Well 3 Base | ScreenCenter $(775, 295)$ |
| `PercentText`, `_1`, `_2` | `UrdtUiTextTarget` | Numerical fill telemetry ($0\% \dots 100\%$) |
| `ProgressText` | `UrdtUiTextTarget` | Screen format `"Заполнено лунок: K / 3"` |
| `LocalInstruction` | `UrdtUiTextTarget` | Real-time procedural sequence instructions |

---

## 3. Mathematical Volumetric Kinematics & Scheduling

Let $W_k \in [0, 1]$ denote the fill volume of well $k \in \{0, 1, 2\}$.
Governing differential equation:
$$\frac{dW_k}{dt} = \begin{cases} 0.48 & \text{if } k = k_{\text{active}} \land |X(t) - X_{k}| \le 55 \land \text{PointerDown} \\ 0 & \text{otherwise} \end{cases}$$

Integrated dwell duration per well:
$$\Delta T_k = \int_0^1 \frac{1}{0.48} \, dW = \frac{1}{0.48} \approx \mathbf{2.083\text{ seconds}}$$

Total cumulative progress:
$$\Pi(t) = \frac{k_{\text{active}} + W_{k_{\text{active}}}(t)}{3} \in [0, 1]$$

---

## 4. General Gameplay Algorithm for AI Agents

1. **Well Coordinate Mapping**:
   - Well 1 Target: $(505, 390)$
   - Well 2 Target: $(640, 390)$
   - Well 3 Target: $(775, 390)$

2. **Sequential Dwell Execution Loop**:
   - For each well $k \in \{1, 2, 3\}$:
     1. Dispatch `pointerDown(TargetCoords_k)`.
     2. Monitor `Urdt2DModuleTarget.ProgressNormalized` at $250$ ms intervals.
     3. Await local quota threshold:
        - Well 1: $\Pi \ge 0.333$
        - Well 2: $\Pi \ge 0.666$
        - Well 3: $\Pi \ge 1.000$
     4. Dispatch `pointerUp(TargetCoords_k)`.
     5. Allow $300$ ms transition buffer before activating next well.

3. **Victory Verification**:
   - Confirm `ProgressNormalized == 1.0`.
   - Confirm `M31_LiquidFilling.IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Spatial Overshoot into Adjacent Well Collars**: Wells 1, 2, and 3 are spaced $135$ px apart ($505 \to 640 \to 775$). With well radii of $55$ px, the inter-well gap is only $25$ px. Dragging carelessly between wells can inadvertently trigger out-of-order warning penalties.
- **Trap: Vertical Raycast Drift**: If pointer $Y$ drifts above $Y > 450$ or below $Y < 310$, the dispenser nozzle escapes the vertical raycast hit zone of the parent Canvas, halting fluid flow. Maintaining constant $Y = 390$ ensures optimal continuous dispensing.
- **Trap: Premature Lift-Off**: Releasing the pointer before the internal `_wellFillAmounts` reaches exactly $1.0$ leaves the well in an incomplete state (e.g. $98\%$), preventing sequence progression.
