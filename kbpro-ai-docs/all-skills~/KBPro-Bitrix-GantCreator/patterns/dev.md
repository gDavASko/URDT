# Pattern: Dev (developers) — validated live on project 94

Task shape: `[Dev][<type>] <Module> - <Submodule>`, types in tags and title.
Complexity field = integer person-hours; 0 means "marker" (negligible effort, scheduled as a 1-hour slot).

## Task type priority inside a module

1. логика (module logic)
2. туторы (module tutorials)
3. графика (module art)
4. анимации (module animations)
5. эффекты (not in the customer's original list — placed after animations)
6. звуки (sounds)
7. озвучка (voiceover)
8. отладка (debugging — schedule last if such tasks exist)

Secondary sort key: task ID ascending (creation order ≈ submodule order).

## Hard sequencing rules (customer requirements, do not violate)

- **Modules strictly one after another.** One module is led by one person; a person never
  mixes tasks of two modules in parallel. Cross-module transition gets an arrow too.
- **Sounds and voiceover go to the tail.** Inside each module do ALL types EXCEPT
  звуки/озвучка, then move to the next module. All звуки/озвучка of the person's modules
  form one final block at the END of that person's chain (closer to release), in module
  order, звуки before озвучка within a module.
- **Main-menu UI at the very start** of development (first person's first block), together
  with the shared programming/command system.
- **Auxiliary UI windows** (Родительский контроль, Окно игровое and similar) — after the
  module-animations stage of the owner's main module.
- **Same-day tasks still go in sequence**: every task uses exact working time inside 10:00–18:00 MSK.
  A 4-hour task starting at 10:00 ends at 14:00; the next task starts at 14:00. Markers get a 1-hour slot,
  stay in the chain, and get an arrow.
- Every task except each person's first has exactly one predecessor (single linear chain
  per person).

## Distribution of modules across people

- Fixed assignments first (from user input), e.g.: shared programming system + main-menu
  UI → first person to start; then that person's named module (гуманоид → Андрей);
  манипулятор → Влад; повар → Алексей.
- Remaining modules: greedy — a person who finishes takes the next module from the queue
  (queue in task-creation/priority order).
- Big composite modules (e.g. Обслуживание with 4 submodules) may be split by submodule
  between people to level the load.
- Optimization goals: minimize makespan, then align everyone's finish dates. Delegate this
  balancing to a reasoning subagent with module person-hour sums; freeze its output into
  config.json `plan`.

## Calendar

- Workdays Mon–Fri, minus config.holidays (RF production calendar + corporate days).
- 1 workday = max 8 person-hours; slots 10:00–18:00 MSK/portal time.
- Each person has an individual start date ("Влад начнёт с 22 июля") and optional end date.

## What the customer sees and asked for (context)

- Miro-style cascade: per-person diagonal chains of green blocks, weekends excluded.
- Complaints that drove these rules: "нельзя делать часть работ из одного модуля, часть из
  другого одновременно", "звуки и озвучка ближе к релизу", "если несколько задач в один
  день — они должны идти по очереди", "все задачи должны быть под связью".
