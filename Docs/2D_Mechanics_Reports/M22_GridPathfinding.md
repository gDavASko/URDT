# M22: Discrete Tile Step Navigation & Obstacle Bypass (Grid Pathfinding)

## 1. Mechanics Overview
The **Grid Pathfinding** mechanic simulates discrete tile navigation, maze traversal, tactical RPG grid movement, or automated guided vehicle (AGV) pathing on a 2D planar lattice (e.g. Sokoban, Chip's Challenge, Fire Emblem grid move). The player controls an avatar starting at an initial coordinate $S = (X_{\text{start}}, Y_{\text{start}})$ and must navigate step-by-step to a goal terminal $G = (X_{\text{goal}}, Y_{\text{goal}})$. Movement is executed via discrete `click` interactions on adjacent orthogonal neighbor tiles ($\Delta X + \Delta Y = 1$). Obstacles including impassable walls and hazard glitch traps constrain available routes.

### Core Mechanics & Obstacles:
- **Grid Dimension & Coordinate System**:
  - $5 \times 5$ planar lattice ($X \in [0, 4]$, $Y \in [0, 4]$). $Y = 0$ is top, $Y = 4$ is bottom.
  - Start node: $S = (0, 4)$ (Bottom-Left).
  - Goal node: $G = (4, 0)$ (Top-Right).
- **Impassable Geometry (Obstacle Walls)**:
  - Walls occupy coordinates: $W = \{(1, 3), (2, 3), (2, 1), (3, 1)\}$.
  - Clicking on an impassable wall is blocked (`"Препятствие! Робот не может пройти сквозь стену"`).
- **Glitch Trap Hazard (`GlitchTrapHazard`)**:
  - Trap coordinate: $T = (1, 1)$, marked with `[X]`.
  - Stepping on the trap locks the entity in stasis (`_isTrapped = true`) for $1.0$ s penalty.
- **Orthogonal Adjacency Constraint**: Movement commands are valid strictly between cells satisfying $L_1$ Manhattan distance:
  $$d_{\text{Manhattan}}(P_{\text{next}}, P_{\text{curr}}) = |X_{\text{next}} - X_{\text{curr}}| + |Y_{\text{next}} - Y_{\text{curr}}| = 1$$
- **Completion Goal**: Reach coordinate $(4, 0)$ in the minimum feasible step budget.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M22_GridPathfinding` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `Tile_X_Y` | `Urdt2DInteractiveAreaTarget` / Button | ScreenCenter $(X, Y)$, `CellX`, `CellY`, Clickable |
| `StepText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Шаги: K | Позиция: (X, Y)"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback |

---

## 3. Mathematical Path Planning ($A^*$ / BFS Traversal)

Let the navigation environment be represented as an unweighted directed graph $G = (V, E)$ where:
$$V = \{ (x, y) \mid 0 \le x < 5, 0 \le y < 5 \} \setminus (W \cup \{T\})$$
$$E = \{ (u, v) \mid u, v \in V \land \|u - v\|_1 = 1 \}$$

The theoretical shortest path distance is bounded below by the Manhattan distance:
$$D_{\min} \ge |4 - 0| + |0 - 4| = 4 + 4 = 8\text{ steps}$$

### Optimal Corridor Identification:
Examining the obstacle distribution reveals that Column $0$ ($X = 0, Y \in [4, 0]$) and Row $0$ ($Y = 0, X \in [0, 4]$) are completely devoid of walls and hazards:
$$\forall y \in [0, 4], (0, y) \notin W \cup \{T\}$$
$$\forall x \in [0, 4], (x, 0) \notin W \cup \{T\}$$

Thus, the peripheral L-corridor constitutes a provably optimal, collision-free $8$-step trajectory:
$$\Pi^* = \langle (0, 4), (0, 3), (0, 2), (0, 1), (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) \rangle$$

---

## 4. General Gameplay Algorithm for AI Agents

1. **Topology & Obstacle Query**:
   - Query all `Tile_X_Y` targets on the grid.
   - Inspect tile properties to map walkable cells vs walls/traps.

2. **Graph Search ($A^*$ / BFS)**:
   - Compute shortest obstacle-avoiding sequence $\Pi^* = \langle P_1, P_2, \dots, P_k \rangle$ using $L_1$ Manhattan heuristic:
     $$h(P) = |X_G - X_P| + |Y_G - Y_P|$$

3. **Sequential Click Dispatch**:
   - For each step $P_i = (x_i, y_i)$ in $\Pi^*$:
     1. Issue `click(Tile_xi_yi)`.
     2. Sleep $100 - 120$ ms to permit visual avatar translation and state update.
     3. Inspect `StepText` to confirm position transition: $P_{\text{curr}} = (x_i, y_i)$.

4. **Status Confirmation**:
   - Confirm `M22_GridPathfinding.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Non-Adjacent Teleport Clicks**: Clicking directly on the goal tile $(4, 0)$ from the starting tile $(0, 4)$ fails silently because $d_{\text{Manhattan}} = 8 \ne 1$. Grid navigation requires stepwise execution across neighboring cells.
- **Trap: Diagonal Shortcuts**: The mechanic enforces strict 4-way orthogonal connectivity. Attempting diagonal hops ($\Delta X = 1, \Delta Y = 1$) fails with distance 2.
- **Trap: Stun Traps**: Stepping onto $(1, 1)$ triggers a 1-second freeze during which all inputs are dropped. Any pathfinding algorithm must assign infinite cost ($w = \infty$) to hazard trap cells.
