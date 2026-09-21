# M28: Dual-Route Bridge Placement & Defect Discrimination (Dual Bridge Route)

## 1. Mechanics Overview
The **Dual Bridge Route** mechanic challenges an AI agent with multi-slot topological bridging and defective resource filtering. Two autonomous bots (Bot A and Bot B) start at the left boundary ($X_{\text{start}} = -230$) along two separate parallel tracks separated vertically ($Y_A = 75$, $Y_B = 5$). Both tracks contain an impassable chasm: Slot A at screen position $(560, 395)$ and Slot B at screen position $(640, 340)$. A tool chest provides three movable bridge planks: two structural wooden planks (`Plank_1`, `Plank_2`) and one cracked hazard plank (`Plank_Broken_Junk` marked with `[X]`). The objective is to drag the two functional planks into their respective track gaps, verify bridge integrity, and initiate navigation (`BtnStart`) so both bots traverse safely to their respective finish goals.

### Core Mechanics & Obstacles:
- **Defective Asset Discrimination**:
  - `Plank_Broken_Junk` has `IsBroken == true`.
  - Snapping a broken plank into either slot triggers immediate rejection: `"Сломанный мостик [X] рухнет под весом бота! Используйте целый мост."` and resets the plank back to origin.
  - Only sound planks (`Plank_1` and `Plank_2`) can be slotted.
- **Topological Snap Tolerance**:
  - Snap radius: $R_{\text{snap}} = 75$ px in root Canvas space.
  - Slotted planks reparent and lock to the gap anchor coordinates.
- **Two-Phase Progression**:
  - Phase 1 (Bridge Placement): Each placed bridge yields $+35\%$ progress ($70\%$ total upon placing both).
  - Phase 2 (Traversal): Clicking `BtnStart` launches a $2.4$ s animated walking routine with procedural vertical bobbing ($y_{\text{bob}} = \pm 5$ px), advancing progress smoothly from $70\%$ to $100\%$.
- **Win Condition**: Both bots reach their goal markers ($X_{\text{goal}} = X_{\text{start}} + 430$), completing the mechanic.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M28_DualBridgeRoute` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Plank_1` | `BridgePlank` (`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`) | ScreenCenter $(480, 232)$, sound bridge 1 |
| `Plank_2` | `BridgePlank` | ScreenCenter $(640, 232)$, sound bridge 2 |
| `Plank_Broken_Junk` | `BridgePlank` | ScreenCenter $(800, 232)$, defective plank (`IsBroken == true`) |
| `SlotA` | Track Gap Target | ScreenCenter $(560, 395)$, snap radius $75$ px |
| `SlotB` | Track Gap Target | ScreenCenter $(640, 340)$, snap radius $75$ px |
| `BtnStart` | `UrdtUiButtonTarget` | ScreenCenter $(640, 288)$, activates when both slots filled |
| `LocalInstruction` | `UrdtUiTextTarget` | Feedback and warning messages |

---

## 3. Mathematical State Invariants & Routing Law

Let $S_A, S_B \in \{\emptyset, P_1, P_2, P_{\text{junk}}\}$ denote slot states.
Validation predicate:
$$V(S_A, S_B) = \begin{cases} \text{Ready} & \text{if } (S_A, S_B) \in \{(P_1, P_2), (P_2, P_1)\} \\ \text{Blocked} & \text{otherwise} \end{cases}$$

Plank selection law:
$$P \in \{P_1, P_2\} \iff \neg\text{IsBroken}(P)$$

### Motion Trajectory During Phase 2:
$$X_A(t) = \text{Lerp}(X_{\text{start}, A}, X_{\text{goal}, A}, t/T), \quad Y_A(t) = Y_A + 5 \sin(10\pi t/T)$$
$$X_B(t) = \text{Lerp}(X_{\text{start}, B}, X_{\text{goal}, B}, t/T), \quad Y_B(t) = Y_B + 5 \cos(10\pi t/T)$$
Where $T = 2.4$ seconds.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Inventory & Target Discovery**:
   - Query all targets matching `Plank*` and `Slot*`.
   - Filter out `Plank_Broken_Junk` based on its identifier or visual label `[X]`.
   - Identify active functional planks: `Plank_1` $(480, 232)$ and `Plank_2` $(640, 232)$.

2. **Sequential Bridge Placement**:
   - Drag `Plank_1` from $(480, 232)$ to `SlotA` $(560, 395)$:
     `drag({ from: { x: 480, y: 232 }, to: { x: 560, y: 395 }, steps: 10 })`
   - Wait $400$ ms for snap resolution.
   - Drag `Plank_2` from $(640, 232)$ to `SlotB` $(640, 340)$:
     `drag({ from: { x: 640, y: 232 }, to: { x: 640, y: 340 }, steps: 10 })`
   - Wait $500$ ms.

3. **Status Verification & Launch**:
   - Verify `LocalInstruction` displays ready status: `Мостики установлены! Нажмите 'ПУСК >>'`.
   - Dispatch `click(BtnStart)` at $(640, 288)$.

4. **Traversal Tracking**:
   - Poll `ProgressNormalized` over $2.4$ s until reaching $1.0$.
   - Confirm `IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Snapping Defective Materials**: If an agent treats all planks generically without checking `IsBroken` or matching IDs, dragging `Plank_Broken_Junk` into a slot fails silently via instant return-to-origin, leaving the route incomplete.
- **Trap: Incomplete Drag Release**: Because planks unparent to the Canvas root during dragging and re-parent on drop, issuing a drag without a clean release (`PointerUp`) leaves the plank floating detached in the hierarchy.
- **Trap: Premature Launch Dispatch**: `BtnStart` is strictly non-interactable until both $S_A$ and $S_B$ evaluate to valid non-broken planks. Clicking before both are snapped is dropped by Unity's EventSystem.
