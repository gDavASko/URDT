# M07: Spring Resistance Wobble & Snap (Extraction Dynamics)

## 1. Mechanics Overview
The **Wobble & Snap** mechanic simulates physical material fatigue and directional extraction under spring/elastic tension (e.g. dental tooth extraction, stubborn peg pulling, or rivet disassembly). The target is restrained by an elastic joint that pulls it back to an anchor center. Direct pulling is blocked until the joint undergoes sufficient angular displacement cycles (wobble) to weaken structural integrity to 100% fatigue, followed by a strong linear extraction gesture.

### Core Mechanics & Obstacles:
- **Prerequisite Tool Attachment**: The object cannot be manipulated directly by hand; a tool (`DentalForceps`) must first be attached to the target.
- **Elastic Return Force**: A restorative spring pulls the object back to $(0,0)$ when pointer motion ceases.
- **Angular Fatigue Accumulation**: Fatigue increases proportionally to the cumulative angular delta ($\Delta \theta$) of the wobble motion around the anchor center:
  $$\Delta \text{Fatigue} = \left(\frac{|\Delta \theta|}{180^\circ}\right) \times \text{Rate}$$
- **Conditional Extraction Gate**: Upward extraction ($\Delta y > \text{Threshold}$) triggers a snap-off only when fatigue $\ge 100\%$.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M07_WobbleAndSnap` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `DentalForceps` | `Urdt2DDraggableTarget` | `ItemId: 'DentalForceps'`, `ScreenCenter` (Initial tool location) |
| `ToothItem` | `Urdt2DDraggableTarget` | `ItemId: 'ToothItem'`, `ScreenCenter` (Target object to extract) |
| `GumSocket` | `Urdt2DSlotTarget` | `SlotId: 'GumSocket'`, `ScreenCenter` (Base restraint) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Tool Engagement**:
   - Locate tool `DentalForceps` and target `ToothItem`.
   - Dispatch `drag(DentalForceps.ScreenCenter, ToothItem.ScreenCenter, steps=20)`.
   - Confirm tool attachment (observing progress leap from $0\%$ to $>15\%$).

2. **Oscillatory Wobble Gesture (`press_move`)**:
   - Generate an oscillating multi-point path alternating left and right around the object center:
     - Center: $(x_0, y_0)$
     - Left: $(x_0 - 70, y_0 + 10)$
     - Right: $(x_0 + 70, y_0 + 10)$
   - Repeat 4-6 oscillation cycles in a single continuous `press_move` trajectory.
   - Sample `ProgressNormalized`. If progress is below $0.85$, execute another oscillation pass.

3. **Directional Extraction Pull**:
   - Once fatigue is maximized, dispatch an upward `drag` gesture:
     - Start: $(x_0, y_0)$
     - End: $(x_0, y_0 + 160)$
     - Steps: 25
   - The joint snaps, triggering `SnapOff()` and completing the level.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Attempting Direct Extraction**: Pulling straight up without wobbling results in zero fatigue and the spring immediately pulls the object back down upon release.
- **Trap: Micro-Displacements Below Threshold**: Wobble movements within a radius $<20$px are filtered out as deadzone jitter. Ensure the left-right amplitudes exceed 50-70px from center.
