# M15: Timing Intercept Window & Moving Object Interception (Timing Interceptor)

## 1. Mechanics Overview
The **Timing Interceptor** mechanic models dynamic moving targets traveling along a linear or curved trajectory towards a fixed intercept boundary or strike zone (analogous to rhythm game note hitting, baseball batting, projectile defense, or conveyor belt sorting). The player must trigger an interaction event (`click` on `BtnIntercept`) precisely when the moving projectile enters the spatial tolerance window around the target boundary ($X_{\text{line}} \pm \Delta X_{\text{tol}}$).

### Core Mechanics & Obstacles:
- **Kinematic Motion**: A projectile spawns at $X_{\text{spawn}} = 220$ and travels leftward toward $X_{\text{exit}} = -190$ at a randomized velocity $V \in [170, 230]$ px/s.
- **Narrow Tolerance Band**: The intercept line is located at $X_{\text{target}} = -100$ with spatial tolerance $\delta = \pm 30$ px. The projectile is valid for interception strictly while $X \in [-70, -130]$.
- **Decoy / Phantom Hazard**: With probability $P_{\text{decoy}} = 28\%$, the incoming object is a phantom decoy (reddish tint / mark). Clicking during a decoy immediately resets the consecutive streak to 0. Decoys must be allowed to bypass the intercept zone without interaction.
- **Streak Requirement**: The player must achieve $N = 3$ consecutive successful interceptions without missing, hitting prematurely/late, or striking a decoy.
- **Deflection Kinematics**: Upon a successful hit, the projectile deflects in reverse at $2 \times V$ toward $X = 250$ (taking $\approx 850 - 950$ ms) before resetting the cycle for the next incoming projectile.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M15_TimingInterceptor` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnIntercept` | `UrdtUiButtonTarget` | `ScreenCenter` $(640, 220)$, trigger command `click` |
| `StreakText` | `UrdtUiTextTarget` | Real-time consecutive counter string (`"Серия перехватов: X / 3"`) |
| `LocalInstruction` | `UrdtUiTextTarget` | Immediate state feedback (`"Точный перехват"`, `"Слишком рано"`, `"Слишком поздно"`, `"Ошибка! Ложный мяч-фантом"`, `"Мяч упущен"`) |
| `MovingBall` *(Dynamic Entity)* | `Urdt2DInteractiveAreaTarget` / Beacon | Instantaneous $X$-coordinate, velocity vector, `IsJunk` flag for decoys |

---

## 3. Mathematical Kinematic & Probability Modeling

### Trajectory Travel Time:
$$\Delta X = X_{\text{spawn}} - X_{\text{target}} = 220 - (-100) = 320\text{ px}$$
At average velocity $\bar{V} = 200\text{ px/s}$:
$$T_{\text{arrival}} = \frac{320}{200} = 1.600\text{ s (1600 ms)}$$

### Tolerance Window in Time:
$$\Delta T = \frac{2 \cdot \delta}{\bar{V}} = \frac{60}{200} = 0.300\text{ s (300 ms)}$$
Across the velocity distribution $V \in [170, 230]$ px/s:
- $V = 170\text{ px/s} \implies T \in [1.706\text{ s}, 2.058\text{ s}]$
- $V = 200\text{ px/s} \implies T \in [1.450\text{ s}, 1.750\text{ s}]$
- $V = 230\text{ px/s} \implies T \in [1.261\text{ s}, 1.521\text{ s}]$

Choosing $T_{\text{click}} = 1600$ ms overlaps with $V \in [181, 218]$ px/s, capturing $>62.5\%$ of valid speeds.

### Deflection Reset Interval:
Distance from strike point ($-100$) to deflection boundary ($250$) is $350$ px.
At deflection speed $2 \bar{V} = 400$ px/s:
$$T_{\text{deflect}} = \frac{350}{400} \approx 0.875\text{ s (875 ms)}$$
Total interval between Hit $i$ and Hit $i+1$:
$$T_{\text{cycle}} = T_{\text{deflect}} + T_{\text{arrival}} = 875\text{ ms} + 1600\text{ ms} = 2475\text{ ms}$$

---

## 4. General Gameplay Algorithm for AI Agents

1. **State Synchronization Phase ($t_0$ Calibration)**:
   - Sample or probe interaction button until a deterministic boundary event occurs:
     - **Decoy Strike / Boundary Reset**: When `LocalInstruction` displays a reset event, mark current timestamp as $t_0$.
     - **Late Pass**: If a late pass is detected, wait $280$ ms for the object to reach boundary $X < -190$ and mark $t_0$.
     - **Direct Hit Probe**: If an initial probe scores a valid hit, initialize streak to 1 and set $t_0 = \text{now}$.

2. **Kinematic Action Execution Loop**:
   - **Hit 1**:
     - Compute sleep interval: $\Delta t_1 = \max(0, (t_0 + 1600\text{ ms}) - \text{now})$.
     - Sleep $\Delta t_1$, then issue `click(BtnIntercept)`.
     - Inspect `LocalInstruction`:
       - If `"Точный перехват"`: increment streak, record new $t_0 = \text{now}$ (deflection start).
       - If failed / decoy: streak resets; immediately loop back to synchronization.
   - **Hit 2 & Hit 3**:
     - Compute sleep interval: $\Delta t_{2,3} = \max(0, (t_0 + 2475\text{ ms}) - \text{now})$.
     - Sleep $\Delta t_{2,3}$, then issue `click(BtnIntercept)`.
     - Read state feedback. If successful, update $t_0$ and continue.

3. **Victory Verification**:
   - Confirm `M15_TimingInterceptor.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Spamming Clicks on Miss**: In this mechanic, any click when the object is outside the tolerance window or during a decoy resets the streak to 0. Rapid unsynchronized spamming guarantees $0\%$ completion rate. Every single click must be planned and paced.
- **Trap: Blind Timing without Decoy Filter**: When dynamic beacons are present, checking `IsJunk` or sprite tint before striking avoids losing streaks to decoy objects.
- **Trap: Unscaled Time vs. Frame Delays**: The game simulation updates via `Time.unscaledDeltaTime`. Any UI thread freezes or WebSocket network latencies directly shift the arrival time; compensating with precise timestamp deltas ($\Delta t = t_{\text{target}} - \text{Date.now()}$) is essential.
