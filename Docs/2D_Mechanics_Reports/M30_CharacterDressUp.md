# M30: Categorical Inventory Fitting & Anatomic Slot Mapping (Character Dress-Up)

## 1. Mechanics Overview
The **Character Dress-Up** mechanic simulates doll dressing, RPG equipment loadouts, surgical or hazmat suit preparation, and avatar customization. The player is presented with a humanoid mannequin silhouette on the left with four designated anatomical equipment slots: `Slot_Head` $(530, 380)$, `Slot_Body` $(530, 325)$, `Slot_Feet` $(530, 250)$, and `Slot_Accessory` $(590, 325)$. A wardrobe panel on the right contains five items: four valid equipment pieces (`Item_Head`, `Item_Body`, `Item_Feet`, `Item_Accessory`) and one contaminated hazard item (`Item_Junk` marked with `[X]`). The goal is to equip all four functional apparel items into their corresponding anatomical slots while avoiding the junk item and preventing mismatched slot assignments.

### Core Mechanics & Obstacles:
- **Anatomical Slot Type Verification**:
  - `Item_Head` $\leftrightarrow$ `Slot_Head` (Helmet)
  - `Item_Body` $\leftrightarrow$ `Slot_Body` (Chest Armor)
  - `Item_Feet` $\leftrightarrow$ `Slot_Feet` (Boots)
  - `Item_Accessory` $\leftrightarrow$ `Slot_Accessory` (Shoulder Shield / Reactor)
- **Defective Item Filtering**:
  - `Item_Junk` has `IsJunk == true`.
  - Dropping junk triggers immediate rejection: `"Бракованный предмет [X] не подходит для экипировки!"` and snaps back to wardrobe.
- **Cross-Slot Rejection**:
  - Dropping an item near an incorrect anatomical slot triggers error feedback: `"Этот предмет предназначен для другого слота!"` and returns to origin.
- **Magnetic Snap Distance**:
  - Valid snap occurs within distance $d \le R_{\text{snap}} \times 2.5 = 137.5$ px in Canvas space.
  - Successfully attached items lock to slot center and increment equipped count.
- **Win Condition**: Equip all $N = 4$ items ($100\%$ progress).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M30_CharacterDressUp` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Item_Head` | `DressUpItem` | Initial position $(715, 360)$, type `Head` |
| `Item_Body` | `DressUpItem` | Initial position $(825, 360)$, type `Body` |
| `Item_Feet` | `DressUpItem` | Initial position $(715, 300)$, type `Feet` |
| `Item_Accessory` | `DressUpItem` | Initial position $(825, 300)$, type `Accessory` |
| `Item_Junk` | `DressUpItem` | Initial position $(770, 240)$, `IsJunk == true` |
| `Slot_Head` | Slot Transform | Destination $(530, 380)$ |
| `Slot_Body` | Slot Transform | Destination $(530, 325)$ |
| `Slot_Feet` | Slot Transform | Destination $(530, 250)$ |
| `Slot_Accessory` | Slot Transform | Destination $(590, 325)$ |
| `ProgressText` | `UrdtUiTextTarget` | Screen format `"Экипировано: K / 4"` |

---

## 3. Mathematical Mapping & Matching Matrix

Let $\mathcal{I} = \{I_{\text{head}}, I_{\text{body}}, I_{\text{feet}}, I_{\text{acc}}, I_{\text{junk}}\}$ and $\mathcal{S} = \{S_{\text{head}}, S_{\text{body}}, S_{\text{feet}}, S_{\text{acc}}\}$.
The valid assignment mapping is a bijective function $f: \mathcal{I} \setminus \{I_{\text{junk}}\} \to \mathcal{S}$:
$$f(I) = \begin{cases} S_{\text{head}} & \text{if } \text{Type}(I) = \text{Head} \\ S_{\text{body}} & \text{if } \text{Type}(I) = \text{Body} \\ S_{\text{feet}} & \text{if } \text{Type}(I) = \text{Feet} \\ S_{\text{acc}} & \text{if } \text{Type}(I) = \text{Accessory} \\ \emptyset & \text{if } \text{IsJunk}(I) \end{cases}$$

Progress accumulation is strictly linear:
$$\text{Progress}(k) = \frac{k}{4} = 0.25 \cdot k \quad (k \in \{0, 1, 2, 3, 4\})$$

---

## 4. General Gameplay Algorithm for AI Agents

1. **Inventory & Target Binding**:
   - Query all targets starting with `Item_` and `Slot_`.
   - Filter out `Item_Junk`.
   - Map each item to its destination:
     - `Item_Head` $(715, 360) \to (530, 380)$
     - `Item_Body` $(825, 360) \to (530, 325)$
     - `Item_Feet` $(715, 300) \to (530, 250)$
     - `Item_Accessory` $(825, 300) \to (590, 325)$

2. **Sequential Precision Drag**:
   - For each item-slot pair $(I_k, S_k)$:
     1. Dispatch `drag(I_k.screenPosition, S_k.screenPosition, 10)`.
     2. Wait $400$ ms.
     3. Verify `ProgressText` incremented: $1/4 \to 2/4 \to 3/4 \to 4/4$.

3. **Completion Verification**:
   - Confirm `ProgressNormalized == 1.0`.
   - Confirm `M30_CharacterDressUp.IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Dragging Junk into Empty Slots**: Junk items visually resemble accessories or armor scraps. Without checking `IsJunk`, dragging junk wastes frames and triggers warning states.
- **Trap: Spatial Proximity Ambiguity**: `Slot_Body` $(530, 325)$ and `Slot_Accessory` $(590, 325)$ share the same vertical coordinate ($Y = 325$) and are separated by only $60$ px. Inaccurate horizontal drop coordinates will cause the body armor to be rejected by the accessory slot.
- **Trap: Interrupted Multi-Item Drags**: Items undergo local re-parenting upon pickup. Dragging item $B$ while item $A$'s snap animation has not settled can drop item $A$ back into the wardrobe panel.
