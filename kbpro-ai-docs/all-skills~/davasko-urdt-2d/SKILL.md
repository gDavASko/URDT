---
name: davasko-urdt-2d
description: Universal skill teaching AI agents how to perceive, navigate, play, test, and master ALL 32 Unity 2D minigame mechanics (M01 – M32) step-by-step in runtime through URDT (Unity Remote Debugging Transport) over WebSocket. Strict adherence to the 4-phase Agentic Loop (Perception -> Reasoning -> Action -> Delta Evaluation), 2D Polymorphic Beacon Hierarchy (Urdt2DModuleTarget, Urdt2DDraggableTarget, Urdt2DSlotTarget, Urdt2DInteractiveAreaTarget), dynamic resolution-independent coordinate targeting, honest Tier 2 device-level pointer input (click, drag, pointer_down, pointer_up, press_move), spring-damper dwell settling, multi-touch channels, and victory transition timing. Absolute ban on screenshots / computer vision and direct C# state manipulation cheats. Contains complete, self-contained playbooks, beacon mappings, math, algorithms, and pitfalls for every mechanic from M01 to M32 directly embedded inside the skill.
---

# DavASko URDT 2D Mechanics Automation Skill

This skill teaches AI agents how to **directly perceive, play, test, and master ALL 32 Unity 2D minigames (M01 – M32)** in real-time through the **URDT (Unity Remote Debugging Transport)** protocol.

---

## 1. Core Philosophy: The AI is the Player (Живой игрок за пультом, а не слепой скрипт!)

**СТРОЖАЙШИЙ ЗАПРЕТ на монолитные пакетные скрипты с жестко закодированными слепыми паузами и цепочками действий!**

ИИ-агент при взаимодействии с 2D-полигоном и любыми 2D-механиками должен действовать как **высококвалифицированный игрок-тестировщик**:
1. **Perception (Восприятие через маячки)**: Запрос живого состояния модуля (`query { activeOnly: true }` или `inspect { testId }`).
   - Получение текущего прогресса `ProgressNormalized` и статуса `IsCompleted`.
   - Обнаружение динамических координат драг-элементов (`ScreenCenter`), слотов, препятствий и зон поражения.
   - Фильтрация мусорных объектов (`IsJunk == true`) и уже зафиксированных деталей (`IsSnapped == true`).
2. **Reasoning (Осмысление и расчет траектории)**:
   - Сопоставление предметов и приемников (`item.ItemId == slot.SlotId` или `slot.AcceptedType`).
   - Расчет безопасной траектории огибания барьеров.
   - Расчет физических параметров: времени удержания $T = \Delta V / R$, угловой дельты $\Delta \theta$, точки перехвата движущейся цели $X(t) = X_0 + V \cdot t$.
3. **Action (Честное действие устройства ввода)**:
   - Отправка **честного события ввода** через URDT (`drag`, `click`, `pointer_down`, `pointer_up`, `press_move`).
   - Все драги обязаны выполняться с интерполяцией (`steps: 15..25`) для корректной работы Unity `EventSystem` (`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`).
4. **Evaluation (Оценка дельты состояния)**:
   - Немедленный опрос `Urdt2DModuleTarget` или конкретного объекта.
   - Проверка изменения: увеличился ли `ProgressNormalized`? Зафиксировалась ли деталь (`IsSnapped == true`)?
   - Если прогресс не изменился — выявление причины (промах мимо радиуса примагничивания, преждевременный отпуск до гашения кинетической энергии, столкновение) и мгновенная адаптация!

---

## 2. Карта расстановки 2D-маячков (Polymorphic 2D Beacon Architecture)

В 2D-окружении каждый интерактивный объект оснащается строго специализированным маячком. Никаких гигантских универсальных монолитов!

