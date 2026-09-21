---
name: davasko-urdt-ui
description: Universal skill teaching AI agents how to interact with, play, test, and automate ANY Unity UI in runtime step-by-step using URDT (Unity Remote Debugging Transport) over WebSocket. Strict adherence to the 4-phase Agentic Loop (Perception -> Reasoning -> Action -> Delta Evaluation), Polymorphic Beacon Hierarchy, viewport-aware auto-scrolling, Virtual Joystick / Stick steering, simultaneous Multi-Touch actions (holding buttons while deflecting sticks on pointerId 1..10), and honest Tier 2 device-level input. Absolute ban on screenshots and monolithic batch test runner scripts. Includes comprehensive guide on WHERE and HOW to place beacons.
---

# DavASko URDT UI Automation Skill

This skill teaches AI agents how to **directly play, inspect, and automate ANY Unity UI in ANY project** in real-time through the **URDT (Unity Remote Debugging Transport)** protocol.

---

## 1. Core Philosophy: The AI is the Player (Не скрипты, а живая игра!)

**СТРОЖАЙШИЙ ЗАПРЕТ на монолитные пакетные скрипты с жестко закодированными задержками и цепочками кликов!**

ИИ-агент при работе с URDT должен действовать как **живой игрок за пультом управления**:
1. **Perception (Восприятие)**: Запрос живого состояния сцены (`query { activeOnly: true }` или `inspect { testId }`) — понять, какие окна активны, где расположены кнопки, не вышли ли они за экран.
2. **Reasoning (Осмысление)**: Анализ контекста:
   - Какое окно сейчас активно (`ActiveWindow`)?
   - Не перекрыт ли экран модальным окном?
   - Не обрезан ли элемент скроллом (например, `screenPosition.y < 0`)?
3. **Action (Действие)**: Отправка **одного атомарного честного действия** виртуальных устройств (`click`, `drag`, `type_text`, `swipe`) через WebSocket (`ws://127.0.0.1:7777/`).
4. **Evaluation (Оценка дельты)**: Немедленная проверка изменения состояния (`inspect`). Проверить, что игра отреагировала (открылось окно, переключился тогл, изменился текст). Адаптация на лету!

---

## 2. Карта расстановки маячков: Куда и что вешать

Маячки (`UrdtUi*Target`) прикрепляются к UI-элементам для предоставления семантики и координат ИИ.
**Правило нулевой связности (Zero Coupling):** Игровые контроллеры (например, `MainMenuController`) остаются чистыми uGUI-компонентами и **ничего не знают** о маячках!

| UI-элемент в Unity | Какой маячок вешать | На какой GameObject | Что отслеживает | Поддерживаемые команды |
|---|---|---|---|---|
| **Экран / Окно / Модалка** | `UrdtUiWindowTarget` | Корневой объект окна (`RectTransform`) | `ActiveWindow`, видимость экрана | `inspect`, `query` |
| **Кнопка (Button / Card / Tab)** | `UrdtUiButtonTarget` | На объект с `UnityEngine.UI.Button` | `IsInteractable`, счетчик кликов `InteractionCount`, результат `LastResult` | `inspect`, `query`, `click`, `double_click`, `multi_click` |
| **Чекбокс / Переключатель** | `UrdtUiToggleTarget` | На объект с `UnityEngine.UI.Toggle` | `ToggleValue` (bool ON/OFF), кликабельность | `inspect`, `query`, `click` |
| **Ползунок / Слайдер** | `UrdtUiSliderTarget` | На объект с `UnityEngine.UI.Slider` | `SliderValue` (float 0..1), координаты для Drag | `inspect`, `query`, `drag`, `press_move`, `swipe` |
| **Поле ввода текста** | `UrdtUiInputTarget` | На объект с `TMP_InputField` / `InputField` | `InputValue` (string), фокус каретки | `inspect`, `query`, `click` (фокус), `type_text` |
| **Выпадающий список** | `UrdtUiDropdownTarget` | На объект с `TMP_Dropdown` / `Dropdown` | `DropdownValue`, `DropdownLabel`, список опций | `inspect`, `query`, `click` |
| **Прокручиваемый список** | `UrdtUiScrollTarget` | На объект с `UnityEngine.UI.ScrollRect` | `ScrollPosition` (x,y), границы вьюпорта и контента | `inspect`, `query`, `scroll`, `drag`, `swipe` |
| **Виртуальный стик / Джойстик** | `UrdtUiStickTarget` | На объект с `UrdtVirtualStick` | `StickValue` (x,y), `Magnitude`, `IsPressed`, `HandleScreenCenter` | `inspect`, `query`, `drag`, `pointer_down`, `pointer_up` |
| **Кнопка с удержанием (Hold Button)**| `UrdtUiButtonTarget` | На объект с `UrdtHoldButton` | `IsHeld` (bool), кликабельность, счетчик нажатий | `inspect`, `query`, `click`, `pointer_down`, `pointer_up` |
| **Холст для рисования (Canvas)** | `UrdtUiDrawingTarget` | На объект с `UrdtDrawingCanvas` | `PenPosition`, `StrokeCount`, `TotalDrawnLength`, `IsDrawing` | `inspect`, `query` |
| **Иконка / Индикатор / Лейбл** | `UrdtUiGenericTarget` | На любой объект с `RectTransform` | Экранные границы `ScreenRect`, центр `ScreenCenter` | `inspect`, `query` |

