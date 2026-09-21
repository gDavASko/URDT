# M16: Alternating Phase Synchronization & Metronomic Cadence (Rhythm Phase Detector)

## 1. Mechanics Overview
The **Rhythm Phase Detector** mechanic models bipedal locomotion, reciprocating pump operation, dual-oar rowing, or metronomic rhythm matching where two opposing inputs (`Left` and `Right` levers) must be actuated strictly in an alternating sequence within a calibrated temporal cadence window $[T_{\min}, T_{\max}]$. Successfully sustaining the rhythm accumulates charge or fills a fluid reservoir to $100\%$ capacity.

### Core Mechanics & Obstacles:
- **Strict Alternation Rule**: Consecutive presses on the same side ($L \to L$ or $R \to R$) are flagged as coordination errors, breaking cadence and deducting series points ($\Delta S = -1$).
- **Metronomic Cadence Window**: Every subsequent beat must occur within interval $\Delta t \in [0.25\text{ s}, 1.15\text{ s}]$ from the previous beat.
  - $\Delta t < 0.25\text{ s}$: Flagged as `"Слишком быстро!"` (hyperactive jitter/spam), penalizing progress.
  - $\Delta t > 1.15\text{ s}$: Flagged as `"Слишком медленно!"` (stalled momentum), penalizing progress.
- **Defect / Junk Hazard**: A central lever (`BtnJunkLever`) represents a broken/defective mechanism marked with `[X]`. Actuating this decoy trips an emergency reset, deducting 2 points and resetting the phase sequence.
- **Completion Goal**: Sustain $N = 8$ consecutive in-tempo alternating beats to achieve $100\%$ reservoir fill.

---

## 2. Perception & URDT Beacon Architecture

| Target Name | Component Type | Key Properties for Perception |
| :--- | :--- | :--- |
| `M16_RhythmPhaseDetector` | `Urdt2DModuleTarget` | `IsCompleted`, `ProgressNormalized`, `Instruction` |
| `BtnLeftLever` | `UrdtUiButtonTarget` | ScreenCenter $(490, 260)$, trigger command `click` |
| `BtnRightLever` | `UrdtUiButtonTarget` | ScreenCenter $(790, 260)$, trigger command `click` |
| `BtnJunkLever` | `UrdtUiButtonTarget` | ScreenCenter $(640, 260)$, contains `Junk` identifier (avoid!) |
| `BeatsText` | `UrdtUiTextTarget` | ScreenCenter $(640, 416)$, string format `"Ритм-такт: X / 8"` |
| `LocalInstruction` | `UrdtUiTextTarget` | ScreenCenter $(640, 442)$, live feedback on tempo/errors |

---

## 3. Mathematical Cadence Modeling

The valid tempo acceptance interval is:
$$[T_{\min}, T_{\max}] = [0.25\text{ s}, 1.15\text{ s}]$$
The optimal target cadence $\tau^*$ is chosen as the central robust point:
$$\tau^* = \frac{T_{\min} + T_{\max}}{2} = \frac{0.25 + 1.15}{2} = 0.70\text{ s}$$
Factoring in client-server network/IPC round-trip latency ($\approx 50-100$ ms), scheduling an inter-click sleep delay of:
$$T_{\text{delay}} = 500\text{ ms (0.50 s)}$$
results in an effective beat interval of $\Delta t \approx 0.58 - 0.64\text{ s}$, squarely within the center of the acceptance window $[0.25\text{ s}, 1.15\text{ s}]$ with $>300$ ms margin of safety on both boundaries.

---

## 4. General Gameplay Algorithm for AI Agents

1. **Lever Classification & Filter**:
   - Discover all interactive levers via URDT query.
   - Filter out any element whose `TestId` contains `Junk`, `Decoy`, or `Defect` (e.g. `BtnJunkLever`).
   - Identify primary operational levers: $L = \text{BtnLeftLever}$ and $R = \text{BtnRightLever}$.

2. **Phase Alternation Loop**:
   - Initialize state: $\text{currentLever} = L$, $\text{beatCount} = 0$.
   - While $\text{beatCount} < 8$:
     1. Issue `click(currentLever)`.
     2. Wait $50$ ms for target update and inspect `BeatsText`.
     3. Verify `BeatsText` incremented without error.
     4. Toggle phase: $\text{currentLever} = (\text{currentLever} == L) ? R : L$.
     5. If $\text{beatCount} < 8$, maintain metronome pacing: `sleep(500 ms)`.
     6. Increment $\text{beatCount} = \text{beatCount} + 1$.

3. **Status Confirmation**:
   - Confirm `M16_RhythmPhaseDetector.IsCompleted == true` and `ProgressNormalized == 1.0`.

---

## 5. Key Pitfalls & Edge Cases

- **Trap: UI Event Spamming (Zero Sleep)**: Rapidly dispatching 8 clicks in a tight synchronous loop trips the minimum interval detector ($< 0.25$ s) on all subsequent clicks, constantly penalizing and never accumulating points. Rhythm mechanics require deliberate, pacing delays.
- **Trap: Single-Lever Repetition**: Double-clicking the same lever resets progress immediately. Maintaining an explicit state tracker ($\text{lastSide}$) ensures strict phase alternation.
- **Trap: Actuating Center Decoys**: Central levers in dual-phase mechanics often represent neutral/reset actuators. Filtering out targets tagged with `Junk` or examining structural roles prevents accidental phase collapse.