| 2D Игровой Элемент | Компонент-маячок | На какой GameObject вешать | Ключевые поля телеметрии | Поддерживаемые URDT-команды |
|---|---|---|---|---|
| **Корневой модуль механики** | `Urdt2DModuleTarget` | Корневой объект префаба механики | `MechanicId`, `MechanicTitle`, `Instruction`, `IsCompleted`, `ProgressNormalized`, `Score`, `TargetScore` | `inspect`, `query` |
| **Перетаскиваемый предмет / деталь** | `Urdt2DDraggableTarget` | На предмет (деталь, токен, фишка, грузик, инструмент) | `ItemId`, `Category`, `IsJunk`, `IsSnapped`, `IsDragging`, `CurrentSlotId`, `ScreenCenter`, `ScreenRect` | `inspect`, `query`, `click`, `drag`, `press_move`, `pointer_down`, `pointer_up` |
| **Слот / Гнездо / Корзина / Приемник**| `Urdt2DSlotTarget` | На зону приема (паз, лоток, чаша весов, посадочное место) | `SlotId`, `AcceptedType`, `IsOccupied`, `CurrentCount`, `RequiredCount`, `ScreenCenter` | `inspect`, `query` |
| **Интерактивная зона / Кнопка / Сектор**| `Urdt2DInteractiveAreaTarget` | На полосы раннера, ячейки сетки, крутилки, триггеры | `AreaId`, `AreaType`, `IsActiveArea`, `NumericValue`, `StateString`, `ScreenCenter` | `inspect`, `query`, `click`, `drag`, `pointer_down`, `pointer_up`, `swipe` |
| **Кнопка управления полигона** | `UrdtUiButtonTarget` | Кнопки навигации, сброса, каталога, запуска | `TargetId`, `IsInteractable`, `InteractionCount`, `LastResult` | `inspect`, `query`, `click` |
| **Информационный текст / Инструкция** | `UrdtUiTextTarget` / `Generic` | Заголовки, инструкции, показания весов | `TextValue`, `ScreenCenter`, `ScreenRect` | `inspect`, `query` |

---

## 3. Как расставлять 2D-маячки (3 способа)

### Способ А: В префабах механик (Рекомендуется для продакшена)
- Откройте префаб механики (например, `Assets/URDT_TestPoligon/2D_Polygon/Prefabs/M01_SnapToSlot.prefab`).
- На корневой объект добавьте `Urdt2DModuleTarget`, укажите `MechanicId = "M01_SnapToSlot"`.
- На подвижные детали добавьте `Urdt2DDraggableTarget`, заполнив `ItemId` и флаг `IsJunk`.
- На гнезда добавьте `Urdt2DSlotTarget`, указав `SlotId` и `AcceptedType`.

### Способ Б: Автоматическая процедурная инструментация (`Urdt2DBeaconUtility`)
Для мгновенного оснащения всей иерархии модуля используется утилита `Urdt2DBeaconUtility`:
```csharp
// Оснащает корень модульным маячком и рекурсивно навешивает маячки на все дочерние детали и слоты
Urdt2DBeaconUtility.InstrumentHierarchy(activeMechanicModule);
// Регистрирует иерархию в рантайм реестре URDT-сервера
KBP.URDT.UrdtServerHost.Instance?.RegisterHierarchy(activeMechanicModule.transform);
```

### Способ В: Через Unity Inspector во время паузы / редактирования
1. Выделите объект в Hierarchy.
2. Нажмите `Add Component` -> `Urdt 2D Draggable Target` (или `Slot Target`).
3. Заполните `TargetId` (например, `"Item_Square"` или `"Slot_Red"`).

---

## 4. Главные инварианты и правила работы ИИ с 2D механиками

1. **АБСОЛЮТНЫЙ ЗАПРЕТ НА СКРИНШОТЫ И COMPUTER VISION**:
   - Никаких снимков экрана, OCR или OpenCV.
   - Вся топология, взаимные расстояния, углы поворота и границы берутся **исключительно** через структурированные маячки URDT WebSocket API (`query`, `inspect`).
2. **АБСОЛЮТНЫЙ ЗАПРЕТ НА ЧИТЫ И ПРЯМУЮ МОДИФИКАЦИЮ C#**:
   - Запрещено напрямую вызывать C# события победы (`OnCompleted.Invoke()`) или форсировать `IsCompleted = true`.
   - Прохождение осуществляется **только** через честную эмуляцию ввода операционной системы / устройств ввода (`click`, `drag`, `pointer_down`, `pointer_up`, `press_move`).
