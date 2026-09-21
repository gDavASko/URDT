# M01: Snap-to-Slot Drag & Drop

## 1. Mechanics Overview
The **Snap-to-Slot** mechanic is a fundamental 2D manipulation pattern in UI/gameplay interfaces. The player must drag multiple distinct geometric objects from an initial spawn tray into their matching target receptacles (slots). Upon release within the target slot's acceptance radius, the item snaps into place and locks.

### Core Mechanics & Obstacles:
- **Valid Items**: Multiple items (e.g., Square, Triangle, Circle) that correspond to specific target slots.
- **Junk/Distractor Items**: Defective or decoy objects (e.g., marked with 'X' or `IsJunk: true`) that do not match any slot and trigger error penalties if slotted.
- **Spatial Barriers**: Static collision or UI obstacles between the spawn tray and target slots that require curved drag trajectories or direct linear jumps across non-blocking zones.
- **Dynamic Shuffling**: Initial positions of slots and items are randomized upon level reset/initialization.

---

## 2. Perception & URDT Beacon Architecture
To pass this mechanic without visual screenshots, the AI must perceive the scene through structured URDT beacons:

| Target Name / Role | Component Type | Key Properties for AI Decision Making |
| :--- | :--- | :--- |
| `M01_SnapToSlot` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction`, `TargetScore` |
| `Item_<Id>` (e.g. `Item_Square`) | `Urdt2DDraggableTarget` | `ItemId`, `IsJunk`, `IsSnapped`, `ScreenCenter`, `ScreenRect` |
| `Slot_<Id>` (e.g. `Slot_Square`) | `Urdt2DSlotTarget` | `SlotId`, `IsOccupied`, `ScreenCenter`, `ScreenRect` |
| `Item_Junk_Defective` | `Urdt2DDraggableTarget` | `IsJunk: true` (Must be filtered out and ignored) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Scene Discovery & Beacon Indexing**:
   - Query all registered targets in the active hierarchy.
   - Separate discovered entities into:
     - `ModuleTarget`: Master tracker.
     - `DraggableItems`: Filter items where `IsJunk == false` and `IsSnapped == false`.
     - `Slots`: Available receptacles where `IsOccupied == false`.

2. **Semantic Matching**:
   - For each valid draggable item, inspect its `ItemId` (e.g., `"square"`).
   - Find the matching slot where `slot.SlotId == item.ItemId` (or matching type attribute).

3. **Trajectory & Input Generation**:
   - Read `from = item.ScreenCenter` (e.g., `{x: 850, y: 250}`).
   - Read `to = slot.ScreenCenter` (e.g., `{x: 640, y: 375}`).
   - Issue a virtual mouse/touch `drag` command:
     - `steps`: Minimum 15-20 intermediate interpolated points. This ensures the Unity `EventSystem` fires `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler` on every physics/UI frame.
     - Never use single-frame teleportation clicks, as drag triggers require pointer motion deltas.

4. **Feedback Evaluation & State Verification**:
   - Wait 100-300ms for Unity layout and snapping animations to resolve.
   - Inspect `Urdt2DModuleTarget.ProgressNormalized`. Verify that progress incremented (e.g., from `0.0` to `0.33`, then `0.66`, then `1.0`).
   - Repeat for all remaining item-slot pairs.

5. **Completion Verification**:
   - Ensure `Urdt2DModuleTarget.IsCompleted == true`.
   - Wait for the victory celebration routine before transitioning to the next module.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Dragging Junk Items**: Distractor items look identical in geometry or color to human eyes but are flagged `IsJunk: true` in beacon metadata. The AI must filter items by `!item.IsJunk`.
- **Trap: Insufficient Drag Steps**: If a drag command dispatches from origin to destination in 1 step, Unity UI `EventSystem` may treat it as an instantaneous click, failing to trigger `OnDrop` on the slot. Always use 15-25 steps.
- **Trap: Position Caching**: Items and slots shuffle on restart. Never hardcode screen coordinates; always dynamically read `ScreenCenter` from the live URDT inspect payload.
