# Patterns: other roles (DRAFTS — refine on first real use)

These are starting points derived from the Dev pattern. Before applying, confirm the
ordering with the user and promote the refined version to its own file.

## Artists (графика / 2D art)

- Chain per artist; assets grouped by module, modules sequential.
- Suggested type order inside a module: концепты → основные ассеты (окружение/персонажи) →
  UI-графика → иконки/мелочь → полиш.
- Art usually must FINISH before the dev task that integrates it starts — if both task sets
  are scheduled, offer cross-role finish-start links (art task → dev графика task).

## Animators

- Chain per animator; module-sequential like Dev.
- Suggested order: риг/сетап → основные циклы (idle/walk) → игровые анимации →
  кат-сцены/полиш.
- Animations depend on final art: if art tasks exist in the same Gantt, the module's
  animation block starts after the module's art block.

## Game designers (ГД)

- Suggested order: концепт/ГДД модуля → балансные таблицы → контент уровней → настройка
  в движке → тюнинг после плейтестов (tail, closer to release — like звуки for Dev).
- ГД concept tasks are natural predecessors of Dev логика tasks — offer cross-role links.

## Common to all roles

- All hard rules from dev.md apply: one module at a time, single linear chain per person,
  markers (`0` hours) in-chain as 1-hour slots inside 10:00–18:00, workdays only, every task linked.
- Tail principle generalizes: each role has "closer to release" work (Dev: звуки/озвучка;
  ГД: тюнинг; artists/animators: полиш) — collect it at the end of the person's chain.