3. **ДИНАМИЧЕСКИЕ КООРДИНАТЫ И НЕЗАВИСИМОСТЬ ОТ РАЗРЕШЕНИЯ (Resolution Independence)**:
   - В Unity Editor Game View холст может динамически масштабироваться (1920x960, 1280x720, 2560x1440).
   - **Категорически запрещено** зашивать статические координаты пикселей в код!
   - Все координаты ВСЕГДА запрашиваются на лету из `ScreenCenter` соответствующего маячка перед каждым действием.
4. **МНОГОШАГОВЫЙ ДРАГ (Multi-step Drag Interpolation)**:
   - Unity `EventSystem` и `GraphicRaycaster` требуют серии событий движения мыши/пальца для срабатывания интерфейсов `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`.
   - Телепортация за 1 шаг воспринимается как одиночный клик и приводит к возврату детали на базу. Драг должен содержать минимум 15–25 промежуточных шагов.
5. **ФИЗИЧЕСКОЕ ЗАТУХАНИЕ И DWELL-ПАУЗЫ (Spring-Damper Dwell Calibration)**:
   - В механиках с упругими пружинами (M07, M20) отпускание детали с ненулевой скоростью выбрасывает её из гнезда.
   - Требуется выдержка (dwell pause 300–450 мс) в точке назначения перед отправкой `pointer_up`.
6. **НЕПРЕРЫВНОЕ УДЕРЖАНИЕ (Continuous Hold)**:
   - Для механик накопления заряда, переливания жидкостей и газа (M10, M27, M31, M32) используется непрерывное удержание `pointer_down`, выдержка расчетного времени и своевременный `pointer_up`.
7. **УЧЕТ ПРАВИЛА ПРАЗДНОВАНИЯ ПОБЕДЫ (Victory Overlay & Auto-Advance)**:
   - При 100% прохождении механики активируется оверлей победы (`VictoryCelebrationRoutine`) длительностью 2.0 секунды, после чего происходит автоматический переход к следующему модулю. Агент обязан выдерживать паузу 2500–2800 мс перед началом следующего теста.

---

## 4.5. Подготовка к прохождению теста на старте (Unified Pre-Flight Startup Checklist)

Для обеспечения 100% стабильного старта и надежного прохождения 2D-полигона агент обязан следовать 5-шаговому подготовительному регламенту:

1. **Связь и Handshake (Шаг 1)**:
   - Подключение к `ws://127.0.0.1:7777/`, отправка токена и projectId.
   - Подтверждение статуса `ready` перед отправкой команд.
2. **Адаптивность к текущему разрешению экрана (Resolution-Agnostic 2D) (Шаг 2)**:
   - **НИКАКОГО ХАРДКОДА ПИКСЕЛЕЙ И ПРИНУДИТЕЛЬНОЙ ФИКСАЦИИ 1080p!**
   - Все координаты драг-элементов (`Item_*`), слотов (`Slot_*`), барьеров и интерактивных зон запрашиваются динамически через `inspect` / `query`.
   - Маячки (`Urdt2DDraggableTarget`, `Urdt2DSlotTarget`) возвращают `ScreenCenter` уже спроецированным в текущее физическое разрешение Game View (4K, 1440p, 1080p, Ultrawide, Free Aspect).
   - Расчет дистанций перемещения, отклонений стиков и векторов рогатки выполняется в относительных долях от текущего размера экрана, а радиус примагничивания ($R_{\text{snap}}$) учитывает текущий масштаб Canvas.
3. **Безопасный синтаксис CLI в Windows PowerShell (Шаг 3)**:
   - В терминале PowerShell одинарные кавычки `'{"testId":"..."}'` теряют двойные кавычки при передаче в Node.js, вызывая краш `JSON.parse` (`Expected property name or '}'`).
   - **Строгое правило**: использовать типизированные подкоманды CLI:
     ```powershell
     node Tools/urdt-cli.js click <testId>
     node Tools/urdt-cli.js inspect <testId>
     node Tools/urdt-cli.js query --active
     node Tools/urdt-cli.js drag <fromId> <toId> --steps 20
     ```
   - Для непрерывной интерактивной сессии без оверхеда запускать REPL:
     ```powershell
     node Tools/urdt-cli.js --repl
     ```
