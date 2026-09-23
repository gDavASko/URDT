# URDT — guide for AI agents (setup, run, use in any Unity project)

> **RU, кратко.** URDT — это пакет для Unity + агент CoreAgent. Пакет поднимает в игре WebSocket-сервер и даёт
> честный ввод (мышь/касания через Input System/EventSystem). Агент L3 играет в игру как человек, проверяет цели,
> находит баги и учится на этой игре. Любая ИИ (Claude, Codex, Cursor, …) подключает агента как MCP-сервер или CLI
> и работает по разделам ниже: §1 установка, §2 запуск, §3 подготовка игры, §4 задачи, §5 знания и самообучение,
> §6 проблемы.

This guide is written for an AI assistant working in *some* Unity project. Follow it top to bottom the first time;
later use §4–§5.

---

## 0. What you get

| Part | Where | What it does |
|---|---|---|
| **URDT package** `com.davasko.urdt` | inside the game (Unity package) | WebSocket server on `ws://127.0.0.1:7777/`, beacons (ids, rects, live state), honest input (pointer/touch/keys through the Input System and EventSystem), capture, audio tap, layout/coverage inspectors, knowledge folder |
| **CoreAgent** | the URDT repository (`CoreAgent/`), runs next to the Editor | L3 agent: navigates, reads texts/GDD, explores, plays with skills, judges success predicates, reports findings, learns per game. Exposed as **MCP server** and **CLI** |
| **Game knowledge** | `<game project>/URDT_Knowledge/` | what L3 learned about this game (map, facts, skill stats, synthesized skills). Commit it with the game |

L3 never calls game code and never changes the game's time scale — it only sends input a player could send.

---

## 1. Install

### 1.1 Unity package (in the game project)
Requirements: Unity 6 (6000.x), `com.unity.inputsystem`, `com.unity.nuget.newtonsoft-json` (resolved automatically).

