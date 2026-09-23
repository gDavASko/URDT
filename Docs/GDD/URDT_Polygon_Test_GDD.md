# «Мастерская испытаний» — тестовый GDD псевдоигры полигона URDT

> **Назначение.** Это условный гейм-дизайн документ игры, которую изображает тестовый полигон URDT
> (`Assets/URDT_TestPoligon`, сцена `URDT_TestPoligon_UI`). Документ служит **эталоном для автономного
> рецензента** (CoreAgent): из прозы человек понимает замысел, а из блоков `urdt-*` агент получает
> правила, цели (DoD), контрактные инварианты и негативные эксперименты.
>
> **Источники.** Составлен по техническому заданию на механики
> (`2DWorldAIExecuteInstructions.md`, `Docs/2D_Mechanics_Reports/*.md`), по плану реальных UI-прогонов
> (`URDT_UI_Debug_Plan.md`) и **сверен с фактическим кодом** механик
> (`Assets/URDT_TestPoligon/2D_Polygon/Scripts/Mechanics`). Где ТЗ расходилось с кодом (координаты под
> 1280×720, «одна плитка» в M01 и т. п.), приоритет отдан коду: экран 1920×1080, позиции перемешиваются
> на каждом запуске — **координаты в GDD не задаются никогда**, агент берёт их из живых маячков.
>
> **Честность ввода.** Все действия — только реальный синтетический ввод URDT (мышь/тач через Input
> System и EventSystem). Вызывать методы игры, менять поля компонентов или читать пиксели запрещено.
> Маячки (`GameState`) — только чтение.

```urdt-meta
{ "title": "URDT Polygon — «Мастерская испытаний»", "version": "1.0" }
```

---

## 1. Концепция

Игрок — стажёр «Мастерской испытаний». Из главного меню он попадает в залы испытаний:

| Зал | Окно | Содержание |
|---|---|---|
| UI-зал | `window_ui_suite` | Пульт управления: модальное окно, поле ввода, переключатель, ползунок, выпадающий список, кнопка, прокручиваемый журнал |
| 2D-зал | `window_2d_suite` | Каталог из 32 мини-игр («механик») с общим игровым экраном |
| 3D-зал, зал интеграции | `window_3d_suite`, `window_integration_suite` | Заглушки (вне рамок этой версии) |

**Базовый цикл 2D-зала:** каталог → карточка механики → игровой экран (заголовок, инструкция,
бейдж статуса, игровая зона) → выполнение цели → поздравительный оверлей (~2 с) → возврат в каталог.
Каждая механика — законченная задача с одной целью, 1–2 ловушками (брак, фантом, опасная зона) и
понятной обратной связью (`ProgressNormalized`, счётчик в тексте).

**Сложность** растёт от манипуляции предметами (M01–M07) через точную моторику (M08–M12, M23, M29, M31)
и тайминг (M15–M17, M20, M21) к реактивным играм (M13, M24–M27, M32) и головоломкам (M18, M19, M22).

## 2. Экраны и навигация

- Главное меню: `btn_open_ui_suite`, `btn_open_2d_suite`, `btn_open_3d_suite`, `btn_open_integration_suite`,
  `btn_run_all_suites` (запускает всё подряд — **запрещено** при исследовании).
- 2D-каталог: 32 карточки `btn_launch_mNN` в прокручиваемом списке, «Назад» (`btn_2d_catalog_main_menu`,
  исторически `btn_2d_back`), «Сквозной прогон» (`btn_2d_run_sequential` — запрещено).
- Игровой экран 2D: «В меню» (`btn_2d_play_main_menu`), «Каталог» (`btn_2d_play_catalog`), «◀ ▶ ⟳»
  (`btn_2d_prev/next/reset` — запрещены при исследовании, меняют механику/прогресс).
- Правило навигации: рецензент **сначала строит карту переходов (Mode 1)**, затем ходит по ней. Если
  экран не найден на карте — `DISCOVERY_REQUIRED`, повторное исследование.

```urdt-navigation
{
  "home": "window_main_menu",
  "entries": {
    "ui": { "window": "window_ui_suite", "via": ["btn_open_ui_suite"] },
    "2d": { "window": "window_2d_suite", "via": ["btn_open_2d_suite"] }
  },
  "denylist": ["btn_run_all_suites", "btn_2d_run_sequential", "btn_2d_full_cycle", "btn_3d_full_cycle",
               "btn_integration_full_cycle", "ui.reset_button", "btn_launch_m*", "btn_2d_prev", "btn_2d_next",
               "btn_2d_reset", "StatusBadge", "World2DSuiteWindow_Title", "btn_2d_single", "btn_3d_single", "btn_integration_single"],
  "explorePattern": "^btn_",
  "backButtons": ["btn_2d_play_catalog", "btn_2d_catalog_main_menu", "btn_2d_back", "btn_ui_back",
                  "btn_3d_back", "btn_integration_back", "btn_2d_play_main_menu"]
}
```

## 3. Модель соответствия GDD (как читать блоки `urdt-spec`)

