---
name: unity-ui-mvp-builder
description: "UI screens, popups (UIPBase, UIVBase, UIPWindow). TextMeshPro only. Triggers: UI screen, UI window, popup, UIPWindow, UIVBase, show params, open UI."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Presentation/UISystem_Core.md
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Presentation/UISystem_Game.md
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Presentation/UISystem_Orthodox.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
license: project-internal
validated_against:
  - AGENTS.md
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
forbidden_actions:
  - Do not use UnityEngine.UI.Text — always TextMeshPro (TMP_Text, TextMeshProUGUI).
  - Do not bypass KBPro MVP — all UI screens must have a separate Presenter (P) and View (V).
  - Do not put business logic in UIVBase subclasses — logic belongs in the Presenter.
  - Do not open UI by direct reference — always use EventBus<EventShowComponent>.
  - Do not hardcode strings — use localization keys or ConstSelector constants.
  - Do not layout UI without checking existing screen conventions first.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Presentation/UISystem_Core.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
known_risks:
  - Adding business logic to the View class instead of the Presenter.
  - Forgetting to unsubscribe View events (button clicks) in Dispose/Hide.
  - Opening UI by direct code call instead of EventBus — breaks UI system routing.
  - Breaking localization by hardcoding display strings.
---

# Unity UI MVP Builder

## Purpose

Create and modify UI screens, windows, and popups using the KBPro MVP stack. Every UI component has a Presenter (logic, data) and a View (layout, animation, references), opened via EventBus.

## When To Use

- Creating a new game screen, menu, popup, or HUD element.
- Modifying an existing UI window's data, layout, or show/hide behavior.
- Reviewing UI code for MVP compliance, localization, or TextMeshPro usage.

## Do Not Use

- Do not use this for in-world 3D UI — this skill is for Canvas-based UI only.
- Do not redesign the UI architecture — use the existing MVP stack.

## KBPro MVP Pattern

```
UIPWindow (Presenter):       UIPBase<UIVWindow>
UIVWindow (View):            UIVBase, IUIShowParams → UIVWindowParameters

Opening:
EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.MY_SCREEN));

Show with params:
EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.MY_SCREEN, new MyParams { Score = 42 }));
```

### Presenter (UIPWindow subclass)
- Holds references to services via `LazySrv<T>`.
- Subscribes to data events in `Initialize`; unsubscribes in `Dispose`.
- Calls `View.Show(params)` and `View.Hide()`.
- Does NOT reference Unity UI components directly.

### View (UIVBase/UIVWindow subclass)
- `[SerializeField]` references to TMP_Text, Image, Button, etc.
- Exposes Unity events (button clicks) via `UnityAction` or `Action` — Presenter subscribes.
- DOTween show/hide animations use `SafeKill()`.
- No business logic.

### Show Params (IUIShowParams)
```csharp
public class MyScreenParams : IUIShowParams
{
    public int Score;
    public string PlayerName;
}
```

## Workflow

1. Read `kbpro-ai-docs/kbpro-wiki/raw/Architecture/Presentation/UISystem_Core.md`.
2. Find existing screens in `Assets/Core/Scripts/Core/ScriptSourses/UI/` for naming/layout conventions.
3. Create `UIPMyScreen.cs` (Presenter) and `UIVMyScreen.cs` (View) in correct folders.
4. Create `UIVMyScreenParameters.cs` (IUIShowParams) if the screen receives data.
5. Register the constant in `Constants.UICOMPONENT`.
6. Open via `EventBus<EventShowComponent>.Raise(...)`.

## Output Format

- Show Presenter, View, and Parameters as separate files.
- List serialized references the designer must wire in the prefab.
- State any EventBus subscriptions and their unsubscribe in Dispose.

## Test Prompts

1. "Create a score popup screen with player name and score value using KBPro MVP."
2. "Add a close button to an existing UIVWindow — show Presenter subscription and unsubscribe."
3. "Review this UI screen — does it follow KBPro MVP? What's wrong?"
4. Negative: "Open this screen by calling UIPMyScreen directly from a module." Skill should enforce EventBus routing.
