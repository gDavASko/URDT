# URDT UI Automation Troubleshooting & Edge Cases

## 1. Stale EventSystem Selection After Screen / Window Transitions

### Symptom
A simulated click is sent to a valid, visible UI element, `hit_test` confirms the element is the top hit, but `Button.onClick` / `IPointerClickHandler` never triggers.

### Root Cause
When an active button (e.g. `OpenUiSuiteButton`) is clicked, Unity's `EventSystem.current.SetSelectedGameObject(...)` marks it as selected.
If that button's parent window/canvas is subsequently hidden (`SetActive(false)`), Unity **does not automatically nullify** `currentSelectedGameObject`.
The `InputSystemUIInputModule` sees an inactive GameObject holding selection and suppresses or misroutes pointer events.

### Solution
- **In Input Simulator**: Automatically check and clear disabled selections before queueing pointer events:
  ```csharp
  var es = UnityEngine.EventSystems.EventSystem.current;
  if (es != null && es.currentSelectedGameObject != null && !es.currentSelectedGameObject.activeInHierarchy)
  {
      es.SetSelectedGameObject(null);
  }
  ```
- **In UI Controllers**: When toggling windows or dialogs, always invoke:
  ```csharp
  if (EventSystem.current != null)
  {
      EventSystem.current.SetSelectedGameObject(null);
  }
  ```

---

## 2. Dynamic ScrollRect Viewport Occlusion

### Symptom
Target button or input field exists in hierarchy, but clicks hit the background ScrollRect or miss entirely.

### Solution
Always ensure the target is within the ScrollRect's visible viewport bounds before attempting interaction.
Use iterative small swipes / scrolls until `ScreenCenter` falls within the viewport `ScreenRect`.

---

## 3. Strict Decoupling Between Game Controllers and Beacons

### Symptom
Game controllers holding serialized references to `UrdtUiTarget` or manually calling `Record(...)`.

### Solution
- Beacons must strictly observe native Unity events (`Button.onClick`, `Toggle.onValueChanged`, `TMP_InputField.onValueChanged`).
- The gameplay/UI controller must remain 100% vanilla uGUI with zero knowledge of URDT.

---

## 4. Free Aspect & Arbitrary Screen Resolution (Resolution-Agnostic Adaptation)

### Symptom
Clicks hit the wrong area or miss when Game View is in Free Aspect, 4K, or Ultrawide, or the agent assumed static 1080p pixel coordinates.

### Root Cause
Using static pixel constants (e.g. `{ x: 960, y: 540 }` or hardcoded swipe offsets). Unity CanvasScaler scales UI according to screen dimensions, meaning pixel positions differ per resolution.

### Solution
- Never hardcode screen coordinates. Always fetch live coordinates from the beacon's `ScreenCenter` via `inspect` or `query` immediately before sending the command.
- URDT automatically calculates `ScreenCenter` via `RectTransformUtility.WorldToScreenPoint` in current Game View resolution.
- Calculate swipe and drag distances as proportional fractions of the viewport (e.g. `viewportHeight * 0.25`), never as fixed pixel counts.
- Verify `0 <= ScreenCenter.x <= Screen.width` and `0 <= ScreenCenter.y <= Screen.height` before interacting; swipe to bring out-of-bounds elements into view.

---

## 5. Windows PowerShell JSON Escaping Crash in CLI

### Symptom
Running CLI commands like `node Tools/urdt-cli.js call click '{"testId":"btn_foo"}'` crashes with:
`❌ Fatal: Expected property name or '}' in JSON at position 1 (line 1 column 2)`.

### Root Cause
Windows PowerShell parses single quotes `'...'` by stripping inner double quotes `"..."` when passing arguments to Node.js `process.argv`.

### Solution
- Avoid raw JSON strings in PowerShell. Use typed CLI commands:
  ```powershell
  node Tools/urdt-cli.js click btn_foo
  node Tools/urdt-cli.js inspect btn_foo
  node Tools/urdt-cli.js query --active
  ```
- Or run in persistent REPL mode:
  ```powershell
  node Tools/urdt-cli.js --repl
  ```
- If raw JSON is strictly necessary, escape quotes explicitly: `\"{\"testId\":\"btn_foo\"}\"`.

