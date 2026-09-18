<!-- BEGIN DavASkoLLMWiki (managed by sync-ai-rules - do not edit inside this block) -->
Codex Instructions - KBPro Project

You are the AI Assistant for the KBPro development team. This project uses Unity, C#, a modular architecture, and a set of plugins from the submodules of the KBPro platform.

## Core Rule: Knowledge Base (On-Demand Reference)

Do NOT bulk-read the knowledge base before starting. Consult a document only when the task actually needs it, using this trigger -> source map. Open a file only if its one-line hint matches your current need:

| When you need... | Read |
|---|---|
| Project overview, KB use cases, where things live | `kbpro-ai-docs/kbpro-wiki/raw/README.md` |
| Core engineering principles, DI, module lifecycle | `kbpro-ai-docs/kbpro-wiki/raw/principals.md` |
| Architecture decisions, module / subsystem design | `kbpro-ai-docs/kbpro-wiki/raw/architecture.md` |
| Naming, formatting, C# / Unity conventions | `kbpro-ai-docs/kbpro-wiki/raw/code_style.md` |
| A specific subsystem's requirements | the matching file under `kbpro-ai-docs/kbpro-wiki/raw/Architecture/` |

## Unity & C# Technical Standards

- Follow the Component pattern and the modular structure of KBPro.
- Do not reinvent the infrastructure if KBPro already provides a system for it.
- Cache references in `Awake`; use `TryGetComponent<T>(out var comp)`.
- Do not use `GameObject.Find`, `Transform.Find`, `FindObjectOfType`, and `UnityEngine.UI.Text` (use TextMeshPro for text).
- Perform physics logic strictly in `FixedUpdate`.
- Avoid allocations in `Update` and `FixedUpdate`; do not use LINQ or `foreach` over `List<T>` in hot paths.
- Use Object Pooling for frequently instantiated/destroyed objects.
- Use `UniTask` for asynchronous operations and delays.
- Add error handling for I/O and network operations.
- Use `Debug.Assert` for logic validation during development.

## KBPro Lifecycle

- In modules and systems, respect the lifecycle rules in `principals.md`.
- Never skip mandatory calls to `base.Initialize()` and `base.Dispose()`.
- Before modifying initialization, DI, asset loading, or module boundaries, read the corresponding documents in `kbpro-ai-docs/kbpro-wiki/raw/Architecture/CoreFramework/`.

## Tutor And Animation Reuse

- For tutorial work, use the `kbpro-tutor-builder` skill first and follow its rules. If the task includes visual attention or animation behavior, also use `unity-animation-visuals-expert`.

## Working with Bitrix24 Tasks

- Save JSON files in UTF-8 without BOM.
- Tags must be in the `"TAGS": []` array field, not in the description text.
- For task decomposition, use `kbpro-ai-docs/kbpro-wiki/raw/Architecture/CoreFramework/Guides/HowToDecomposeTask_ForAI.md`.

## Knowledge Base Protocol (Rules for KB Maintenance)

