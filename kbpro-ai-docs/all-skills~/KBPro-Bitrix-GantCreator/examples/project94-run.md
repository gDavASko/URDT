# Reference run: project 94 «DavASko АИ-Песочница» (2026-07-15)

The run this skill was distilled from. Numbers to sanity-check your own runs against.

## Input

- 142 tasks `[Dev][<type>] <Module> - <Submodule>`, all status=2, no plan dates.
- Complexity field `UF_TASKS_TASK_1783529349965` (double, integer person-hours), total 1472 h after conversion from the old 184 person-day plan.
- Modules (pd): UI и общепроектные части 19, Робот пылесос 19, Робот повар 12,
  Электромобиль 12, Робот доставщик 20, Робот гуманоид 23, Робот манипулятор 20,
  Режим схватка 20, Обслуживание 34 (4 submodules), Оценка 5.
- 5 people with staggered starts (см. project94-config.json).

## Load balancing (deep-reasoner subagent)

- Makespan lower bound proven: 17.09.2026 (the 4 later starters lack capacity for an
  earlier finish; the first person's fixed chain + indivisible «Оценка» spans to 17.09).
- «Оценка» given to the FIRST person (who freed up first) — this aligned the other four
  to finish on exactly the same day, 15.09.2026.
- «Обслуживание» split by submodule between two people to level the load.
- Final loads after conversion: 376 / 320 / 304 / 256 / 216 person-hours.

## Output

- 142 date updates (3 batches), 137 finish-start links via legacy DEPENDS_ON
  (116 real-task links + 21 in-chain zero-complexity markers), 0 errors.
- 5 chain starts (one per person) are the only tasks without a predecessor.
- Finish: four people 15.09.2026, first person 17.09.2026.

## Mistakes made and fixed (already baked into the skill, listed for recognition)

1. First attempt created links via `task.dependence.add` in batch → phantom links,
   no arrows in UI. Fix: legacy `task.item.update` DEPENDS_ON.
2. Zero-complexity markers were first pinned into arbitrary same-day slots
   WITHOUT chain links → "hanging tasks" complaint. Fix: 1-hour in-window slots
   (now 1-hour in-window slots) + full chain membership.
3. Sounds/voiceover were first scheduled inside each module → customer requires them
   as a tail block at the end of each person's chain ("closer to release").
4. Customer's stated weekdays for start dates were wrong (15.07.2026 is Wednesday, not
   Tuesday) — always recompute weekdays, never trust remarks in parentheses.

## Leftovers to offer the user

- Old deadlines (all 18.07) still conflict with plan dates — offer batch-aligning
  DEADLINE to END_DATE_PLAN.
- Red bars = PRIORITY=2 («важная»), can be batch-reset to 1 if the user wants all-blue.
