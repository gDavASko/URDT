# Snippet for a game project's AGENTS.md / CLAUDE.md

Copy the block below into the Unity game's `AGENTS.md` (or `CLAUDE.md`) so that any AI working in that project knows
URDT is available and how to use it. Replace `<URDT>` with the path of the URDT clone.

```markdown
## In-game verification (URDT)

This game has the URDT package (`com.davasko.urdt`). An AI agent (URDT L3) can play the game with honest input and
verify features. Full guide: `<URDT>/Docs/URDT_AI_Guide.md`.

- After changing a mechanic, screen or flow: compile (0 errors) → enter Play Mode (URDT auto-starts on
  ws://127.0.0.1:7777) → run a task with MCP `urdt_run_task` (or `npm --prefix <URDT>/CoreAgent run urdt -- run task.json`)
  → fix what the findings say → repeat. Do not call a feature done on compilation alone.
- Give every gameplay object a beacon (`Urdt2DModuleTarget` on level roots, `Urdt2DDraggableTarget`,
  `Urdt2DSlotTarget`, `Urdt2DInteractiveAreaTarget`); expose state as public read-only properties; read input via
  EventSystem / Input System only (no `UnityEngine.Input`, no `OnMouse*`).
- What the agent learned about this game is in `URDT_Knowledge/` — commit it. If a task result has `skillRequest`,
  write a controller for that mechanic and submit it with `urdt_submit_skill`.
- The agent never calls game code and never changes `Time.timeScale`.
```
