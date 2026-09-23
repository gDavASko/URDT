# Brief: four arcade mechanics for the 2D polygon (M33–M36)

You write ONE new mechanic for the Unity 2D test polygon. It is played by humans and by an autonomous AI player
(URDT L3) through honest pointer input only. Read these files first:

- `Assets/URDT_TestPoligon/2D_Polygon/Scripts/Core/BaseMechanic2DModule.cs` — base class: stages (`StageCount`,
  `CurrentStage`, `GetStageInstruction`), `SetProgress/NotifyProgress/CompleteMechanic`, `FailStage(reason)`,
  `IsInTransition`, `FindFreeAnchoredPosition`. Read its header comment — it defines the stage/fail contract.
- `Assets/URDT_TestPoligon/2D_Polygon/Scripts/Core/Urdt2DBeaconUtility.cs` — how objects become visible to the AI
  (beacons are created from object names/components when base `Initialize()` runs `InstrumentHierarchy`).
- `Docs/GDD/STAGES_BRIEF.md` — the general rules all mechanics follow (idempotent Initialize, no ghosts, public
  read-only state properties, EventSystem input only, Russian player-facing texts).
- One existing mechanic for style, e.g. `Assets/URDT_TestPoligon/2D_Polygon/Scripts/Mechanics/M17_TugOfWarBalance/`.

## Hard requirements

1. **Self-building UI.** The prefab root is an empty full-stretch `RectTransform` with only your mechanic component
   (the orchestrator creates the prefab). Your `Initialize()` must build (or rebuild) the whole board in code
   under `transform`: backgrounds, objects, texts (TextMeshProUGUI), buttons (UnityEngine.UI.Button + Image). Keep
   references to what you create and destroy/reuse it on every `Initialize()` (called on start, every stage change
   and every fail). Stable, distinct object names — they become the AI-visible ids. Use `DestroyImmediate` for
   children you rebuild inside `Initialize()` (avoids same-name ghosts in the same frame) — or reuse objects.
   Call `base.Initialize()` **after** building the board so the beacons are created for the new objects.
2. The play area fits in about 1100×600 UI units centred on the root; put a short instruction text at the top of
   the board (the host also shows `Instruction`). Colours must make the objects distinguishable.
3. Input: EventSystem interfaces (`IPointerDownHandler`, `IPointerUpHandler`, `IBeginDragHandler`, `IDragHandler`,
   `IEndDragHandler`, `IPointerClickHandler`) or `Button.onClick`. No `UnityEngine.Input`, no `OnMouse*`, no
   keyboard requirement. Convert screen → local with `RectTransformUtility.ScreenPointToLocalPointInRectangle`
   using the canvas camera (`canvas.renderMode == ScreenSpaceOverlay ? null : canvas.worldCamera`). Every Image
   that must NOT catch clicks gets `raycastTarget = false`; objects that must be pressed/dragged need
   `raycastTarget = true`.
4. Game logic in `Update()` with `Time.deltaTime`; ignore everything while `IsInTransition || IsCompleted`. No
   Physics2D — simple own kinematics are fine and deterministic.
5. **Stages = 2**: `public override int StageCount => 2;`. Stage 1 = the target given below; stage 2 a bit
   harder (described below). Stage win → `CompleteMechanic()` (the base advances to stage 2 or completes).
   Progress: `SetProgress(localFraction)`.
6. **Fails** → `FailStage("причина по-русски")` as specified below (the base restarts the stage after 1 s).
7. **Public read-only state** on the mechanic (the AI reads it as `game.*` on the module beacon): everything
   listed below, plus anything else a player sees on screen (score, counters, positions of moving things in UI
   units relative to the board centre are fine). Moving objects must also be separate named GameObjects so the
   AI sees their screen positions.
8. Namespace `KBP.URDT.TestPoligon.Mechanics2D.<Folder>`; class `<MechanicId>Mechanic : BaseMechanic2DModule`;
   `protected override string GetStageInstruction(int stage)` with Russian texts. Mechanic metadata
   (`_mechanicId/_title/_instruction` serialized fields of the base) is set by the orchestrator — don't touch.
9. Write the files under `E:\Projects\URDT\Staging\<Folder>\` (NOT under Assets — the orchestrator moves them).
   Do not run Unity or the unity CLI. Unity 6000.4, C# 9. Only UnityEngine, UnityEngine.UI, UnityEngine.EventSystems,
   TMPro, System, System.Collections(.Generic). Double-check every API you call exists.
10. Write `E:\Projects\URDT\Docs\GDD\sections\<MechanicId>.md` in Russian in the same style as the existing
    sections in `Docs/GDD/sections/` (read two of them): pitch, goal, controls, rules, objects, stages table, fails
    and consequences, player texts, edge cases. First line: `# <Mxx> — <название>`. No beacons/URDT/code names.
