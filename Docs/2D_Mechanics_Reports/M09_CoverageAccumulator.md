# M09: Surface Coverage & Scratch-Mask Cleaning (Coverage Accumulator)

## 1. Mechanics Overview
The **Coverage Accumulator** mechanic represents surface wiping, scratch-card revealing, or painting tasks where a circular brush or tool must clear a required threshold percentage (e.g. $\ge 90\%$) of a partitioned target area (represented as a 2D spatial grid of dirt cells, mist droplets, or opacity masks).

### Core Mechanics & Obstacles:
- **Spatial Discretization**: A discrete grid of cells (e.g., $4 \times 5 = 20$ cells) tracking individual cleanliness states.
- **Radial Brush Stamp**: Moving the brush samples all cells within its interaction radius ($R_{\text{brush}} = 40$ px) on every displacement frame.
- **Permanent Defect / Hazard Marker**: One or more cells represent structural cracks, scars, or indelible stains (`IsPermanentHazard: true`) marked with 'X' that cannot be cleaned. The pass threshold is normalized exclusively against cleanable cells.
- **High Completion Threshold**: Requiring $\ge 90\%$ coverage means the AI cannot simply swipe across a single row; it must systematically cover all quadrants.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M09_CoverageAccumulator` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ToolSponge` | `Urdt2DDraggableTarget` | `ItemId: 'ToolSponge'`, `ScreenCenter` |
| `DirtCell_<row>_<col>` | `Urdt2DInteractiveAreaTarget` / Target | `ScreenCenter`, `IsCleaned`, `IsPermanentHazard` |

---

## 3. General Gameplay Algorithm for AI Agents (Boustrophedon / Lawnmower Sweep)

1. **Grid Geometry Extraction**:
   - Query all dirt cell targets.
   - Group cells by row coordinates ($y$) and sort by column coordinates ($x$).
   - Determine bounding box:
     - $X_{\min} = 450$, $X_{\max} = 830$
     - $Y_{\text{row } 0} = 400$, $Y_{\text{row } 1} = 345$, $Y_{\text{row } 2} = 290$, $Y_{\text{row } 3} = 235$

2. **Serpentine (Boustrophedon) Path Planning**:
   - Construct a continuous sweep path that visits every row, alternating direction to minimize pointer displacement:
     - Start at tool: $(870, 320)$
     - Row 0: Left to right: $(450, 400) \rightarrow (830, 400)$
     - Transition: Drop down to Row 1: $(830, 345)$
     - Row 1: Right to left: $(830, 345) \rightarrow (450, 345)$
     - Transition: Drop down to Row 2: $(450, 290)$
     - Row 2: Left to right: $(450, 290) \rightarrow (830, 290)$
     - Transition: Drop down to Row 3: $(830, 235)$
     - Row 3: Right to left: $(830, 235) \rightarrow (450, 235)$

3. **Motion Dispatch (`press_move`)**:
   - Execute the sweep with `press_move(sweepPath, stepsPerSegment=10)`.
   - The unbroken motion guarantees every cell falls within $R_{\text{brush}} \le 40$ px of the path.

4. **Progress Verification**:
   - Confirm `ProgressNormalized` reaches $1.0$ and `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Discrete Point Clicks**: Clicking individual dirt cells is slow and prone to missed corners. A continuous serpentine drag (`press_move`) sweeps the entire area in a fraction of a second.
- **Trap: Getting Stuck on Permanent Scars**: Trying to scrub the permanent hazard cell (`IsPermanentHazard: true`) in an attempt to reach $100\%$ raw cells will stall the AI. The mechanic only requires $90\%$ of cleanable cells.
