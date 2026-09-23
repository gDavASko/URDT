---
name: urdt-game-verification
description: Verify a Unity gameplay module you just built by letting the URDT L3 agent play it in the live game with honest input, then fix what it reports. Use after implementing or changing any game mechanic, UI screen or flow in a URDT-instrumented Unity project, or when asked to "test in game", "play-test", "check the mechanic works", "close the AI dev loop".
---

# URDT game verification (meta-AI ↔ L3 agent)

> Setting URDT up in a new project (package install, auto-start, CoreAgent, MCP registration for Claude/Codex/Cursor):
> `Docs/URDT_AI_Guide.md`.

You are the **meta-AI**: you know what the module should do. URDT's **L3 agent** knows how to *play*:
it reaches the screen, perceives the game through semantic beacons (never pixels), acts with real
synthetic input (mouse/touch through Unity Input System + EventSystem, frame-paced), explores unknown
mechanics, and reports evidence. You give it a goal and a definition of success; it gives you back
`success | fail | needs_clarification | blocked`, findings and evidence. Then you fix code and re-run.

## 1. The loop

```
build/change module → compile (0 errors) → Play Mode → urdt_run_task → read result
      ↑                                                                    │
      └──── fix code (use findings + evidence packet) ←────── fail / finding
```
Repeat until `status: success` **and** no CRITICAL/MAJOR findings. Never mark the work done on
compilation alone.

## 2. Bring the game up

1. Editor open on the project (on this machine D3D12 crashes the editor on Play Mode exit):
   `unity open <project> --editor-version <ver> --args -force-d3d11`
2. After code changes: `unity command editor_stop` → `unity command recompile` → poll
   `unity command recompile_status` until `completed` with `compilationFailed:false`.
3. `unity command editor_play`. URDT listens on `ws://127.0.0.1:7777` (token `urdt-test-poligon`).
4. Check: MCP `urdt_status` (or `npm --prefix CoreAgent run urdt -- status`) → `runtime_ready: true`.

Play Mode may be restarted any time — the agent waits for the runtime and retries the task once.

## 3. Tools

MCP server (register once):
`claude mcp add urdt -- npx --prefix E:/Projects/URDT/CoreAgent tsx E:/Projects/URDT/CoreAgent/src/mcp/urdt_mcp_server.ts`

| Tool | Use |
|---|---|
| `urdt_status` | runtime readiness |
| `urdt_observe {scope?}` | what the agent sees: windows, modules, beacons (id, kind, role, text, live `game` state). Use it to write precise success predicates. |
| `urdt_run_task {task}` | run a verification task (schema §4); blocks up to `budget.timeMs` |
| `urdt_answer {taskId, questionId, optionId \| success \| scope \| entry \| note}` | answer a clarification; the task resumes |
| `urdt_get_result {taskId}` | re-read a result |
| `urdt_map {action: summary\|route\|unvisited\|regressions\|full, target?}` | application map of the game: screens, transitions, known route to a module, never-pressed controls (coverage), navigation regressions vs earlier builds |
| `urdt_layers {scope?}` | UI layers of the current screen (window/module stack, modal, controls covered by something else) + layout/localization defects |
| `urdt_knowledge {prefix?}` | facts L3 learned about this game (routes, match rules, fail causes, which skill solved which module), with build + confidence |
| `urdt_skills` | skill library: shared core skills and this game's candidates, with verified outcome statistics |
| `urdt_submit_skill {name, description, requires, source}` | give L3 a controller for a mechanic it could not complete (see §5c) |
| `urdt_act {action, payload}` | one manual honest-input action / read-only query (click, drag, inspect, hit_test, capture, audio, coverage…) |

CLI equivalent (CI, scripts): `npm --prefix CoreAgent run urdt -- run task.json [--answer answer.json]`
→ JSON on stdout; exit 0 success, 1 fail, 2 needs_clarification, 3 blocked.

## 4. Task format (what you give L3)

