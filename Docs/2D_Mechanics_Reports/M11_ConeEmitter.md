# M11: Directional Spray Cone & Continuous HP Degradation (Cone Emitter)

## 1. Mechanics Overview
The **Cone Emitter** mechanic tests rotational aim and continuous beam/stream guidance (e.g. fire extinguisher, flashlight, spray nozzle, or laser beam). The player holds down a pointer gesture to activate an active stream emitting from a fixed origin nozzle within a designated angular dispersion sector ($\theta_{\text{half}} = 18^\circ$, max range $R = 480$ px). The stream applies continuous damage-over-time ($\text{DPS} = 65\text{ HP/s}$) to extinguish active fire targets ($100$ HP each).

### Core Mechanics & Obstacles:
- **Rotational Aiming from Origin**: Aim direction is determined by $\vec{D} = \text{PointerPos} - \text{NozzlePos}$.
- **Target Verification in Sector**: A target receives damage if and only if:
  $$\text{dist}(\text{Target}, \text{Nozzle}) \le R_{\max} \quad \text{and} \quad \angle(\vec{D}, \vec{D}_{\text{target}}) \le \theta_{\text{half}}$$
- **Hazard Obstacle (Electric Box)**: An electric panel sits between the nozzle and fire targets along the central axis ($y = 0$). Allowing the stream to bathe the electric panel triggers warnings or shock penalties.
- **Simultaneous Target Overlap**: Targets positioned closely along the same ray (e.g. Fire 1 and Fire 3) fall within the same cone sector and can be damaged simultaneously.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M11_ConeEmitter` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Nozzle` | Transform | Origin coordinates $(x_0, y_0) = (450, 320)$ |
| `FireTarget_*` | `Urdt2DInteractiveAreaTarget` / Entity | Screen coordinates, HP state |
| `Obstacle_ElectricBox` | Transform | Hazard coordinates $(720, 320)$ (Central axis) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Geometry Mapping**:
   - Nozzle: $N = (450, 320)$.
   - Hazard: $H = (720, 320)$ (Avoid aiming near $y = 320$).
   - Upper Fire Cluster: Fire 1 at $(800, 415)$, Fire 3 at $(870, 405)$ (Elevation $+15^\circ$ to $+18^\circ$).
   - Lower Fire Target: Fire 2 at $(800, 225)$ (Depression $-15^\circ$).

2. **Sequential Aim & Dwell Execution**:
   - **Step 1: Upper Cluster**:
     - Aim above the hazard at $P_{\text{up}} = (800, 425)$.
     - Dispatch `pointer_down(P_{\text{up}})`.
     - Dwell for $\approx 1900$ ms. Because the cone sector covers both Fire 1 and Fire 3, both targets extinguish in a single pass.
     - Dispatch `pointer_up()`.
   - **Step 2: Lower Target (Bypassing Hazard)**:
     - Aim below the hazard directly at $P_{\text{down}} = (800, 215)$.
     - Dispatch `pointer_down(P_{\text{down}})`.
     - Dwell for $\approx 1900$ ms to deplete Fire 2's $100$ HP.
     - Dispatch `pointer_up()`.

3. **Status Verification**:
   - Check `Urdt2DModuleTarget.ProgressNormalized == 1.0` and `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Direct Line Drag Across Center**: Dragging the pointer continuously from the top cluster $(800, 425)$ to the bottom target $(800, 215)$ sweeps the cone directly across the central electric box $(720, 320)$, triggering penalties. Releasing the pointer or arcing wide around the right perimeter prevents accidental hazard contact.
- **Trap: Undershooting Dwell Time**: Each target has $100$ HP and takes $\approx 1.54$ s to extinguish at $65$ DPS. Releasing before $1.6$ s leaves residual HP and fails completion.
