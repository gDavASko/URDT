# M18: Discrete Topological Node Rotation & BFS Graph Closure (Graph Flow Closure)

## 1. Mechanics Overview
The **Graph Flow Closure** mechanic models fluid, hydraulic, electrical circuit, or optical laser beam routing across a discrete planar grid (e.g. Pipe Mania, Bioshock hacking puzzle, Plumber, or circuit breaker boards). A grid of rotatable tiles connects a fixed flow emitter (`Source` at $(0,0)$) to an absorption collector (`Sink` at $(2,2)$). Each discrete interaction (`click` on a pipe tile) rotates the tile by $90^\circ$ clockwise ($\Delta \theta = -90^\circ$). When adjacent tile ports exhibit matching reciprocal connection bitmasks, fluid propagates along the graph. The mechanic is completed once a connected topological path from Source to Sink is closed.

### Core Mechanics & Obstacles:
- **Port Bitmask Geometry**:
  - `Up` = 1 ($2^0$), `Right` = 2 ($2^1$), `Down` = 4 ($2^2$), `Left` = 8 ($2^3$).
  - A valid hydraulic junction between cell $A$ and adjacent neighbor $B$ requires reciprocal alignment:
    - If $B$ is to the right of $A$, then $(M_A \ \& \ 2) \ne 0$ and $(M_B \ \& \ 8) \ne 0$.
    - If $B$ is below $A$, then $(M_A \ \& \ 4) \ne 0$ and $(M_B \ \& \ 1) \ne 0$.
- **Broken Conduit Hazard (`BrokenJunk`)**: One or more tiles contain ruptured pipes marked with `[X]` ($M = 0$). Fluid cannot pass through broken tiles under any rotation, requiring the flow graph to navigate around obstacles.
- **Topological Variants**: The layout is selected from 5 guaranteed planar bypass configurations (e.g. bypassing central junk along the upper perimeter $(0,0) \to (1,0) \to (2,0) \to (2,1) \to (2,2)$).
- **Dynamic BFS Flow Evaluation**: Upon every rotation event, an unweighted Breadth-First Search (BFS) explores all connected nodes originating from `Source`, dynamically updating visual water fill state and progress percentage.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M18_GraphFlowClosure` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `PipeTile_X_Y` | `Urdt2DInteractiveAreaTarget` / Clickable | ScreenCenter coordinates $(X, Y)$, `GridX`, `GridY` |
| `StatusText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Заполнено узлов: K / 9 (Вариант #V)"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status instructions |

---

## 3. Mathematical Graph Model & Algorithmic Closure

Let $G = (V, E)$ be the directed grid graph where $V = \{(x, y) \mid 0 \le x < W, 0 \le y < H\}$.
Each tile $v \in V$ possesses a port bitmask $M(v, \theta) \in [0, 15]$ where rotation $\theta \in \{0, 1, 2, 3\}$ shifts bits cyclically:
$$M(v, \theta + 1) = \text{RotateBitsRight4}(M(v, \theta))$$

### Reciprocal Adjacency Predicate:
Two orthogonal neighbors $u = (x_1, y_1)$ and $w = (x_2, y_2)$ share an active edge $(u, w) \in E$ iff:
$$\exists d \in \{0, 1, 2, 3\} \text{ s.t. } w = u + \vec{\delta}_d \land (M_u \ \& \ \text{Mask}_d) \ne 0 \land (M_w \ \& \ \text{OppMask}_d) \ne 0$$

### Incremental ReAct Feedback Exploitation:
Because `StatusText` immediately exposes the BFS reachable set cardinality $K = |\text{ReachableNodes}|$:
$$\Delta K = K_{\text{after}} - K_{\text{before}}$$
An AI agent does not even need to invert the internal sprite bitmaps. By traversing the candidate bypass topology:
1. When rotating tile $(x_i, y_i)$, if $K$ increases, the tile has successfully mated with the upstream pressurized flow!
2. If $K$ does not increase, rotate again ($\le 3$ clicks per tile).
3. Once $K$ reaches the sink or `"Цепь замкнута!"` appears, termination is instantaneous.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Topology & Variant Identification**:
   - Inspect `StatusText` to read active layout index (e.g. `Вариант #1`).
   - Identify candidate topological path avoiding the `BrokenJunk` coordinate:
     - E.g. Path: $(1,0) \to (2,0) \to (2,1) \to (2,2)$.

2. **Sequential Incremental Closure Loop**:
   - For each intermediate node $P_i = (x_i, y_i)$ along the target path:
     1. Read current reachable count $K$.
     2. If $K > i$, node $P_i$ is already energized by flow; proceed to $P_{i+1}$.
     3. For $r = 1 \dots 4$:
        - Dispatch `click(PipeTile_x_y)`.
        - Sleep $100$ ms.
        - Inspect `StatusText`.
        - If `StatusText` contains `"Цепь замкнута!"` or $K$ increases: lock in current orientation and advance to next node.

3. **Status Confirmation**:
   - Verify `M18_GraphFlowClosure.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Connecting into Broken Nodes**: Rotating tiles to connect into a `BrokenJunk` node creates a dead end ($M = 0$). Topological path planners must explicitly blacklist broken tile coordinates.
- **Trap: Blind 4-Click Reset**: When rotating a tile, stopping immediately upon positive feedback ($\Delta K > 0$) is critical. Performing a 4th redundant click rotates the tile out of alignment back to an open circuit.
- **Trap: Ambiguous T-Junctions / Sinks**: Sinks accept flow from all 4 directions ($M_{\text{sink}} = 15$). Once the final penultimate pipe connects into the sink, the closure triggers immediately without requiring manual interaction with the sink tile itself.