Add to `Packages/manifest.json` → `"dependencies"`:
```json
"com.davasko.urdt": "https://github.com/gDavASko/URDT.git?path=/Packages/com.davasko.urdt#urdt-l3-universal-player"
```
(or copy `Packages/com.davasko.urdt` from the URDT repo into the game's `Packages/` folder as an embedded package).

The project must use the **Input System**: Player Settings → Active Input Handling = *Input System Package* or *Both*
(**a fresh Unity 6 project defaults to the old Input Manager** — switch it and restart the Editor; headless:
`ProjectSettings.asset` → `activeInputHandler: 2`). The package compiles in the Editor and in development builds
automatically; to include it in a release build define `URDT_ENABLED`.
Game input must be read through EventSystem handlers (`IPointerDownHandler`, `IDragHandler`, `Button.onClick`, …) or
Input System actions — `UnityEngine.Input` and `OnMouseDown` cannot be reached by synthetic input (§3).

### 1.2 CoreAgent (once per machine)
```bash
git clone https://github.com/gDavASko/URDT.git
cd URDT/CoreAgent
npm install                     # Node 22+
npx tsc --noEmit                # sanity check
```
Optional senses (not needed for most games): `bash scripts/fetch_models.sh` downloads Qwen3-VL (vision) and
Qwen3-ASR + Silero VAD (hearing) into `CoreAgent/models`; llama.cpp servers go to `CoreAgent/tools/` (see the
script header). Switch/disable in `CoreAgent/config/agent.config.json` (`vision.enabled`, `hearing.enabled`).
Without them L3 works on beacons and texts only.

### 1.3 Connect the agent to your AI tool
The MCP server speaks stdio. Use the absolute path of your clone (`<URDT>`):

- **Claude Code**: `claude mcp add urdt -- node <URDT>/CoreAgent/node_modules/tsx/dist/cli.mjs <URDT>/CoreAgent/src/mcp/urdt_mcp_server.ts`
- **Codex** (`~/.codex/config.toml`):
  ```toml
  [mcp_servers.urdt]
  command = "node"
  args = ["<URDT>/CoreAgent/node_modules/tsx/dist/cli.mjs", "<URDT>/CoreAgent/src/mcp/urdt_mcp_server.ts"]
  ```
- **Cursor / other MCP clients** (`mcp.json`): same `command` + `args`.
- **No MCP**: use the CLI — `npm --prefix <URDT>/CoreAgent run urdt -- status | observe [scope] | run task.json [--answer a.json]`
  (JSON on stdout; exit 0 success, 1 fail, 2 needs_clarification, 3 blocked).

Environment (optional): `URDT_URL` (default `ws://127.0.0.1:7777/`), `URDT_TOKEN` (default: tries `urdt-local`, then
`urdt-test-poligon`).

---

## 2. Run the game with URDT

1. Open the project in the Editor. On Windows prefer `-force-d3d11` (D3D12 may crash on Play Mode exit).
2. Enter **Play Mode**. The package auto-starts the server (`[URDT_Server]`, log line `[URDT] auto-started on
   ws://127.0.0.1:7777/`). If the project already has its own `UrdtServerHost` in the scene, that one is used.
   - Disable auto-start: env `URDT_AUTOSTART=0`. Port/token: `-urdtPort`, `-urdtToken` args or `URDT_PORT`, `URDT_TOKEN`.
   - Development player builds: run with `-urdt` (release builds never start the server).
   - Headless CI: `-executeMethod URDT.Runtime.Runner.UrdtTestRunner.RunHeadless -urdtPort 0 -urdtToken <t> -urdtHandshakeFile <f>`.
3. Check: MCP `urdt_status` (or `npm --prefix <URDT>/CoreAgent run urdt -- status`) → `runtime_ready: true`,
   `knowledge_dir` = `<project>/URDT_Knowledge`, `build_id`.

With the Unity CLI (needs the `com.unity.pipeline` package in the project) you can drive the Editor headlessly: `unity command editor_play | editor_stop | recompile |
recompile_status | console | menu --path "<menu item>" | eval_file --file <cs>`.

**One session at a time:** URDT accepts one agent connection. Close other MCP/CLI sessions before starting another.

---

## 3. Make the game testable (do it while you write features)

L3 sees the game through **beacons**. UI `Selectable`s (Button, Toggle, Slider, Dropdown, InputField, ScrollRect)
get beacons automatically. For gameplay objects add a beacon component (package `KBP.URDT.Inspect`):

| Object | Component | L3 reads |
|---|---|---|
| a level / mechanic / screen root — **every task needs one as its `scope`** | `Urdt2DModuleTarget` (or `UrdtUiWindowTarget` for windows/canvases) | `IsCompleted`, `ProgressNormalized`, `Instruction`, and `game.*` = public properties of the gameplay component on the same object |
| things the player drags | `Urdt2DDraggableTarget` | position, rect, `game.*` (e.g. `ItemTypeId`, `Level`, `IsJunk`) |
| drop zones / slots | `Urdt2DSlotTarget` | position, `game.*` (e.g. `AcceptedTypeId`, `IsOccupied`) |
| anything else the player presses/watches (movers, targets, tiles) | `Urdt2DInteractiveAreaTarget` | position, `game.*` |
| texts | `UrdtUiTextTarget` | `Text` |

Rules:
- **Stable, unique ids** (`TargetId`, defaults to the object name). Changing `TargetId` at runtime re-registers the object.
- **Expose state as public read-only properties** on the gameplay component: progress, counters, flags
  (`IsCompleted`, `IsJunk`, `IsOccupied`), stage (`CurrentStage`, `StageCount`), fails (`FailCount`, `LastFailReason`,
  `IsInTransition`), and for real-time mechanics positions/velocities/windows (`BallVX`, `NoteSpeed`, `HitWindow`).
  Anything that decides play and is only visible as colour/animation must also be a property.
- Format numbers in state strings with `CultureInfo.InvariantCulture`.
- Input through EventSystem/Input System only. Spawned objects must not lie on top of each other.
- Check with MCP `urdt_act {action:"coverage", payload:{scope:"<module>"}}` (interactables without beacons, legacy
  `OnMouse*`) and `urdt_layers` (covered controls, layout defects).

---

## 4. Use: tasks (what you give L3)

Tools (MCP): `urdt_status`, `urdt_observe {scope?}`, `urdt_run_task {task}`, `urdt_answer`, `urdt_get_result`,
`urdt_act {action,payload}`, `urdt_map`, `urdt_layers`, `urdt_knowledge`, `urdt_skills`, `urdt_submit_skill`.

```json
{
  "taskId": "shop_buy_sword",
  "goal": "Buying the sword spends 100 gold and adds it to the inventory",
  "context": { "module": "Shop", "gddPath": "E:/Game/Docs/GDD.md", "designNotes": "sword costs 100" },
  "target":  { "scope": "window_shop", "entry": ["btn_open_shop"] },
  "success": { "all": [ { "beacon": "InventorySword", "path": "game.Count", "op": ">=", "value": 1 } ] },
  "forbid":  [ { "beacon": "GoldLabel", "path": "Text", "op": "num<=", "value": -1 } ],
  "doNotTouch": ["btn_delete_save"],
  "autonomy": "ask",
  "budget": { "timeMs": 180000, "actions": 400 },
  "audit": true
}
```
- `target.scope` = the beacon that must be on screen (module/window); `entry` optional — L3 finds routes (and
  remembers them in the app map). `@scope` in predicates = the scope beacon.
- Ops: `== != >= <= > < contains changed unchanged num>= num<= num==`.
- `gddPath` (absolute or relative to the URDT repo): L3 reads the GDD section whose `## ` heading names the module id.
- `autonomy: "ask"` → ambiguities come back as `needs_clarification`; answer with `urdt_answer`. `"full"` for CI.
- **Whole game run**: `"target": {"mode": "campaign", "entry": ["<run-all button>"]}` — L3 plays every level the game
  presents, in order, and skips a stuck one with the game's "next" control.

Reading results: `status` (`success` only if all predicates held and no forbid/audit defect), `findings[]`
(`GOAL_NOT_REACHED`, `FORBIDDEN_STATE`, `CONSOLE_ERROR`, `UNGUARDED_SHORTCUT`, `OCCLUDED`, `LAYOUT`, `CONTROL`,
`UNOBSERVABLE`, `NAVIGATION`, `ASSUMPTION`), `strategy[]` (what L3 did and what happened), `learnedSkills[]`,
`evidenceFile`, `knowledge.skillsTried`, and `skillRequest` when L3 could not finish.

The loop: **change code → compile (0 errors) → Play Mode → `urdt_run_task` → fix what findings say → repeat**.
Never call a feature done on compilation alone.

---

## 5. Knowledge and self-learning

- Everything about **this game** lives in `<project>/URDT_Knowledge/` (map, facts, skill stats, candidates, requests,
  `history/` snapshots). L3 reads it before acting. Shared generic skills live in `<URDT>/CoreAgent/knowledge/core`.
- **When L3 fails a module**, `result.skillRequest` points to a JSON with the design text, state schema, state samples
  observed at normal speed, beacons, attempts and fail reasons. Write a controller and submit it:
  ```ts
  export default async function (api) {          // no imports; only the api
    while (!api.expired()) {
      const s = await api.state();                // mechanic state + IsCompleted, IsInTransition
      if (s.IsCompleted) return 'COMPLETED';
      if (s.IsInTransition) { await api.sleep(100); continue; }
      const parts = await api.parts();            // beacons: testId, kind, center, rect, game, visible
      await api.motor.tap({ testId: 'BtnLeft' }); // tap/drag/hold/press/release/slice — honest input
    }
    return 'TIMEOUT';
  }
  ```
  `urdt_submit_skill {name, description, requires:{scopeGame?, beacons?, buttons?}, source}` → a **candidate** of this
  game; it runs first next time its `requires` match. Screen space is Unity's: origin bottom-left, **y up**.
- **Promotion** to the shared library only through the gate: `npx tsx scripts/regression_gate.ts --baseline` once,
  then `--candidate <name> --modules <M..> --runs 2` (all target runs solved by the candidate, smoke set not regressed).
- **Anti-degradation** (automatic): bans need ≥2 observations and are lifted by contrary progress; one ban per run is
  re-tested; a module unsolved with bans is retried without them (bans removed if that works); knowledge from other
  code builds must be re-confirmed; `--check-knowledge` rolls the knowledge back to the baseline snapshot on regression.

---

## 5b. Verified end-to-end on a fresh project

A new empty Unity 6000.4 project with only this package (+ uGUI, Input System): switch Active Input Handling, add a
`UrdtUiWindowTarget` (`window_main`) on the Canvas and a `UrdtUiTextTarget` on a label, enter Play Mode →
`[URDT] auto-started on ws://127.0.0.1:7777/`; from the game folder
`npm --prefix <URDT>/CoreAgent run urdt -- status` reports `knowledge_dir = <game>/URDT_Knowledge`; a task
"pressing the button increments the counter" (scope `window_main`, success `CounterText.Text contains "Presses: 1"`)
returns `success`; the MCP command of §1.3 started from the game folder exposes all tools.

---

## 6. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `not reachable` / `E_DISCONNECTED` on handshake | not in Play Mode; another agent session holds the connection (close stray `urdt_mcp_server` processes); wrong token → set `URDT_TOKEN` |
| clicks have no effect when the Game view is not focused | handled by the package (input goes to the Game view during a session); make sure the project uses the Input System |
| control never reacts | not a beacon, covered by another object (`urdt_layers`), or read via `UnityEngine.Input`/`OnMouse*` (`coverage` reports it) |
| session drops after editing scripts | Unity recompiled and reloaded the domain (Play Mode ends); re-enter Play Mode — CLI/MCP reconnect |
| Editor crashes leaving Play Mode (Windows) | start the Editor with `-force-d3d11` |
| `[URDT] auto-started` never appears / no URDT types | Active Input Handling still *Input Manager (Old)*, or a release build without `URDT_ENABLED`, or `URDT_AUTOSTART=0` |
| `unity command …` says "No Pipeline instance" | add `com.unity.pipeline` to the project's manifest |
| coordinates look mirrored vertically | URDT screen space is y-up (origin bottom-left) |
| L3 keeps failing a real-time mechanic | expected without a matching skill: read `skillRequest`, submit a controller (§5) |