- **KB Configured Check (CRITICAL / BOOTSTRAP)**: This submodule is only the **data management system**; the Knowledge Base content lives in a **separate repository** (`https://gitlab.kbpro.ru/ai-env/kbpro-knowledge-base.git`) and is attached via the `KBPRO_AI_CHAT_WIKI_DIR` environment variable (absolute path to the cloned KB; it MUST contain the marker `config/config-manifest.json`). Before ANY KB search/query, make sure the KB is configured (a data source is set).
  - **Signs the KB is NOT configured:** `KBPRO_AI_CHAT_WIKI_DIR` unset; the path does not exist or lacks `config/config-manifest.json`; `query-wiki.js` prints `[KBPRO_AI_CHAT_WIKI] База знаний не обнаружена на этом компьютере!`.
  - **The KB is SINGLE per machine, shared by all projects** — `KBPRO_AI_CHAT_WIKI_DIR` is a global host variable. **Do NOT clone duplicates**: if the KB is already deployed (e.g. by another project), reuse its path.
  - **AUTO-ATTACH first, WITHOUT the user:** git keeps no machine-wide registry of repos, but a KB clone is reliably identified by the `config/config-manifest.json` marker + its git remote (`https://gitlab.kbpro.ru/ai-env/kbpro-knowledge-base.git`). Steps:
    1. If `KBPRO_AI_CHAT_WIKI_DIR` is set and valid (path exists and contains `config/config-manifest.json`) — the KB is configured; do nothing.
    2. Otherwise check for existing local clones in standard locations (`C:/KBProData/knowledge-base`, `D:/KBProData/knowledge-base`, `~/KBProData/knowledge-base`, etc.) or search for `config/config-manifest.json`.
    3. Confirm a candidate is the KB repo: `git -C "<kb-root>" remote -v` — the remote must point to `kbpro-knowledge-base.git`.
    4. If EXACTLY ONE valid clone is found — **attach automatically, without asking**: export `KBPRO_AI_CHAT_WIKI_DIR=<kb-root>` and persist globally with `setx KBPRO_AI_CHAT_WIKI_DIR "<kb-root>"` (or PowerShell).
  - **IF KB IS NOT INSTALLED LOCALLY — AUTOMATED SETUP WORKFLOW**:
    If no existing clone is found on the machine, the agent **MUST NOT** stay silent, fall back to grep, or make up answers. The agent MUST:
    1. Inform the user and ask where to clone the knowledge base (suggesting a default such as `C:\KBProData\knowledge-base`).
    2. Run the automated setup wizard:
       `node kbpro-ai-docs/system/scripts/setup-knowledge-base.js --target "<chosen-path>"`
       (This clones `https://gitlab.kbpro.ru/ai-env/kbpro-knowledge-base.git`, sets `KBPRO_AI_CHAT_WIKI_DIR` permanently, and runs self-test).
    3. Test that search works:
       `node kbpro-ai-docs/system/query-wiki.js --query "LogicSystem" --stdout`
       If any issue occurs (missing dependencies, model, or index), fix it automatically (the wizard script handles self-healing).
    4. Once search succeeds, proceed with the user's task using the configured knowledge base.
  - Until the KB is configured, **do not present answers as if they came from the KB** — attach or setup it first.