```json
{
  "taskId": "shop_buy_sword_v2",
  "goal": "Buying the sword in the shop must spend 100 gold and add the sword to the inventory",
  "context": { "module": "Shop", "changedFiles": ["Assets/Shop/ShopController.cs"],
               "designNotes": "sword costs 100; buying with <100 gold must be refused",
               "gddPath": "Docs/GDD/Game.md", "gddScenarioId": "SHOP-01" },
  "target":  { "scope": "window_shop", "entry": ["btn_open_shop"] },
  "success": { "all": [
      { "beacon": "InventorySword", "path": "game.Count", "op": ">=", "value": 1 },
      { "beacon": "GoldLabel", "path": "Text", "op": "num==", "value": 400 } ] },
  "forbid":  [ { "beacon": "GoldLabel", "path": "Text", "op": "num<=", "value": -1, "text": "gold never negative" } ],
  "doNotTouch": ["btn_delete_save", "btn_buy_real_money*"],
  "hint": { "playbook": "match_and_place", "params": {} },
  "autonomy": "ask",
  "budget": { "timeMs": 120000, "actions": 300 },
  "audit": true
}
```
Rules:
- **You define success.** Prefer predicates over beacon properties: `path` is a beacon property
  (`IsCompleted`, `ProgressNormalized`, `Text`, `InteractionCount`…) or `game.<Prop>` (public simple
  property of the gameplay component on that object). `beacon: "@scope"` = the scope root.
  Ops: `== != >= <= > < contains changed unchanged num>= num<= num==` (`num*` compare the first number in a text).
- **Scope** = the beacon that must be visible (a mechanic module, a window). It also bounds exploration.
  `entry` = clicks that open it (optional — the agent searches menus and matches names, e.g. `M05…` → `btn_launch_m05`).
- `hint` is optional. Without it the agent explores; with it, it starts with that strategy.
- `autonomy: "ask"` → ambiguities come back as `needs_clarification`; `"full"` → the agent assumes and
  reports each assumption as an `ASSUMPTION` finding (use in CI).
- No GDD needed. If you have one, pass `gddPath` + `gddScenarioId`: scope/entry/success are filled from its `urdt-spec` block.

## 5. Where L3 gets information

1. **Your task** (goal, success, forbid, context) — the authority.
2. **Live beacons** — every `UrdtDebugTarget` subclass; 2D mechanics are auto-instrumented (referenced
   entities, gameplay components, runtime-spawned objects) and expose read-only `GameState`.
3. **GDD** (optional) — `urdt-spec` JSON blocks (`Docs/GDD/*.md`).
4. **Its own experience** — rules induced during exploration (reported as `learnedSkills`).
It never reads pixels for decisions and never calls game code.

## 5b. How L3 looks, listens and thinks (all local, inside URDT)

Before acting, L3 runs a **briefing** (reported in `result.briefing` and `strategy[]`):
1. **Reads** the module instruction, every text beacon and button captions (a label inside a button names it).
2. **Listens**: the URDT `audio` command returns the game's own mix (16 kHz ring buffer on the AudioListener,
   plus AudioSource play events); Silero VAD finds speech, **Qwen3-ASR** (30 languages incl. Russian)
   transcribes it. Voice hints are treated exactly like on-screen captions (`[voice]#n`).
3. **Understands text**: verbs (drag/hold/rotate/trace/scrub/alternate/mash/timing), sequences `A → B → C`,
   "avoid X", numbers → ordered plans ("place in this order, then press the button captioned Пуск").
4. **Looks** (rarely): one full-resolution end-of-frame screenshot (`capture {hd:true}`) goes to **Qwen3-VL**
   with the list of beacon ids + boxes; the answer is a JSON plan restricted to live beacon ids, avoided
   decoys and visual cues. Screenshot saved as `URDT_Sandbox/tasks/<task>/briefing_<scope>.jpg`.
5. **When stuck** (≈10 actions without effect) it takes a fresh screenshot, listens again and asks the vision
   model "these attempts did nothing — what is missing?" (max 3 times, ≥20 s apart).
6. Plans from the briefing run first; then the universal explorer; then recognized skills (after restarting
   the level through the known route); then exploration again.

Pixels and sound are **auxiliary** — decisions and verification always go through beacons.

