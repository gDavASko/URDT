# URDT Autonomous Reviewer: AI Agent Execution Runbook & Master Plan
*(Machine-Actionable Implementation Protocol, State Tracking, Ralph Self-Correction Loop & Drive E: Isolation)*

---

> [!IMPORTANT]
> **ИНСТРУКЦИЯ ДЛЯ ИСПОЛНЯЮЩЕГО ИИ-АГЕНТА:**
> Этот документ является **нормативным исполнительным руководством (Runbook)** для автономного ИИ-агента.
> Любой ИИ-агент, приступающий к выполнению задач из этого плана, **ОБЯЗАН строго следовать приведенным здесь правилам, командам верификации, протоколу самоисцеления и состоянию трекера `Docs/URDT_Execution_Progress.json`**.

---

## 1. Мета-директивы и железные правила для ИИ (AI Iron Rules)

При выполнении любой задачи из этого плана ИИ обязан беспрекословно соблюдать 6 фундаментальных правил:

### Правило 1: Полная изоляция дискового пространства (Drive E: Rule)
- **КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО** скачивать файлы, кэши, модели или создавать временные данные на диске `C:`.
- Кэш npm: строго `E:\Projects\URDT\.npm_cache\` (флаг `--cache E:\Projects\URDT\.npm_cache`).
- Веса моделей (GGUF): строго `E:\Projects\URDT\CoreAgent\models\`.
- Временные файлы и дампы: строго `E:\Projects\URDT\CoreAgent\.tmp\`.
- Игровые сохранения: строго `E:\Projects\URDT\URDT_Sandbox\`.
- Любая скачиваемая утилита или бинарник: строго в `E:\Projects\URDT\Tools\` или `E:\Projects\URDT\bin\`.

### Правило 2: Абсолютный запрет на пропуски ошибок (No Skipping / Self-Healing)
- Если команда сборки, тест или валидатор завершились с ненулевым кодом (`exit code != 0`), **КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО** переходить к следующему шагу или рапортовать об успехе!
- ИИ обязан:
  1. Остановиться и проанализировать текст ошибки / лог компилятора.
  2. Активировать **Узел решения проблемы (Self-Correction Node)**.
  3. Внести точечное исправление в код.
  4. Повторно запустить команду верификации.
  5. Только после получения `exit code 0` переходить к следующей подзадаче.
- Если после 3 самостоятельных попыток ошибка не устранена — остановиться и обратиться к пользователю с описанием вариантов решения.

### Правило 3: Запрет на симуляцию и самовалидацию (Machine Judge First)
- Запрещено утверждать "код работает", "функционал проверен", если не был запущен **реальный машинный судья** (Machine Judge: `dotnet build`, unit test, CLI-тест, линтер).
- Запрещено галлюцинировать ответы консоли, тестов или других агентов. Все вызовы должны выполняться реально через инструменты исполнения команд.

### Правило 4: Атомарная фиксация прогресса в трекере (State Persistence)
- ИИ ведет машиночитаемый учет выполненной работы в файле:  
  `Docs/URDT_Execution_Progress.json`
- Перед началом задачи ИИ переводит ее статус в `"IN_PROGRESS"`.
- После успешного прохождения проверки Machine Judge статус переводится в `"COMPLETED"` с фиксацией времени и хэша верификации.

### Правило 5: Минимальное вмешательство (Minimum Intervention)
- Не ломать существующий рабочий код `Packages/com.davasko.urdt`.
- Сохранять исходные сигнатуры методов и архитектурный стиль.
- Zero-Allocation в горячих путях C# (запрет `new` в `Update`/`FixedUpdate`, использовать `ArrayPool<T>`, статические делегаты).

### Правило 6: Цикл Ральфа (Ralph / Harness Verification Loop)
Каждая подзадача выполняется строго по замкнутому 5-шаговому циклу:
```
  [ 1. READ SPEC ] ──► [ 2. CODE SURGICALLY ] ──► [ 3. MACHINE JUDGE ]
          ▲                                                   │
          │             [ 4. SELF-CORRECTION NODE ]           ▼
          └───────────── (Fix code & retry if exit != 0) ◄─ [ OK? ]
                                                              │ YES
                                                              ▼
                                                    [ 5. PERSIST STATE ]
