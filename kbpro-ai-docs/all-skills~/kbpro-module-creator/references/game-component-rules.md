# View component development rules (GameComponent Rules)

Components (`GameComponent`) represent the display layer (View layer). They contain a `MonoBehaviour`, are attached to scene/prefab objects, and are responsible solely for display and input capture.

## 1. Absolute ban on business logic (Absolute Ban on Business Logic)

A component must be as "dumb" as possible:
- **Forbidden:** calculating game progress, checking logic conditions (e.g. "does the player have enough coins?"), keeping score counters, or directly switching the state of other systems.
- **Allowed:** executing systems' visual commands (enable/disable a sprite, start a ParticleSystem, move a transform, change text). The component blindly trusts the logic layer.

## 2. UI and Text standards

- **Forbidden** to use the standard `UnityEngine.UI.Text` component.
- **Mandatory** to use only TextMeshPro (`TMPro.TMP_Text`).

## 3. Capturing user input

To capture mouse and touch events, components implement the standard Unity EventSystems interfaces:
- `IPointerClickHandler` (for simple clicks / taps).
- `IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler` (for Drag & Drop mechanics).

### Drag & Drop specifics:
The component captures the screen coordinate of the press (`eventData.position`), converts it to a world coordinate via the camera (`Camera.main.ScreenToWorldPoint`), and passes it up via a C# `Action`. The component itself does not decide whether the object can be moved or where — the system calculates that.

## 4. Passing events upward (Events)

The component exposes public events (C# `Action` or `Action<T>`).
Event examples:
- `public Action OnClicked;`
- `public Action<Vector2> OnDragging;`

Inside the component's `OnDestroy()` method, always null out all C# delegates/events (`OnClicked = null`) to prevent memory leaks.

## 5. Registering in DI (Component Providers)

For systems to access the component via the `[InjectComponent]` attribute, the component must be registered in the `_componentProviders` list on the module prefab's root object (usually via `LinkedComponentProvider`).