Models are switched in `CoreAgent/config/agent.config.json`:
- `vision.active`: `qwen3vl-4b` (default, ~7 s/frame with `imageMaxTokens: 512`) | `qwen3vl-2b`
- `hearing.active`: `qwen3-asr-1.7b` (default) | `qwen3-asr-0.6b`
- `backend`: `cuda` (tools/llama.cpp-cuda) | `vulkan` (tools/llama.cpp); `enabled: false` disables a sense.
Servers start on demand (`llama-server` on ports 8091 vision / 8092 hearing).

## 5c. Knowledge, self-learning and skill synthesis

**Where knowledge lives.** Everything L3 learns about a game is stored in the game project itself:
`<project>/URDT_Knowledge/` (next to `Assets`; in a player build next to the executable). URDT reports the path in
its handshake (`health.knowledge_dir`, plus `build_id`) and L3 opens it before anything else. Commit it with the
game; delete it to make L3 re-learn. Other games never read it.

| File | Content |
|---|---|
| `app_map.json` | screens, transitions (with build), UI layer reports; used for navigation (known route first) |
| `facts.json` | `route:<module>`, `rules:<module>` (induced match rules), `fail:<module>` (actions that caused fails — never repeated), `solvedBy:<module>`; each with build, evidence, confidence (halved on a new build until re-confirmed) |
| `skills/stats.json` | verified outcome per skill per module (only the task's success predicate counts, never the skill's own claim) |
| `skills/candidates/<name>@vN/` | skills synthesized for this game (`manifest.json` + `controller.ts`) |
| `skills/requests/<module>.json` | what L3 needs when it could not complete a module |

Shared generic skills live in `CoreAgent/knowledge/core` and get there **only through the regression gate**.

**When L3 fails a module** the result carries `skillRequest` → a JSON with the module's design text, state schema,
state samples observed at normal speed for 3 s (what changes without input, what moves), beacons, the attempts and
fail reasons, and the controller API. Write a controller and submit it:

```ts
export default async function (api) {          // no imports, no process/fs, no raw URDT calls
  while (!api.expired()) {
    const s = await api.state();                // public state of the mechanic (+ IsCompleted, IsInTransition)
    if (s.IsCompleted) return 'COMPLETED';
    if (s.IsInTransition) { await api.sleep(100); continue; }   // stage change / fail restart
    const parts = await api.parts();            // beacons: testId, kind, center, rect, game, props, visible
    await api.motor.tap({ testId: 'BtnLeft' }); // tap/drag/hold/press/release/slice — honest input only
  }
  return 'TIMEOUT';
}
```
`urdt_submit_skill {name, description, requires: {scopeGame?, beacons?, buttons?}, source}` — `requires` is the
applicability contract (keys in the module state / regexes of beacon or button ids); the skill never runs where it
does not match. It becomes a **candidate of this game** and runs first on the next attempt when it matches.
Screen space is Unity's: origin bottom-left, **y up**.

**Promotion to the shared library** (`npx tsx scripts/regression_gate.ts --candidate <name> --modules <M..> --runs 2`,
after a one-time `--baseline`): the candidate must complete every target run and the 8-mechanic smoke set must not
regress. Candidates that keep losing (≥6 runs, <20%) or go unused for 30 days are archived automatically; at most 3
active skills per applicability contract. Versions are immutable (a resubmission is v2, v1 is archived).

**Knowledge cannot silently degrade play** — four independent guards:
1. *Mis-attribution:* a learned "this action causes a fail" ban blocks the action only after ≥2 observations; an
   action that later makes real progress is removed from the bans (contradiction).
2. *Staleness:* the build id is the player build GUID, or in the Editor a fingerprint of the compiled game code;
   bans learned on other code are demoted to one observation and must be re-confirmed.
3. *Harmful knowledge:* if a module is not solved while bans are active, the second pass runs without them; solving
   it then deletes the bans (reported as an INFO finding).
4. *Drift:* every campaign/baseline snapshots the game knowledge (`URDT_Knowledge/history`, last 12);
   `scripts/regression_gate.ts --check-knowledge` compares the smoke set with the baseline and, on regression, rolls
   the knowledge back to the baseline snapshot — kept only if it fixes the regression.