4. **Пайплайн двухэтапной навигации полигона (Шаг 4)**:
   - **Типовая ошибка**: искать маячки M01 сразу после старта сцены. На старте активен только `MainMenu`! Префабы механик еще не инстанциированы.
   - **Этап А**: Клик `node Tools/urdt-cli.js click btn_open_2d_suite` ➔ `inspect btn_open_2d_suite` ➔ подтвердить дельту: `InteractionCount > 0` и окно `World2DSuiteWindow` активно (`Active: true`).
   - **Этап Б**: Клик `node Tools/urdt-cli.js click btn_2d_run_sequential` (`>> Автопрогон всех тестов (1 -> 32)`) в `CatalogPanel` ➔ хост `Mechanic2DHost` инициализирует `MechanicPlayArea` и спавнит первый префаб `M01_SnapToSlot`.
5. **Валидация спавна и вьюпорта (Шаг 5)**:
   - Выполнить `node Tools/urdt-cli.js query --active` ➔ убедиться, что появился маяк `Urdt2DModuleTarget` с `MechanicId == "M01_SnapToSlot"`.
   - Проверить попадание координат в экран ($0 \le X \le \text{Screen.width}$, $0 \le Y \le \text{Screen.height}$).
   - Только после этого переходить к пошаговому решению механики!

---

## 5. Универсальный пошаговый цикл игры (Agentic ReAct Loop)

