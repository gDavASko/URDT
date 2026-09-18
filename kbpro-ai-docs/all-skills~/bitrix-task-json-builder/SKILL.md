---
name: bitrix-task-json-builder
description: "Convert spec (ТЗ/GDD) to Bitrix24 JSON backlog. Triggers: разбить ТЗ на задачи, сделай бэклог из ТЗ, сгенерируй JSON задач, decompose spec."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/CoreFramework/Guides/HowToDecomposeTask_ForAI.md
  - kbpro-ai-docs/llm-wiki/raw/ide-rules/GEMINI.md
  - kbpro-ai-docs/plombir-buildings-wiki/raw/create-plombir-tasks.js
license: project-internal
validated_against:
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
required_reading:
  - task-patterns/INDEX.md
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/CoreFramework/Guides/HowToDecomposeTask_ForAI.md
  - kbpro-ai-docs/kbpro-wiki/wiki/runbooks/decompose-task-to-backlog.md
  - references/decomposition-rules.md
  - references/description-blocks.md
  - references/field-contract.md
  - references/gdoc-heading-deeplink-algorithm.md
known_risks:
  - Generating the full JSON before the user approved the task-title schema (skip of the Этап 1.7 gate).
  - Inventing steps that are not in the spec instead of extracting the spec's own structure.
  - Emitting one task per whole module (an epic) instead of one task per spec step.
  - Emitting identical DESCRIPTIONs across the 7 functional tasks of one step — each must be a per-functional delta.
  - Placing images by guess instead of GDD provenance (an image must come from that step's own section and its functional bucket; no duplicates, no extras).
  - Handing tasks to import without passing the sufficiency gate (≥7) and the image-provenance gate.
  - Saving the JSON without BOM, which breaks Cyrillic on import.
  - Putting tags into DESCRIPTION instead of the dedicated TAGS array.
---

# Bitrix Task JSON Builder

## Purpose

Convert a technical specification (ТЗ), GDD section, or feature brief into a **JSON array of Bitrix24 tasks** that the `bitrix-task-importer` skill can upload without errors. This skill is the implementation of the protocol in
[HowToDecomposeTask_ForAI.md](references/decomposition-rules.md). That protocol is the source of truth — this skill operationalizes it.

## When To Use

* The user provides a ТЗ / GDD / brief and asks for a task backlog or task JSON.
* You need to decompose a designed module into Bitrix tasks before import.
* You are regenerating or extending an existing `*_tasks.json` backlog from an updated spec.

## Do Not Use

* For sending tasks to Bitrix24 — use `bitrix-task-importer`.
* For module architecture/design itself — that is Этап 1 of the protocol (MasterCatalog and module files), done before this skill.

## Core Rules (summary — full text in references/)

0. **Select the task pattern FIRST.** The task type/department determines the formation rules, and each type has its own pattern under [`task-patterns/`](task-patterns/) (internal skill documentation). Read [`task-patterns/INDEX.md`](task-patterns/INDEX.md), pick the matching pattern (dev / art / animation / GDD …); if it is ambiguous, present the existing options and let the user choose. See Phase 0.
1. **Steps come FROM the spec, not from your imagination.** The ТЗ is almost always already split into large blocks (sections, numbered items, lists, stages). Map each spec item **1:1** to a task, preserving the spec's wording and order. See [decomposition-rules.md](references/decomposition-rules.md).
2. **One task = one step, never a whole module.** A module is an epic; "весь модуль стройки дома" is forbidden as a single task.
2a. **Split EACH step by functionality (per-step), FULL set of 7.** Each step is its own screen; the assignee takes a step and does everything for it. So every step gets all 7 tasks: `логика · графика · анимации · эффекты · звуки · озвучка · туторы`. The spec often describes these commonly per module (steps are similar) — that is NOT a reason to make a shared task; duplicate per step. Never collect tutorials/graphics/animations into one module-level block. Skip a functional for a step only if genuinely N/A (service screen) and mark it. See [decomposition-rules.md](references/decomposition-rules.md).
3. **`DESCRIPTION` is a complete 4-part task card, NOT a verbatim GDD dump**: (1) full goal for THIS functional, including the expected result and applicable requirements → (2) deep-link to the exact GDD heading (scroll-to) → (3) `Что сделать` (imperative, functional-specific) → (4) measurable `Готово, когда` (DoD), plus images placed by provenance. Per-functional **deltas** — never the same text across the 7 functionals. Governed by the selected pattern (dev → [dev-task-pattern.md](task-patterns/dev-task-pattern.md) §5–§6); assembly in [description-blocks.md](references/description-blocks.md).
4. **Field contract is fixed**: `GROUP_ID=100` (for robots), `RESPONSIBLE_ID=66`, `PRIORITY` 1–3, `ALLOW_TIME_TRACKING="1"`, tags only in `TAGS`. See [field-contract.md](references/field-contract.md).
5. **Encoding: UTF-8 WITH BOM.** Cyrillic must render correctly on import.
6. **Completeness without invention**: every spec item is covered; nothing extra is added. Ambiguities go into a separate "Вопросы по ТЗ" list, NOT into invented tasks.

## Workflow

> Two-phase: **first agree the schema (titles only), THEN generate JSON.** Never jump straight to JSON.
> But before both phases, **select the governing task pattern** (Phase 0).

### Phase 0 — Pattern selection (before anything else)

The formation rules depend on the **task type / department**; each type has its own pattern under [`task-patterns/`](task-patterns/) — the skill's internal documentation. Before decomposing:

1. **Read [`task-patterns/INDEX.md`](task-patterns/INDEX.md)** — the registry of available patterns (dev, art, animation, GDD, …).
2. **Pick the pattern matching the request.** Infer from what is being produced: dev tasks by GDD steps → [`task-patterns/dev-task-pattern.md`](task-patterns/dev-task-pattern.md); art assets → art pattern; animator tasks → animation pattern; etc.
3. **If the correct pattern is ambiguous, DO NOT guess** — present the existing pattern options from the INDEX and ask the user to choose (use the ask-question tool).
4. **If the matching pattern is registered but not yet implemented** (status «Планируется»), stop and raise it with the user; never invent formation rules.
5. **Follow the chosen pattern as the governing spec** for the rest of this workflow — source handling, decomposition, card composition, images, field contract, and scoring all come from it.

### Phase A — Schema approval gate (Этап 1.7)

1. **Switch to plan mode** before presenting the schema.
2. **Read the spec carefully.** Open the actual ТЗ text — do not work from memory.
   * For a **Google-Doc source**, resolve `DOC_ID` (user URL or Drive search) and build the **heading-anchor index** (`heading → {h.xxxx, tab_id}`) and the **image manifest** now. **CRITICAL**: To build the heading-anchor index, you MUST follow the verified algorithm in [gdoc-heading-deeplink-algorithm.md](references/gdoc-heading-deeplink-algorithm.md). The only reliable method is HTML-export parsing (`export?format=html&tab=TAB_ID` → extract `id="h.xxxx"` from `<h1>`–`<h6>` tags). Do NOT guess heading IDs, do NOT use KIX `headingId`, do NOT use Playwright DOM parsing — these methods produce broken links. For large docs (>10 MB) use Google Docs API `documents.get` as a fallback. The image manifest is built from inline objects. These feed the per-step deep-links and image placement downstream.
3. **Identify the module/epic boundaries** (usually a top-level section of the ТЗ).
4. **Extract steps** from the spec's own structure (sub-sections, numbered/bulleted items, stages). Do NOT create "каркас"/"обзор"/"общий цикл" steps unless the spec explicitly requires them.
5. **Write the SCHEMA as a TREE to a `.md` file next to the source ТЗ** (same folder, `<имя_тз>_tasks_schema.md`) — not only to chat. Hierarchy: `Модуль/эпик → Этап/шаг из ТЗ → задачи по функционалам`, with a real GDD **deep-link** (`?tab=t.<TAB_ID>#heading=h.xxx` from the anchor index) next to each module and stage, and the leaves being the `TITLE`s in format `[Dev][<функционал>] <Модуль> - <Шаг>`.
   * **Each functional = its own task line** (separate heading), never combined onto one line.
   * **No abbreviation:** list every task in full — no «[7 функц.]», «…».
   * Per game step the full set of 7 (`логика·графика·анимации·эффекты·звуки·озвучка·туторы`); service screens — reduced set by sense.
   * **Mark duplicates** `🔁`: a step that recurs across modules is flagged with where else it appears (reuse the implementation).
   * Add a separate "Вопросы по ТЗ" block and a summary (tasks/modules/steps). **Do NOT write DESCRIPTION/TAGS/priorities or create the JSON yet.** See the tree format in HowToDecomposeTask_ForAI.md → Этап 1.7.
6. **Wait for the user** to confirm, merge, split, rename, add, or remove items. Apply edits; re-show the schema if changes are substantial. **Proceed only after explicit approval of the final list.**

### Phase B — JSON generation (Этап 2, only after approval)

1. **For each approved step, build the task object**:
   * `TITLE` = the approved title `[Dev][<функционал>] <Модуль> - <Шаг>`.
   * `DESCRIPTION` = the **4-part card** (see [dev-task-pattern.md](task-patterns/dev-task-pattern.md) §5): a full, self-contained goal for THIS functional, including expected result and applicable requirements (`Зависит от:` first if applicable; for `🔁` a "Дубль механики …, переиспользовать" note) → `📄 Раздел ГДД:` deep-link to the step's heading → `Что сделать` (all necessary imperative, functional-specific items via the functional lens §4) → `Готово, когда` (measurable DoD). **Per-functional delta** — the 7 functionals of one step must NOT share the same text. Keep it concise, but never omit a requirement needed to implement or accept the task. The full verbatim GDD stays in the doc, reached by the deep-link — do NOT copy it into the task.
   * `TAGS` = `["Dev", "<версия>", "<p1..p5>", "<функционал>"]`.
   * `PRIORITY` = логика→3, графика/анимации/эффекты→2, туторы/звуки/озвучка→1.
   * `GROUP_ID`=100 (for robots, match project), `RESPONSIBLE_ID`=66, `ALLOW_TIME_TRACKING`="1".
   * **Task complexity (estimate)** in **person-hours** (человеко-часы). Must be an integer (`1`, `4`, `8`, `20`, `77`). If source gives person-days, convert once: `ceil(personDays * 8)`. ⚠️ Set it via the system UF key `"UF_TASKS_TASK_1783529349965"` (or `UF_TASK_CAPACITY` if allowed by API).
   * **Capacity-aware Gantt rule**: One working day = 8 person-hours (10:00-18:00 MSK). Set `DURATION_TYPE="hours"` and `DURATION_PLAN` = capacity. Write arrows via `task.item.update` / `DEPENDS_ON`.
   * **Images by PROVENANCE, not by guess** (see [dev-task-pattern.md](task-patterns/dev-task-pattern.md) §6): an image goes into a task ONLY if it physically sits in that step's own GDD section AND matches the task's functional bucket (logic image → `логика`, animation image → `анимации`, …). Each image lands in **exactly one** task — no duplicates, no extras, nothing added "by meaning". Placement is driven by the **image manifest** built in Phase A, never invented.
 8. **Assemble the JSON via a GENERATOR SCRIPT.** For a Google-Doc source the script resolves `DOC_ID`, fetches the HTML-export of each tab (`export?format=html&tab=TAB_ID`), and extracts the **heading-anchor index** by parsing `id="h.xxxx"` from heading tags — see the full algorithm in [gdoc-heading-deeplink-algorithm.md](references/gdoc-heading-deeplink-algorithm.md). For large docs (>10 MB, where HTML export is capped) use the Google Docs API `documents.get` as a fallback. The script also builds the **image manifest** (`image_id, section_heading, step, functional_bucket, doc_position`) from inline objects. Then per step it emits the 4-part card for each functional (deep-link `?tab=t.<id>#heading=h.xxx` from the verified index, images from the manifest by provenance), JSON-escapes, writes UTF-8+BOM. Self-checks: parses, task count, every deep-link includes `?tab=` and resolves to a **real** `h.xxxx` from the exported HTML, the 7 functional DESCRIPTIONs of a step are not textually identical, no image is duplicated/out-of-section, no filler phrases. Keep the generator outside `Assets/` and delete it after.
 9. **Save** as UTF-8 **with BOM** in the **same folder as the source ТЗ** (`<имя_тз>_tasks.json`); split per-module into several `*_tasks.json` if the volume is large.
 10. **Report**: number of tasks, coverage map (spec item → task), and the open-questions list.

### Phase D — Evaluation & rework (gate ≥7, for patterns that require it)

Before handing the JSON to `bitrix-task-importer`, run the pattern's scoring loop (dev → [dev-task-pattern.md](task-patterns/dev-task-pattern.md) §8):

1. **Image-provenance gate (deterministic, 0 tokens).** A script checks every task's images against the manifest: in-section, right functional bucket, unique, no extras. Any violation → hard fail → rework.
2. **Sufficiency judge (developer test).** An **independent, session-isolated subagent** (never the worker's own session; same model family is allowed, cross-family preferred) scores each card 0–10 by the rubric — *"given only this DESCRIPTION, without the link, could a developer do the task?"*. Input = card text only (no GDD, no link) → cheap; batch 8–12 tasks per judge call.
3. **Rework `<7` or gate-failed tasks only**, targeted by the judge's `missing[]` / the gate violation — not a full regeneration. Re-judge only the reworked tasks.
4. **Cap 2 rounds.** Tasks still `<7` after that are flagged for a human; the rest are not blocked.
5. **Report** the score distribution and the list of tasks below 7.

Token economy: deterministic checks by script; the LLM judge never reads the GDD, runs in batches, and re-judges only dirty tasks.

## Output Format

* **Phase A** (in plan mode): the schema as a **tree written to `<имя_тз>_tasks_schema.md` next to the source ТЗ** — every task in full (each functional its own line), duplicates marked `🔁`, plus "Вопросы по ТЗ" and counts. No JSON yet.
* **Phase B** (after approval): a JSON file in the **same folder as the source ТЗ** — a top-level array of `{ "fields": { ... } }` objects — plus a short summary (task count, spec-item→task mapping, open questions).
* **Phase D** (if the pattern requires scoring): a QA report — score distribution, image-gate results, and the list of tasks scored below 7 (with the reason), after the rework loop.
* See [examples/sample-tz.md](examples/sample-tz.md) and [examples/sample-backlog.json](examples/sample-backlog.json) for a complete worked pair.

## Test Prompts

1. "Вот раздел ТЗ '4. Строительство дома' с пунктами 4.1–4.4 — собери JSON-бэклог задач для Битрикса."
2. "Разбей это ТЗ на задачи 1:1 по его пунктам и покажи, где у тебя возникли вопросы по ТЗ."
3. "Дополни существующий plombir_buildings_tasks.json новыми шагами из обновлённого раздела ТЗ."
