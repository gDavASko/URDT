# Report Writing Rules

Write for management. A CTO or producer should understand the report without
reading code.

## Required Structure

1. Title and period.
2. Method note: commit titles and MR comments excluded; MR approval only.
3. Tabs:
   - `Рейтинг`;
   - one tab per employee;
   - separate CTO/AI-infrastructure tab when applicable;
   - `Методика`.
4. On `Рейтинг`:
   - short management conclusion;
   - ranking table with total, pace, quality, value, reason.
5. On every employee tab:
   - score cards;
   - work context by repositories/projects;
   - Bitrix24 task context when task data is available;
   - concrete examples block;
   - metric table with employee-specific explanations.

## Metric Table Rule

The last column must be titled:

`Конкретный пример / почему выбивается`

Forbidden weak text:

- "Показывает техническую сложность работы."
- "Оценка соответствия внутренним правилам."
- "Насколько изменения выглядят завершёнными."
- "Было X, сейчас Y" without explaining why.

Required strong text:

- name real modules, files, repos, or product areas;
- say whether it is a strong side, normal context, or risk point;
- explain the concrete effect in product/management language.

Example:

Bad:

`Завершённость 5.5: насколько изменения выглядят доведёнными.`

Good:

`Завершённость 5.5: WaterPumpModel looks like a finished interaction, but the
same period contains repeated sound/fix/rework cycles: fix sound configs, fix
other sounds, sounds reworks, high-rise/swimming pool sounds. This means useful
delivery with stabilization debt.`

## Concrete Evidence Patterns

Use evidence like:

- `Debug.Log` left in gameplay system;
- `private -> protected` widening to support inheritance;
- shared base extraction, e.g. `BaseScenarioStageModule`;
- repeated edits to a shared system, e.g. drag system;
- new module/stage with tutor systems and prefab wiring;
- prefab-heavy work in a named module;
- MR approval gap;
- many commits not found in merged MR;
- many closed tasks without linked MR/commits;
- high-complexity Bitrix tasks that explain moderate Git volume;
- reopened/returned tasks linked to later Git rework;
- tasks moved through an incorrect workflow;
- repeated fix/rework commits in the same product area.

Translate technical evidence:

- "direct core coupling" -> "feature starts depending on core systems, raising
  future support cost";
- "prefab-heavy" -> "scene/instrument/tutorial/sound integration work";
- "churn" -> "cost to stabilize after initial delivery";
- "no-MR share" -> "work is less visible to reviewers and management";
- "approval coverage" -> "formal review trace exists".
- "reopen/return rate" -> "tasks had to be sent back after being considered
  done, increasing producer/tester load";
- "task-to-MR traceability" -> "management can connect delivered tasks with
  code changes and review history".

## Tone

Do not use profanity, sarcasm, internal debugging notes, or casual labels.
Do not write defensive service notes about formula edits. Write the actual
evidence:

`Normal executor level, but below the group by scale and completion.`
