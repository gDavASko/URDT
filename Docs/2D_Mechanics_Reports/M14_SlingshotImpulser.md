# M14: Reverse-Vector Ballistic Slingshot & Lob Trajectory (Slingshot Impulser)

## 1. Mechanics Overview
The **Slingshot Impulser** mechanic simulates projectile launching using reverse-vector elastic deformation and parabolic Newtonian ballistics (e.g., Angry Birds, mortar firing, pinball pull-plungers). The player drags a projectile away from a fixed anchor point $\vec{A}$ by displacement $\vec{D} = \vec{P} - \vec{A}$. Upon release, the projectile launches with an initial velocity vector opposing the displacement:
$$\vec{V}_0 = -K_{\text{force}} \cdot \vec{D}$$
under downward vertical gravitational acceleration $g = 250\text{ px/s}^2$. The objective is to knock down 3 targets (cans) positioned at varying elevations behind a vertical barrier pillar.

### Core Mechanics & Obstacles:
- **Reverse Vector Law**: Pulling backward (left/down) accelerates the projectile forward (right/up).
- **Parabolic Arc Physics**:
  $$X(t) = X_0 + V_x t$$
  $$Y(t) = Y_0 + V_y t - \frac{1}{2} g t^2$$
- **Obstacle Occlusion (Central Pillar)**: A tall vertical obstacle pillar ($X = 30$, $Y_{\text{top}} = +57$) blocks flat/direct line-of-sight trajectories. Direct horizontal shots strike the pillar and ricochet backward.
- **Mortar / High-Arc Solution**: To hit targets behind the pillar, shots must be launched along elevated parabolic trajectories that peak above the pillar height ($Y_{\text{peak}} > +57$) before descending into target hitboxes.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M14_SlingshotImpulser` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ProjectileBall` | `Urdt2DDraggableTarget` | Screen center $(460, 280)$ (Anchor) |
| `TargetCan_1` | `Urdt2DSlotTarget` / Entity | Upper target $(810, 370)$, rotation tracking |
| `TargetCan_2` | `Urdt2DSlotTarget` / Entity | Mid-deep target $(850, 310)$, rotation tracking |
| `TargetCan_3` | `Urdt2DSlotTarget` / Entity | Lower target $(810, 250)$, rotation tracking |
| `Obstacle_Pillar` | Transform | Shielding pillar $(30, -20)$, height $130$ px |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Ballistic Simulation & Trajectory Optimization**:
   - For each target $T_i = (\Delta X, \Delta Y)$ relative to anchor:
   - Perform forward simulation of the trajectory equation:
     $$P(t) = A + 7 \cdot \vec{\text{pull}} \cdot t + \frac{1}{2}(0, -g) t^2$$
   - Filter candidate $(\text{pull}_x, \text{pull}_y)$ combinations where:
     1. $|\vec{\text{pull}}| \le 90$ px.
     2. $\text{dist}(P(t), \text{Pillar}) \ge 40$ px for all $t$.
     3. $\text{dist}(P(t_{\text{hit}}), T_i) \le 35$ px.

2. **Calibrated Slingshot Drag Solutions**:
   - **TargetCan_3 (Lower Target)**:
     - Pull: $dx = 80, dy = 4$
     - Drag to: $(460 - 80, 280 - 4) = (380, 276)$
   - **TargetCan_2 (Mid-Deep Target)**:
     - High-arc pull: $dx = 37, dy = 30$
     - Drag to: $(460 - 37, 280 - 30) = (423, 250)$
   - **TargetCan_1 (Upper Target)**:
     - High-arc mortar pull: $dx = 42, dy = 35$
     - Drag to: $(460 - 42, 280 - 35) = (418, 245)$

3. **Sequential Shot Execution**:
   - For each shot:
     - Issue `drag(Anchor, PullPosition, steps=15)`.
     - Allow flight and settle interval ($2000-2500$ ms).
     - Verify can knockdown via target rotation ($z = 75^\circ$) and progress increment.

4. **Status Confirmation**:
   - Confirm all 3 cans knocked down: `ProgressNormalized == 1.0` and `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Pulling Toward the Target**: Intuitive human UI might suggest dragging toward the target. In slingshot mechanics, dragging toward $(+X, +Y)$ shoots backward into the left wall. The drag vector must be strictly inverted: $\vec{P}_{\text{pull}} = \vec{A} - \vec{D}$.
- **Trap: Flat Trajectory Collision**: Attempting fast shots ($dx > 70$) flattens the parabola, slamming directly into the obstacle pillar. High-arc mortar shots ($dx \in [35, 45]$, $dy \in [30, 40]$) clear the obstruction cleanly.
