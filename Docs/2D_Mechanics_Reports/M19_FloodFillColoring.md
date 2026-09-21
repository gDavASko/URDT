# M19: Mask Segment Categorization & Palette Assignment (Flood Fill Coloring)

## 1. Mechanics Overview
The **Flood Fill Coloring** mechanic simulates spatial segmentation, CAD layer tagging, anatomical organ labeling, or interactive coloring book mechanics (e.g. MS Paint bucket fill, schematic wiring color-coding). The player is presented with an uncolored schema partitioned into distinct topological regions (`Segment_1` through `Segment_4`) and a color palette consisting of discrete tint swatches (`Red`, `Green`, `Blue`, `Yellow`). The objective is to select the correct active color swatch from the palette and apply it to each segment to reproduce a ground-truth reference schema (`Etalon`).

### Core Mechanics & Obstacles:
- **Two-Step State Action Model**:
  1. Actuate palette selector: `click(BtnColor)` sets active brush tint.
  2. Actuate target partition: `click(Segment_i)` applies active brush tint to region.
- **Bijective Permutation Mapping**: The 4 reference colors are randomly permuted across the 4 segments ($\pi: \{S_1, S_2, S_3, S_4\} \to \{C_1, C_2, C_3, C_4\}$), ensuring each color is used exactly once.
- **Mud / Contaminant Hazard (`BtnMudJunk`)**: An auxiliary palette swatch contains contaminated sludge marked with `[X]`. Applying mud soils the segment, triggering warning notifications and resetting progress for that partition.
- **Completion Goal**: Correctly assign the exact matching color to all 4 segments simultaneously ($100\%$ progress).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M19_FloodFillColoring` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnRed` | `UrdtUiButtonTarget` | ScreenCenter $(500, 228)$, Palette Color #1 |
| `BtnGreen` | `UrdtUiButtonTarget` | ScreenCenter $(570, 228)$, Palette Color #2 |
| `BtnBlue` | `UrdtUiButtonTarget` | ScreenCenter $(640, 228)$, Palette Color #3 |
| `BtnYellow` | `UrdtUiButtonTarget` | ScreenCenter $(710, 228)$, Palette Color #4 |
| `BtnMudJunk` | `UrdtUiButtonTarget` | ScreenCenter $(780, 228)$, Contaminant identifier (avoid!) |
| `Segment_1` | `Urdt2DInteractiveAreaTarget` / Clickable | ScreenCenter $(575, 375)$, Top-Left Partition |
| `Segment_2` | `Urdt2DInteractiveAreaTarget` / Clickable | ScreenCenter $(705, 375)$, Top-Right Partition |
| `Segment_3` | `Urdt2DInteractiveAreaTarget` / Clickable | ScreenCenter $(575, 295)$, Bottom-Left Partition |
| `Segment_4` | `Urdt2DInteractiveAreaTarget` / Clickable | ScreenCenter $(705, 295)$, Bottom-Right Partition |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback |

---

## 3. Mathematical Permutation Search & ReAct Formulation

Let $S = \{S_1, S_2, S_3, S_4\}$ be the set of segment partitions and $C = \{C_{\text{red}}, C_{\text{green}}, C_{\text{blue}}, C_{\text{yellow}}\}$ be the available palette colors.
The objective function evaluates the cardinality of correctly mapped partitions:
$$P(\vec{c}) = \frac{1}{|S|} \sum_{i=1}^{|S|} \mathbb{I}[c_i = c_i^*]$$
where $c_i^*$ is the ground-truth color of segment $S_i$.

Because progress updates monotonically with each correctly matched partition ($\Delta P = +0.25$), an AI agent can determine the bijection in polynomial time $O(|S| \cdot |C|)$ via greedy iterative elimination without requiring computer vision or screenshot inspection:
1. For segment $S_i$, sequentially test each unused color $c \in C_{\text{remaining}}$:
   - Select $c$ via `click(c)`.
   - Apply to $S_i$ via `click(S_i)`.
   - Inspect $P_{\text{new}}$.
2. If $P_{\text{new}} > P_{\text{old}}$:
   - Match confirmed: $\pi(S_i) = c$.
   - Remove $c$ from $C_{\text{remaining}}$.
   - Advance to segment $S_{i+1}$.
3. Worst-case evaluation cost: $4 + 3 + 2 + 1 = 10$ click pairs ($< 3$ seconds total execution).

---

## 4. General Gameplay Algorithm for AI Agents

1. **Hazard Filtering**:
   - Query all buttons in the mechanic.
   - Discard any button whose identifier contains `Mud`, `Junk`, or `Hazard` (`BtnMudJunk`).
   - Define clean palette $C = [\text{BtnRed}, \text{BtnGreen}, \text{BtnBlue}, \text{BtnYellow}]$.

2. **Greedy Elimination Assignment Loop**:
   - Initialize $C_{\text{pool}} = C$ and $P_{\text{baseline}} = 0.0$.
   - For each segment $S_i \in [\text{Segment\_1}, \text{Segment\_2}, \text{Segment\_3}, \text{Segment\_4}]$:
     - For each candidate color $C_j \in C_{\text{pool}}$:
       1. Issue `click(C_j)`.
       2. Sleep $50$ ms.
       3. Issue `click(S_i)`.
       4. Sleep $80$ ms.
       5. Inspect `ProgressNormalized`.
       6. If $\text{ProgressNormalized} \ge P_{\text{baseline}} + 0.20$:
          - $P_{\text{baseline}} = \text{ProgressNormalized}$.
          - Evict $C_j$ from $C_{\text{pool}}$.
          - Break to next segment.

3. **Status Verification**:
   - Confirm `M19_FloodFillColoring.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Forgetting to Click Palette First**: Directly clicking a segment without first activating a palette button applies whatever default color was previously loaded. The stateful nature of the brush requires explicit, paired actions: `[Select Color] -> [Apply Segment]`.
- **Trap: Mud Contamination**: Selecting `BtnMudJunk` turns the brush into contaminant mode ($ID = 99$). If clicked onto a segment, it overwrites previously correct progress and requires cleaning.
- **Trap: Blind Overwriting**: Re-clicking a segment that is already verified correct changes its color and drops progress. Maintaining an immutable assignment list prevents accidental regressions.
