# M02: Multi-Item Container Sorting (Bucket Sorting)

## 1. Mechanics Overview
The **Container Sorting** mechanic involves segregating a multi-class pool of objects into corresponding categorical containers (e.g., color-coded or item-typed buckets). Unlike 1-to-1 slot snapping where each receptacle accepts exactly one unique token, containers have capacities greater than one and accept multiple tokens of a given category or type.

### Core Mechanics & Obstacles:
- **Categorical Mapping**: Multiple items map to a single target container (e.g., Red Apples -> Red Bucket, Blue Berries -> Blue Bucket).
- **Junk / Unwanted Entities**: Hazardous or invalid objects (e.g., Stones marked with `IsJunk: true`) present in the item pool that degrade accuracy or cause level failure if placed into containers.
- **Dynamic Re-sorting**: Items in the staging tray must be inspected individually for categorical alignment before dispatch.

---

## 2. Perception & URDT Beacon Architecture
Without rendering visual images, an autonomous AI relies on the following URDT beacons:

| Target Name / Pattern | Component Type | Key Properties for Perception & Filtering |
| :--- | :--- | :--- |
| `M02_ContainerSorting` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Item_Apple_*` | `Urdt2DDraggableTarget` | `ItemId`, `IsJunk: false`, `Category: 'apple'`, `ScreenCenter` |
| `Item_Berry_*` | `Urdt2DDraggableTarget` | `ItemId`, `IsJunk: false`, `Category: 'berry'`, `ScreenCenter` |
| `Item_Stone_Junk` | `Urdt2DDraggableTarget` | `IsJunk: true` (Must be strictly avoided) |
| `Bucket_Red` | `Urdt2DSlotTarget` | `SlotId: 'Bucket_Red'`, `AcceptedType: 'apple'`, `ScreenCenter` |
| `Bucket_Blue` | `Urdt2DSlotTarget` | `SlotId: 'Bucket_Blue'`, `AcceptedType: 'berry'`, `ScreenCenter` |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Scene Enumeration & Categorization**:
   - Query all targets under the active module.
   - Filter `DraggableItems` where `IsJunk == false`.
   - Index all `ContainerSlots` by their identifier or accepted type.

2. **Semantic Association**:
   - Parse instruction keywords or target name tags:
     - "Red" / "Apple" -> `Bucket_Red`
     - "Blue" / "Berry" -> `Bucket_Blue`
   - Match item identities (e.g., `Item_Apple_1`, `Item_Apple_2`) to the red bucket target position.
   - Match item identities (e.g., `Item_Berry_1`, `Item_Berry_2`) to the blue bucket target position.

3. **Sequential Execution Pipeline**:
   - For each valid item in sequence:
     - Read live `item.ScreenCenter`.
     - Read target `container.ScreenCenter`.
     - Dispatch `drag(from, to, steps=20)`.
     - Allow an interaction cooldown of 250-300ms for physics simulation and UI layout updates.

4. **Progress Verification & Completion**:
   - Read `Urdt2DModuleTarget.ProgressNormalized` to confirm increments (e.g. 0.25 -> 0.50 -> 0.75 -> 1.00).
   - Confirm `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Overlapping Drop Bounds**: When dropping multiple items into the same bucket, do not offset coordinates outside the bucket's collider boundary. Dropping exactly at `container.ScreenCenter` is safe and universally recognized by container trigger zones.
- **Trap: Concurrency Collisions**: Dragging a second item while the first item is still animating its entry drop can cause input interception or raycast blocking. Enforce sequential queueing with post-drag settle delays.
- **Trap: Premature Completion Exit**: Some games require all items to be sorted before triggering the victory callback. Ensure the agent awaits `IsCompleted == true` from `Urdt2DModuleTarget`.
