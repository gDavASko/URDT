# Polymorphic Beacon Hierarchy (Clean OOP & SRP)

## Core Architectural Principle

In URDT (Unity Remote Debugging Transport), beacons (markers placed on GameObjects for automated tracking and testing) strictly follow the **Single Responsibility Principle (SRP)** and **Clean Object-Oriented Polymorphism**.

### The Anti-Pattern: Monolithic Markers ("Мусор в маркерах")

In naive architectures, a single `DebugTarget` script is placed on every element. Such monolithic scripts declare fields for every imaginable control:
```csharp
// ❌ ANTI-PATTERN: Monolithic marker with clutter and unused fields
public class MonolithicDebugTarget : MonoBehaviour
{
    public Selectable button;
    public Toggle toggle;         // Empty on Buttons!
    public Slider slider;         // Empty on Buttons, Toggles!
    public InputField input;      // Empty on 90% of objects!
    public Dropdown dropdown;     // Clutters the Unity Inspector!
    public ScrollRect scrollRect; // Violates SRP!
}
```
**Consequences of the Monolithic Anti-Pattern**:
1. **Inspector Noise**: When inspecting a simple Button in Unity, the developer sees 5-8 empty `None (Toggle)`, `None (Slider)` fields with warning clutter.
2. **Brittle Logic**: Serialization bugs occur when serializing empty fields.
3. **Violates SRP**: A Button is NOT a Slider. An InputField is NOT a ScrollRect.

---

## The URDT Solution: Dedicated Polymorphic Hierarchy

Every UI element in URDT uses a dedicated, type-safe subclass deriving from `UrdtUiTarget` (which in turn derives from `UrdtDebugTarget`).

```
                    ┌─────────────────────────┐
                    │     UrdtDebugTarget     │ (Universal Base for ANY GameObject)
                    │   - TargetId            │ (Zero UI coupling)
                    │   - TargetKind          │
                    │   - Module              │
                    │   - Commands (Enum List)│
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │       UrdtUiTarget      │ (UI Base)
                    │   - RectTransform       │ (Geometry & Canvas Mapping ONLY)
                    │   - Canvas              │
                    │   - ScreenRect          │
                    │   - ScreenCenter        │
                    └────────────┬────────────┘
                                 │
     ┌──────────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
     │          │          │          │          │          │          │          │          │
┌────┴───┐ ┌────┴───┐ ┌────┴───┐ ┌────┴───┐ ┌────┴───┐ ┌────┴───┐ ┌────┴───┐ ┌────┴───┐
│ Button │ │ Toggle │ │ Slider │ │ Input  │ │Dropdown│ │ Scroll │ │ Stick  │ │Drawing │
│ Target │ │ Target │ │ Target │ │ Target │ │ Target │ │ Target │ │ Target │ │ Target │
└────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘
```

---

## Beacon Specifications

### 1. `UrdtDebugTarget` (Universal Base)
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtDebugTarget.cs`
- **Responsibility**: Universal beacon for 3D props, actors, cameras, spawners, gameplay triggers, and non-UI systems.
- **Fields**:
  - `TargetId` (string): Stable test identifier.
  - `TargetKind` (string): Semantic kind (`actor`, `prop`, `light`, `trigger`).
  - `Module` (string): Owning subsystem.
  - `ActiveWindow` / `ActiveModule` (string): Hierarchy context.
  - `Commands` (`List<UrdtCommandType>`): Strongly typed list of allowed actions.

### 2. `UrdtUiTarget` (UI Geometry Base)
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiTarget.cs`
- **Responsibility**: Common UI geometry and screen-space mapping.
- **Exposed Properties**: `ScreenRect`, `ScreenCenter`, `RectSize`.
- **Contains**: `RectTransform`, `Canvas`.
- **Zero Controls**: Does NOT contain any button, slider, or toggle fields.