- **DoD (макро)** — что должно быть правдой, когда задача выполнена.
- **Инварианты (мезо)** — формальные предикаты над маячками (`@module` — корневой маячок механики).
  Проверяются после игры; нарушение `CRITICAL/MAJOR` = дефект.
- **Эксплойты** — негативные тройки Хоара: агент **намеренно** делает «неправильное» действие до
  прохождения и проверяет, что игра устояла (брак не принят, прогресс не вырос).
- **Гипотезы (микро)** формирует сам агент во время игры из живого состояния; они попадают в отчёт.

---

## 4. UI-зал

Все элементы UI-зала переставляются при каждом запуске, варианты выпадающего списка перемешиваются.
Агент перед каждым шагом заново читает маячок и прокручивает зал реальным свайпом по `ui.suite_scroll`.

### UI-01 Модальное окно
Открыть модальное окно кнопкой и закрыть его крестиком.

```urdt-spec
{
  "id": "UI-01_Modal", "title": "Модальное окно: открыть и закрыть", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "click", "target": "ui.modal_button", "expect": [ { "path": "LastResult", "op": "==", "value": "modal_open" } ] },
    { "do": "click", "target": "ui.modal_close_button", "expect": [ { "path": "LastResult", "op": "==", "value": "modal_closed" } ] }
  ] },
  "dod": ["Модальное окно открывается по кнопке", "Модальное окно закрывается крестиком и больше не перекрывает UI"],
  "invariants": [
    { "id": "INV-UI01-01", "text": "модальное окно закрыто", "beacon": "ui.modal_close_button", "path": "LastResult", "op": "==", "value": "modal_closed" }
  ],
  "timeoutMs": 60000
}
```

### UI-02 Ввод текста
Ввести `привет Мир`, отредактировать до `приМир` (курсор + Backspace), затем очистить поле.

```urdt-spec
{
  "id": "UI-02_TextInput", "title": "Поле ввода: ввод, правка, очистка", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "click", "target": "ui.text_input" },
    { "do": "type", "target": "ui.text_input", "value": "привет Мир", "expect": [ { "path": "InputValue", "op": "==", "value": "привет Мир" } ] },
    { "do": "key", "target": "ui.text_input", "value": "left", "repeat": 3, "pauseMs": 120 },
    { "do": "key", "target": "ui.text_input", "value": "backspace", "repeat": 4, "pauseMs": 120 },
    { "do": "wait", "value": 300, "expect": [] },
    { "do": "key", "target": "ui.text_input", "value": "right", "repeat": 3, "pauseMs": 120, "expect": [ { "path": "InputValue", "op": "==", "value": "приМир" } ] },
    { "do": "key", "target": "ui.text_input", "value": "backspace", "repeat": 6, "pauseMs": 120 },
    { "do": "wait", "value": 300 }
  ] },
  "dod": ["Кириллица вводится посимвольно", "Навигация курсором и Backspace редактируют текст", "Поле можно очистить"],
  "invariants": [
    { "id": "INV-UI02-01", "text": "поле очищено", "beacon": "ui.text_input", "path": "InputValue", "op": "==", "value": "" }
  ],
  "timeoutMs": 90000
}
```

### UI-03 Переключатель
```urdt-spec
{
  "id": "UI-03_Toggle", "title": "Переключатель: включить и выключить", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "click", "target": "ui.state_toggle", "expect": [ { "path": "ToggleValue", "op": "==", "value": true } ] },
    { "do": "wait", "value": 1000 },
    { "do": "click", "target": "ui.state_toggle", "expect": [ { "path": "ToggleValue", "op": "==", "value": false } ] }
  ] },
  "dod": ["Переключатель меняет состояние по клику в обе стороны"],
  "invariants": [ { "id": "INV-UI03-01", "text": "переключатель выключен", "beacon": "ui.state_toggle", "path": "ToggleValue", "op": "==", "value": false } ],
  "timeoutMs": 60000
}
```

### UI-04 Ползунок
```urdt-spec
{
  "id": "UI-04_Slider", "title": "Ползунок: 0.5 → 0.3 → 1.0 → 0.0", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "set_slider", "target": "ui.value_slider", "value": 0.5, "expect": [ { "path": "SliderValue", "op": ">=", "value": 0.46 }, { "path": "SliderValue", "op": "<=", "value": 0.54 } ] },
    { "do": "set_slider", "target": "ui.value_slider", "value": 0.3, "expect": [ { "path": "SliderValue", "op": ">=", "value": 0.26 }, { "path": "SliderValue", "op": "<=", "value": 0.34 } ] },
    { "do": "set_slider", "target": "ui.value_slider", "value": 1.0, "expect": [ { "path": "SliderValue", "op": ">=", "value": 0.97 } ] },
    { "do": "set_slider", "target": "ui.value_slider", "value": 0.0, "expect": [ { "path": "SliderValue", "op": "<=", "value": 0.03 } ] }
  ] },
  "dod": ["Ползунок принимает перетаскивание и точно устанавливает значение"],
  "invariants": [ { "id": "INV-UI04-01", "text": "ползунок в нуле", "beacon": "ui.value_slider", "path": "SliderValue", "op": "<=", "value": 0.03 } ],
  "timeoutMs": 60000
}
```

