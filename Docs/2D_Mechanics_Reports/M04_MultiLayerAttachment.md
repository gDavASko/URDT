# M04: Multi-Layer Hierarchical Assembly & Staged Attachment

## 1. Mechanics Overview
The **Multi-Layer Hierarchical Assembly** mechanic models progressive construction where components must be mounted onto a central chassis in strict technological order across multiple distinct operational stages. Components cannot be attached until their prerequisite structural foundations are fully mounted.

### Core Mechanics & Obstacles:
- **Prerequisite Dependencies**: Socket installation requires specific layer dependencies:
  - Stage 1 (Core Base): Layer 1 (`Part_Core`) must precede Layer 2 (`Part_Armor`).
  - Stage 2 (External Modules): Layer 3 (`Part_Head`) must precede Layer 4 (`Part_Battery`).
- **Phased Lobbies / Trays**: Components for future stages are partitioned or revealed only when the preceding stage finishes.
- **Socket Occlusion & Re-anchoring**: Sockets may share spatial centers or overlap (e.g. Core and Armor occupy identical root coordinates), making visual differentiation difficult; beacons provide explicit socket IDs.
- **Defective Sub-components**: Each stage spawns a decoy component (`Part_Junk1`, `Part_Junk2`) that must not be assembled.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M04_MultiLayerAttachment` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Part_Core` | `Urdt2DDraggableTarget` | `ItemId: 'Part_Core'`, `IsJunk: false`, Layer: 1 |
| `Socket_Core` | `Urdt2DSlotTarget` | `SlotId: 'Socket_Core'`, `ScreenCenter` |
| `Part_Armor` | `Urdt2DDraggableTarget` | `ItemId: 'Part_Armor'`, `IsJunk: false`, Layer: 2 |
| `Socket_Armor` | `Urdt2DSlotTarget` | `SlotId: 'Socket_Armor'`, `ScreenCenter` |
| `Part_Head` | `Urdt2DDraggableTarget` | `ItemId: 'Part_Head'`, `IsJunk: false`, Layer: 3 |
| `Socket_Head` | `Urdt2DSlotTarget` | `SlotId: 'Socket_Head'`, `ScreenCenter` |
| `Part_Battery` | `Urdt2DDraggableTarget` | `ItemId: 'Part_Battery'`, `IsJunk: false`, Layer: 4 |
| `Socket_Battery` | `Urdt2DSlotTarget` | `SlotId: 'Socket_Battery'`, `ScreenCenter` |
| `Part_Junk*` | `Urdt2DDraggableTarget` | `IsJunk: true` (Must be ignored) |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Dependency Hierarchy Modeling**:
   - Query all available parts and extract layer order.
   - Establish execution order:
     - Step 1: Core -> `Socket_Core`
     - Step 2: Armor -> `Socket_Armor`
     - Step 3: Transition settle (allow stage transition animation ~500ms)
     - Step 4: Head -> `Socket_Head`
     - Step 5: Battery -> `Socket_Battery`

2. **Sequential Assembly Execution**:
   - For each step:
     - Read live coordinates of the required part and socket.
     - Dispatch `drag(from, to, steps=20)`.
     - Verify layer registration via `ProgressNormalized`.

3. **Stage Transition Handling**:
   - When Stage 1 concludes (Progress = 0.50), the system transitions trays.
   - Allow a brief pause (500ms) before querying Stage 2 part coordinates.

4. **Final Lock & Verification**:
   - Once all 4 layers are mounted, confirm `ProgressNormalized == 1.0` and `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Reverse Installation Sequence**: Dragging `Part_Armor` before `Part_Core` causes rejection because the socket prerequisite is unfulfilled. AI must observe dependencies.
- **Trap: Coordinate Overlap in Co-axial Sockets**: Both `Socket_Core` and `Socket_Armor` sit at screen coordinate $(640, 356)$. Trying to visually pick targets by color fails; targeting by exact beacon `TargetId` ensures perfect resolution.