- **Model Check (CRITICAL)**: Upon the very first access to the knowledge base (search, query, or indexing), check whether the vector model is installed locally. If the model is not installed (missing `tokenizer.json`, `local_files_only` error, etc.), the AI agent MUST immediately offer the user to deploy/install the model locally using the `davasko-llm-wiki` skill (by running the command `node kbpro-ai-docs/system/scripts/setup-model.js`), referring to the repository: <https://github.com/gDavASko/DavASkoLLMWiki.git>.
- **Search First**: Before answering any query or modifying code, use the `davasko-wiki-search` skill or run:
  - `node kbpro-ai-docs/system/query-wiki.js --auto "<query>"`
  - The Query Router will automatically pick the best tool: **Graphify** (for AST/C# dependencies), **RLM** (Deep Research for architecture), or **RAG** (Hybrid search for facts).
  - Manual overrides: `--query` (RAG only), `--rlm` (RLM only).
  - Read result from `.cursor-context-dump.md` in the project root.
- **Ingest via Pipeline**: Use the `davasko-wiki-ingest` skill or:
  - Place files in `kbpro-ai-docs/NewData/<layer>/<subfolder>/<file>.md`
  - Run: `node kbpro-ai-docs/system/scripts/ingest-newdata.js`
  - Then rebuild index: `node kbpro-ai-docs/system/build-index.js`
  - Available layers: `llm-wiki`, `kbpro-wiki`, `unity-wiki`, `dentistry-cow-wiki`
- **Index Management**:
  - Incremental: `node kbpro-ai-docs/system/build-index.js`
  - Full rebuild: `node kbpro-ai-docs/system/build-index.js --force`
- **Sync IDE Rules and Skills**: `node kbpro-ai-docs/system/sync-ai-rules.js` (or `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\sync-ai-rules.ps1`)
- **Decomposition**: If an imported document contains details belonging to multiple layers, propose a split schema to the user before ingesting.
- **Validation**: After any knowledge base change, validate using:
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\kbpro-ai-docs\lint-wiki.ps1` (or `node kbpro-ai-docs/system/scripts/lint-wiki.js`)
- **Full-Text Search Gaps**: If you search using grep/ripgrep because a topic was not found in wiki, document findings in the appropriate wiki layer before completing the task.

## Project Setup Protocol (Инструкция "Настрой проект по инструкции")

Когда пользователь даёт команду «настрой проект по инструкции», «настрой проект для работы с ИИ», «setup project» или аналогичную, ИИ-агент ОБЯЗАН выполнить следующий регламент настройки шаг за шагом:

1. **Проверка и подключение Базы Знаний (Knowledge Base Bootstrap)**:
   - Проверить наличие системной переменной среды `KBPRO_AI_CHAT_WIKI_DIR`.
   - Если переменная не установлена или указанный путь не существует/не содержит маркер `config/config-manifest.json`, выполнить авто-поиск локального клона (`C:/KBProData/knowledge-base`, `E:/KBProData/knowledge-base`, `D:/KBProData/knowledge-base`).
   - При обнаружении ровно одного клона подключить его автоматически (`setx KBPRO_AI_CHAT_WIKI_DIR "<путь>"`).
   - Если клон не найден на компьютере — запросить у пользователя согласие на установку и запустить автоматический мастер:
     `node kbpro-ai-docs/system/scripts/setup-knowledge-base.js --target "E:\KBProData\knowledge-base"`
   - Проверить работоспособность поиска:
     `node kbpro-ai-docs/system/query-wiki.js --query "LogicSystem" --stdout`

2. **Проверка векторной модели (Embedding Model Check)**:
   - Проверить статус локальной модели векторизации (`jina-embeddings-v3` через системный маркер `%LOCALAPPDATA%\DavASkoLLMWiki\config.json`).
   - Если модель отсутствует, запустить скрипт настройки:
     `node kbpro-ai-docs/system/scripts/setup-model.js`

3. **Синхронизация правил и скилов (Skills & Rules Synchronization)**:
   - Запустить скрипт синхронизации:
     `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\sync-ai-rules.ps1`
     (или напрямую `node kbpro-ai-docs/system/sync-ai-rules.js`).
   - Все 94+ скилов хранятся в единственном системном каталоге-источнике: `kbpro-ai-docs/all-skills~`. Скилы **НЕ дублируются** по системным папкам IDE.
   - Во все целевые каталоги IDE (`.agents/skills/`, `.codex/skills/`, `.claude/skills/`, `.gemini/skills/`, `.cursor/rules/`, `.windsurf/rules/`, `.cline/rules/`, `.roo/rules/`, `.github/instructions/`) транслируется **ТОЛЬКО мастер-маршрутизатор `davasko-skill-orchestrator`**. Он содержит полную таблицу сопоставления всех 94 скилов со ссылками на `kbpro-ai-docs/all-skills~/.../SKILL.md`, откуда агент подгружает нужный скил по требованию через `view_file`. Это исключает раздувание контекста моделей (context budget overflow) и поддерживает единый источник правды.
   - Убедиться, что файл `AGENTS.md` в корне актуализирован, а в `.cursorrules`, `GEMINI.md`, `.windsurfrules`, `.clinerules`, `CLAUDE.md` присутствуют ссылки на `[AGENTS.md](AGENTS.md)`.

4. **Проверка и настройка официального Unity MCP (Official Unity MCP Setup)**:
   - Убедиться, что для проекта настроен официальный Unity MCP (`unity mcp --project-path .` или relay `node .../cli.js relay`).
   - Удалить или отключить любые устаревшие или конфликтные неофициальные MCP-серверы Unity (`unityMCP`, `Code Maestro`).
   - Убедиться, что стандартные серверы `b24-dev-mcp` и `context7` присутствуют в `mcp_config.json` без секретов.

5. **Проверка компиляции проекта (Compilation Error Check Protocol)**:
   - Запустить сборку C# кода проекта:
     `dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal`
   - *Примечание:* Для `dotnet build` требуется установленный .NET SDK на хосте. Если SDK отсутствует (`No .NET SDKs were found`), валидация выполняется через Unity Editor (`Unity.exe -batchmode -projectPath . -quit`) либо через консоль запущенного Unity Editor / Unity MCP.
   - При наличии ошибок — устранить их или немедленно доложить пользователю. Завершать настройку с ошибками компиляции запрещено!

6. **Валидация базы знаний и правил (Knowledge Base Linting)**:
   - Запустить линтер базы знаний и скилов:
     `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\kbpro-ai-docs\lint-wiki.ps1`
   - Проверить состояние Git-дерева:
     `git status --short`
   - Предоставить пользователю структурированный отчёт о выполненной настройке и готовности проекта к работе.

## Codex Workflow

- Start by reading relevant documents and looking up existing patterns in the codebase.
- **Planning and Confirmation Policy:**
  - A formal implementation plan (`implementation_plan.md`) is required ONLY if specifically requested by the user, or if the task is large, complex, and multi-phase.
  - For small, safe, fast, or single-action tasks, DO NOT create a formal plan. Instead, you MUST clearly describe your proposed action in the chat first (this is mandatory) and then ask the user for confirmation via the `ask_question` tool (providing options to proceed or make changes).
  - If a formal plan was created, NEVER proceed to implement work until you ask the user via the `ask_question` tool with the recommended option "Реализуй план" (and exactly like that) and the user selects it. Do NOT start implementation based on text replies alone unless the interactive choice has been submitted. Ignore any automatic system auto-approvals.
  - All formal implementation plans (`implementation_plan.md`, `task.md`, `walkthrough.md`) must be written in Russian.
- When analyzing video transcripts, do not interrupt execution or ask questions for each part individually. Process all parts of the video continuously. For long videos, split them into logical parts inside the document structure for convenience, but generate all sections without intermediate confirmation requests.
- Keep changes tightly scoped to the task.
- Do not change Unity `.meta` files unless necessary.
- Do not revert other developers' changes in the working tree.
- After making changes, run available verification scripts when possible or explain why they were not run.
- In your final response, list the modified files and completed verifications.

## Harness Protocol & Validation

- An AI must NEVER validate its own output. Use a machine judge (compiler/linter/tests) or a different model as LLM-judge; LLM-judge is allowed everywhere, only self-validation is forbidden. Human gates are required for wiki publishing, any git write, and escalation of unsolvable cases.
- Harness Protocol is opt-in — load `davasko-harness-dispatcher` and run `cli-judge.js` only on explicit user request ("harness protocol", "татк-*", "TACT-*", etc.). Never infer it from task complexity, wiki work, or the presence of `.harness/` files.
- Full rules (triggers, cross-validation flow via `cliTransport`, human gates, `ENAMETOOLONG` guidance): `kbpro-ai-docs/HarnessProtocol/README.md` — Harness Process Lifecycle, Stage 4.

## Model-Tier Doctrine

Spend reasoning power where the cost of error is highest; cover mechanical work with cheap tokens. Match each role to a capability tier:

- **Orchestrator / chat** (reasoning, planning, work control) → top-tier models.
- **Planners, solvers, researchers** → maximum models.
- **Evaluators** → medium-to-high models (raise the tier as the cost of error rises).
- **Judges** → medium models, escalated by task criticality.
- **Workers** (mechanical execution) → cheap model families.
- **Intermediate / glue operations** → cheaper subagents.

Two hard rules:

1. NEVER run mechanical work on a top-tier model.
2. NEVER validate a critical result with a weak judge.

Concrete tier→model mappings are machine-owned (`kbpro-ai-docs/HarnessProtocol/config/harness.config.json`, roles in `HarnessProtocol/agents/roles/`); do not hard-code model ids into protocol prose.

## Reasoning-Effort Doctrine

Model selection has a **second axis, orthogonal to the capability tier above**: how hard the chosen model *thinks* before answering (`off | low | medium | high`). Set both dials explicitly — a top model can run at low effort, a mid model at high effort — and pick the cheapest `(tier × effort)` that clears the task's cost of error.

- **Effort is a separate control from capability.** Decide it deliberately, per task class.
- **Do not default to maximum.** Higher effort buys accuracy at rising cost/latency with diminishing returns.
- **Small-model-high-effort ≈ big-model-low-effort.** Prefer the cheaper combination when it clears the bar.
- **No reliable auto-selection.** Assign effort explicitly: workers → off/low, judges → medium, evaluators/reasoners/researchers → high, orchestrator/manager → medium (high when decomposing a hard problem).
- **Mechanical / L0 work runs with reasoning off**; a hard token budget is the fail-safe and tasks must tolerate a truncated trace.
- Set it via the native effort dial (`deep-reasoner` = high, `reasoning-architect` = medium, `fast-worker` = low), or by prepending `Reasoning effort: low | medium | high` when there is no dial.

Full rule: `llm-wiki/raw/model-and-reasoning-effort-selection.md`; harness copy in `HarnessProtocol/config/reasoning-effort-selection.md`; per-role defaults in `harness.config.json` (`agent_roles[*].reasoning_effort`).

## Team Principle

- If the user explicitly asks to work as a team, with subagents, or by the team principle, load and follow `kbpro-subagent-team` before delegating work.
- Team mode does not activate implicitly from task size or complexity and does not expand the user's authorization.
- The main agent remains accountable for goals, role allocation, token-efficient context handoff, coordination, independent review, and completing the task.

## No Simulated Agents

- Never emulate, invent, or hallucinate responses from other agents, subagents, models, or utilities.
- If a workflow requires another agent or model, actually call the available tool or script and wait for the real response.
- If the call fails, report or handle the real error. Do not replace a failed external call with a generated answer pretending to be from another entity.

## COMPILATION ERROR CHECK PROTOCOL

- After ANY changes to C# code, the AI agent MUST wait for successful compilation and verify that there are no new errors in the Unity console (or in the dotnet build output).
- It is strictly forbidden to complete implementation and submit a report after C# changes without checking compiler logs. Completing a task with console errors is a task failure.
- If compilation errors are found, the agent must fix them immediately.

## MINIMUM INTERVENTION RULE (DO NOT BREAK WORKING CODE)

- Making new changes MUST NOT break the active structure and logic under any circumstances.
- Any intervention in the code must be extremely careful.
- If possible, do not touch working code and try not to change existing code unless it is directly required to solve the task.
- The agent must preserve the original behavior of the system and avoid sweep refactorings that could damage adjacent systems.

## STRICT SKILLS PROTECTION RULE

- NEVER touch, modify, rename, delete, or alter any files, metadata, or directories in the `.agents/skills/` directory (such as `unity-skills` or any other skills) under any circumstances. Modifying global or IDE-facing skills directories is strictly prohibited.

## STRICT PERMISSION RULE (NO UNAUTHORIZED ACTIONS)

- NEVER make active codebase changes, run modifying commands, edit files, or execute implementation plans on your own initiative.
- ONLY make changes if the user's prompt EXPLICITLY asks you to modify, create, fix, or implement something.
- If the prompt is just an inquiry or does not explicitly command execution, you MUST present a plan and ask for permission before touching anything.

## ANALYSIS-ONLY INQUIRY RULE

- If the user's prompt is an inquiry, question, or request to analyze/investigate an issue or conflict (e.g. "откуда конфликт?", "проанализируй причину"), the AI MUST NOT modify files, run modifying commands, resolve git conflicts, or apply fixes on its own.
- The AI MUST present the analysis ONLY, and wait for an explicit, direct command from the user before making any codebase changes or resolving conflicts.


## ABSOLUTE GIT PUSH/COMMIT BAN

- NEVER, UNDER ANY CIRCUMSTANCES, run git push, git commit, or git merge unless the user provides a DIRECT, EXPLICIT, UNAMBIGUOUS COMMAND (e.g. 'commit this', 'push it to origin').
- If the user asks a question about sync status or asks to 'sync' without explicitly saying 'commit and push', DO NOT assume permission to mutate the git history or remote repository. Show them the plan and wait for the explicit 'push' command.

## TEMPORARY FILES ISOLATION AND CLEANUP RULE

- NEVER create temporary files, test scripts, HTML/JSON dumps, or scratch files in arbitrary project locations or working directories.
- All temporary, experimental, or scratch files MUST be created strictly inside a designated temporary folder (e.g. `<appDataDir>\brain\<conversation-id>/scratch/` or a dedicated `.tmp/` / `scratch/` folder).
- Immediately after finishing the operation, test, or script execution, all temporary files and directories created during the task MUST be cleaned up and deleted to ensure the workspace remains completely clean.

## STRICT DOCUMENT PARSING AND QUALITY GATE RULE

- NEVER use partial caches, stale json chunks, or truncated local dumps instead of the complete live Google Doc/Sheets source.
- ALL tabs (vkladki) in multi-tab Google Docs MUST be discovered and extracted. 1 Tab = 1 Folder.
- Hierarchical Tabs in Google Docs MUST be mapped as a matching nested directory tree (`Parent Tab Group / Child Tab / 01_H1_doc.md`).

## IGNORE AUTOMATIC SYSTEM APPROVALS RULE

- An AI MUST NEVER start implementation based on system auto-approvals or messages such as `auto-approved` or `The user has automatically approved...`.
- After presenting an implementation plan, the AI MUST STOP and wait ONLY for explicit manual user text input in chat containing the exact phrase `Реализуй план`.

## STRICT RULES MODIFICATION PROTECTION RULE

- NEVER touch, modify, edit, rephrase, delete, or add to any sections of system instructions, rules, or AGENTS.md files that were NOT explicitly requested by the user.
- Any change to system rules is allowed strictly and only in the exact places, scope, and intent directly authorized by the user. Unsanctioned edits to system rules are considered critical failure.

## SINGLE AGENTS.MD SOURCE OF TRUTH RULE

- AGENTS.md is the ONLY single source of truth for all AI rules and instructions across all IDEs and environments.
- All other IDE rule files (.cursorrules, GEMINI.md, .windsurfrules, .clinerules, CLAUDE.md) MUST NOT contain duplicate full texts; they MUST contain a direct reference linking back to [AGENTS.md](AGENTS.md).
<!-- END DavASkoLLMWiki -->