---

## 3. Как расставлять маячки (3 практических способа)

### Способ А: В редакторе Unity через Inspector
1. Выделите объект в Hierarchy (например, кнопку `BtnSettings`).
2. Нажмите `Add Component` -> выберите соответствующий маяк (например, `Urdt Ui Button Target`).
3. Заполните понятный и стабильный `TargetId` (например, `"menu.btn_settings"`).
4. Поле компонента (`_selectable`, `_toggle`, `_slider`) привяжется **автоматически** благодаря встроенному методу `AutoBind()` в `Awake()` и `Reset()`.

### Способ Б: В UI Prefab'ах (Рекомендуется для продакшена)
- Откройте префаб кнопки или карточки.
- Повесьте на него маяк. Все создаваемые в рантайме экземпляры автоматически получат маячок и зарегистрируются в реестре URDT.

### Способ В: Процедурно из C# (для динамического UI)
```csharp
Button btn = go.GetComponent<Button>();
UrdtUiButtonTarget beacon = go.AddComponent<UrdtUiButtonTarget>();
beacon.ConfigureButton(
    targetId: "hud.btn_attack",
    activeWindow: "window_battle",
    activeModule: "combat",
    module: "ui",
    supportedCommands: new[] { UrdtCommandType.Inspect, UrdtCommandType.Query, UrdtCommandType.Click },
    selectable: btn,
    clickResult: "attack_executed"
);
```

---

## 4. Главные инварианты и правила работы ИИ

1. **Автономный пошаговый цикл (No "Dead Scripts")**:
   - ИИ сам выполняет ходы turn-by-turn. Запрещено генерировать 100-строчный монолитный батч-скрипт с `setTimeout` и запускать его вслепую.
2. **Полиморфная иерархия маячков (SRP)**:
   - 1 элемент = 1 специализированный маяк. Никаких «комбайнов» с 10 пустыми полями в инспекторе.
3. **Учет границ экрана и автоскролл (Viewport Awareness)**:
   - Если у элемента `screenPosition.y < 0` или он за пределами экрана — **не кликать вслепую!** Найти родительский `UrdtUiScrollTarget` и сделать `swipe`, пока элемент не войдет во вьюпорт.
4. **Честный ввод 2-го уровня (Tier 2 Device Input)**:
   - Никогда не вызывать `button.onClick.Invoke()`. Все действия отправляются через URDT (`click`, `drag`, `type_text`, `swipe`), эмулируя виртуальные устройства в Unity `InputSystem`.
