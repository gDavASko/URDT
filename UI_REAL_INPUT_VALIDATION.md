# Real UI Input Validation

Date: 2026-07-13
Status: PASS - 10 / 10 randomized full runs

## Purpose

This document records the verified real-input UI coverage of URDT. It is an
execution record, not a capability claim based only on code inspection.

## Integrity Rules

- The test scenario is fixed; defects are corrected in URDT, not by changing a
  test assertion or directly setting the UI state.
- Each action follows `observe -> act -> delta`: current state is read with
  `inspect`, device-level input is sent, then the expected visible/debug-state
  change is observed.
- No `Button.onClick.Invoke`, direct UI callback invocation, or component-state
  setter is used by the runner.
- The runner retains no screen-coordinate cache. It resolves every action from
  fresh runtime state.

## Test Composition

The full suite executes the following UI operations in sequence:

1. Open and close a modal window.
2. Focus a TMP input field; enter `привет Мир`; edit it to `приМир`; clear it.
3. Turn a toggle on and then off.
4. Drag a slider to `0.5`, `0.3`, `1.0`, and `0.0`.
5. Select `Mode C`, `Mode A`, and `Mode B` in a dropdown.
6. Single-click and double-click the primary button.
7. Navigate scroll content and verify state movement in both directions.
8. Execute all preceding operations in one continuous run.

Additional coverage verifies navigation from the UI suite back to the main menu.

## Randomization and Discovery

- Existing UI controls are rearranged when the suite is entered; their type and
  stable `TestId` remain unchanged.
- Dropdown option order is randomized.
- The runner calls `inspect` before every action and derives actionable points
  from the current target state.
- Dropdown options are selected by discovered labels, never by a remembered
  index or a hard-coded screen position.

## Input Paths Exercised

- Virtual mouse: click, double click, slider drag, wheel scroll, and swipe.
- Virtual touchscreen: tap and swipe paths.
- Virtual keyboard plus native text input: Unicode TMP text entry and editing.
- uGUI `EventSystem` and `InputSystemUIInputModule` process the input naturally.

## Results

- One post-fix full run: PASS, 46 recorded actions.
- Nine additional full runs in the same Unity Play Mode: PASS, with 41, 45, 49,
  43, 41, 49, 43, 45, and 41 recorded actions.
- Final result: **10 / 10 PASS**.
- Unity compile validation after C# changes: `debug_get_errors` returned `0`.

## Failures Found and URDT Fixes

| Failure | Cause | URDT fix |
| --- | --- | --- |
| Some buttons received a queued mouse click without a state delta. | The pressed state could be released without a processed hold frame. | Standard clicks hold the mouse button for at least one input frame before release. |
| Outer scrolling was slow and could target a nested ScrollRect. | Generic swipe point resolution allowed the nested control to consume the gesture. | Targeted ScrollRect swipe resolves a live non-scrollable descendant point for the addressed container. |
| The runner rejected partially clipped large nested controls. | It required the entire target bounds to fit the outer viewport. | It accepts a currently visible actionable center, then validates the real action delta. |
| Wheel navigation required many small steps. | Wheel input is intentionally granular. | The stress runner uses a real virtual-mouse swipe for fast outer navigation; wheel remains separately validated. |

## Scope and Limitations

This evidence covers Unity uGUI on the tested Input System setup. It does not
claim coverage for UI Toolkit, legacy `Input.*` polling, or arbitrary third-party
controls that bypass the standard EventSystem pipeline.

## Artifacts

- [URDT_UI_Debug_Plan.md](URDT_UI_Debug_Plan.md): immutable scenario and process.
- [URDT_UI_Debug_Report.md](URDT_UI_Debug_Report.md): chronological report and
  estimated token accounting.
- [Tools/randomized-ui-stress-runner.js](Tools/randomized-ui-stress-runner.js):
  real-input semantic runner.