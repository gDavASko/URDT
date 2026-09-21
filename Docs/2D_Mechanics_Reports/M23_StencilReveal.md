# M23: Spatial Stencil Masking & Dwell Aperture Filtering (Stencil Reveal)

## 1. Mechanics Overview
The **Stencil Reveal** mechanic simulates obscured object discovery, sonar sweeps, x-ray inspection, or magnifying aperture scanning (e.g. Where's Waldo, hidden object games, metal detector sweeps, microscopic sample scanning). The interaction area is cloaked in darkness (`DarkSearchArea`). The player manipulates a circular optical lens (`StencilLens`) with a fixed aperture radius ($R_{\text{reveal}} = 65$ px). When the aperture center comes within $R_{\text{reveal}}$ of a hidden entity, the entity transitions from hidden to revealed. To collect a discovered entity, the lens must remain stationary within the capture radius for an uninterrupted dwell period ($T_{\text{hold}} = 2.0$ s). The goal is to discover and harvest $N = 3$ hidden crystals.

### Core Mechanics & Obstacles:
- **Spatial Aperture Filtering**:
  - Entity is revealed iff: $\|\vec{P}_{\text{lens}} - \vec{P}_{\text{target}}\|_2 \le R_{\text{reveal}} = 65$ px.
- **Continuous Dwell Integration**:
  - When focused on an uncollected target, a dwell timer integrates:
    $$\frac{d\tau}{dt} = 1.0\text{ s}^{-1}$$
  - If the lens moves away before $\tau \ge 2.0$ s, the dwell progress collapses back to $0$.
- **Junk Hazard (`JunkDust`)**: A cluster of fake dust marked with `[X]` is positioned inside the dark zone (at $\approx (750, 255)$). Dwelling on dust triggers warning alerts (`"Подозрительный объект [X]"`) and wastes exploration time.
- **Completion Goal**: Harvest all 3 genuine hidden crystals ($100\%$ progress).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M23_StencilReveal` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `DarkSearchArea` | `Urdt2DDraggableTarget` | ScreenCenter $(640, 295)$, bounds $500 \times 210$ px, drag canvas |
| `StencilLens` | `Urdt2DInteractiveAreaTarget` | ScreenCenter $(X, Y)$, controllable via `drag` |
| `ScoreText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Найдено: C / 3"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback: exposes active dwell state: `"Изучение кристалла... Удержание: T / 2.0 сек"` vs `"Подозрительный объект [X]"` |

---

## 3. Mathematical Lawnmower Boustrophedon Sweep

Let the search domain be a rectangular bounding box:
$$\Omega = [X_{\min}, X_{\max}] \times [Y_{\min}, Y_{\max}] = [440, 840] \times [220, 370]$$
The width $W = 400$ px and height $H = 150$ px.

Because hidden crystals are invisible prior to aperture overlap, an autonomous agent executes a deterministic Boustrophedon (lawnmower) grid sweep ensuring complete coverage:
$$\text{Step Size } \Delta \le \sqrt{2} \cdot R_{\text{reveal}} = \sqrt{2} \cdot 65 \approx 91.9\text{ px}$$
To provide guaranteed overlapping coverage with zero blind spots, the grid step is chosen as:
$$\Delta_{\text{grid}} = 45\text{ px}$$
Sweep waypoints:
$$X \in \{450, 495, 540, 585, 630, 675, 720, 765, 810\}$$
$$Y \in \{235, 280, 325, 360\}$$
Total scan points: $9 \times 4 = 36$ waypoints.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Aperture Drag Maneuvers**:
   - Instead of single-frame teleport clicks, displace the lens smoothly using:
     `drag(StencilLens, { x: X, y: Y }, steps = 4)`.

2. **Perception & Dwell Cycle**:
   - At each grid node $(X_k, Y_k)$:
     1. Move lens to $(X_k, Y_k)$ via `drag`.
     2. Wait $70$ ms for physics/overlap update.
     3. Inspect `LocalInstruction`.
     4. If text contains `"Подозрительный объект [X]"`:
        - Identify as `JunkDust`; immediately skip without dwelling.
     5. If text contains `"Изучение кристалла"`:
        - Genuine crystal detected!
        - Hold lens position stationary for $2200$ ms ($T_{\text{hold}} = 2.0\text{ s} + 200\text{ ms margin}$).
        - Inspect `ScoreText` to confirm increment.
        - Check `M23_StencilReveal.IsCompleted`. If true, terminate sweep immediately.

3. **Status Confirmation**:
   - Confirm `ScoreText` equals `3 / 3`, `M23_StencilReveal.IsCompleted == true`, and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Dragging Without Holding Dwell**: Moving the lens continuously without pausing resets the hold timer ($\tau \to 0$) on every frame. Once a crystal signature is detected in `LocalInstruction`, all motion must cease completely for $2.0$ seconds until the collection event fires.
- **Trap: Wasting Time on Dust Decoys**: Distinguishing between genuine targets (`"Изучение кристалла"`) and hazard decoys (`"Подозрительный объект"`) via string parsing prevents wasting dwell cycles on non-scoring decoys.
- **Trap: Canvas Clipping Boundaries**: The lens center is physically clamped to $\pm 215$ px horizontally and $\pm 70$ px vertically from the container center. Drag waypoints outside these bounds saturate against the container walls.