5. **Мультитач и одновременное удержание (Multi-Touch & Hold-to-Act)**:
   - Мышь (`pointerId: 0`) строго монопольна. Нельзя одной мышью одновременно удерживать кнопку и тянуть джойстик.
   - Для одновременных действий используются **раздельные каналы виртуального тачскрина** (`pointerId: 1..10`):
     - Канал 1 (`pointerId: 1`): удержание кнопки (`pointer_down` на кнопку действия, например `ui.btn_draw`).
     - Канал 2 (`pointerId: 2`): отклонение джойстика (`pointer_down` со смещением от центра базы стика $(X_0, Y_0) + \vec{D}_{\text{norm}} \times R$).
   - Движение длится расчетное время $T = \frac{L}{V}$, после чего стик центрируется через `pointer_up` на канале 2.
   - Перо/персонаж освобождается через `pointer_up` на канале 1.
6. **Абсолютный запрет на скриншоты для валидации UI**:
   - Валидация состояния проводится ТОЛЬКО через структурированные данные WebSocket (`inspect`, `query`, `hit_test`).

---

## 4.5. Подготовка к взаимодействию и предстартовый чеклист (Unified Pre-Flight Startup Checklist)

Для обеспечения 100% надежности старта в любом окружении агент обязан соблюдать 5-шаговый подготовительный регламент:

1. **Связь и Handshake (Шаг 1)**:
   - Подключение к `ws://127.0.0.1:7777/` с передачей токена и projectId.
   - Подтверждение статуса `ready` перед отправкой любых команд взаимодействия.
2. **Адаптивность к текущему разрешению экрана (Resolution-Agnostic UI) (Шаг 2)**:
   - **НИКАКОГО ХАРДКОДА ПИКСЕЛЕЙ И ПРИНУДИТЕЛЬНОЙ ФИКСАЦИИ 1080p!**
   - Сервер URDT вычисляет координаты через `RectTransformUtility.WorldToScreenPoint`, возвращая `ScreenCenter` и `ScreenRect` в реальных физических пикселях текущего окна Game View (4K, 1440p, 1080p, Ultrawide, Free Aspect).
   - Все координаты запрашиваются динамически через `inspect` / `query` перед каждым действием.
   - Дистанции жестов (свайпы, скролл) рассчитываются исключительно в долях от текущего размера вьюпорта (например, $\text{distance} = \text{ScreenRect.height} \times 0.25$), а не константами в пикселях.
3. **Безопасный синтаксис CLI в Windows PowerShell (Шаг 3)**:
   - В терминале PowerShell одинарные кавычки `'{"testId":"..."}'` теряют двойные кавычки при передаче в Node.js, вызывая краш `JSON.parse` (`Expected property name or '}'`).
   - **Строгое правило**: использовать типизированные подкоманды CLI:
     ```powershell
     node Tools/urdt-cli.js click <testId>
     node Tools/urdt-cli.js inspect <testId>
     node Tools/urdt-cli.js query --active
     ```
   - Для комплексных сессий запускать постоянный интерактивный режим без перезапуска процесса:
     ```powershell
     node Tools/urdt-cli.js --repl
     ```
4. **Проверка вьюпорта и автоскролл (Шаг 4)**:
   - Перед кликом проверить попадание в границы экрана: $0 \le X \le \text{Screen.width}$ и $0 \le Y \le \text{Screen.height}$.
   - Если элемент обрезан скроллом — выполнить адаптивный `swipe` по родительскому `UrdtUiScrollTarget`, пока элемент не войдет во вьюпорт.
5. **Замкнутый цикл дельта-валидации (Closed-Loop Evaluation) (Шаг 5)**:
   - Никогда не считать клик успешным вслепую.
   - После клика вызвать `inspect` и проверить дельту: увеличился ли `InteractionCount`, изменилось ли `ActiveWindow` или состояние контрола.
   - Если дельта не зафиксирована (пропуск кадра EventSystem) — повторить клик с удержанием в 1–2 кадра (`hold_frames: 2`).

---

## 5. Пошаговый цикл игры ИИ с любым интерфейсом

