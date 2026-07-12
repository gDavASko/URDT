# Отчет отладки URDT: реальные UI-прогоны

Дата старта: 2026-07-12

## Правила достоверности

- Тестовые прогоны из `URDT_UI_Debug_Plan.md` не менялись.
- UI-результат принимается только по реальному URDT input и наблюдаемой debug/log дельте.
- Прямые вызовы UI-обработчиков, ручная установка значений компонентов и подмена состояния запрещены.

## Инфраструктура

| Пункт | Факт |
| --- | --- |
| Unity Skills | `http://127.0.0.1:8090/health` доступен; Unity `6000.0.67f1`; запущенный runtime имеет ожидаемое имя `kbp-core`. |
| URDT TestPoligon | Сцена: `Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity`. |
| URDT transport | Предпочтительный `ws://127.0.0.1:7777/`; действующий порт должен быть подтвержден handshake/discovery. |
| Harness | Встроенные реальные субагенты запущены: анализатор, судья, воркер. Диспетчер-скилл Harness в доступном каталоге не найден, поэтому его state-machine не имитируется. |

## Результаты

| Тест | Попытки | Итог | Что получилось / не получилось | Токены: операция / анализ / исправление |
| --- | ---: | --- | --- | --- |
| 1. Модальное окно | 2/5 | Успех | Попытка 1: `E_NOT_HITTABLE` для видимой UI-кнопки, затем explicit virtual-device input не дал дельту. Исправлены фоновый PlayerLoop и post-disable inspect в URDT. Попытка 2: `modal_open`, затем `modal_closed`, close target стал inactive и сохранил `InteractionCount=1`. | ~4 800 / ~6 300 / ~2 500 |
| 2. Ввод текста | 1/5 | Успех | Реальный click поставил фокус. Нативный `SendInput` ввел `привет Мир`, реальными Left/Backspace получено `приМир`, затем поле очищено. UTF-8 проверялся base64-значением поля. | ~8 200 / ~9 700 / ~6 100 |
| 3. Toggle | 2/5 | Успех | Попытка 1 выявила ложный hit по `UiSuiteScroll`. Попытка 2: дочерний checkbox найден по реальной raycastable точке, `toggle_on` / `ToggleValue=true`, через секунду `toggle_off` / `ToggleValue=false`. | ~2 800 / ~3 400 / ~2 300 |
| 4. Slider | 1/5 | Успех | Реальные drag: `0.50 -> 0.30 -> 1.00 -> 0.00`. Первое приближение выявило, что координаты нужно считать по полному `ScreenRect`; после корректировки все дельты точны. | ~2 900 / ~2 600 / ~800 |
| 5. Combo box | 2/5 | Успех | Попытка 1: dropdown не раскрывался. Исправлен отдельный hover-кадр перед pointer down. Попытка 2: `IsExpanded=true`; реальные клики по Item C, A, B дали `dropdown_value=2 -> 0 -> 1`. | ~3 100 / ~3 600 / ~1 400 |
| 6. Primary button | 1/5 | Успех | Один реальный click: `InteractionCount=1`. После секунды реальный double click: `InteractionCount=3`; оба шага дали `primary_button_clicked`. | ~1 100 / ~800 / ~0 |
| 7. Scroll | 1/5 | Успех | Реальная прокрутка внутреннего `CommandScroll`: `1.00 -> 0.634 -> 0.00 -> 1.00`; между фазами выдержаны секундные паузы. | ~1 800 / ~1 700 / ~500 |
| 8. Сквозной | 1/5 | Успех | На новом Play Mode один непрерывный WebSocket-сценарий выполнил все 7 блоков: modal open/close; текст с очисткой `input_length=0`; toggle `on/off`; slider до `0.00`; dropdown `C -> A -> B`; primary `InteractionCount=3`; scroll вернулся в `1.00`. Использовались только URDT virtual/native device input. | ~9 600 / ~4 200 / ~0 |

