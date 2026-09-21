# M06: Elastic Wire Graph Node Pairing

## 1. Mechanics Overview
The **Node Pairing / Wire Connecting** mechanic requires the player to establish point-to-point connections (wires, laser beams, or logical graph edges) between source nodes on one terminal and corresponding destination nodes on another terminal. A continuous drag gesture originates on a source terminal pin and terminates inside the matching destination pin.

### Core Mechanics & Obstacles:
- **Graph Edge Creation**: Dispatches an elastic wire rendered via dynamic LineRenderer or procedural UI meshes during pointer motion.
- **Color / Semantic Matching**: Pins share color codes or identifier tokens (e.g. Pin A -> Pin A, Pin B -> Pin B, Pin C -> Pin C).
- **Distractor / Defective Terminals**: Defective pins (`Pin_Src_Junk`, `Pin_Tgt_Junk`) marked `IsJunk: true` that trigger short-circuits or resets if connected.
- **Vertical Shuffling**: Vertical positions of target and source pins are randomized to prevent static horizontal gestures.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Decision Making |
| :--- | :--- | :--- |
| `M06_NodePairing` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Pin_Src_<Id>` | `Urdt2DDraggableTarget` | `ItemId: 'Pin_Src_<Id>'`, `IsJunk: false`, `ScreenCenter` |
| `Pin_Tgt_<Id>` | `Urdt2DDraggableTarget` / Slot | `ItemId: 'Pin_Tgt_<Id>'`, `IsJunk: false`, `ScreenCenter` |
| `Pin_*_Junk` | `Urdt2DDraggableTarget` | `IsJunk: true` (Must be ignored) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Terminal Mapping & Filtration**:
   - Query all pin targets across the module.
   - Separate pins into:
     - `SourcePins`: Pins on the input rail (`Pin_Src_*`), filtering out `IsJunk == true`.
     - `TargetPins`: Pins on the output rail (`Pin_Tgt_*`), filtering out `IsJunk == true`.

2. **Matching by Key Suffix**:
   - Match by token identifier suffix (e.g., `_A`, `_B`, `_C`).
   - Pair `Pin_Src_A` with `Pin_Tgt_A`.
   - Pair `Pin_Src_B` with `Pin_Tgt_B`.
   - Pair `Pin_Src_C` with `Pin_Tgt_C`.

3. **Elastic Wire Drag Dispatch**:
   - For each valid pair:
     - Read exact `from = srcPin.ScreenCenter`.
     - Read exact `to = tgtPin.ScreenCenter`.
     - Dispatch `drag(from, to, steps=25)`.
     - Allow a 250ms interval for wire latching and progress update.

4. **Status Evaluation**:
   - Monitor `ProgressNormalized` after each wire connection (e.g. 0.33 -> 0.67 -> 1.00).
   - Verify `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Straight-Line Assumptions**: Even if the labels say A, B, C on both sides, the right-hand pins are vertically shuffled (e.g. A at y=290, B at y=410, C at y=350). The AI must read live coordinates from `ScreenCenter` rather than drawing horizontal lines.
- **Trap: Insufficient Interpolation**: If the drag step count is too low, the raycast line test inside Unity might skip intermediate samples, missing the target pin collider. Use at least 20-25 steps.
