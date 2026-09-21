# M10: Timed Pressure Accumulator & Zone Release (Timed Hold)

## 1. Mechanics Overview
The **Timed Accumulator** mechanic models continuous pressure accumulation, gauge charging, or spring winding where an interactive button must be pressed and held continuously (`pointer_down`), allowing a scalar state variable to integrate upwards at a steady rate until it enters a designated target acceptance band (e.g. $[70\%, 85\%]$). Releasing the pointer (`pointer_up`) inside this zone locks in the value and completes the test.

### Core Mechanics & Obstacles:
- **Linear Accumulation**: While held, value increases according to $\frac{dV}{dt} = R_{\text{fill}} = 0.35\text{ s}^{-1}$.
- **Target Acceptance Band**: The gauge must be released strictly within $[V_{\min}, V_{\max}] = [0.70, 0.85]$.
- **Blowout / Overcharge Hazard**: Exceeding the critical safety limit ($V \ge 0.92$) triggers an explosive pressure blow-off, resetting $V$ to $0$.
- **Passive Decay**: If released prematurely below $V_{\min}$, pressure leaks out at rate $R_{\text{decay}} = 0.20\text{ s}^{-1}$.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M10_TimedAccumulator` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `ButtonPump` | `Urdt2DInteractiveAreaTarget` | `ScreenCenter`, `AreaType: 'interactive_zone'` |

---

## 3. Mathematical Hold Time Modeling

To hit the exact center of the target green band $V^* = \frac{0.70 + 0.85}{2} = 0.775$:
$$T_{\text{hold}} = \frac{V^*}{R_{\text{fill}}} = \frac{0.775}{0.35\text{ s}^{-1}} \approx 2.214\text{ seconds}$$

Acceptable hold window:
$$T_{\min} = \frac{0.70}{0.35} = 2.000\text{ s}$$
$$T_{\max} = \frac{0.85}{0.35} = 2.428\text{ s}$$

A hold duration of $T = 2.20\text{ s}$ ($2200$ ms) lands precisely at $V = 2.20 \times 0.35 = 0.770$ (77.0%), safely centered within the $[70\%, 85\%]$ tolerance window and well clear of the $92\%$ blowout threshold.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Parameter Extraction**:
   - Query target `ButtonPump.ScreenCenter`.
   - Identify fill rate ($0.35$) and target window ($[0.70, 0.85]$) from metadata or level specifications.

2. **Timed Hold Execution**:
   - Issue `pointer_down(ButtonPump.ScreenCenter)`.
   - Start high-resolution async sleep for $T = 2200$ ms.
   - Issue `pointer_up(ButtonPump.ScreenCenter)`.

3. **Status Verification**:
   - Wait $200$ ms for the release callback to register.
   - Confirm `IsCompleted == true`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Single-Click Desync**: Sending a simple `click` event fires down and up within a single frame, resulting in negligible charge ($V \approx 0.005$). True hold interactions require explicit, decoupled `pointer_down` and `pointer_up` calls separated by a deliberate duration.
- **Trap: System Lag / Frame Drops**: If running on low-spec hardware where frame rate drops, unscaled time vs. scaled time matters. In URDT, commands are evaluated against real/deterministic frame deltas.
