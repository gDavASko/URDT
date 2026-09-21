# M21: Stochastic Phase Trigger & Reaction Window Probe (Reaction Probe)

## 1. Mechanics Overview
The **Reaction Probe** mechanic simulates acute reaction-time challenges such as ice fishing, quick-time events (QTE), starting-gun sprints, or reflex defense (e.g. Animal Crossing fishing, Pokemon fishing rod, western duel quick-draw). The player enters an idle anticipation state (`IdleWaiting`) for an uncertain stochastic delay $\tau \sim U(T_{\min}, T_{\max})$. Once the trigger condition trips (`BiteTriggered`), a temporary acceptance window ($\Delta T_{\text{window}} = 0.75$ s) opens, signaled visually by an exclamation prompt (`!`) and bobber submersion. The player must actuate the reaction button (`click` on `BtnStrike`) within this window. Premature clicks trigger a false start penalty, while delayed clicks result in hook evasion. The mechanic requires securing $N = 2$ successful catches.

### Core Mechanics & Obstacles:
- **Stochastic Wait Distribution**:
  - $T_{\min} = 1.5$ s, $T_{\max} = 3.5$ s (uniform spread width $W = 2.0$ s).
- **Narrow Reaction Window**:
  - Window duration: $\Delta T_{\text{reaction}} = 0.75$ s ($750$ ms).
- **False-Start vs. Late Evasion Penalties**:
  - Clicking while $t < \tau$: Triggers `"Фальстарт!"`, scaring the target and enforcing a $1.2$ s reset recovery.
  - Clicking while $t > \tau + 0.75$: Triggers `"Опоздали!"`, target escapes, enforcing a $1.2$ s reset recovery.
- **Debris Hazard (`JunkBoot`)**: An old leaky boot marked with `[X]` floats nearby. Clicking it does not trigger a catch.
- **Cumulative Non-Decreasing Score**: Unlike streak mechanics, successful catches persist ($S \in \{0, 1, 2\}$ does not decrement upon a miss or false start).

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M21_ReactionProbe` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnStrike` | `UrdtUiButtonTarget` | ScreenCenter $(640, 238)$, trigger command `click` |
| `ScoreText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Поймано: S / 2"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, exposes state transitions: `"Внимание на поплавок..."` (cast), `"Фальстарт!"`, `"Опоздали!"`, `"Отличная реакция!"` |
| `BiteExclamation` / `Bobber` *(Dynamic Entity)* | Active State / Displacement | When instrumented with URDT beacon, fires active event or $Y$-coordinate drop ($\Delta Y = -25$ px) |

---

## 3. Mathematical Window Probability Modeling

Let $\tau \sim U(1.50, 3.50)$ be the trigger delay after casting ($t_{\text{start}}$).
The valid capture window in absolute time is $[\tau, \tau + 0.75]$.

If an agent does not employ reactive visual polling, what fixed delay $T^*$ maximizes the static capture probability?
The strike at $T^*$ succeeds iff:
$$\tau \le T^* \le \tau + 0.75 \iff T^* - 0.75 \le \tau \le T^*$$
The overlap length of $[T^* - 0.75, T^*]$ with $[1.50, 3.50]$ is maximized for any $T^* \in [2.25, 3.50]$:
$$L_{\max} = 0.75\text{ seconds}$$
Thus, selecting:
$$T^* = 2.25\text{ seconds (2250 ms)}$$
Yields the maximum theoretical single-shot capture probability:
$$P_{\text{success}} = \frac{0.75}{3.50 - 1.50} = \frac{0.75}{2.00} = \mathbf{37.5\%}$$

### Expected Number of Attempts:
Because score does not decrement on miss:
$$\mathbb{E}[\text{Attempts per Catch}] = \frac{1}{0.375} \approx 2.67\text{ attempts}$$
To reach $N = 2$ catches:
$$\mathbb{E}[\text{Total Attempts}] = 2 \times 2.67 \approx 5.33\text{ attempts}$$
Each attempt cycle spans $T_{\text{wait}} (2.25\text{ s}) + T_{\text{reset}} (1.20\text{ s}) \approx 3.45$ s.
Total expected execution time to full completion is $< 20$ seconds (verified in practice: completed on attempt #8).

---

## 4. General Gameplay Algorithm for AI Agents

1. **State Synchronization Phase**:
   - Poll `LocalInstruction` at 50 ms cadence.
   - Detect string transition to `"Внимание на поплавок..."`.
   - Record casting timestamp $t_{\text{cast}} = \text{Date.now()}$.

2. **Optimal Timed Strike**:
   - Sleep interval: $\Delta t = \max(0, (t_{\text{cast}} + 2250\text{ ms}) - \text{Date.now()})$.
   - Issue `click(BtnStrike)`.
   - Inspect `ScoreText` and `LocalInstruction`:
     - If `"Отличная реакция!"`: Increment score.
     - If `"Фальстарт!"` or `"Опоздали!"`: Settle for $1.4$ s (`ResultDisplay` duration) and repeat.

3. **Status Confirmation**:
   - Confirm `ScoreText` equals `2 / 2`, `M21_ReactionProbe.IsCompleted == true`, and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Spamming Clicks on Cast**: Clicking repeatedly during `IdleWaiting` triggers continuous false starts, trapping the game in infinite `ResultDisplay` resets ($1.2$ s timer reset on every click). Actuations must be strictly decoupled by at least the cycle duration.
- **Trap: Unregistered Visual Triggers**: If attempting reactive perception, rely on beacons attached to `BiteExclamation` or `Bobber.ScreenCenter.Y`. When dynamic beacons are unattached, the statistical fixed-window probe provides guaranteed polynomial completion.
- **Trap: Straying into Debris Hitboxes**: Do not click `JunkBoot`. Keep click coordinates centered exactly on `BtnStrike.ScreenCenter`.