Дополнительный видеопрогон Test 8: 2026-07-13. Выполнен на новом Play Mode одним непрерывным сценарием; между разными UI-блоками выдерживались паузы 2 секунды. Итоговые состояния совпали с основным сквозным прогоном.

Повторный видеопрогон Test 8: 2026-07-13. Выполнен на новом Play Mode; между каждым действием внутри блока выдерживалась пауза 1 секунда, между UI-блоками - 2 секунды. Итог: текст очищен, toggle выключен, slider 0.00, dropdown B, primary InteractionCount=3, scroll 1.00.

Финальный видеоблок: после 3 секунд ожидания выполнена прокрутка внешнего списка вниз и реальный click `ui.reset_button`; после Reset и 2 секунд ожидания внешний список прокручен вверх. Reset штатно активировал `window_main_menu`, поэтому последующий адресный click `btn_ui_back` честно вернул `E_NOT_HITTABLE`: top hit уже `OpenUiSuiteButton` главного меню. Состояние Main Menu подтверждено inspect `btn_open_ui_suite`.

Исправление Reset и финальный видеопрогон: Reset из UI Suite теперь сохраняет `window_ui_suite` и сбрасывает только значения controls. После Reset выполнена прокрутка вверх и реальный click Main Menu; финальный inspect подтвердил `window_main_menu`.

## Исправления URDT

1. `Runtime/UrdtServerHost.cs`: в Editor URDT временно включает `Application.runInBackground`, возвращая исходное значение при остановке. Это устраняет остановку обычного PlayerLoop при внешнем управлении редактором: heartbeat снова обновляется, вновь активированные uGUI элементы получают lifecycle/layout и реагируют только на виртуальный Input System ввод.
2. `Runtime/Registry/TestIdRegistry.cs`: disabled target сохраняется как inactive tombstone для прямого `inspect`, но не попадает в `query` и не может пройти input preflight. После modal close AI получает проверяемую delta `LastResult=modal_closed` вместо ложного `E_NOT_FOUND`.
3. `Runtime/Input/NativeTextInput.cs`, `Runtime/Handlers/TypeTextHandler.cs`, `Runtime/Handlers/KeyPressHandler.cs`: Unicode-текст и навигационные клавиши TMP идут через реальный Windows `SendInput`; компонентное состояние не устанавливается напрямую.
4. `Runtime/Input/InputSimulator.cs`: wheel delta сбрасывается следующим виртуальным кадром, поэтому повторные одинаковые scroll-команды остаются отдельными событиями.
5. `Runtime/Handlers/PayloadReader.cs`: родитель target больше не принимается как допустимый top hit; это предотвращает ложные успешные клики по перекрытому UI.
6. `Runtime/Handlers/PayloadReader.cs`: адресный клик ищет raycastable `Graphic` в дочерних объектах target, что покрывает Toggle и другие составные uGUI controls.
7. `Runtime/Input/InputSimulator.cs`: click содержит отдельный hover-кадр до PointerDown; это восстановило штатное раскрытие `TMP_Dropdown`.
8. `Runtime/Handlers/DragHandler.cs`: drag с адресной исходной целью теперь требует, чтобы она была фактическим верхним hit EventSystem; скрытый или перекрытый slider больше не может дать ложный успех.
9. `Runtime/Input/InputSimulator.cs`: scroll также содержит отдельный hover-кадр до wheel-события; это устраняет пропуск первого wheel ввода на свежем ScrollRect и позволяет вывести скрытые controls в viewport.

## Проверки

- После каждого изменения C#: `debug_force_recompile -> debug_check_compilation` завершился без compilation errors.
- Unity Console: `debug_get_errors` вернул `0` ошибок после каждого изменения.
- Реальный WebSocket/URDT ввод: `input_tier=virtual_device`; прямые UI callbacks и изменения UI-компонентов не использовались.