### UI-05 Выпадающий список
Варианты перемешаны — выбор только по подписи, обнаруженной живым `hit_test`.

```urdt-spec
{
  "id": "UI-05_Dropdown", "title": "Выпадающий список: C → A → B", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "select_option", "target": "ui.mode_dropdown", "value": "Mode C", "expect": [ { "path": "DropdownLabel", "op": "==", "value": "Mode C" } ] },
    { "do": "select_option", "target": "ui.mode_dropdown", "value": "Mode A", "expect": [ { "path": "DropdownLabel", "op": "==", "value": "Mode A" } ] },
    { "do": "select_option", "target": "ui.mode_dropdown", "value": "Mode B", "expect": [ { "path": "DropdownLabel", "op": "==", "value": "Mode B" } ] }
  ] },
  "dod": ["Список раскрывается, выбранная подпись отображается"],
  "invariants": [ { "id": "INV-UI05-01", "text": "выбран Mode B", "beacon": "ui.mode_dropdown", "path": "DropdownLabel", "op": "==", "value": "Mode B" } ],
  "timeoutMs": 90000
}
```

### UI-06 Основная кнопка
```urdt-spec
{
  "id": "UI-06_PrimaryButton", "title": "Кнопка: клик, пауза, двойной клик", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "click", "target": "ui.primary_button", "expect": [ { "path": "InteractionCount", "op": "changed" } ] },
    { "do": "wait", "value": 1000 },
    { "do": "double_click", "target": "ui.primary_button", "expect": [ { "path": "InteractionCount", "op": "changed" } ] }
  ] },
  "dod": ["Каждый клик засчитывается ровно один раз"],
  "invariants": [ { "id": "INV-UI06-01", "text": "кнопка получила клики", "beacon": "ui.primary_button", "path": "InteractionCount", "op": ">=", "value": 3 } ],
  "timeoutMs": 60000
}
```

