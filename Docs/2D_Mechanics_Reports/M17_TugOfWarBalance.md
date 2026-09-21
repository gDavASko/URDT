# M17: Input Rate Accumulator & Continuous Decay (Tug-of-War Balance)

## 1. Mechanics Overview
The **Tug-of-War Balance** mechanic models continuous physical exertion against an active dissipative force (e.g. rope tug-of-war, rapid-tap button mashing, struggling out of a grapple, or frantic bilge-pumping). A state variable $B(t) \in [-1.0, 1.0]$ represents the physical balance of power between the player and an autonomous opposing force. The player must supply discrete input pulses (`click` events on `BtnPull`) at a frequency high enough to overcome constant decay, driving the balance past a victory threshold ($B \ge B_{\text{win}} = 0.85$).

### Core Mechanics & Obstacles:
- **Discrete Impulse Gain**: Each click on `BtnPull` delivers an instantaneous positive impulse $\Delta B = +K_{\text{tap}} = +0.085$.
- **Continuous Opponent Decay**: While active, the balance continuously dissipates leftward toward the opponent's side according to:
  $$\frac{dB}{dt} = -R_{\text{decay}} = -0.38\text{ s}^{-1}$$
- **Slippage / Hazard Trap**: A secondary button (`BtnSlipHazard`, marked with `[X]`) triggers a sudden rope slip, instantly docking $\Delta B = -0.25$.
- **Victory Condition**: Maintain high-cadence burst input until $B(t) \ge 0.85$ (92.5% visual bar fill), at which point completion locks in permanently.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M17_TugOfWarBalance` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnPull` | `UrdtUiButtonTarget` | ScreenCenter $(530, 255)$, trigger command `click` |
| `BtnSlipHazard` | `UrdtUiButtonTarget` | ScreenCenter $(770, 255)$, contains `Hazard` identifier (avoid!) |
| `BalanceText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Баланс сил: X% (Цель: 92%)"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 444)$, status feedback |

---

## 3. Mathematical Input Frequency Modeling

To achieve net positive drift, the player's input rate $f_{\text{tap}} = \frac{1}{\Delta t_{\text{interval}}}$ must strictly exceed the break-even critical frequency:
$$f_{\text{crit}} = \frac{R_{\text{decay}}}{K_{\text{tap}}} = \frac{0.38}{0.085} \approx 4.47\text{ taps/second} \implies \Delta t_{\text{crit}} \approx 223\text{ ms}$$
Any tapping pace slower than 4.5 taps/s results in net backward retreat.

By adopting a rapid burst cadence of $\Delta t = 45\text{ ms}$ ($f = 22.2\text{ taps/s}$):
$$\frac{dB_{\text{net}}}{dt} = (22.2 \times 0.085) - 0.38 = 1.887 - 0.38 = +1.507\text{ s}^{-1}$$
Time required to advance from neutral ($B = 0$) to victory ($B = 0.85$):
$$T_{\text{win}} = \frac{0.85}{1.507} \approx 0.56\text{ seconds}$$
At $22.2$ taps/s, exactly $N = 30$ consecutive taps across $\approx 1.35$ seconds reliably drives the balance to $95\%$, surpassing the $92\%$ threshold cleanly.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Target Identification & Safety Filter**:
   - Query all buttons within the mechanic module.
   - Filter out targets with identifiers matching `Slip`, `Hazard`, or `Penalty` (`BtnSlipHazard`).
   - Identify the primary input actuator: `BtnPull`.

2. **Rapid High-Frequency Burst Loop**:
   - Set inter-click interval $\Delta t = 45 - 50$ ms.
   - Dispatch discrete `click(BtnPull)` commands sequentially.
   - Periodically (every 5-10 taps) inspect `BalanceText` and `M17_TugOfWarBalance.ProgressNormalized`.
   - Terminate loop immediately once `IsCompleted == true` or progress reaches $1.0$.

3. **Victory Verification**:
   - Confirm `M17_TugOfWarBalance.IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Lazy Polling Latency**: If the client pauses for extensive status inspection or round-trip serialization between every tap (e.g. 300 ms inspection latency per tap), the continuous decay ($-0.38$/s) wipes out the gain of the previous tap, keeping the system permanently stalled near neutral.
- **Trap: False Hazard Actuation**: Under rapid mashing, agents must strictly bind their click coordinates to the centroid of `BtnPull` without drifting into the adjacent `BtnSlipHazard` hitbox.
- **Trap: CPU Throttling during WebSocket Dispatch**: In standalone environments, ensure WebSocket messages are dispatched asynchronously without blocking the event loop to preserve uniform 20+ Hz tapping density.