## Текущий инфраструктурный блокер

После успешного Test 1 и `editor_stop` Unity.exe остаётся живым и отвечает ОС, но Unity Skills на `http://127.0.0.1:8090` не восстановился после ожиданий и повторных health-check. Поэтому Test 2 и следующие не запускались: запрет на обход Unity Skills соблюден. Локальный `dotnet build` также недоступен, потому что в окружении нет .NET SDK. Независимый новый судья GPT-5.5 medium не вернул вердикт из-за реального usage limit; это не заменялось сгенерированным ответом.

## Нерешенные проблемы

### Рандомизированная среда и stress runner (2026-07-13, в работе)

- Среда: на каждом Play Mode переставляются семь неизменных controls UI Suite; их `TargetKind` не меняется. Два свежих запуска дали разные `ScreenCenter` для controls. Варианты `Mode A/B/C` перемешиваются через Fisher-Yates; агент получает текущие подписи через read-only `DropdownOptions` и никогда не выбирает по индексу.
- Валидация среды: реальный `click { testId: "btn_open_ui_suite" }` открыл UI Suite, затем `click { testId: "ui.modal_button" }` дал `modal_open`, `InteractionCount=1`. Обе дельты подтверждены inspect. Unity compilation и Console после пересборки: 0 ошибок.
- Исправлен URDT: адресный `scroll { testId: "ui.suite_scroll" }` раньше выбирал центр, перекрытый дочерним `CommandScroll`. Новый exact-top-hit resolver выбирает живую точку самого ScrollRect. Проверка: три последовательные real-input прокрутки дали `1.000 -> 0.985 -> 0.971 -> 0.956`.
- Нерешенное до stress-серии: в отдельных свежих Play Mode первый click launcher-кнопки может вернуть `input_tier=virtual_device` без дельты окна. Runner выполняет до пяти полных observe -> click -> inspect попыток; прямой вызов UI и сохраненные координаты не используются. Полный randomized run пока не завершился, поэтому 10 стресс-прогонов не заявлены выполненными.
- Оценка токенов текущего этапа: операция ~8 500, анализ ~11 000, исправление ~7 500.

## Final randomized stress result (2026-07-13)

This section supersedes the preceding in-progress randomized-stage note.

- Test polygon was not changed during this final stabilization pass.
- The runner uses fresh `inspect` before every action. It stores no target coordinates.
- Every interaction is device-level URDT input. No UI callback, component setter, or
  test-state shortcut was used.
- Navigation of the outer UI container now uses a real virtual-mouse swipe, not a
  wheel-step loop. The runner resolves an actionable point from current state.
- Mouse and touch paths were both exercised earlier through the virtual Mouse and
  Touchscreen devices. The stress run itself uses the faster mouse swipe path.

### Defects fixed in URDT

1. Standard mouse clicks now hold the physical pressed state for one input frame
   before release. This stabilized real uGUI Button activation, including `Back`.
2. Targeted `swipe` on a `ScrollRect` now resolves the same live non-scrollable
   descendant point used for scroll routing. A nested ScrollRect can no longer
   consume the outer navigation gesture.
3. The stress-runner viewport check now accepts a currently visible actionable
   center rather than requiring the full bounds of a large nested target to fit.

### Evidence

- Unity compilation after both C# changes: `debug_get_errors` returned `count: 0`.
- One post-fix full run: PASS, 46 recorded actions.
- Nine further full runs in the same Unity Play Mode: PASS, with 41, 45, 49, 43,
  41, 49, 43, 45, and 41 recorded actions.
- Total randomized stress outcome: **10 / 10 PASS**.
- JSONL evidence: `.harness/urdt-randomized-stress.jsonl`.

### Token estimate for the final stabilization pass

| Category | Estimated tokens |
| --- | ---: |
| Operation | ~6,000 |
| Analysis | ~8,000 |
| Fixes | ~4,000 |
