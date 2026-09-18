# Testing and validation of a game module

This reference contains instructions and checklists for verifying that a game module's implementation is correct before committing.

---

## 1. Compilation check and project build

Before running any tests, make sure the project has no C# compilation errors:
- Always check the **Console** tab in the Unity Editor.
- Make sure your new code does not break the build of other Assembly Definitions (`asmdef`). The module's code must live inside the project's corresponding asmdef (for example, `RailwayCow.Runtime.asmdef`).

---

## 2. Quick module testing in the Unity Editor (PlayModule)

The base class `AbstractGameModule` has a built-in tool for testing a module in isolation, without having to run the whole game from the start.

### Running PlayModule:
1. Open the scene or select your module's prefab in the Hierarchy.
2. In the inspector of your module's component, find the **Play Module** button (supported via a custom Odin Inspector editor or built-in buttons).
3. When you press the button:
   - If Unity is not in Play Mode, the editor will automatically start Play Mode.
   - After 0.5 seconds the full initialization lifecycle is invoked: `Initialize()` → `Startable()`.
   - The system automatically runs `InjectComponent` for all registered component providers.
4. During gameplay you can press the **C** key on the keyboard to simulate an `OnComplete()` call.

---

## 3. Checking memory cleanup (Dispose) and leaks

KBPro's modular architecture relies strictly on correct resource cleanup. Failing to clean up leads to subscriptions "hanging" in memory, object leaks in the scene, and bugs when the module is re-run.

### Lifecycle verification checklist:
- [ ] **Calling base methods**:
  - `base.Initialize()` must be called strictly as the **last** line in `Initialize()`.
  - `base.Startable()` must be called strictly as the **first** line in `Startable()`.
  - `base.OnComplete()` must be called strictly as the **last** line in `OnComplete()`.
  - `base.Dispose()` must be called strictly as the **last** line in `Dispose()`.
- [ ] **EventBus**:
  - Every `EventBus<T>.Register(binding)` subscription must have a mirrored `EventBus<T>.Unregister(binding)` call in the `Dispose()` method.
- [ ] **LazySrv**:
  - All declared `LazySrv<T>` fields must be released via a `_service.Dispose()` call in the `Dispose()` method.
- [ ] **DOTween animations**:
  - All started DOTween animations (`DOMove`, `DOFade`, etc.) must end with a `.SetLink(gameObject)` call so tweens are automatically destroyed when the object is destroyed.
  - If a tween is started on a long-lived object, keep a `Tween myTween` reference and call `myTween.Kill()` in `Dispose()`.
- [ ] **Unity Actions and events**:
  - All C# `Action` subscriptions to component events (e.g. `_component.OnSpotClicked += OnSpotClicked`) must be unsubscribed in the logic system's `Dispose()` or the component's `OnDestroy()`.

---

## 4. File encoding control (UTF-8 with BOM)

All source code files (`.cs`), configuration files (`.json`), and documentation files (`.md`) must be saved with **UTF-8 with BOM (Byte Order Mark)** encoding. This is critical for Cyrillic text to display correctly across platforms and in the Unity Editor on Windows.

### How to check and fix the encoding:
In PowerShell you can force re-encode files with this command:
```powershell
# Example for a specific C# file
$path = "Assets/Railway-cow/Scripts/Modules/TrainCleaning/TrainCleaningModule.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($true)))
```
Always run the `sync-ai-rules.ps1` sync script or re-encoding scripts after adding new files.