### UI-07 Прокручиваемый журнал
```urdt-spec
{
  "id": "UI-07_Scroll", "title": "Журнал: прокрутка вниз и обратно", "suite": "ui", "module": "window_ui_suite",
  "playbook": "ui_scenario",
  "params": { "viewport": "ui.suite_scroll", "steps": [
    { "do": "scroll", "target": "ui.command_scroll", "value": -1, "expect": [ { "path": "ScrollPosition", "op": "changed" } ] },
    { "do": "wait", "value": 1000 },
    { "do": "scroll", "target": "ui.command_scroll", "value": 1, "expect": [ { "path": "ScrollPosition", "op": "changed" } ] }
  ] },
  "dod": ["Колесо мыши прокручивает вложенный журнал, а не внешний зал"],
  "invariants": [ { "id": "INV-UI07-01", "text": "UI-зал открыт", "beacon": "window_ui_suite", "path": "IsVisible", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

---

## 5. 2D-зал: механики

Общие инварианты каждой механики: `IsCompleted == true` и `ProgressNormalized >= 1` по окончании.

### M01 Детали по пазам (Snap-to-Slot)
Три фигуры (круг, квадрат, треугольник) нужно вставить в пазы той же формы; бракованная деталь «X»
не подходит никуда. Управление: перетаскивание. Правило соответствия: `ItemId` детали = `SlotId` паза.

```urdt-spec
{
  "id": "M01_SnapToSlot", "title": "Детали по пазам", "suite": "2d", "launch": "btn_launch_m01", "module": "M01_SnapToSlot",
  "playbook": "match_and_place", "params": { "match": [["ItemId", "SlotId"]] },
  "dod": ["Все три фигуры в своих пазах", "Брак не принят ни одним пазом"],
  "invariants": [
    { "id": "INV-M01-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M01-02", "text": "прогресс 100%", "beacon": "@module", "path": "ProgressNormalized", "op": ">=", "value": 1 },
    { "id": "INV-M01-03", "text": "брак не установлен", "beacon": "Item_Junk_Defective", "path": "IsSnapped", "op": "==", "value": false }
  ],
  "exploits": [ { "id": "EXP-M01-01", "text": "брак в паз — отклонён, прогресс не растёт", "action": "drag_junk_to_slot",
    "expect": [ { "id": "EXP-M01-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" },
                { "id": "EXP-M01-01b", "text": "брак не защёлкнут", "beacon": "Item_Junk_Defective", "path": "IsSnapped", "op": "==", "value": false } ] } ],
  "timeoutMs": 60000
}
```

### M02 Сортировка по корзинам
Красные яблоки — в красную корзину, синие ягоды — в синюю (по 2), камень-брак не принимается.
Правило: `ItemTypeId` = `AcceptedTypeId` корзины.

```urdt-spec
{
  "id": "M02_ContainerSorting", "title": "Сортировка по корзинам", "suite": "2d", "launch": "btn_launch_m02", "module": "M02_ContainerSorting",
  "playbook": "match_and_place", "params": { "match": [["ItemTypeId", "AcceptedTypeId"]], "placedKeys": ["IsDeposited"] },
  "dod": ["В каждой корзине по 2 предмета своего цвета", "Камень не принят"],
  "invariants": [
    { "id": "INV-M02-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M02-02", "text": "камень не принят", "beacon": "Item_Stone_Junk", "path": "game.IsDeposited", "op": "==", "value": false }
  ],
  "exploits": [ { "id": "EXP-M02-01", "text": "камень в корзину — отклонён", "action": "drag_junk_to_slot", "target": "Bucket_Red",
    "expect": [ { "id": "EXP-M02-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" } ] } ],
  "timeoutMs": 60000
}
```

### M03 Весы
Левая чаша — эталон 15 кг. На правую нужно положить гири ровно на 15 кг и дать весам 1 с
постоять неподвижно. Гиря-брак весит 0 и лишь занимает место.

```urdt-spec
{
  "id": "M03_WeightComparator", "title": "Весы", "suite": "2d", "launch": "btn_launch_m03", "module": "M03_WeightComparator",
  "playbook": "balance_scale", "params": {},
  "dod": ["Масса правой чаши равна эталону (±0.5 кг)", "Равновесие удержано 1 с"],
  "invariants": [
    { "id": "INV-M03-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M03-02", "text": "правая чаша = 15 кг", "beacon": "RightPan", "path": "game.CurrentMass", "op": "==", "value": 15 }
  ],
  "timeoutMs": 60000
}
```

### M04 Многослойная сборка (2 этапа)
Собрать изделие слоями строго по порядку `LayerIndex` (ядро → броня; затем голова → батарея).
Второй этап появляется после первого. Бракованный блок не монтируется.

```urdt-spec
{
  "id": "M04_MultiLayerAttachment", "title": "Многослойная сборка", "suite": "2d", "launch": "btn_launch_m04", "module": "M04_MultiLayerAttachment",
  "playbook": "match_and_place", "params": { "match": [["Stage", "Stage"], ["LayerIndex", "LayerIndex"]], "order": "LayerIndex", "placedKeys": ["IsLocked", "IsInstalled"] },
  "dod": ["Все 4 слоя смонтированы в правильном порядке"],
  "invariants": [ { "id": "INV-M04-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "exploits": [ { "id": "EXP-M04-01", "text": "бракованный блок в гнездо — отклонён", "action": "drag_junk_to_slot", "target": "Socket_Core",
    "expect": [ { "id": "EXP-M04-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" } ] } ],
  "timeoutMs": 90000
}
```

### M05 Программа робота
Составить из фишек программу «Вперёд → Поворот → Вперёд» (дизайн уровня, маячками не выводится)
и нажать «Выполнить»; робот должен дойти до флага. Фишка «Сбой [X]» ломает выполнение.

```urdt-spec
{
  "id": "M05_TimelineSequencer", "title": "Программа робота", "suite": "2d", "launch": "btn_launch_m05", "module": "M05_TimelineSequencer",
  "playbook": "match_and_place",
  "params": { "sequence": ["Forward", "Turn", "Forward"], "sequenceKey": "CommandType", "finalTap": "Button_Execute", "placedKeys": [] },
  "dod": ["Программа из 3 шагов в верном порядке", "Робот дошёл до флага"],
  "invariants": [ { "id": "INV-M05-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M06 Соединение контактов
Соединить проводом одноцветные контакты (A-A, B-B, C-C). Контакты «Junk» дают замыкание.

```urdt-spec
{
  "id": "M06_NodePairing", "title": "Соединение контактов", "suite": "2d", "launch": "btn_launch_m06", "module": "M06_NodePairing",
  "playbook": "match_and_place",
  "params": { "itemFilter": { "IsSource": true }, "targetKind": "draggable", "targetFilter": { "IsSource": false }, "match": [["PairId", "PairId"]], "placedKeys": ["IsConnected"] },
  "dod": ["Три пары соединены", "Бракованные контакты не соединены"],
  "invariants": [
    { "id": "INV-M06-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M06-02", "text": "брак не соединён", "beacon": "Pin_Src_Junk", "path": "game.IsConnected", "op": "==", "value": false }
  ],
  "exploits": [ { "id": "EXP-M06-01", "text": "провод от бракованного контакта — короткое замыкание, связь не создаётся", "action": "drag_junk_to_slot", "junk": "Pin_Src_Junk", "target": "Pin_Tgt_A",
    "expect": [ { "id": "EXP-M06-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" } ] } ],
  "timeoutMs": 60000
}
```

### M07 Удаление зуба
Наложить щипцы на зуб, «раскачать» его влево-вправо до полной усталости связки (`Fatigue = 1`),
затем резко потянуть вверх — в одном непрерывном движении.

```urdt-spec
{
  "id": "M07_WobbleAndSnap", "title": "Удаление зуба", "suite": "2d", "launch": "btn_launch_m07", "module": "M07_WobbleAndSnap",
  "playbook": "wobble_extract", "params": { "tool": "DentalForceps", "item": "ToothItem", "amplitudePx": 90 },
  "dod": ["Щипцы наложены", "Зуб извлечён"],
  "invariants": [ { "id": "INV-M07-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M08 Пила по разметке
Провести пилу через 5 точек разметки строго по порядку, не касаясь гвоздя (радиус опасности 75 px
экрана — касание сбрасывает прогресс).

```urdt-spec
{
  "id": "M08_WaypointTracking", "title": "Пила по разметке", "suite": "2d", "launch": "btn_launch_m08", "module": "M08_WaypointTracking",
  "playbook": "trace_path", "params": { "tool": "ToolSaw", "waypointPrefix": "Waypoint_", "hazardRole": "hazardObstacle", "hazardRadius": 75, "speedPxPerS": 320 },
  "dod": ["Все 5 точек пройдены по порядку", "Гвоздь не задет"],
  "invariants": [ { "id": "INV-M08-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M09 Отмыть пластину
Губкой стереть ≥90% грязи; одно пятно — несмываемый дефект, его можно не трогать.

```urdt-spec
{
  "id": "M09_CoverageAccumulator", "title": "Отмыть пластину", "suite": "2d", "launch": "btn_launch_m09", "module": "M09_CoverageAccumulator",
  "playbook": "scrub_coverage", "params": { "tool": "ToolSponge", "cellPrefix": "DirtCell_" },
  "dod": ["Очищено ≥90% смываемых клеток"],
  "invariants": [ { "id": "INV-M09-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M10 Накачка до зелёной зоны
Удерживать «НАКАЧКА» и отпустить, когда давление в зелёной зоне 70–85%. Выше 92% — сброс.

```urdt-spec
{
  "id": "M10_TimedAccumulator", "title": "Накачка до зелёной зоны", "suite": "2d", "launch": "btn_launch_m10", "module": "M10_TimedAccumulator",
  "playbook": "hold_in_band", "params": { "button": "ButtonPump", "bandMin": 0.70, "bandMax": 0.85 },
  "dod": ["Кнопка отпущена в зелёной зоне"],
  "invariants": [ { "id": "INV-M10-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M11 Пожарный брандспойт
Зажать указатель и направить струю (конус ±18°, дальность ~720 px) на три очага до их тушения.
Электрощит — отвлекающий объект.

```urdt-spec
{
  "id": "M11_ConeEmitter", "title": "Пожарный брандспойт", "suite": "2d", "launch": "btn_launch_m11", "module": "M11_ConeEmitter",
  "playbook": "spray_targets", "params": { "targetRole": "targets" },
  "dod": ["Все три очага потушены"],
  "invariants": [ { "id": "INV-M11-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M12 Рабочий вентиль
Повернуть рабочий вентиль суммарно на 720°. Заклинивший вентиль-обманка не вращается.

```urdt-spec
{
  "id": "M12_AngularDeltaTracker", "title": "Рабочий вентиль", "suite": "2d", "launch": "btn_launch_m12", "module": "M12_AngularDeltaTracker",
  "playbook": "rotate_wheel", "params": { "turnsPerGesture": 1.25 },
  "dod": ["Накоплено 720°", "Заклинивший вентиль не трогали"],
  "invariants": [
    { "id": "INV-M12-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M12-02", "text": "обманка не вращалась", "beacon": "Valve_Jammed_Junk", "path": "game.AccumulatedAngle", "op": "==", "value": 0, "severity": "MINOR" }
  ],
  "timeoutMs": 60000
}
```

### M13 Бегун по полосам — свайпы
Три полосы, сверху падают монеты и барьеры. Свайп влево/вправо меняет полосу. Собрать 5 монет;
барьер отнимает монету.

```urdt-spec
{
  "id": "M13_LaneSwitcherRunner", "title": "Бегун: свайпы", "suite": "2d", "launch": "btn_launch_m13", "module": "M13_LaneSwitcherRunner",
  "playbook": "lane_runner", "params": { "control": "swipe", "avatar": "PlayerAvatar", "coinRole": "spawned:Coin", "hazardRole": "spawned:Barrier", "dangerPx": 170 },
  "dod": ["Собрано 5 монет"],
  "invariants": [
    { "id": "INV-M13-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M13-02", "text": "режим управления — свайпы", "beacon": "@module", "path": "game.ControlMode", "op": "==", "value": "Swipe", "severity": "MINOR" }
  ],
  "timeoutMs": 120000
}
```

### M14 Рогатка
Оттянуть снаряд назад и отпустить (импульс ∝ натяжению, гравитация). Сбить 3 банки за колонной.
Физику агент не знает заранее — выводит из наблюдения за первым выстрелом.

```urdt-spec
{
  "id": "M14_SlingshotImpulser", "title": "Рогатка", "suite": "2d", "launch": "btn_launch_m14", "module": "M14_SlingshotImpulser",
  "playbook": "slingshot", "params": { "ball": "ProjectileBall", "anchor": "SlingshotAnchor", "targetPrefix": "TargetCan_", "obstacleRole": "obstaclePillar", "maxPullPx": 132 },
  "dod": ["Сбиты все 3 банки"],
  "invariants": [ { "id": "INV-M14-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

### M15 Перехват мяча
Нажать «ОТРАЗИТЬ», когда мяч проходит зелёную линию (±45 px). Красные полупрозрачные мячи —
фантомы: попытка отбить сбрасывает серию. Нужна серия из 3 перехватов.

```urdt-spec
{
  "id": "M15_TimingInterceptor", "title": "Перехват мяча", "suite": "2d", "launch": "btn_launch_m15", "module": "M15_TimingInterceptor",
  "playbook": "timing_intercept", "params": { "ball": "MovingBall", "line": "InterceptorLine", "button": "BtnIntercept", "tolerancePx": 45 },
  "dod": ["Серия из 3 точных перехватов", "Фантомы не отбивались"],
  "invariants": [ { "id": "INV-M15-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

### M16 Ритм рычагов
Чередовать левый и правый рычаги с интервалом 0.25–1.15 с, 8 тактов подряд. Средний рычаг сломан.

```urdt-spec
{
  "id": "M16_RhythmPhaseDetector", "title": "Ритм рычагов", "suite": "2d", "launch": "btn_launch_m16", "module": "M16_RhythmPhaseDetector",
  "playbook": "rhythm_alternate", "params": { "left": "BtnLeftLever", "right": "BtnRightLever", "periodMs": 550, "beats": 8 },
  "dod": ["8 тактов ровного чередования"],
  "invariants": [ { "id": "INV-M16-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M17 Перетягивание каната
Часто нажимать «ТЯНУТЬ», перебарывая затухание силы, до заявленной цели **92%** (так обещает
интерфейс игроку). Кнопка «Скользко» — ловушка.

*Проверка порога.* Шкала на экране — `(баланс + 1) · 50%`, один рывок даёт ≈ +4.25%. При обычной
игре победа может «перепрыгнуть» границу, и нарушение порога осталось бы незамеченным. Рецензент
зондирует границу: подводит шкалу к окну 86–91% и делает последний рывок оттуда — победа обязана
наступить не ниже заявленных 92%. (Сверено с кодом: порог `0.85` = 92.5% экранной шкалы.)

```urdt-spec
{
  "id": "M17_TugOfWarBalance", "title": "Перетягивание каната", "suite": "2d", "launch": "btn_launch_m17", "module": "M17_TugOfWarBalance",
  "playbook": "mash_button", "params": { "button": "BtnPull", "tapsPerSecond": 9, "readout": "BalanceText", "approachWindow": [86, 91] },
  "dod": ["Баланс сил достиг заявленной цели 92%"],
  "invariants": [
    { "id": "INV-M17-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M17-02", "text": "победа засчитана не раньше заявленных игроку 92%", "beacon": "BalanceText", "path": "Text", "op": "num>=", "value": 92, "severity": "MAJOR" }
  ],
  "timeoutMs": 60000
}
```

### M18 Водопровод
Поворачивая трубы кликом (по 90°), замкнуть поток от истока до стока. Ориентации скрыты —
видно только, где есть вода.

```urdt-spec
{
  "id": "M18_GraphFlowClosure", "title": "Водопровод", "suite": "2d", "launch": "btn_launch_m18", "module": "M18_GraphFlowClosure",
  "playbook": "pipe_puzzle", "params": { "tilePrefix": "PipeTile_" },
  "dod": ["Вода дошла до стока"],
  "invariants": [ { "id": "INV-M18-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

### M19 Раскраска по эталону
Выбрать цвет на палитре и закрасить каждый сегмент как на эталоне. «Грязь» портит сегмент.

```urdt-spec
{
  "id": "M19_FloodFillColoring", "title": "Раскраска по эталону", "suite": "2d", "launch": "btn_launch_m19", "module": "M19_FloodFillColoring",
  "playbook": "paint_by_key", "params": { "segmentPrefix": "Segment_", "palette": { "1": "BtnRed", "2": "BtnGreen", "3": "BtnBlue", "4": "BtnYellow" } },
  "dod": ["Все сегменты совпадают с эталоном"],
  "invariants": [ { "id": "INV-M19-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M20 Башня из блоков
Кран раскачивает блок; сбросить его так, чтобы три блока встали башней (допуск ~52 px).

```urdt-spec
{
  "id": "M20_VerticalStacking", "title": "Башня из блоков", "suite": "2d", "launch": "btn_launch_m20", "module": "M20_VerticalStacking",
  "playbook": "stack_drop", "params": { "block": "ActiveBlock", "button": "BtnDrop", "base": "BasePlatform", "tolerancePx": 52, "blocks": 3 },
  "dod": ["Башня из 3 блоков устояла"],
  "invariants": [ { "id": "INV-M20-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 90000
}
```

### M21 Рыбалка
Ждать поклёвки (поплавок резко уходит вниз) и подсекать за 0.75 с. Ранняя подсечка — промах.
Поймать 2 рыбы.

```urdt-spec
{
  "id": "M21_ReactionProbe", "title": "Рыбалка", "suite": "2d", "launch": "btn_launch_m21", "module": "M21_ReactionProbe",
  "playbook": "reaction_strike", "params": { "bobber": "Bobber", "button": "BtnStrike", "dipPx": 22, "catches": 2 },
  "dod": ["Поймано 2 рыбы"],
  "invariants": [ { "id": "INV-M21-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 90000
}
```

### M22 Робот в лабиринте
Кликая по соседним клеткам, довести робота от старта до цели; стены непроходимы, ловушка
замораживает на 1 с.

```urdt-spec
{
  "id": "M22_GridPathfinding", "title": "Робот в лабиринте", "suite": "2d", "launch": "btn_launch_m22", "module": "M22_GridPathfinding",
  "playbook": "grid_path", "params": { "tilePrefix": "Tile_", "avatar": "RobotAvatar", "blocked": ["ObstacleWall"], "avoid": ["GlitchTrapHazard"], "goalType": "Goal" },
  "dod": ["Робот на клетке цели"],
  "invariants": [ { "id": "INV-M22-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M23 Лупа
Водить лупой по тёмной области; задержать её 2 с над каждым из трёх самоцветов. Пыль — пустышка.

```urdt-spec
{
  "id": "M23_StencilReveal", "title": "Лупа", "suite": "2d", "launch": "btn_launch_m23", "module": "M23_StencilReveal",
  "playbook": "lens_dwell", "params": { "lens": "StencilLens", "dwellMs": 2400 },
  "dod": ["Найдены 3 самоцвета"],
  "invariants": [
    { "id": "INV-M23-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M23-02", "text": "пыль не засчитана", "beacon": "JunkDust", "path": "game.IsCollected", "op": "==", "value": false, "severity": "MINOR" }
  ],
  "timeoutMs": 90000
}
```

### M24 Пузыри
Лопать всплывающие пузыри (6 штук), не трогая бомбы.

```urdt-spec
{
  "id": "M24_TargetElimination", "title": "Пузыри", "suite": "2d", "launch": "btn_launch_m24", "module": "M24_TargetElimination",
  "playbook": "pop_targets", "params": { "role": "PoppableTarget" },
  "dod": ["Лопнуто 6 пузырей"],
  "invariants": [ { "id": "INV-M24-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 90000
}
```

### M25 Бегун — тапы по половинам
Как M13, но смена полосы — тап по левой/правой половине экрана.

```urdt-spec
{
  "id": "M25_LaneSwitcherTapHalves", "title": "Бегун: тапы по половинам", "suite": "2d", "launch": "btn_launch_m25", "module": "M25_LaneSwitcherTapHalves",
  "playbook": "lane_runner", "params": { "control": "tap_halves", "avatar": "PlayerAvatar", "coinRole": "spawned:Coin", "hazardRole": "spawned:Barrier", "dangerPx": 170 },
  "dod": ["Собрано 5 монет"],
  "invariants": [ { "id": "INV-M25-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

### M26 Бегун — ведение пальцем
Как M13, но аватар ведётся пальцем по горизонтали и при отпускании встаёт на ближайшую полосу.

```urdt-spec
{
  "id": "M26_LaneSwitcherDirectDrag", "title": "Бегун: ведение пальцем", "suite": "2d", "launch": "btn_launch_m26", "module": "M26_LaneSwitcherDirectDrag",
  "playbook": "lane_runner", "params": { "control": "direct_drag", "avatar": "PlayerAvatar", "coinRole": "spawned:Coin", "hazardRole": "spawned:Barrier", "dangerPx": 170 },
  "dod": ["Собрано 5 монет"],
  "invariants": [ { "id": "INV-M26-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

### M27 Машинка по холмам
Газ и тормоз — экранные педали (клавиатура в этой сборке не используется). На земле — газ; в воздухе
газ крутит машину назад, тормоз — вперёд. Доехать до флага на 2200 м, не перевернувшись.

```urdt-spec
{
  "id": "M27_PhysicsCarHills", "title": "Машинка по холмам", "suite": "2d", "launch": "btn_launch_m27", "module": "M27_PhysicsCarHills",
  "playbook": "car_drive", "params": { "gas": "BtnGas", "brake": "BtnBrake", "body": "CarRoot", "distanceText": "DistanceText", "pitchLimitDeg": 25 },
  "dod": ["Флаг финиша достигнут"],
  "invariants": [ { "id": "INV-M27-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 180000
}
```

### M28 Два моста
Уложить две целые доски в проёмы A и B (треснувшая доска не держит), затем нажать «Старт» —
оба путника дойдут до целей.

```urdt-spec
{
  "id": "M28_DualBridgeRoute", "title": "Два моста", "suite": "2d", "launch": "btn_launch_m28", "module": "M28_DualBridgeRoute",
  "playbook": "match_and_place", "params": { "targetIds": ["SlotA", "SlotB"], "finalTap": "BtnStart", "placedKeys": [] },
  "dod": ["Оба проёма перекрыты целыми досками", "Путники дошли"],
  "invariants": [ { "id": "INV-M28-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "exploits": [ { "id": "EXP-M28-01", "text": "треснувшая доска в проём — отклонена", "action": "drag_junk_to_slot", "junk": "Plank_Broken_Junk", "target": "SlotA",
    "expect": [ { "id": "EXP-M28-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" } ] } ],
  "timeoutMs": 60000
}
```

### M29 Вырезать звезду
Провести ножницы по контуру звезды (16 вершин) от «СТАРТ» по кругу, не уходя от линии дальше ~97 px.

```urdt-spec
{
  "id": "M29_ContourCutting", "title": "Вырезать звезду", "suite": "2d", "launch": "btn_launch_m29", "module": "M29_ContourCutting",
  "playbook": "trace_path", "params": { "tool": "ScissorTool", "waypointPrefix": "Point_", "closeLoop": true, "speedPxPerS": 300 },
  "dod": ["Контур пройден полностью"],
  "invariants": [ { "id": "INV-M29-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 60000
}
```

### M30 Одень персонажа
Надеть на манекен голову, тело, обувь и аксессуар в свои слоты (`SlotType` = суффикс слота);
испачканная вещь не надевается.

```urdt-spec
{
  "id": "M30_CharacterDressUp", "title": "Одень персонажа", "suite": "2d", "launch": "btn_launch_m30", "module": "M30_CharacterDressUp",
  "playbook": "match_and_place", "params": { "targetIds": ["Slot_Head", "Slot_Body", "Slot_Feet", "Slot_Accessory"], "match": [["SlotType", "id:suffix"]], "placedKeys": ["IsEquipped"] },
  "dod": ["4 вещи на своих местах", "Испачканная вещь отклонена"],
  "invariants": [
    { "id": "INV-M30-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true },
    { "id": "INV-M30-02", "text": "грязная вещь не надета", "beacon": "Item_Junk", "path": "game.IsEquipped", "op": "==", "value": false }
  ],
  "exploits": [ { "id": "EXP-M30-01", "text": "грязная вещь на манекен — отклонена", "action": "drag_junk_to_slot", "junk": "Item_Junk", "target": "Slot_Body",
    "expect": [ { "id": "EXP-M30-01a", "text": "прогресс не изменился", "beacon": "@module", "path": "ProgressNormalized", "op": "unchanged" } ] } ],
  "timeoutMs": 60000
}
```

### M31 Розлив по лункам
Перемещать дозатор над лунками строго по порядку 1 → 2 → 3 и держать над каждой, пока не заполнится.

```urdt-spec
{
  "id": "M31_LiquidFilling", "title": "Розлив по лункам", "suite": "2d", "launch": "btn_launch_m31", "module": "M31_LiquidFilling",
  "playbook": "fill_wells", "params": { "tool": "DispenserTool", "wellPrefix": "Well_", "percentPrefix": "PercentText", "holdMs": 2400 },
  "dod": ["Три лунки заполнены по порядку"],
  "invariants": [ { "id": "INV-M31-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 90000
}
```

### M32 Самолётик
Самолётик летит вперёд; касанием задаётся высота. Собрать 5 звёзд; тучи — только визуальная помеха.

```urdt-spec
{
  "id": "M32_AirplaneStarGlider", "title": "Самолётик", "suite": "2d", "launch": "btn_launch_m32", "module": "M32_AirplaneStarGlider",
  "playbook": "glider", "params": { "plane": "AirplaneRoot", "starRole": "activeStars", "container": "FlightContainer" },
  "dod": ["Собрано 5 звёзд"],
  "invariants": [ { "id": "INV-M32-01", "text": "механика завершена", "beacon": "@module", "path": "IsCompleted", "op": "==", "value": true } ],
  "timeoutMs": 120000
}
```

---

## 6. Известные расхождения ТЗ ↔ реализация (заложены в инварианты)

| Механика | Замысел (GDD) | Реализация | Как проверяется |
|---|---|---|---|
| M17 | Победа при заявленных игроку 92% | `_winThreshold = 0.85` по внутренней шкале −1..1 = 92.5% экранной | Подозрение **снято** зондированием границы (`INV-M17-02`): при 85–91% победы нет |
| M13/M25/M26 | Барьер отнимает монету, прогресс отражает счёт | Счёт уменьшается, `ProgressNormalized` не пересчитывается | Наблюдается в отчёте по тексту `ScoreText` |
| M32 | «Избегайте грозовых туч [X]» | Тучи не влияют на игру | Отмечено в GDD как визуальная помеха |
| M27/M28/M13/M15/M21/M23/M32 | Управление клавиатурой (подсказки в UI) | Чтение через legacy `UnityEngine.Input` — недоступно виртуальным устройствам Input System | Агент использует только экранные элементы |