11. Winnable by a competent human with a mouse and by precise synthetic input (reaction latency 20–60 ms).

Report: files written, object names the AI will see, public properties, stage parameters, fail rules, and
anything you were unsure about.

## The four mechanics

### M33_Arkanoid (folder `M33_Arkanoid`)
Paddle at the bottom, ball, brick wall at the top, walls left/right/top. Controls: drag anywhere on the field
(or on the paddle) — the paddle's x follows the pointer (clamped). While the ball rests on the paddle, a tap/click on
the field launches it upwards (slightly angled). Reflection off paddle depends on hit offset (classic). Bricks:
named `Brick_<row>_<col>`, destroyed on hit (deactivate; expose `IsDestroyed`).
Stage 1: 8 bricks (2 rows × 4), ball speed ≈ 380 u/s. Stage 2: 12 bricks (3 × 4), speed ≈ 460 u/s, paddle 20% narrower.
Fail: ball falls below the paddle line → `FailStage("Мяч упущен")`.
Public: `BricksLeft`, `BricksTotal`, `BallLaunched`, `BallX`, `BallY`, `BallVX`, `BallVY`, `PaddleX`,
`PaddleWidth`, `FieldWidth`, `FieldHeight`, `PaddleY` (board-local units). Objects: `Paddle`, `Ball`, `Field`, bricks.

### M34_Snake (folder `M34_Snake`)
Grid 14 × 9 cells (cell ≈ 44 u). The snake (length 3 at start, head `SnakeHead`, body `SnakeSeg_<i>`) moves one
cell per tick in its direction. Controls: four on-screen buttons `BtnUp`, `BtnDown`, `BtnLeft`, `BtnRight`
(big, under/next to the field) AND swipe on the field (drag with direction). Reversing into itself is ignored.
Food `Food` spawns on a random free cell; eating grows the snake by 1.
Stage 1: collect 5 food, tick 0.30 s. Stage 2: collect 7 food, tick 0.22 s.
Fail: head hits a wall or its own body → `FailStage("Змейка врезалась")`.
Public: `HeadCol`, `HeadRow`, `Direction` ("up"/"down"/"left"/"right"), `FoodCol`, `FoodRow`, `Length`,
`Collected`, `TargetFood`, `GridCols`, `GridRows`, `TickSeconds`, and `BodyCells` (string "c,r;c,r;...").
The game starts moving only after the first direction input (so the player has time to read).

### M35_Merge2 (folder `M35_Merge2`)
Board of 4 × 4 cells `Cell_<row>_<col>`. Button `BtnSpawn` («Создать») places a level-1 item in a random empty cell.
Items are draggable, named `MergeItem_<n>` (unique n, increasing), each shows its level number and a colour per
level; expose `Level`, `Row`, `Col` on the item component. Drag an item onto another item of the SAME level →
they merge into one item of level+1 in the target cell. Drop onto an empty cell → move there. Drop onto a
different level or outside → return to origin (no fail).
Stage 1: create a level-4 item, 20 spawns available. Stage 2: create a level-5 item, 34 spawns.
Fail: spawns exhausted and no possible merge left, OR board full and no pair of equal levels
→ `FailStage("Нет ходов")`.
Public: `MaxLevel`, `TargetLevel`, `SpawnsLeft`, `ItemCount`, `EmptyCells`.

### M36_Match3 (folder `M36_Match3`)
Board 6 × 6 of gems, 5 colours (distinct colours + a letter/shape on each for colour-blind players). Fixed cell
objects `Gem_<row>_<col>` whose colour changes (do not destroy/recreate gems — swap/refill colours). Swap two
adjacent gems by dragging a gem onto a neighbour (or drag in a direction ≥ 30 u). If the swap creates a line of
3+ same colour → clear, gravity drop, refill from top (cascades resolve automatically). A swap with no match
reverts (no fail, but it spends a move). Initial board and refills must not contain ready matches, and there must
always be at least one possible move (reshuffle if none).
"Combination" = one player move that clears at least one line (cascades from the same move don't add).
Stage 1: 4 combinations within 15 moves. Stage 2: 6 combinations within 14 moves.
Fail: moves exhausted before the target → `FailStage("Ходы закончились")`.
Public: `Combos`, `TargetCombos`, `MovesLeft`, `BoardColors` (string, rows separated by '/', one char per gem
colour, row 0 = top), and on each gem component `ColorId`, `Row`, `Col`.
