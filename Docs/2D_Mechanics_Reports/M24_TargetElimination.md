# M24: Direct Kinematic Interception & Hazard Discrimination (Target Elimination)

## 1. Mechanics Overview
The **Target Elimination** mechanic simulates shooting galleries, clay pigeon skeet shooting, balloon popping, or aerial intercept defense (e.g. Duck Hunt, Fruit Ninja slice taps, Point Blank gallery, anti-air point defense). Dynamic entities (`PoppableTarget`) continuously spawn along the bottom boundary ($Y_{\text{spawn}} = -180$) across a random horizontal distribution $X \in [-220, 220]$ and rise vertically at velocities $V_y \in [90, 160]$ px/s. The player must dispatch targeted clicks (`click` at screen coordinates $(X, Y)$) to pop rising bubbles while discriminating against lethal hazard bombs marked with `[X]`. The goal is to pop $N = 6$ clean bubbles.

### Core Mechanics & Obstacles:
- **Kinematic Ascending Targets**:
  - Spawn interval: $T_{\text{spawn}} = 0.85$ s.
  - Ascending velocity: $V_y \in [90, 160]$ px/s.
  - Target radius: $R_{\text{hit}} \approx 32.5$ px (hitbox diameter $65 \times 65$ px).
  - Screen boundary despawn: $Y > 210$.
- **Lethal Explosive Hazard (`HazardBomb`)**:
  - Spawn probability: $P_{\text{bomb}} = 25\%$ ($75\%$ clean bubbles, $25\%$ spike bombs).
  - Clicking a hazard bomb triggers an explosive detonation deducting $2$ points ($\Delta S = -2$), bounded below by $0$.
- **Clean Bubble Pop**:
  - Clicking a clean bubble grants $+1$ point ($\Delta S = +1$).
- **Completion Goal**: Accumulate $S = 6$ net pops to achieve $100\%$ progress.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M24_TargetElimination` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ScoreText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Лопнуто: S / 6"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback: `"Пузырь лопнут! (S/6)"` vs `"ВЗРЫВ БОМБЫ [X]! Штраф -2 очка!"` |
| `PoppableTarget` *(Dynamic Entity)* | `IPointerClickHandler` | Ephemeral runtime entity receiving device clicks |

---

## 3. Mathematical Drift Modeling & Grid Dispersion

Let $B$ denote the event of striking a bubble ($\Delta S = +1$) with prior probability $P(B) = 0.75$, and $H$ denote striking a hazard bomb ($\Delta S = -2$) with prior probability $P(H) = 0.25$.
For any unguided click that intersects a rising entity:
$$\mathbb{E}[\Delta S] = P(B) \cdot (+1) + P(H) \cdot (-2) = (0.75 \times 1) - (0.25 \times 2) = 0.75 - 0.50 = \mathbf{+0.25}$$

Because the expected value per hit is strictly positive ($\mathbb{E}[\Delta S] > 0$), the cumulative score under spatial grid sampling exhibits a steady positive drift:
$$S(t) \sim S_0 + 0.25 \cdot N_{\text{hits}}(t)$$

### Spatial Grid Sampling Matrix:
Given bubble diameter $65$ px, establishing a 2D sampling grid with horizontal pitch $\Delta X = 60$ px across $X \in [460, 820]$ and vertical tiers $Y \in \{240, 290, 340\}$ creates an impenetrable intercept net through which rising targets cannot pass undetected.

When a bomb detonation is detected via `LocalInstruction` (`"ВЗРЫВ БОМБЫ"`), pausing input for $900$ ms lets existing hazard bombs exit the screen ceiling while fresh, high-probability clean bubbles populate the airspace.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Airspace Geometry Mapping**:
   - Define intercept corridor: $X \in [460, 820]$ (step $60$ px), $Y \in [240, 340]$ (step $50$ px).
   - Initialize $S = 0$.

2. **Intercept Grid Dispatch Loop**:
   - While $S < 6$:
     - For each altitude tier $Y_k \in \{240, 290, 340\}$:
       - For each column $X_j \in \{460, 520, 580, 640, 700, 760, 820\}$:
         1. Issue `click({ x: X_j, y: Y_k })`.
         2. Sleep $35$ ms.
         3. Inspect `ScoreText`.
         4. If score changed:
            - Inspect `LocalInstruction`.
            - If `"ВЗРЫВ БОМБЫ"`: Pause $900$ ms for airspace clearance.
            - Update $S = S_{\text{new}}$.
            - If $S \ge 6$: Terminate immediately.

3. **Status Confirmation**:
   - Confirm `M24_TargetElimination.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Uncontrolled Clustered Spamming**: Rapidly clicking in a tight cluster where a bomb just detonated causes multiple hits on the same bomb before it despawns, driving the score to $0$. An enforced cooldown ($\ge 900$ ms) upon bomb feedback is essential.
- **Trap: Leading the Target Incorrectly**: When dynamic beacons are not attached to moving projectiles, trying to predict individual bubble coordinates without vision is fragile. Dense spatial grid sweeps reliably intercept targets without needing visual tracking.
- **Trap: Edge Desync Outside Screen Boundaries**: Clamping click coordinates strictly within $[460, 820] \times [220, 360]$ ensures clicks stay within the Canvas graphic raycast receiving area.