```

---

## 2. Протокол холодного старта, возобновления и восстановления после сбоев (Cold Start & Disaster Recovery Protocol)

Любой ИИ-агент (новый в сессии, после сброса контекста, смены модели или падения процесса) **ОБЯЗАН** начать свою работу со следующего детерминированного алгоритма:

```
                  ВХОД НОВОГО ИИ-АГЕНТА В ПРОЕКТ
                                │
                                ▼
         ┌─────────────────────────────────────────────┐
         │ 1. ЧТЕНИЕ ТРЕКЕРА:                          │
         │    Открыть Docs/URDT_Execution_Progress.json│
         └──────────────────────┬──────────────────────┘
                                │
                                ▼
         ┌─────────────────────────────────────────────┐
         │ 2. ПОИСК ТОЧКИ ОСТАНОВКИ (Active Pointer):  │
         │    Найти первый таск со статусом != COMPLETED│
         └──────────────────────┬──────────────────────┘
                                │
          ┌─────────────────────┴─────────────────────┐
          │ Статус: IN_PROGRESS или FAILED            │ Статус: PENDING
          ▼                                           ▼
┌──────────────────────────────┐            ┌──────────────────────────────┐
│ 3A. БЫЛ СБОЙ / ОБРЫВ СЕССИИ: │            │ 3B. ШТАТНОЕ ПРОДОЛЖЕНИЕ:     │
│ - Предыдущий агент упал тут! │            │ - Все предыдущие таски готовы│
│ - Запустить Machine Judge    │            │ - Запустить sanity-check     │
│   (dotnet build / npm test)  │            │   сборки (Exit 0)            │
│ - Прочитать реальный текст   │            │ - Взять этот таск в работу   │
│   ошибки из консоли          │            │ - Перевести в IN_PROGRESS    │
│ - Включить Self-Correction   │            └──────────────┬───────────────┘
│   (не перескакивать дальше!) │                           │
└──────────────┬───────────────┘                           │
               │                                           │
               ▼                                           ▼
      [ Устранить ошибку ]                        [ Выполнить задачу ]
               │                                           │
               └───────────────────┬───────────────────────┘
                                   │
                                   ▼
         ┌─────────────────────────────────────────────┐
         │ 4. ФИКСАЦИЯ И ОТЧЕТ:                        │
         │    Перевести в COMPLETED -> Отчет в чат     │
         └─────────────────────────────────────────────┘
