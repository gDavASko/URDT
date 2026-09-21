# M13: Multi-Lane Runner & Horizontal Discrete Evasion (Lane Switcher)

## 1. Mechanics Overview
The **Lane Switcher Runner** mechanic represents continuous 2D endless-runner style obstacle courses where the player controls an avatar situated across discrete parallel lanes (e.g. 3 lanes: Left, Center, Right). Entities (collectibles like gold coins, and hazards like road barriers) scroll downward toward the player at constant speed ($V = 160\text{ px/s}$). The player must collect a target quota ($5$ coins) while evading hazards.

### Core Mechanics & Obstacles:
- **Discrete Multi-Lane Grid**: Lanes positioned at offsets $[-140, 0, +140]$ px from center line ($x \in \{500, 640, 780\}$ px).
- **Multi-Modal Controls**:
  1. *Swipe Mode*: Horizontal swipe gestures ($\Delta x < -50\text{ px} \implies \text{Left}$, $\Delta x > +50\text{ px} \implies \text{Right}$).
  2. *Half-Screen Tap*: Tapping left/right canvas halves.
  3. *Virtual D-Pad Buttons*: Dedicated UI buttons (`BtnLeft`, `BtnRight`).
- **Obstacle Collisions**: Hitting a barrier (`BarrierHazard` with 'X') deducts coins or resets run progress.
- **Scroll Timing & Hitbox Overlap**: Collectibles check overlap in real-time as they scroll past the avatar's $y$-coordinate.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M13_LaneSwitcherRunner` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnLeft`, `BtnRight` | `UrdtUiButtonTarget` | Direct lane shift triggers |
| `SpawnContainer` | Transform | Parent of scrolling coin and barrier items |
| `PlayerAvatar` | Transform | Current player lane position |

---

## 3. General Gameplay Algorithm for AI Agents

1. **Lane Definition & Coordinate Registration**:
   - Left Lane: $x = 500$ px
   - Center Lane: $x = 640$ px
   - Right Lane: $x = 780$ px

2. **Perception-Driven Reactive Switching**:
   - Continuous polling loop inspects scrolling items approaching $y \in [240, 320]$:
     - If coin in Lane $L$: trigger move toward Lane $L$.
     - If barrier in current lane: shift to adjacent free lane.
   - Dispatch lane shifts via `click(BtnLeft)` or `click(BtnRight)` or horizontal `drag` swipe:
     - Left swipe: `drag({x: 680, y: 300}, {x: 520, y: 300}, 8)`
     - Right swipe: `drag({x: 600, y: 300}, {x: 760, y: 300}, 8)`

3. **Quota Tracking & Completion**:
   - Monitor `ProgressNormalized` as coins are captured ($1/5 \rightarrow 2/5 \rightarrow \dots \rightarrow 5/5$).
   - Await `IsCompleted == true`.

---

## 4. Key Pitfalls & Edge Cases

- **Trap: Over-Steering**: Issuing multiple rapid swipes can cause the avatar to overshoot the center lane directly into an outer hazard. Enforce single-lane cooldowns ($150-200$ ms).
- **Trap: Stale Spatial Positions**: Obstacles move continuously along the vertical axis ($160$ px/s). An AI must evaluate velocities, not just static $y$-positions.