### 3. `UrdtUiButtonTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiButtonTarget.cs`
- **Responsibility**: Clickable buttons and selectables.
- **Inspector Fields**: `Selectable _selectable` ONLY.
- **Supported Commands**: `Inspect`, `Query`, `Click`, `DoubleClick`, `MultiClick`.

### 4. `UrdtUiToggleTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiToggleTarget.cs`
- **Responsibility**: Checkboxes, switches, radio toggles.
- **Inspector Fields**: `Toggle _toggle` ONLY.
- **Exposed Properties**: `ToggleValue` (bool).
- **Supported Commands**: `Inspect`, `Query`, `Click`.

### 5. `UrdtUiSliderTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiSliderTarget.cs`
- **Responsibility**: Value sliders and range adjusters.
- **Inspector Fields**: `Slider _slider` ONLY.
- **Exposed Properties**: `SliderValue` (float).
- **Supported Commands**: `Inspect`, `Query`, `Drag`, `PressMove`, `Swipe`.

### 6. `UrdtUiInputTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiInputTarget.cs`
- **Responsibility**: Text input (supports standard uGUI `InputField` and TextMeshPro `TMP_InputField` via decoupled reflection).
- **Inspector Fields**: `Selectable _inputSelectable` ONLY.
- **Exposed Properties**: `InputValue` (string).
- **Supported Commands**: `Inspect`, `Query`, `Click`, `TypeText`, `KeyPress`.

### 7. `UrdtUiDropdownTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiDropdownTarget.cs`
- **Responsibility**: Dropdown selector menus (uGUI & TMPro).
- **Inspector Fields**: `Selectable _dropdownSelectable` ONLY.
- **Exposed Properties**: `DropdownValue` (int), `DropdownLabel` (string), `DropdownOptions` (string).
- **Supported Commands**: `Inspect`, `Query`, `Click`.

### 8. `UrdtUiScrollTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiScrollTarget.cs`
- **Responsibility**: Scroll views and virtualized lists (`ScrollRect`).
- **Inspector Fields**: `ScrollRect _scrollRect` ONLY.
- **Exposed Properties**: `ScrollPosition` (Vector2), `ScrollContentHeight` (float), `ScrollViewportHeight` (float).
- **Supported Commands**: `Inspect`, `Query`, `Scroll`, `Drag`, `Swipe`.

### 9. `UrdtUiStickTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiStickTarget.cs`
- **Responsibility**: Virtual analog joysticks and thumbsticks (`UrdtVirtualStick`).
- **Inspector Fields**: `UrdtVirtualStick _stick` ONLY.
- **Exposed Properties**: `StickValue` (Vector2 -1..1), `Magnitude` (float 0..1), `IsPressed` (bool), `HandleScreenCenter` (Vector2).
- **Supported Commands**: `Inspect`, `Query`, `Drag`, `PointerDown`, `PointerUp`.

### 10. `UrdtUiDrawingTarget`
- **Path**: `Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiDrawingTarget.cs`
- **Responsibility**: Interactive drawing pads, telemetry canvas surfaces (`UrdtDrawingCanvas`).
- **Inspector Fields**: `UrdtDrawingCanvas _canvas` ONLY.
- **Exposed Properties**: `PenPosition` (Vector2), `StrokeCount` (int), `TotalDrawnLength` (float), `IsDrawing` (bool).
- **Supported Commands**: `Inspect`, `Query`.

---

## Strongly Typed Commands (`UrdtCommandType`)

Never use comma-separated magic strings for commands in code. Use the strongly-typed enum:
```csharp
public enum UrdtCommandType
{
    Inspect = 0,
    Query = 1,
    Click = 2,
    DoubleClick = 3,
    MultiClick = 4,
    Drag = 5,
    MultiDrag = 6,
    Swipe = 7,
    Scroll = 8,
    Pinch = 9,
    PressMove = 10,
    TypeText = 11,
    KeyPress = 12,
    HitTest = 13,
    WaitFor = 14,
    ResetState = 15
}
```
In Unity Inspector, this displays as a reorderable, type-safe enum list with dropdown selection.