```

### 2.1. Четыре шага самонастройки ИИ (Bootstrapping Algorithm)
1. **Шаг 1 (Инспекция состояния):**
   - Прочитать файл [`Docs/URDT_Execution_Progress.json`](file:///e:/Projects/URDT/Docs/URDT_Execution_Progress.json).
2. **Шаг 2 (Определение активной задачи):**
   - Найти первую задачу в словаре `tasks`, где `status !== "COMPLETED"`.
   - Если статус `"IN_PROGRESS"`: сессия прервалась прямо во время выполнения!
   - Если статус `"FAILED"`: предыдущая попытка завершилась ошибкой.
   - Если статус `"PENDING"`: это следующая задача по графику.
3. **Шаг 3 (Sanity Check репозитория):**
   - Перед внесением любых правок убедиться, что компилятор чист:
     ```powershell
     dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
     ```
   - Если проект не компилируется (Exit != 0), **КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО** начинать новый таск! Сначала восстановить зеленую сборку для предыдущего шага.
4. **Шаг 4 (Подтверждение контекста в первом ответе):**
   - ИИ обязан в первом сообщении пользователю объявить точку входа:
     > *"Выполнен протокол холодного старта. Обнаружена точка остановки на задаче `[TASK-ID]`. Предыдущие задачи верифицированы. Приступаю к реализации `[TASK-ID]` по регламенту."*

### 2.2. Протокол устранения ошибок и тупиков (Self-Correction & Escalation Protocol)
1. Если Machine Judge вернул `exit code != 0`:
   - Запрещено игнорировать лог! Прочитать вывод компилятора или трассировку исключения.
   - Локализовать файл и строку (CS0246, CS0103, CS1061 и т.д.).
   - Применить точечный фикс (Surgical Edit) и повторить вызов Machine Judge.
2. Лимит попыток:
   - ИИ разрешается сделать **до 3 самостоятельных попыток** исправления ошибки.
   - Если за 3 попытки ошибка не ушла, ИИ обязан остановить выполнение и эскалировать проблему пользователю, указав:
     - Текст ошибки компилятора.
     - Что было предпринято.
     - 2–3 альтернативных инженерных варианта решения.
3. Запрет на перескок:
   - Запрещено помечать падающий таск как `"COMPLETED"` или переходить к следующей задаче!

### 2.3. Универсальный промпт для запуска любого стороннего ИИ (Zero-Shot Agent Launch Prompt)
Пользователь может скопировать и отправить любому новому агенту (ChatGPT, Claude, Cursor, Windsurf, Antigravity) следующую директиву:

> **«Действуй строго по регламенту `Docs/URDT_Detailed_Implementation_Plan.md`. Выполни Протокол холодного старта (Раздел 2.1), прочитай `Docs/URDT_Execution_Progress.json`, проверь компиляцию репозитория, определи активную задачу и продолжи реализацию по Циклу Ральфа строго с изоляцией на диске E:.»**

---

## 3. Навигатор по документации (Source of Truth Map)

Перед редактированием файлов ИИ обязан прочитать соответствующий раздел спецификации:

| Область разработки | Файл спецификации для обязательного чтения | Что именно изучить перед кодом |
| :--- | :--- | :--- |
| **Бинарный фрейм 27 байт и C# структуры** | [`Docs/URDT_Wire_Protocol_Specification_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md) | Разделы 8, 9, 10 (структуры с `Pack=1`, раскладка байт, CRC8). |
| **Named Pipe и MainThreadDispatcher** | [`Docs/URDT_Wire_Protocol_Specification_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md) | Разделы 1, 5, 6 (MPSC RingBuffer, FixedUpdate-синхронизация). |
| **Авто-инструментация и Runtime Graphify** | [`Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md) | Разделы 3, 5, 6 (`UrdtAutoInstrumentation`, MurmurHash3, $K=3$). |
| **L1 Кинематика (Flash-Hogan) и Ввод** | [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md) | Раздел 2 (полиномы 5-й степени, 9 примитивов, New Input System). |
| **L2 Тактика, HTN-over-BT и Micro-SLM** | [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md) | Раздел 3 (двухконтурный цикл, GBNF-грамматики, анти-зацикливание). |
| **L4 Save-State и чистка корутин** | [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md) | Раздел 5 (`IUrdtSaveable`, 4-фазный протокол отката). |
| **CI/CD, Headless запуск и рециклинг** | [`Docs/URDT_CI_CD_Orchestration_EN.md`](file:///e:/Projects/URDT/Docs/URDT_CI_CD_Orchestration_EN.md) | Разделы 2, 3, 5, 6, 8 (`UrdtTestRunner.cs`, порт 0, `exit 42`). |
| **Пакет дефектов и Dashcam в RAM** | [`Docs/URDT_Failure_Evidence_Contract_EN.md`](file:///e:/Projects/URDT/Docs/URDT_Failure_Evidence_Contract_EN.md) | Разделы 3, 4, 5 (JSON Schema, `UrdtCrashDashcam.cs`, WebP base64). |

---

## 4. Машиночитаемый трекер прогресса (`Docs/URDT_Execution_Progress.json`)

ИИ обязан обновлять этот JSON-файл после завершения каждого атомарного таска.

```json
{
  "$schema": "urdt/execution_progress_v1.json",
  "project": "URDT Autonomous Reviewer",
  "hostHardware": "AMD Ryzen 9 7950X, RTX 2070, 32GB RAM",
  "storageRule": "STRICT_DRIVE_E_ONLY",
  "lastUpdated": "2026-09-22T15:30:00Z",
  "currentPhase": "PHASE_1_WIRE_PROTOCOL_CSHARP",
  "tasks": {
    "TASK-1.1-STRUCTS": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-1.2-MAIN-DISPATCHER": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-1.3-NAMED-PIPE-SERVER": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-1.4-CRASH-DASHCAM": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-1.5-IL2CPP-LINK-XML": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-1.6-PHASE1-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    
    "TASK-2.1-CORE-AGENT-SCAFFOLD": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-2.2-CONFIG-DRIVE-E": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-2.3-MODEL-DOWNLOAD-STREAM": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-2.4-IPC-CLIENTS-TS": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-2.5-PHASE2-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },

    "TASK-3.1-AUTO-INSTRUMENTATION": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-3.2-TRIAD-DISCOVERY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-3.3-RUNTIME-GRAPHIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-3.4-LAYOUT-CDN-INSPECTORS": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-3.5-PHASE3-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },

    "TASK-4.1-MULTI-TOUCH-MANAGER": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-4.2-FLASH-HOGAN-TICKER": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-4.3-MOTOR-PRIMITIVES": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-4.4-INPUT-INJECTOR-CSHARP": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-4.5-PHASE4-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },

    "TASK-5.1-TACTICAL-DUAL-LOOP": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-5.2-MICRO-SLM-GBNF": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-5.3-HTN-BT-HYBRID": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-5.4-STAGNATION-MODAL-POLICY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-5.5-PHASE5-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },

    "TASK-6.1-SAVE-STATE-ROLLBACK": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-6.2-HEADLESS-CI-RUNNER": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-6.3-CRASH-WATCHDOG": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-6.4-FAILURE-EVIDENCE-EXPORT": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null },
    "TASK-6.5-PHASE6-VERIFY": { "status": "PENDING", "machineVerdict": "NOT_RUN", "evidence": null }
  }
}
```

---

## 5. Пошаговые исполнительные карты подзадач (Actionable Task Cards)

---

### ФАЗА 1: C# Драйвер Wire Protocol v2 в Unity (`Packages/com.davasko.urdt`)

#### 🟩 TASK-1.1: Бинарные структуры команд C# (Fixed Memory Layout)
- **Цель:** Реализовать низкоуровневые C# структуры с `[StructLayout(LayoutKind.Sequential, Pack = 1)]` для передачи данных через Named Pipe без аллокаций.
- **Спецификация:** [`Docs/URDT_Wire_Protocol_Specification_EN.md#section-10`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md).
- **Файл для создания:** `Packages/com.davasko.urdt/Runtime/IPC/UrdtCommandStructs.cs`
- **Что сделать:**
  - Создать структуру `UrdtRawCommand` ровно на 27 байт (CmdType, PointerId, SequenceNumber, ScreenX, ScreenY, Pressure, TargetTimestampMs, IdempotencyKey, Crc8).
  - Создать `UrdtUiCommand` и `UrdtPhysicsCommand`.
  - Добавить `unsafe sizeof(UrdtRawCommand) == 27` assert в статический конструктор.
- **Команда верификации (Machine Judge):**
  ```powershell
  dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
  ```
- **Критерий приемки:** Сборка завершилась с `Exit code: 0`, 0 ошибок.
- **Самоисцеление (Self-Correction):** При ошибках компиляции типов `Vector2` добавить `using UnityEngine;`. При ошибках `unsafe` убедиться, что проверка размера использует `Marshal.SizeOf<UrdtRawCommand>() == 27`.

---

#### 🟩 TASK-1.2: Диспетчер главного потока (`UrdtMainThreadDispatcher.cs`)
- **Цель:** Исключить `UnityException` при обращении из потоков Named Pipe/WebSocket через раздельные lock-free очереди MPSC.
- **Спецификация:** [`Docs/URDT_Wire_Protocol_Specification_EN.md#section-5`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md).
- **Файл для создания:** `Packages/com.davasko.urdt/Runtime/IPC/UrdtMainThreadDispatcher.cs`
- **Что сделать:**
  - Реализовать кольцевые буферы `MpscRingBuffer<UrdtUiCommand>` и `MpscRingBuffer<UrdtPhysicsCommand>` на 256 слотов без аллокаций в куче.
  - В `Update()` разгружать очередь UI (`_uiQueue.TryDequeue`).
  - В `FixedUpdate()` разгружать очередь физики (`_physicsQueue.TryDequeue`), синхронизируя с тиком PhysX.
- **Команда верификации (Machine Judge):**
  ```powershell
  dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
  ```
- **Критерий приемки:** `Exit code: 0`, 0 ошибок компиляции.

---

#### 🟩 TASK-1.3: Сервер Windows Named Pipe (`UrdtNamedPipeServer.cs`)
- **Цель:** Обеспечить прием 27-байтных бинарных кадров L1 кинематики с субмиллисекундным IPC $< 0.05\text{ мс}$.
- **Спецификация:** [`Docs/URDT_Wire_Protocol_Specification_EN.md#section-1`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md), [`section-8`](file:///e:/Projects/URDT/Docs/URDT_Wire_Protocol_Specification_EN.md).
- **Файл для создания:** `Packages/com.davasko.urdt/Runtime/IPC/UrdtNamedPipeServer.cs`
- **Что сделать:**
  - Использовать `System.IO.Pipes.NamedPipeServerStream`.
  - Имя канала по умолчанию: `urdt_fast_ticker` (полный путь: `\\.\pipe\urdt_fast_ticker`).
  - Чтение ровно по 27 байт в цикле асинхронного чтения без создания промежуточных `byte[]` массивов (использовать фиксированный буфер `byte[27]`).
  - Декодирование в `UrdtRawCommand` и отправка в `UrdtMainThreadDispatcher`.
- **Команда верификации (Machine Judge):**
  ```powershell
  dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
  ```
- **Критерий приемки:** `Exit code: 0`, 0 ошибок компиляции.

---

#### 🟩 TASK-1.4: Кольцевой видеорегистратор аварий в RAM (`UrdtCrashDashcam.cs`)
- **Цель:** Записывать последние 5 секунд (25 кадров JPEG @ 5 FPS) в кольцевой буфер RAM (~1.5 МБ) для прикрепления к карточке бага.
- **Спецификация:** [`Docs/URDT_Failure_Evidence_Contract_EN.md#section-5`](file:///e:/Projects/URDT/Docs/URDT_Failure_Evidence_Contract_EN.md).
- **Файл для создания:** `Packages/com.davasko.urdt/Runtime/Inspectors/UrdtCrashDashcam.cs`
- **Что сделать:**
  - Pre-allocated буфер на 25 массивов `byte[]`.
  - Захват кадра каждые 200 мс через `AsyncGPUReadback.Request` (исключить микрофризы рендера).
  - Сжатие в JPEG (640x360, 60% quality).
  - Метод `string FreezeAndExportBase64WebP()` для формирования анимированного WebP base64.
- **Команда верификации (Machine Judge):**
  ```powershell
  dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
  ```
- **Критерий приемки:** `Exit code: 0`.

---

#### 🟩 TASK-1.5: Правила сохранения типов IL2CPP (`link.xml`)
- **Спецификация:** [`Docs/URDT_CI_CD_Orchestration_EN.md#section-9`](file:///e:/Projects/URDT/Docs/URDT_CI_CD_Orchestration_EN.md).
- **Файл для создания:** `Packages/com.davasko.urdt/link.xml`
- **Что сделать:** Добавить сохранение рефлексии для `KBP.URDT`, `UnityEngine.UI`, `Unity.InputSystem`.
- **Команда верификации:** Проверка существования файла и валидности XML.

---

#### 🟩 TASK-1.6: Машинная проверка Фазы 1
- **Команда проверки сборки Unity:**
  ```powershell
  dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
  ```
- **Обновление трекера:** Выставить `COMPLETED` для всех задач 1.1–1.6 в `Docs/URDT_Execution_Progress.json`.

---

### ФАЗА 2: Развертывание `CoreAgent` (Node.js/TS) строго на Диске E:

#### 🟩 TASK-2.1: Инициализация проекта с кэшем на диске E:
- **Цель:** Создать структуру `CoreAgent/` и жестко заблокировать npm от записи в `%APPDATA%` на диске C:.
- **Команды исполнения:**
  ```powershell
  New-Item -ItemType Directory -Force -Path "E:\Projects\URDT\CoreAgent", "E:\Projects\URDT\CoreAgent\models", "E:\Projects\URDT\CoreAgent\config", "E:\Projects\URDT\CoreAgent\src", "E:\Projects\URDT\.npm_cache"
  Set-Location -Path "E:\Projects\URDT\CoreAgent"
  npm config set cache "E:\Projects\URDT\.npm_cache" --location project
  ```
- **Создание `package.json`:**
  - Зависимости: `ws`, `@types/ws`, `node-llama-cpp`, `@langchain/langgraph`, `zod`, `typescript`, `ts-node`.
- **Команда установки (Machine Judge):**
  ```powershell
  cd E:\Projects\URDT\CoreAgent; npm install --cache "E:\Projects\URDT\.npm_cache"
  ```
- **Критерий приемки:** `node_modules` создан, `package-lock.json` сгенерирован, кэш записан строго в `E:\Projects\URDT\.npm_cache`.

---

#### 🟩 TASK-2.2: Конфигурационный файл `agent.config.json`
- **Файл:** `E:\Projects\URDT\CoreAgent\config\agent.config.json`
- **Содержимое:** Конфигурация из раздела 3 [`Docs/URDT_Detailed_Implementation_Plan.md`](file:///e:/Projects/URDT/Docs/URDT_Detailed_Implementation_Plan.md) со всеми путями на диск `E:\`.

---

#### 🟩 TASK-2.3: Скрипт потоковой загрузки весов модели на диск E:
- **Цель:** Скачать `qwen2.5-1.5b-instruct-q4_k_m.gguf` (1.1 ГБ) с HuggingFace стримингом напрямую в `E:\Projects\URDT\CoreAgent\models\`.
- **Файл для создания:** `E:\Projects\URDT\CoreAgent\scripts\download-models.js`
- **Что сделать:**
  - Использовать `https.get` со стримингом в `fs.createWriteStream`.
  - Никаких временных файлов на диске C:!
  - Поддержка возобновления загрузки и проверка размера файла (~1.11 ГБ).
- **Команда исполнения и верификации (Machine Judge):**
  ```powershell
  node E:\Projects\URDT\CoreAgent\scripts\download-models.js --target "E:\Projects\URDT\CoreAgent\models\qwen2.5-1.5b-instruct-q4_k_m.gguf"
  ```
- **Критерий приемки:** Файл существует по пути `E:\Projects\URDT\CoreAgent\models\qwen2.5-1.5b-instruct-q4_k_m.gguf`, размер $> 1\,100\,000\,000$ байт.

---

#### 🟩 TASK-2.4: Клиенты Named Pipe и WebSocket на TypeScript
- **Файлы:**
  - `E:\Projects\URDT\CoreAgent\src\protocol\pipe_client.ts` — подключение к `\\.\pipe\urdt_fast_ticker` через `net.createConnection`, отправка бинарных буферов 27 байт.
  - `E:\Projects\URDT\CoreAgent\src\protocol\urdt_client.ts` — подключение к `ws://127.0.0.1:9002` с флагом `{ noDelay: true }`.
  - `E:\Projects\URDT\CoreAgent\src\protocol\handshake_manager.ts` — согласование токена сессии и Heartbeat.
- **Команда компиляции TypeScript (Machine Judge):**
  ```powershell
  cd E:\Projects\URDT\CoreAgent; npx tsc --noEmit
  ```
- **Критерий приемки:** `Exit code: 0`, 0 ошибок типов.

---

### ФАЗА 3: Режим 1 — Подготовка, авто-инструментация и Runtime Graphify

#### 🟩 TASK-3.1: Авто-инструментация UI (`UrdtAutoInstrumentation.cs`)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-3`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md).
- **Файл:** `Packages/com.davasko.urdt/Runtime/Inspectors/UrdtAutoInstrumentation.cs`
- **Что сделать:**
  - Автопоиск всех `Selectable` компонентов в активной сцене.
  - Инъекция прокси `UrdtUiTarget` без модификации исходных скриптов разработчиков.
  - Извлечение меток текста из дочерних `TextMeshProUGUI` и стандартного `Text`.
- **Команда верификации:** `dotnet build .\Assembly-CSharp.csproj ...`

---

#### 🟩 TASK-3.2: Модуль Triad Control Scheme Discovery
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-4`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\control_discovery\triad_discovery.ts`
- **Что сделать:** Реализовать логику сопоставления гипотезы GDD $\to$ экранные токены $\to$ 100 мс физический микро-импульс.

---

#### 🟩 TASK-3.3: Движок топологической карты (Runtime Graphify)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-5`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l3_gdd\runtime_graphify_engine.ts`
- **Что сделать:**
  - Реализовать типы `UrdtGraphNode` и `UrdtGraphEdge`.
  - Хэширование MurmurHash3 64-bit с фильтрацией чисел (`/\b\d+([.,]\d+)?\b/g -> <NUM>`).
  - Алфавитная сортировка маячков, отсечение `ScrollRect`.
  - Экспорт в `application_map.json`.
- **Команда верификации:** `cd E:\Projects\URDT\CoreAgent; npx tsc --noEmit`

---

#### 🟩 TASK-3.4: Инспекторы верстки и ассетов (`UrdtLayoutInspector.cs`, `UrdtAsyncLoadingInspector.cs`)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-6`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md).
- **Файлы:**
  - `Packages/com.davasko.urdt/Runtime/Inspectors/UrdtLayoutInspector.cs` — детекция `tmp.isTextTruncated`, `fontSize < 10.0f`, глифов `\uFFFD`.
  - `Packages/com.davasko.urdt/Runtime/Inspectors/UrdtAsyncLoadingInspector.cs` — мониторинг Addressables handles и `UnityWebRequest` для исключения ложных софт-локов.
- **Команда верификации:** `dotnet build .\Assembly-CSharp.csproj ...`

---

### ФАЗА 4: Режим 2 — L1 Биоманипулятор и мультитач-кинематика

#### 🟩 TASK-4.1: Мультитач-диспетчер (4 канала)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l1_kinematics\multi_touch_dispatcher.ts`
- **Что сделать:** Реализовать независимые каналы `pointerId: 0..3` с маппингом на Unity `touchId: 1..10`.

---

#### 🟩 TASK-4.2: Тикер Flash-Hogan (16.6 мс на `process.hrtime`)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-4`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l1_kinematics\flash_hogan.ts`
- **Что сделать:**
  - Вычисление перемещения: $S(\tau) = 10\tau^3 - 15\tau^4 + 6\tau^5$.
  - Генератор Perlin/Gaussian микротремора ($\sigma = 0.8\text{px}$).
  - Отправка кадров в Named Pipe каждые 16.6 мс.

---

#### 🟩 TASK-4.3: Каталог 9 моторных примитивов
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l1_kinematics\motor_primitives.ts`
- **Что сделать:** Реализовать фабрику примитивов: `TAP`, `DRAG`, `SWIPE`, `SLICE`, `HOLD_EVENT_CONDITIONED`, `CHARGE_AND_RELEASE`, `CONTINUOUS_STEER_STREAM`, `PINCH_ZOOM`, `QTE_TIMED_TAP`.

---

#### 🟩 TASK-4.4: Инжектор ввода в Unity (`UrdtInputInjector.cs`)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-7`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файл:** `Packages/com.davasko.urdt/Runtime/Input/UrdtInputInjector.cs`
- **Что сделать:**
  - Инъекция через New Input System (`InputSystem.QueueStateEvent` для `Touchscreen.current`).
  - UGUI fallback (`ExecuteEvents.ExecuteHierarchy`).
  - Проверка перекрытия `BLOCKED_BY_UI_OVERLAY`.
  - Затухание Snap-to-Slot ($T_{settle} = 100\text{ мс}$).
- **Команда верификации:** `dotnet build .\Assembly-CSharp.csproj ...`

---

### ФАЗА 5: Режим 2 — L2 Тактический диспетчер и Micro-SLM

#### 🟩 TASK-5.1: Двухконтурный тактический контроллер (0.01 мс vs 50 мс)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-2`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l2_tactics\tactical_dispatcher.ts`
- **Что сделать:** Быстрый роутинг известных действий (0.01 мс) без вызова нейросети; переключение на Micro-SLM только при аномалиях.

---

#### 🟩 TASK-5.2: Интеграция Micro-SLM с GBNF-грамматиками
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-7`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l2_tactics\micro_slm_arbiter.ts`
- **Что сделать:**
  - Инициализация `node-llama-cpp` с загрузкой `qwen2.5-1.5b-instruct-q4_k_m.gguf` с диска E:.
  - GPU offloading (`gpuLayers: 28` на RTX 2070).
  - Динамическая GBNF-грамматика, ограничивающая генерацию токенов только валидными ID экранных кнопок.
  - Zod-валидация JSON выхода.

---

#### 🟩 TASK-5.3: Гибридный исполнитель HTN-over-BT
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l2_tactics\htn_bt_executive.ts`
- **Что сделать:** Иерархический генератор подзадач + реактивное дерево тика 50 мс с защитой от циклов (Loop-Break Invariant).

---

#### 🟩 TASK-5.4: Политика замираний и 5-слойный фильтр модальных окон
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l2_tactics\stagnation_policy.ts`
- **Что сделать:** Реализовать 5 фильтров: GDD контракт $\to$ `UrdtModalTarget` $\to$ активные анимации $\to$ regex таймера $\to$ 4.0с грейс-период.

---

### ФАЗА 6: L4 Save-State Engine, CI/CD и закрытие контура AI-разработки

#### 🟩 TASK-6.1: Движок сохранений и откатов (`IUrdtSaveable`)
- **Спецификация:** [`Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-5`](file:///e:/Projects/URDT/Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md).
- **Файлы:**
  - `Packages/com.davasko.urdt/Runtime/SaveState/IUrdtSaveable.cs`
  - `Packages/com.davasko.urdt/Runtime/SaveState/UrdtSaveStateManager.cs`
- **Что сделать:**
  - Реализовать 4-фазный протокол: принудительный килл корутин, `DOTween.KillAll(complete: false)`, отмена токенов UniTask, сброс sub-контейнеров DI.
  - Сохранение временных снимков строго в `E:\Projects\URDT\URDT_Sandbox\`.
- **Команда верификации:** `dotnet build .\Assembly-CSharp.csproj ...`

---

#### 🟩 TASK-6.2: CI/CD Раннер (`UrdtTestRunner.cs`)
- **Спецификация:** [`Docs/URDT_CI_CD_Orchestration_EN.md#section-3`](file:///e:/Projects/URDT/Docs/URDT_CI_CD_Orchestration_EN.md).
- **Файл:** `Packages/com.davasko.urdt/Runtime/Runner/UrdtTestRunner.cs`
- **Что сделать:** Парсинг аргументов `-urdtPort 0`, `-urdtHandshakeFile`, `-urdtToken`, поддержка кода `exit 42 (RECYCLE_REQUESTED)`.
- **Команда верификации:** `dotnet build .\Assembly-CSharp.csproj ...`

---

#### 🟩 TASK-6.3: Post-Mortem Native Crash Watchdog
- **Спецификация:** [`Docs/URDT_CI_CD_Orchestration_EN.md#section-8`](file:///e:/Projects/URDT/Docs/URDT_CI_CD_Orchestration_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l3_gdd\crash_watchdog.ts`
- **Что сделать:** Перехват `exit(code != 0)`, парсинг последних 200 строк `Player.log`, извлечение стека вызовов нативных библиотек.

---

#### 🟩 TASK-6.4: Экспорт пакета доказательств для AI-кодера
- **Спецификация:** [`Docs/URDT_Failure_Evidence_Contract_EN.md#section-3`](file:///e:/Projects/URDT/Docs/URDT_Failure_Evidence_Contract_EN.md).
- **Файл:** `E:\Projects\URDT\CoreAgent\src\l3_gdd\failure_evidence_builder.ts`
- **Что сделать:**
  - Сборка `UrdtFailureEvidencePacket` по схеме JSON Schema Draft-07.
  - Прикрепление 25 кадров анимированного WebP из `UrdtCrashDashcam.cs`.
  - Экспорт в `.harness/failure_evidence.json` на диске E:.

---

#### 🟩 TASK-6.5: Сквозная машинная верификация Фазы 6
- Запуск тестового прогона:
  ```powershell
  cd E:\Projects\URDT\CoreAgent; npm test
  ```
- Проверка генерации дашборда `Docs/QA_Audit_Report.html`.
- Финальное обновление трекера `Docs/URDT_Execution_Progress.json` со статусом всех подзадач `"COMPLETED"`.

---

## 6. Протокол сдачи результатов пользователю

По завершении любой задачи или фазы ИИ обязан выдать пользователю отчет следующего формата:

```markdown
### Отчет о выполнении [TASK-ID]:
1. **Статус Machine Judge:** [Exit 0 / SUCCESS] (с приведением вывода компилятора/теста).
2. **Измененные/созданные файлы:** кликабельные ссылки на файлы.
3. **Проверка правила диска E:** Запись производилась строго в E:\..., диск C не затронут.
4. **Статус трекера:** Задача [TASK-ID] переведена в COMPLETED в Docs/URDT_Execution_Progress.json.
5. **Следующий шаг:** Переход к [NEXT-TASK-ID].
```
