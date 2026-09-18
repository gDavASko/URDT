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
