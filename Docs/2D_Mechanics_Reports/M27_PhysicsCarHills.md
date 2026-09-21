# M27: Longitudinal Newtonian Dynamics & Airborne Attitude Stabilization (Physics Car Hills)

## 1. Mechanics Overview
The **Physics Car Hills** mechanic (inspired by 2D physics vehicular games such as *Hill Climb Racing*) presents a continuous longitudinal terrain-traversal challenge. The player controls a twin-axle vehicle equipped with an independent two-point suspension across an undulating, analytical terrain profile stretching $L = 2400\text{ m}$ ($X \in [-200, 2200]$). Driving inputs comprise throttle (`BtnGas`, Key $D$) and braking/reverse (`BtnBrake`, Key $A$). In airborne phases, throttle and brake modulate pitch attitude through active air torque. The goal is to reach the finish banner at $X_{\text{finish}} = 2200\text{ m}$ without suffering catastrophic roof roll-overs or structural flips.

### Core Mechanics & Obstacles:
- **Continuous Newtonian Physics**:
  - Twin suspension hubs (Rear and Front) with spring-damper compliance ($k_{\text{spring}} = 380$, $c_{\text{damp}} = 22$).
  - All-Wheel Drive (AWD): $60\%$ rear axle torque, $40\%$ front axle torque ($F_{\text{engine}} = 480$ N, $V_{\text{max}} = 380$ px/s).
  - Normal-dependent ground adhesion: wheels generate traction only when the vehicle chassis is upright ($\vec{u} \cdot \vec{n} > 0.1$).
- **Airborne Attitude Dynamics**:
  - Flight detection: $\text{IsAirborne} \iff \neg\text{rearGrounded} \land \neg\text{frontGrounded}$.
  - Throttle torque in flight: $+420^{\circ}/\text{s}^2$ (counter-clockwise pitch, nose-up / backflip).
  - Brake torque in flight: $-420^{\circ}/\text{s}^2$ (clockwise pitch, nose-down / frontflip).
- **Lethal Crash Invariants**:
  - Roof strike: $Y_{\text{roof}} \le Y_{\text{terrain}}(X_{\text{roof}}) + 4$ px.
  - Excessive roll angle: $|\theta| > 76^{\circ}$ with chassis ground impact.
  - Abyss excursion: $Y_{\text{chassis}} < Y_{\text{terrain}}(X) - 35$ px.
  - Crash penalty: $1.35$ s lock-out with rollback respawn to last safe grounded checkpoint.
- **Victory Condition**: Front bumper physically intersects $X \ge 2200$ within the flag vertical clearance ($Y \in [Y_{\text{ground}} - 15, Y_{\text{ground}} + 60]$).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M27_PhysicsCarHills` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnGas` | `UrdtUiButtonTarget` | ScreenCenter $(1154, 112)$, throttle actuation via `pointerDown`/`pointerUp` |
| `BtnBrake` | `UrdtUiButtonTarget` | ScreenCenter $(126, 112)$, braking and pitch-down stabilization |
| `SpeedometerText` | `UrdtUiTextTarget` | Telemetry string `"Скорость: V км/ч [В ВОЗДУХЕ!]"` |
| `DistanceText` | `UrdtUiTextTarget` | Progress string `"Дистанция: D / 2400 м"` |
| `InstructionText` | `UrdtUiTextTarget` | Dynamic crash warning and recovery state feedback |

---

## 3. Mathematical Terrain Profile & Attitude Control Law

The terrain follows a continuous analytical curve:
$$Y_{\text{terrain}}(x) = Y_{\text{base}} + \left[ \sum_{i=1}^3 A_i \sin(\omega_i x + \phi_i) + \sum_{j=1}^3 R_j \exp\left(-\left(\frac{x - X_j}{\sigma_j}\right)^2\right) \right] \cdot B(x)$$
Where $B(x)$ smoothly ramps down slopes at the starting grid and terminal finish plateau.

### Attitude Control Law:
To prevent vehicle inversion over the three high-speed launch ramps ($x \in \{520, 1120, 1720\}$), the input policy is defined piecewise:
$$u(t) = \begin{cases} \text{Throttle ON} & \text{if } \neg\text{IsAirborne} \land \neg\text{IsCrashed} \\ \text{Throttle OFF} \land \text{Brake Pulse} & \text{if } \text{IsAirborne} \\ \text{Neutral} & \text{if } \text{IsCrashed} \end{cases}$$

Releasing throttle upon airborne detection immediately neutralizes the $+420^{\circ}/\text{s}^2$ nose-up air torque. Applying brief braking pulses injects negative angular acceleration, forcing the chassis to descend parallel to the descending landing slope.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Telemetry Initialization**:
   - Locate `BtnGas` $(1154, 112)$ and `BtnBrake` $(126, 112)$.
   - Establish baseline query polling at $\Delta t = 100-150$ ms.

2. **Longitudinal Control Loop**:
   - While `IsCompleted == false`:
     1. Read `SpeedometerText` and `InstructionText`.
     2. If `"КРУШЕНИЕ"` in text:
        - Ensure `pointerUp(BtnGas)`.
        - Await respawn duration ($600$ ms).
     3. If `"[В ВОЗДУХЕ!]"` in text:
        - Release gas immediately: `pointerUp(BtnGas)`.
        - Dispatch stabilization tap: `click(BtnBrake)`.
     4. Else (Vehicle Grounded):
        - Engage throttle: `pointerDown(BtnGas)`.
     5. Poll `M27_PhysicsCarHills.ProgressNormalized`.

3. **Finish Verification**:
   - Front bumper impacts finish post at $2200$ m.
   - Confirm `IsCompleted == true` and `ProgressNormalized == 1.0`.
   - Release all active pointers.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Latched PointerDown During Crash**: If `pointerDown` remains asserted after a crash, the respawned vehicle immediately wheelies upon respawn, resulting in an infinite crash loop. Pointers must be cleared when a crash string is detected.
- **Trap: Holding Gas on Jump Crests**: High-velocity launches off ramps induce rapid pitch rotation. Failure to release throttle within $150$ ms of leaving the ground reliably induces a lethal roof collision.
- **Trap: Overshooting into the Stratosphere**: Excess speed combined with extreme pitch-up can loft the car far above the finish flag height ($Y > Y_{\text{flag}} + 60$), passing the $X = 2200$ mark without triggering the finish sensor. Proper pitch leveling ensures landing within the valid flag contact envelope.
