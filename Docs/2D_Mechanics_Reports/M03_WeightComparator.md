# M03: Mass Balancing & Weight Comparator

## 1. Mechanics Overview
The **Weight Comparator** mechanic tests physics-based and threshold-based equilibrium. The player must balance a two-pan lever scale by dragging weights onto the active pan until the total mass matches the counterweight exactly (within a defined tolerance, e.g., $\pm 0.5$ kg).

### Core Mechanics & Obstacles:
- **Numerical Mass Addition**: Different weights have specific mass attributes (e.g. 10 kg, 5 kg, 3 kg). The goal is to reach an exact sum ($10 + 5 = 15$ kg).
- **Decoy / Junk Mass**: A junk weight (`Weight_Junk`) with zero or deceptive mass that prevents balance.
- **Physical Inertia & Stabilization Period**: Once the exact mass is placed, the beam oscillates. The player must hold the balanced state continuously for a stabilization duration (e.g., $1.0$ second) before completion is registered.

---

## 2. Perception & URDT Beacon Architecture
The AI perceives the scale and weights via the following beacons:

| Target Name | Component Type | Key Properties for Decision Making |
| :--- | :--- | :--- |
| `M03_WeightComparator` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `LeftPan` | `Urdt2DSlotTarget` | `CurrentMass`, `ScreenCenter` (Reference / Counterweight pan) |
| `RightPan` | `Urdt2DSlotTarget` | `CurrentMass`, `ScreenCenter` (Target receptacle pan) |
| `Weight_10kg` | `Urdt2DDraggableTarget` | `ItemId: 'Weight_10kg'`, `IsJunk: false`, `ScreenCenter` |
| `Weight_5kg` | `Urdt2DDraggableTarget` | `ItemId: 'Weight_5kg'`, `IsJunk: false`, `ScreenCenter` |
| `Weight_3kg` | `Urdt2DDraggableTarget` | `ItemId: 'Weight_3kg'`, `IsJunk: false` (Surplus weight) |
| `Weight_Junk` | `Urdt2DDraggableTarget` | `IsJunk: true` (Defective weight) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Mass Extraction & Target Evaluation**:
   - Parse instruction or module target: Target mass $= 15$ kg on `RightPan`.
   - Filter available weights, discarding any item with `IsJunk == true`.

2. **Subset Sum Solver**:
   - Solve the subset sum problem to determine which combination of weights equals the target mass ($10\text{ kg} + 5\text{ kg} = 15\text{ kg}$).

3. **Drag & Drop Placement**:
   - Drag `Weight_10kg` from its `ScreenCenter` to `RightPan.ScreenCenter`.
   - Wait 250ms for pan physics to register.
   - Drag `Weight_5kg` from its `ScreenCenter` to `RightPan.ScreenCenter`.

4. **Inertia & Stabilization Wait**:
   - When the balanced state is entered, do not trigger further inputs.
   - Wait for the stabilization duration ($1.0 - 1.5$ seconds) while monitoring `ProgressNormalized`.
   - The beam smoothly rotates to 0° and `IsCompleted` transitions to `true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Premature Module Exit**: Assuming completion immediately upon dropping the last weight. Physics scales require stabilization time. Always poll `IsCompleted` or wait for the host's auto-advance.
- **Trap: Overloading / Underloading**: Adding all weights naively ($10 + 5 + 3 = 18$ kg) tilts the scale to max tilt angle and resets the stabilization timer. The subset sum must be precise.