**Never change the game's time scale** to make a real-time mechanic easier: games use unscaled/realtime timers,
audio and network clocks; a slowed game is not the game the player gets. L3 observes dynamics at normal speed.

## 6. Clarifications

```json
{ "status": "needs_clarification",
  "clarification": { "questionId": "success", "question": "How do I know \"…\" is achieved?",
    "why": "No success criteria given.",
    "options": [ { "id": "completed", "label": "M05.IsCompleted == true", "payload": { "success": [ … ] } } ],
    "observed": { "scopeProps": { … }, "scopeGame": { … } } } }
```
Answer with `urdt_answer {taskId, questionId:"success", optionId:"completed"}` or give explicit
`success` predicates / `scope` / `entry`. Questions you will see: `scope` (what to test), `entry` (how
to open it), `success` (what counts as done).

## 7. Reading the result

- `status` — `success` only when all success predicates held (completion is latched even if the game
  tears the screen down right after) and no forbid predicate became true.
- `findings[]` — `GOAL_NOT_REACHED`, `FORBIDDEN_STATE`, `CONSOLE_ERROR` (exceptions logged while playing),
  `OCCLUDED` (a control/item covered by another object — a player would press/grab the wrong thing),
  `LAYOUT` (truncated text, overflow, unreadable font size, missing glyphs),
  `UNGUARDED_SHORTCUT` (with `audit:true` L3 drops a junk item and an item of a *different type* — found by
  matching item/receptacle `game.*` string values such as `ItemTypeId=red` ↔ `AcceptedTypeId=blue` — into
  receptacles; acceptance is a MAJOR defect and makes `status: fail` even if the goal was reachable), `NAVIGATION`, `STALL`, `UNOBSERVABLE`, `ASSUMPTION`.
  Treat CRITICAL/MAJOR as bugs to fix even when `status` is success.
- `strategy[]` — what the agent tried, with observed effects (`reward +…`, `no effect`). A long run of
  `no effect` on your new control usually means: not a beacon, blocked by an overlay, wrong input
  handler (legacy `UnityEngine.Input` is unreachable for virtual devices), or the handler never fires.
- `learnedSkills[]` — e.g. `match rule: item.game.ItemId == receptacle.game.SlotId`. If the learned rule
  differs from your design, your data wiring is wrong.
- `evidenceFile` — `.harness/failure_evidence_<task>.json`: beacon snapshot, last 30 input actions,
  console errors, dashcam frames. Use it to locate the defect in code.

## 8. Making a module testable (do this while you write it)

- Put a beacon (`UrdtUiButtonTarget`, `Urdt2DDraggableTarget`, `Urdt2DSlotTarget`, `Urdt2DModuleTarget`,
  `Urdt2DInteractiveAreaTarget`…) on every control and every object the goal depends on; give stable `TargetId`s.
- Expose state as **public read-only properties** on the gameplay component (`IsCompleted`, `Progress`,
  `IsLocked`, `ItemId`…) — they appear in `game.*` automatically.
- Read input via EventSystem handlers / Input System, not `UnityEngine.Input`.
- A visual-only cue (colour, blinking) that decides play must also be a property.
- Check with `urdt_act {action:"coverage", payload:{scope:"<module>"}}`: it lists interactable objects without a
  beacon (`no_beacon`) and objects using legacy `OnMouse*` (`legacy_OnMouse`, unreachable by synthetic input).
  L3 runs it at the start of every task and reports each as an `UNOBSERVABLE` finding (MINOR / MAJOR).
  Limit: input polled in `Update()` (raycasts from `Mouse.current`) cannot be detected statically.

## 9. Limits (be honest in your own reports)

- Exploration solves arrangement/assembly, holds, traces, rotations and puzzles with observable feedback well;
  fast timing and physics mostly rely on recognized skills; a genuinely new genre may need a `hint`
  or a clearer success predicate. Check `strategy[]` to see which path was used.
- Clicks are instantaneous (~5 ms) — the agent is faster than a human; do not use it to judge human difficulty.
