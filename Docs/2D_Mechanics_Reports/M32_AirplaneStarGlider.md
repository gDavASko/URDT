# M32: Continuous Altitude Guidance & Horizon Interception (Airplane Star Glider)

## 1. Mechanics Overview
The **Airplane Star Glider** mechanic captures the essence of classic side-scrolling aerial gliders, Flappy/Jetpack continuous altitude modulation, and flight simulation games. The player commands an aircraft (`AirplaneRoot`) situated at fixed longitudinal station $X = -180$ within a restricted atmospheric ceiling ($Y \in [-120, 120]$). Celestial stars and thunder clouds scroll from right to left at $V_{\text{scroll}} = 160\text{ px/s}$. The player steers the aircraft vertically via pointer drags or keyboard keys ($W/S$ / Up/Down) to intercept and collect $N = 5$ golden stars while evading turbulent storm obstacles.

### Core Mechanics & Obstacles:
- **Dynamic Altitude & Pitch Integration**:
  - Steering input updates vertical target: $Y_{\text{target}} = \text{clamp}(Y_{\text{pointer}}, -120, 120)$.
  - Exponential altitude tracking: $Y_{\text{plane}} \leftarrow \text{Lerp}(Y_{\text{plane}}, Y_{\text{target}}, 8 \cdot \Delta t)$.
  - Procedural flight attitude: dynamic pitch angle $\theta = \text{clamp}(V_y \cdot 0.15, -30^{\circ}, 30^{\circ})$ banks the aircraft nose up/down based on vertical climb velocity.
- **Scroll Kinematics & Infinite Torus Wrapping**:
  - Entities scroll at $160\text{ px/s}$.
  - Boundary recycling: entities crossing $X < -320$ instantly wrap around to $X = +320$ (and clouds to $+340$), forming an infinite periodic corridor.
- **Hitbox Geometry & Hazard Evasion**:
  - Star collection radius: $\|\vec{p}_{\text{star}} - \vec{p}_{\text{plane}}\| \le 38\text{ px}$.
  - Thunder cloud turbulence radius: $\|\vec{p}_{\text{cloud}} - \vec{p}_{\text{plane}}\| \le 42\text{ px}$.
  - Entering storm cloud triggers aerodynamic turbulence and warning: `"Турбулентность! Избегайте грозовых туч [X]!"`.
- **Win Condition**: Intercept $5$ golden stars to reach $100\%$ progress and trigger celebratory fly-out.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M32_AirplaneStarGlider` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `FlightContainer` | Viewport Container | Screen center $(640, 332)$, vertical span $[-120, 120]$ |
| `AirplaneRoot` | Aircraft Entity | Longitudinal position $X = 460$ (local $-180$), vertical $Y(t)$ |
| `StarsContainer` | Collection Root | Houses orbital golden star objects |
| `CloudsContainer` | Obstacle Root | Houses procedural storm barriers |
| `StarsCountText` | `UrdtUiTextTarget` | Real-time star counter: `"Звезды: K / 5"` |
| `LocalInstruction` | `UrdtUiTextTarget` | Procedural flight guidance and stall/turbulence alerts |

---

## 3. Mathematical Arrival Schedule & Guidance Law

Let container center be $(X_c, Y_c) = (640, 332)$. The aircraft maintains station at screen coordinate $X_{\text{plane}} = X_c - 180 = 460$.
Initial star orbital positions $(X_{k}, Y_{k})$ relative to container:
- $\text{Star}_0$: $X = +80$, $Y = +55 \implies \text{Screen } Y = 387$
- $\text{Star}_1$: $X = +220$, $Y = -45 \implies \text{Screen } Y = 287$
- $\text{Star}_2$: $X = +360$, $Y = +60 \implies \text{Screen } Y = 392$
- $\text{Star}_3$: $X = +500$, $Y = -20 \implies \text{Screen } Y = 312$
- $\text{Star}_4$: $X = +640$, $Y = +40 \implies \text{Screen } Y = 372$

Relative longitudinal travel distance $\Delta X_k = X_k - (-180)$.
Arrival epoch $t_k = \frac{\Delta X_k}{V_{\text{scroll}}}$:
$$t_0 = \frac{260}{160} = 1.625\text{ s}, \quad t_1 = \frac{400}{160} = 2.500\text{ s}, \quad t_2 = \frac{540}{160} = 3.375\text{ s}, \quad t_3 = \frac{680}{160} = 4.250\text{ s}, \quad t_4 = \frac{820}{160} = 5.125\text{ s}$$

Optimal flight altitude schedule:
$$Y_{\text{control}}(t) = \begin{cases} 387 & \text{for } t \in [0, 1.9]\text{ s} \\ 287 & \text{for } t \in [1.9, 2.8]\text{ s} \\ 392 & \text{for } t \in [2.8, 3.7]\text{ s} \\ 312 & \text{for } t \in [3.7, 4.6]\text{ s} \\ 372 & \text{for } t \in [4.6, 5.5]\text{ s} \end{cases}$$

---

## 4. General Gameplay Algorithm for AI Agents

1. **Perception & Horizon Calibration**:
   - Establish aircraft intercept column: $X = 460$.
   - Read container vertical offset: $Y_c = 332$.

2. **Guidance Waypoint Steer Loop**:
   - For each orbital altitude target $Y_k \in \{387, 287, 392, 312, 372\}$:
     1. Dispatch `pointerDown({ x: 460, y: Y_k })`.
     2. Aircraft lerps smoothly to target altitude ($8 \cdot \Delta t$).
     3. Poll `StarsCountText` and `Urdt2DModuleTarget.ProgressNormalized` every $200$ ms.
     4. When star capture event increments score, transition immediately to next scheduled altitude.
     5. If score reaches $5/5$ ($100\%$): break out immediately.

3. **Flight Termination**:
   - Dispatch `pointerUp`.
   - Confirm `M32_AirplaneStarGlider.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: Clamping Beyond Aerodynamic Ceiling**: Passing pointer coordinates outside $Y \in [212, 452]$ ($Y_{\text{local}} \in [-120, 120]$) is clamped internally, but extreme out-of-bounds clicks can cause pointer raycast dropouts. Always constrain target $Y$ within the active envelope.
- **Trap: Vertical Lag Due to Exponential Smoothing**: Because the aircraft uses exponential smoothing ($\text{speed} = 8$), issuing a steering command at the exact millisecond of star arrival is too late; the aircraft needs approximately $200-300$ ms lead time to ascend/descend. Pre-positioning before the star arrives ensures $100\%$ capture reliability.
- **Trap: Storm Cloud Overlap During Transitions**: Navigating directly through intermediate storm clouds $(X_{\text{cloud}} = 160, 440)$ can induce turbulence. Smooth diagonal climbs/dives minimize dwell time within turbulent zones.
