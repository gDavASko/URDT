# M05: Timeline & Instruction Queue Sequencer

## 1. Mechanics Overview
The **Timeline Sequencer** mechanic introduces sequential command execution (algorithmic/visual programming pattern). Instead of immediate interactive effects, the player populates an ordered sequence of instruction blocks (chips) into a timeline slot strip, and then triggers execution via an explicit commit button ("RUN" / "ПУСК").

### Core Mechanics & Obstacles:
- **Order-Sensitive Programming**: Slots represent indexed execution steps ($T_0, T_1, T_2$). Commands must match the target algorithm (e.g. Forward -> Turn -> Forward).
- **Two-Phase Action Loop**:
  - Phase 1: Planning / Slotting (dragging commands into the queue).
  - Phase 2: Evaluation / Commit (clicking the execute button).
- **Junk Instruction Chips**: Deceptive command chips (`Chip_Glitch_Junk` with `IsJunk: true`) cause execution runtime errors if placed.
- **Simulation Time**: After clicking the execute button, a robot or runner executes the commands sequentially. The AI must wait for the simulation to finish before expecting completion.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Decision Making |
| :--- | :--- | :--- |
| `M05_TimelineSequencer` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Slot_0`, `Slot_1`, `Slot_2` | `Urdt2DSlotTarget` | `SlotId`, `ScreenCenter` (Ordered timeline receptacles) |
| `Chip_Fwd_1`, `Chip_Fwd_2` | `Urdt2DDraggableTarget` | `ItemId: 'forward'`, `IsJunk: false`, `ScreenCenter` |
| `Chip_Turn` | `Urdt2DDraggableTarget` | `ItemId: 'turn'`, `IsJunk: false`, `ScreenCenter` |
| `Chip_Glitch_Junk` | `Urdt2DDraggableTarget` | `IsJunk: true` (Must be ignored) |
| `Button_Execute` | `UrdtUiButtonTarget` | `ScreenCenter`, Clickable trigger for sequence execution |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Instruction Parsing & Queue Formulation**:
   - Parse instruction specification: "Вперед -> Поворот -> Вперед" (Forward -> Turn -> Forward).
   - Formulate mapping:
     - `Slot_0` $\leftarrow$ `Chip_Fwd_1`
     - `Slot_1` $\leftarrow$ `Chip_Turn`
     - `Slot_2` $\leftarrow$ `Chip_Fwd_2`

2. **Drag & Drop Timeline Loading**:
   - Drag each required chip to its corresponding indexed slot using `drag(from, to, steps=20)`.
   - Disregard any decoy chip flagged `IsJunk == true`.

3. **Execution Commit**:
   - Locate `Button_Execute` coordinates.
   - Dispatch `click({x, y})`.

4. **Simulation Await**:
   - The sequence takes 1.5 - 2.0 seconds to execute its animation steps.
   - Wait 2000ms.
   - Poll `Urdt2DModuleTarget.IsCompleted` to confirm success.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Premature Execution Click**: Clicking `Button_Execute` before all slots are populated triggers a failed execution state.
- **Trap: Missing Phase 2 Trigger**: Populating slots does not complete the mechanic on its own (`ProgressNormalized` remains 0 until the program executes and succeeds). The AI must remember to click the commit trigger.
