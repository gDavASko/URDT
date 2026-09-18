# Unity CLI Integration & Separation of Concerns

## Division of Responsibilities

When developing and automating Unity projects with AI, maintain strict boundaries between **Unity CLI** and **URDT**:

```
┌─────────────────────────────────────────────────────────────┐
│                          Unity CLI                          │
│  - Editor process lifecycle (`unity open`, `unity build`)   │
│  - Asset management, scene opening, and saving              │
│  - Play Mode control (`command editor_play`, `editor_stop`) │
│  - Compilation status verification (`command recompile`)    │
└──────────────────────────────┬──────────────────────────────┘
                               │
               (Starts Editor into Play Mode)
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                         URDT System                         │
│  - Sole authority for UI inspection, query, and assertions  │
│  - Honest device-level input injection (clicks, drags, keys)│
│  - Viewport-aware scrolling and state verification          │
│  - Continuous test runner automation over WebSocket         │
└─────────────────────────────────────────────────────────────┘
```

---

## Allowed Unity CLI Operations

1. **Check connected Editor instances**:
   ```bash
   unity status
   ```
2. **Recompile scripts and verify compilation status**:
   ```bash
   unity command recompile
   unity command recompile_status
   ```
3. **Open scenes and enter Play Mode**:
   ```bash
   unity command open_scene "Assets/Scenes/MyScene.unity"
   unity command editor_play
   ```
4. **Exit Play Mode and save assets**:
   ```bash
   unity command editor_stop
   unity command save_scene
   ```

---

## Forbidden Anti-Patterns

- **NEVER** use Unity CLI to directly invoke UI callbacks (e.g. evaluating C# to call `button.onClick.Invoke()`). This bypasses the graphics raycaster, active canvas state, and interactability checks.
- **NEVER** use screenshots or image vision for UI assertions. All assertions must come from the live URDT WebSocket protocol state.