```
┌────────────────────────────────────────────────────────────────────────┐
│ 1. CONNECT    ws://127.0.0.1:7777/ ➔ handshake { token, projectId }   │
├────────────────────────────────────────────────────────────────────────┤
│ 2. PERCEIVE   query { activeOnly: true } ➔ Получить видимую топологию │
├────────────────────────────────────────────────────────────────────────┤
│ 3. REASON     Оценить ActiveWindow, модалки и экранные координаты      │
│               • Открыта модалка ➔ сначала закрыть / подтвердить модалку│
│               • Элемент вне экрана ➔ сделать swipe UrdtUiScrollTarget  │
├────────────────────────────────────────────────────────────────────────┤
│ 4. ACT        Отправить атомарный ввод (click / drag / type_text)      │
├────────────────────────────────────────────────────────────────────────┤
│ 5. EVALUATE   inspect { testId } ➔ Проверить дельту состояния игры     │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Справочные руководства

- [Beacon Instrumentation Guide](references/beacon-instrumentation-guide.md) — Полное руководство по расстановке маячков во всех деталях.
- [Agentic Interactive Loop Guide](references/agentic-interactive-loop.md) — Пошаговое руководство по циклу восприятия, осмысления, действия и оценки.
- [Multi-Touch & Stick Controls Guide](references/multitouch-and-stick-controls.md) — Руководство по управлению виртуальными стиками, расчету векторов отклонения и одновременным действиям на мультитач-каналах (pointerId: 1..10).
- [Polymorphic Beacon Hierarchy](references/polymorphic-beacon-hierarchy.md) — Архитектура классов, диаграммы наследования и принципы SRP.
- [WebSocket Protocol Spec](references/websocket-protocol-spec.md) — JSON-RPC спецификация всех команд URDT.
- [URDT Architecture Spec](references/urdt-architecture.md) — Внутреннее устройство сервера, MainThreadDispatcher и InputSimulator.
- [Unity CLI Integration Guide](references/unity-cli-integration.md) — Границы между CLI и рантайм отладкой.
- [Troubleshooting & Edge Cases](references/troubleshooting.md) — Решение проблем с залипанием фокуса EventSystem, скроллом и модалками.

---

## 7. Примеры кода

### Антипаттерны (Как делать ЗАПРЕЩЕНО)
- [Bad 07: Monolithic Batch Runner](examples/bad_07_monolithic_batch_runner.js) — Написание мертвых скриптов вместо живого пошагового управления ИИ.
- [Bad 01: Monolithic Target Anti-Pattern](examples/bad_01_monolithic_target.js) — Захламление инспектора универсальными маркерами.
- [Bad 02: Screenshot Verification](examples/bad_02_screenshot_verification.js) — Использование скриншотов вместо структурированного инспекта.
- [Bad 03: Direct Callback Invocation](examples/bad_03_direct_callback_invocation.js) — Вызов коллбеков в обход рейкастов и InputSystem.
- [Bad 04: Cached Coordinates Drift](examples/bad_04_cached_coordinates_drift.js) — Использование устаревших закешированных координат.
- [Bad 05: Raw Magic String Commands](examples/bad_05_string_command_mutation.js) — Магические строки вместо строго типизированных команд.
- [Bad 06: Unbounded Blind Clicks](examples/bad_06_unbounded_blind_clicks.js) — Слепые клики по элементам вне экрана.

### Продакшен-паттерны (Как нужно делать)
- [Good 07: Agentic Step-by-Step Player Loop](examples/good_07_agentic_step_by_step_player.js) — Живой пошаговый цикл игры ИИ.
- [Good 06: Autonomous Goal-Driven Agent Session](examples/good_06_complex_workflow.js) — Автономная сессия достижения сложных целей без жестких пауз.
- [Good 01: Polymorphic Inspection](examples/good_01_polymorphic_inspection.js) — Инспекция специализированных маячков.
- [Good 02: Honest Device Input](examples/good_02_honest_device_input.js) — Честные события устройств ввода.
- [Good 03: Viewport-Aware Scrolling](examples/good_03_viewport_aware_scrolling.js) — Автоскролл обрезанных элементов перед кликом.
- [Good 04: Typed Text Injection](examples/good_04_typed_text_injection.js) — Фокус, печать текста и верификация дельты.
- [Good 05: Dynamic Dropdown Selection](examples/good_05_dynamic_dropdown_selection.js) — Открытие меню и выбор опций.
- [Good 08: Multi-Touch Stick Drawing (House)](examples/good_08_stick_drawing_house.js) — Одновременное удержание кнопки («Hold to Draw») и управление виртуальным стиком для рисования домика.