```
┌────────────────────────────────────────────────────────────────────────┐
│ 1. CONNECT & HANDSHAKE                                                 │
│    ws://127.0.0.1:7777/ ➔ { action: "handshake", payload: { token } }  │
├────────────────────────────────────────────────────────────────────────┤
│ 2. DISCOVER ACTIVE MECHANIC                                            │
│    query { activeOnly: true } ➔ Получить Urdt2DModuleTarget             │
│    Идентифицировать MechanicId (например, "M01_SnapToSlot")            │
├────────────────────────────────────────────────────────────────────────┤
│ 3. PERCEIVE BEACONS & STATE                                            │
│    Найти все доступные Draggables, Slots, Areas                        │
│    Отфильтровать нерелевантные (IsJunk == true, IsSnapped == true)     │
├────────────────────────────────────────────────────────────────────────┤
│ 4. REASON & SYNTHESIZE TRAJECTORY                                      │
│    Определить стартовую точку From = Draggable.ScreenCenter            │
│    Определить целевую точку To = Slot.ScreenCenter                     │
│    Построить промежуточные узлы огибания препятствий                   │
├────────────────────────────────────────────────────────────────────────┤
│ 5. EXECUTE HONEST INPUT                                                │
│    drag(From, To, steps: 20) / press_move(path) / pointer_down        │
├────────────────────────────────────────────────────────────────────────┤
│ 6. EVALUATE DELTA & ADAPT                                              │
│    inspect(ModuleTarget) ➔ проверить рост ProgressNormalized           │
│    Если Progress == 1.0 ➔ ожидать 2.8с до следующего уровня!           │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Мастер-каталог всех 32 механик: Краткий справочник

Полные, детальные алгоритмы с формулами, маячками и ловушками содержатся в файлах справочника:
- [`references/mechanics-catalog-m01-m16.md`](references/mechanics-catalog-m01-m16.md)
- [`references/mechanics-catalog-m17-m32.md`](references/mechanics-catalog-m17-m32.md)

| ID | Название механики | Тип взаимодействия | Ключевые маячки | Основная ловушка |
|---|---|---|---|---|
| **M01** | Snap-to-Slot Puzzle | Drag & Drop | `Item_*`, `Slot_*`, `Obstacle_*` | Мусорные детали с `IsJunk == true`, радиус защелки 80px |
| **M02** | Multi-Container Sorting | Drag & Drop | `Item_*`, `Bin_*` | Несовпадение типов `AcceptedType`, пересечение траекторий |
| **M03** | Weight Comparator | Drag & Drop | `Weight_*`, `Scale_RightPan`, `ScaleReadout` | Опрокидывание чаши при избыточном весе; сумма: 200+100+50=350г |
| **M04** | Multi-Layer Attachment | Monotonic Drag | `Part_*`, `Socket_*`, `Layer_*` | Нарушение Z-порядка слоев: строго 1 -> 2 -> 3 -> 4 |
| **M05** | Timeline Sequencer | Slot Insertion | `Frame_*`, `TrackSlot_*` | Случайное перемешивание слотов при быстром драге |
| **M06** | Node Pairing | Graph Wire Drag | `Pin_Src_*`, `Pin_Tgt_*` | Отпускание провода дальше 110px от приемного пина |
| **M07** | Wobble & Snap | Spring Dwell Drag| `WobblePendulum`, `CaptureBasin` | Инерционный отскок: требуется dwell-пауза 450мс перед отпусканием |
| **M08** | Waypoint Tracking | Spline Tracing | `WaypointProbe`, `Waypoint_1..5` | Срезание углов за пределами канала 45px вызывает сброс |
| **M09** | Coverage Accumulator | Area Scrubbing | `SpongeBrush`, `DirtCell_*` | Пропущенные угловые ячейки сетки очистки |
| **M10** | Timed Accumulator | Hold to Fill | `HoldButton`, `GaugeReadout` | Перелив выше 100% вызывает штрафной сброс |
| **M11** | Cone Emitter | Directional Burst | `TurretAim`, `FireTarget_*` | Разброс снарядов; стрельба только при соосности конуса |
| **M12** | Angular Delta Tracker | Rotary Dial Drag | `RotaryWheel`, `AngleDial` | Скачок через угол $360^\circ \to 0^\circ$; вращение по касательным |
| **M13** | Lane Switcher Runner | Lane Dodge | `RunnerPlayer`, `Obstacle_*`, `Lane_*` | Задержка реакции при смене полосы |
| **M14** | Slingshot Impulser | Tension Drag | `SlingshotAnchor`, `TargetCan_*` | Вектор натяжения направлен ПРОТИВОПОЛОЖНО цели |
| **M15** | Timing Interceptor | Moving Intercept | `TargetMoving`, `SweepCursor`, `BtnHit` | Пиксельное опережение с учетом лага WebSocket (~30-50мс) |
| **M16** | Rhythm Phase Detector | Periodic Beat Tap| `BeatIndicator`, `HitZone`, `BtnTap` | Нажатие в противофазе; синхронизация с периодом $T$ |
| **M17** | Tug-of-War Balance | Equilibrium Tap | `TugMarker`, `CenterZone`, `BtnPull` | Перетягивание за пределы шкалы; клики со скважностью |
| **M18** | Graph Flow Closure | Tile Rotation | `PipeTile_*`, `CircuitIn`, `CircuitOut`| Незамкнутые тупиковые ветви труб |
| **M19** | Flood Fill Coloring | Palette Fill | `ColoringSegment_*`, `ColorPalette_*` | Закрашивание соседних сегментов одинаковым цветом |
| **M20** | Vertical Stacking | Physics Stacking | `StackBlock_*`, `StackPlatform` | Смещение центра масс и крутящий момент; вертикальная соосность |
| **M21** | Reaction Probe | Visual Prompt Tap| `ReactionField`, `ReactionSignal` | Фальстарт до появления зеленого сигнала |
| **M22** | Grid Pathfinding | Maze Step Drag | `PlayerToken`, `GridCell_*`, `ExitCell` | Шаг в стену лабиринта вызывает сброс пути |
| **M23** | Stencil Reveal | Scratch Sweep | `ScratchCover`, `HiddenObject_*` | Недостаточный охват штрихов (требуется $\ge 85\%$ очистки) |
| **M24** | Target Elimination | Rapid Clicking | `BubbleTarget_*`, `ScoreText` | Пропуск лопающихся пузырей с истекающим таймером |
| **M25** | Lane Switcher Halves | Screen Half Taps | `RunnerCar`, `TapLeft`, `TapRight`, `Hazard_*`| Клик не по той половине экрана в стрессовом окне |
| **M26** | Lane Switcher Drag | Continuous Drag | `PlayerShip`, `HazardBlock_*` | Отрыв пальца от экрана при маневре |
| **M27** | Physics Car Hills | Throttle Hold | `PedalGas`, `PedalBrake`, `PhysicsCar` | Опрокидывание машины на крутых холмах; сброс газа на гребне |
| **M28** | Dual Bridge Route | Plank Bridging | `BridgePlank_*`, `AbutmentLeft`, `Right` | Планка не перекрывает зазор между опорами моста |
| **M29** | Contour Cutting | Continuous Trace| `CuttingTool`, `ContourPoint_*` | Выход лезвия за допуск контура ($> 15$ px) |
| **M30** | Character Dress Up | Layered Slots | `ClothingItem_*`, `BodySlot_*` | Нарушение порядка слоев (штаны поверх ботинок) |
| **M31** | Liquid Filling | Tilt / Pour Hold | `FlaskSource`, `VesselTarget`, `FluidLevel` | Брызги и перелив через край мензурки |
| **M32** | Airplane Star Glider | Pitch Hold Flap | `AirplaneGlider`, `CollectibleStar_*` | Столкновение с горами при затяжном пикировании |

---

## 7. Справочные руководства внутри скила

- [`references/mechanics-catalog-m01-m16.md`](references/mechanics-catalog-m01-m16.md) — Полные руководства по механикам M01 – M16.
- [`references/mechanics-catalog-m17-m32.md`](references/mechanics-catalog-m17-m32.md) — Полные руководства по механикам M17 – M32.
- [`references/beacon-instrumentation-2d-guide.md`](references/beacon-instrumentation-2d-guide.md) — Руководство по расстановке 2D-маячков.
- [`references/urdt-2d-interaction-protocol.md`](references/urdt-2d-interaction-protocol.md) — Протокол WebSocket, команды ввода, координатные пространства.
- [`references/unity-editor-and-cli-lifecycle.md`](references/unity-editor-and-cli-lifecycle.md) — Управление редактором через Unity CLI и MCP.
- [`references/troubleshooting-2d.md`](references/troubleshooting-2d.md) — 15 типовых проблем и способы их устранения.

---

## 8. Примеры кода внутри скила

- [`examples/urdt_client.js`](examples/urdt_client.js) — Готовый универсальный клиент URDT на Node.js.
- [`examples/good_01_agentic_2d_player.js`](examples/good_01_agentic_2d_player.js) — Пошаговый цикл прохождения 2D-полигона.
- [`examples/good_02_dynamic_drag_and_drop.js`](examples/good_02_dynamic_drag_and_drop.js) — Алгоритм динамического Drag & Drop без зашитых координат.
- [`examples/good_03_continuous_hold_and_timing.js`](examples/good_03_continuous_hold_and_timing.js) — Алгоритм удержания и перехвата целей по времени.
- [`examples/good_04_spline_and_contour_tracing.js`](examples/good_04_spline_and_contour_tracing.js) — Непрерывная трассировка контуров и сплайнов.
- [`examples/good_05_runner_lane_switching.js`](examples/good_05_runner_lane_switching.js) — Динамическое уклонение в раннерах.
- [`examples/bad_01_direct_csharp_cheat.js`](examples/bad_01_direct_csharp_cheat.js) — Антипаттерн: прямой вызов C# событий.
- [`examples/bad_02_screenshot_cv_reliance.js`](examples/bad_02_screenshot_cv_reliance.js) — Антипаттерн: использование скриншотов.
- [`examples/bad_03_blind_static_coordinates.js`](examples/bad_03_blind_static_coordinates.js) — Антипаттерн: жестко зашитые координаты экрана.
- [`examples/bad_04_zero_step_teleport_drag.js`](examples/bad_04_zero_step_teleport_drag.js) — Антипаттерн: 1-шаговый драг без промежуточных событий.
