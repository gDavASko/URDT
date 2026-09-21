# M29: Continuous Path Orthogonal Projection & Geometric Tracing (Contour Cutting)

## 1. Mechanics Overview
The **Contour Cutting** mechanic simulates precision robotic manufacturing, laser tracing, surgical incisions, or scissors cutting games (e.g. cutting cardboard templates, stencils, fabric patterns). The player controls a continuous cutting tool (`ScissorTool`) that must trace an 8-pointed star contour composed of $N = 16$ vertices ($8$ outer peaks at $R_{\text{outer}} = 120\text{ px}$, $8$ inner valleys at $R_{\text{inner}} = 70\text{ px}$) centered at $(640, 332)$. The tool must start at the designated rightmost anchor (Vertex $0$, screen coordinates $(760, 332)$), follow the closed star perimeter clockwise, and stay strictly within the spatial tolerance envelope ($R_{\text{tol}} = 65\text{ px}$) until the star is completely detached ($100\%$ progress).

### Core Mechanics & Obstacles:
- **Orthogonal Vector Projection**:
  - For each segment between vertices $P_k$ and $P_{k+1}$, local pointer coordinate $\vec{p}$ is orthogonally projected onto the segment vector $\vec{s} = P_{k+1} - P_k$:
    $$t = \text{clamp}\left( \frac{(\vec{p} - P_k) \cdot \vec{s}}{\|\vec{s}\|^2}, 0, 1 \right), \quad \vec{p}_{\text{proj}} = P_k + t\vec{s}$$
- **Tolerance Envelope & Off-Track Detection**:
  - Distance threshold: $d = \|\vec{p} - \vec{p}_{\text{proj}}\| \le 65\text{ px}$.
  - If $d > 65\text{ px}$, tool state switches to `_isOffTrack = true`, tool color shifts red (`#FF5555`), cutting progress freezes, and feedback displays: `"Сход с линии! Верните инструмент ближе к пунктиру."`.
- **Sequential Segment Advance**:
  - A segment $k$ is severed and marked neon green when $t \ge 0.75$ or distance to $P_{k+1} \le 33.6\text{ px}$.
  - Progress advances by $1/16$ ($6.25\%$) per segment.
- **Victory Condition**:
  - Closing all 16 segments advances progress to $1.0$ ($100\%$), firing `CompleteMechanic()`.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M29_ContourCutting` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ContourContainer` | Transform Container | Center $(640, 332)$, parent coordinate frame |
| `ScissorTool` | Dynamic Tool Target | Starts at $(760, 332)$, tracks pointer position |
| `Segment_0` .. `Segment_15` | Visual Feedback Beacons | Turn green (`#1AFF8C`) upon cut completion |
| `ProgressText` | `UrdtUiTextTarget` | Screen format `"Вырезано: P%"` |
| `LocalInstruction` | `UrdtUiTextTarget` | Real-time tracking status and edge warnings |

---

## 3. Mathematical Coordinate Modeling & Spline Path

The 8-pointed star vertices are defined analytically:
$$\theta_k = \frac{k}{16} \cdot 2\pi, \quad r_k = \begin{cases} 120\text{ px} & \text{if } k \text{ is even} \\ 70\text{ px} & \text{if } k \text{ is odd} \end{cases} \quad (k \in \{0, 1, \dots, 16\})$$

Transforming from container-local coordinates to Canvas screen coordinates:
$$X_k = 640 + r_k \cos(\theta_k), \quad Y_k = 332 + r_k \sin(\theta_k)$$

The complete path is a 17-point closed polygonal chain:
$$\mathcal{P} = \big[ (X_0, Y_0), (X_1, Y_1), \dots, (X_{15}, Y_{15}), (X_0, Y_0) \big]$$

Executing a unified `press_move` along $\mathcal{P}$ with linear interpolation steps per segment ($S_{\text{seg}} = 8$) synthesizes a continuous 131-frame cursor trajectory that stays within $d < 5$ px of the mathematical contour, far below the $65$ px error threshold.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Analytical Trajectory Generation**:
   - Query `ContourContainer.screenPosition` $\rightarrow (X_c, Y_c) = (640, 332)$.
   - Generate waypoints array $\mathcal{P}$ for $k = 0 \dots 16$ using $R_{\text{outer}} = 120$ and $R_{\text{inner}} = 70$.
   - Verify start waypoint $(760, 332)$ matches `ScissorTool.screenPosition`.

2. **Continuous Path Dispatch**:
   - Call URDT `press_move`:
     `c.call('press_move', { path: P, steps: 8 })`
   - URDT driver enqueues `PointerPhase.Down` at $(760, 332)$, generates 8 intermediate `Move` steps along each of the 16 star segments, and issues `PointerPhase.Up` at the terminal loop closure.

3. **Status Confirmation**:
   - Inspect `ProgressText` $\rightarrow `"Вырезано: 100%"`.
   - Confirm `M29_ContourCutting.IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Starting Away from Vertex 0**: If the drag gesture begins at an arbitrary segment or in the star center, the mechanic rejects the input (`"Начните с точки СТАРТ!"`) and ignores movements until the tool touches the initial tolerance circle at $(760, 332)$.
- **Trap: Sub-Segment Shortcut Cutting**: Moving directly from Vertex 0 to Vertex 4 across the interior skips intermediate vertices; the mechanic enforces strict monotonic sequential order ($k \to k+1$), causing all shortcut movements to register as off-track errors.
- **Trap: Incomplete Loop Closure**: Stopping at Vertex 15 leaves the final segment uncut ($15/16 = 93.75\%$). The trajectory must explicitly terminate by returning to Vertex 0 ($k = 16$).
