# M08: Continuous Trajectory Tracing & Hazard Avoidance (Waypoint Tracking)

## 1. Mechanics Overview
The **Waypoint Tracking** mechanic tests continuous trajectory guidance and dynamic obstacle avoidance. The player must trace a tool (such as a wood saw, laser cutter, or scalpel) sequentially through an ordered set of waypoints ($W_0 \rightarrow W_1 \rightarrow \dots \rightarrow W_n$) without lifting the pointer or intersecting proximity-based hazard zones (e.g., knots, nails, or landmines).

### Core Mechanics & Obstacles:
- **Strict Sequential Gate**: Waypoints must be visited in strict numerical order. Visiting $W_2$ before $W_1$ yields no progress.
- **Proximity-Based Hazard Fields**: Static hazard zones with spherical/circular trigger radii ($R_{\text{hazard}} = 50$ px). Intersecting a hazard field immediately resets the tool to start and zeroes out progress.
- **Tolerant Capture Radius**: Each waypoint has a capture radius ($R_{\text{capture}} = 45$ px).
- **Narrow Clearance Channels**: A hazard zone may sit directly adjacent to a waypoint ($D < R_{\text{hazard}} + R_{\text{capture}}$), requiring the trajectory to skirt the waypoint's outer boundary to maintain safe distance from the hazard while satisfying capture criteria.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M08_WaypointTracking` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ToolSaw` | `Urdt2DDraggableTarget` | `ItemId: 'ToolSaw'`, `ScreenCenter` (Controlled tool) |
| `Waypoint_*` | `Urdt2DInteractiveAreaTarget` / Transform | `anchoredPosition`, capture radius |
| `HazardKnot` | `Urdt2DInteractiveAreaTarget` / Transform | `anchoredPosition`, hazard radius ($50$ px) |

---

## 3. Mathematical Obstacle Avoidance & Path Generation

Let the hazard center be $H = (0, -38)$ with trigger radius $R_h = 50$.
Let Waypoint 2 be $W_2 = (0, 0)$ with capture radius $R_c = 45$.

A direct collinear trajectory along $y = 0$ results in:
$$\text{dist}((0, 0), (0, -38)) = 38 < 50 \implies \text{CRITICAL HAZARD COLLISION}$$

To safely clear the hazard while capturing $W_2$:
$$\begin{cases}
\text{dist}(P, H) > 50 & \implies \sqrt{x^2 + (y + 38)^2} > 50 \\
\text{dist}(P, W_2) \le 45 & \implies \sqrt{x^2 + y^2} \le 45
\end{cases}$$

At $x = 0$, choosing $y = +20$:
- Hazard distance: $20 - (-38) = 58 > 50$ (Safe by 8 px)
- Waypoint distance: $20 \le 45$ (Captured)

Converting to screen space with canvas center $(640, 320)$:
- $W_0$: $(440, 338)$
- $W_1$: $(540, 338)$
- $W_2$: $(640, 340)$ (Curved arc clearing the hazard)
- $W_3$: $(740, 338)$
- $W_4$: $(840, 338)$

---

## 4. General Gameplay Algorithm for AI Agents

1. **Path Waypoint Extraction**:
   - Extract the ordered list of waypoint positions and any hazard zones from beacon data.

2. **Trajectory Clearance Planning**:
   - Check segment-by-segment clearance against all known hazard centers.
   - Offset path vertices perpendicularly away from the hazard center so that $\text{dist}(\text{Path}, \text{Hazard}) > R_{\text{hazard}} + 5\text{ px}$, while ensuring $\text{dist}(\text{Path}, \text{Waypoint}) \le R_{\text{capture}}$.

3. **Continuous Motion Dispatch (`press_move`)**:
   - Dispatch the planned curve using `press_move(path, stepsPerSegment=15)`.
   - Continuous pressure ensures `IDragHandler` is invoked without pointer release.

4. **Progress Confirmation**:
   - Confirm `ProgressNormalized` reaches $1.0$ and `IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Linear Interpolation Collisions**: A straight line between $(x_1, y_1)$ and $(x_3, y_3)$ may cut through an intervening hazard. Multi-point arcs must be provided.
- **Trap: Drag Cancellation on Pointer Up**: In waypoint tracing mechanics, releasing the pointer prematurely cancels the cut. Use `press_move` with unbroken pointer contact from first to last point.
