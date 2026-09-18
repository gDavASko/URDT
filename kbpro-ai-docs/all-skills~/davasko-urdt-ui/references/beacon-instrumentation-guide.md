# Руководство по расстановке маячков URDT (Beacon Instrumentation Guide)

Это официальное и исчерпывающее руководство для ИИ-агентов и разработчиков: **как, куда и зачем расставлять маячки URDT в ЛЮБОМ Unity-проекте**, чтобы ИИ мог автономно взаимодействовать с интерфейсом через WebSocket без скриншотов.

---

## 1. Фундаментальные принципы маячков

1. **Правило 1 к 1 (Strict Single Responsibility / SRP)**:
   - 1 UI-элемент = 1 специализированный компонент-маячок.
   - Никаких «универсальных» комбайнов с пустыми полями!
   - Кнопке нужен ТОЛЬКО маяк кнопки. Слайдеру — ТОЛЬКО маяк слайдера.
2. **Маяк — это автономный наблюдатель (Observer)**:
   - Маяк **сам** находит свой UI-компонент на том же GameObject (`AutoBind`).
   - Маяк **сам** подписывается на нативные события Unity (`Button.onClick`, `Toggle.onValueChanged`, `TMP_InputField.onValueChanged`).
   - Маяк **сам** фиксирует телеметрию (`InteractionCount`, `LastResult`, `LastInputAction`).
3. **Полная отвязка от игрового кода (Zero Coupling)**:
   - Игровые контроллеры (например, `MainMenuController`, `InventoryView`) **НЕ ДОЛЖНЫ** содержать ссылок на маячки URDT!
   - Игровой код пишется как обычный чистый Unity uGUI. Маячки прикрепляются снаружи и не влияют на геймплей.

---

## 2. Карта расстановки: Куда какой маячок вешать

| UI-элемент в сцене | Какой маячок вешать | На какой GameObject | Что отслеживает | Поддерживаемые команды |
|---|---|---|---|---|
| **Экран / Окно / Модалка** | [`UrdtUiWindowTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiWindowTarget.cs) | На **корневой GameObject** окна/панели (с `RectTransform`) | Активность окна, перекрытие, имя экрана | `inspect`, `query` |
| **Кнопка (Button / Card / Tab)** | [`UrdtUiButtonTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiButtonTarget.cs) | На GameObject с `UnityEngine.UI.Button` | `IsInteractable`, клики, двойные клики | `inspect`, `query`, `click`, `double_click`, `multi_click` |
| **Чекбокс / Переключатель** | [`UrdtUiToggleTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiToggleTarget.cs) | На GameObject с `UnityEngine.UI.Toggle` | `ToggleValue` (bool ON/OFF), кликабельность | `inspect`, `query`, `click` |
| **Ползунок / Слайдер громкости** | [`UrdtUiSliderTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiSliderTarget.cs) | На GameObject с `UnityEngine.UI.Slider` | `SliderValue` (float 0..1), Drag | `inspect`, `query`, `drag`, `press_move`, `swipe` |
| **Поле ввода текста (uGUI / TMP)** | [`UrdtUiInputTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiInputTarget.cs) | На GameObject с `TMP_InputField` или `InputField` | Фокус, ввод текста с клавиатуры | `inspect`, `query`, `click` (фокус) |
| **Выпадающий список (Dropdown)** | [`UrdtUiDropdownTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiDropdownTarget.cs) | На GameObject с `TMP_Dropdown` или `Dropdown` | Текущее значение, список опций | `inspect`, `query`, `click` |
| **Прокручиваемый список (Scroll)** | [`UrdtUiScrollTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiScrollTarget.cs) | На GameObject с `UnityEngine.UI.ScrollRect` | `ScrollPosition` (x, y), границы вьюпорта | `inspect`, `query`, `scroll`, `drag`, `swipe` |
| **Индикатор / Иконка / Текст** | [`UrdtUiGenericTarget`](file:///e:/Projects/URDT/Packages/com.davasko.urdt/Runtime/Inspect/UrdtUiGenericTarget.cs) | На любой UI GameObject с `RectTransform` | Экранные границы, видимость в Canvas | `inspect`, `query` |

---

## 3. Пошаговая настройка каждого типа маячка

### 1. Окна и Экраны (`UrdtUiWindowTarget`)
* **Куда вешать**: На корневой объект экрана (например, `Canvas/MainMenuWindow` или `Canvas/InventoryWindow`).
* **Поля в Инспекторе**:
  - `TargetId`: Уникальный ID окна (например, `"window_main_menu"` или `"window_inventory"`).
  - `Module`: Название подсистемы (например, `"launcher"`, `"gameplay"`).
  - `ActiveWindow`: Имя этого же окна (например, `"window_main_menu"`).
* **Как работает**: При вызове `gameObject.SetActive(true)` маяк в методе `OnEnable()` автоматически регистрирует себя как текущее активное окно в `UrdtUiWindowTarget.CurrentActiveWindow`.

### 2. Кнопки (`UrdtUiButtonTarget`)
* **Куда вешать**: На тот же GameObject, где висит `Button`.
* **Поля в Инспекторе**:
  - `TargetId`: Семантический ID кнопки (например, `"menu.btn_play"`, `"inventory.btn_close"`).
  - `_selectable`: Ссылка на `Button`. **Если оставить пустым**, компонент сам найдёт кнопку через `GetComponent<Selectable>()` в `Awake()` и `Reset()`.
  - `_clickResult`: Произвольная семантическая строка результата (например, `"play_clicked"`, `"dialog_closed"`, по умолчанию `"clicked"`).
  - `ActiveWindow`: ID родительского окна (например, `"window_main_menu"`).
* **Как работает**: Автоматически вешает слушатель на `Button.onClick`. При реальном клике виртуальной мыши URDT увеличивает `InteractionCount` и записывает `LastResult = _clickResult`.

### 3. Переключатели (`UrdtUiToggleTarget`)
* **Куда вешать**: На тот же GameObject, где висит `Toggle`.
* **Поля в Инспекторе**:
  - `TargetId`: Например, `"settings.toggle_sound"`.
  - `_toggle`: Ссылка на `Toggle` (авто-биндинг при пустом поле).
* **Как работает**: Автоматически слушает `Toggle.onValueChanged`. В свойстве `ToggleValue` всегда отдает актуальный `bool`.

### 4. Слайдеры (`UrdtUiSliderTarget`)
* **Куда вешать**: На тот же GameObject, где висит `Slider`.
* **Поля в Инспекторе**:
  - `TargetId`: Например, `"settings.volume_slider"`.
  - `_slider`: Ссылка на `Slider` (авто-биндинг при пустом поле).
* **Как работает**: Слушает `Slider.onValueChanged`, транслирует текущий float `SliderValue`. ИИ перемещает его через команду `drag` от центра ползунка.

### 5. Поля ввода текста (`UrdtUiInputTarget`)
* **Куда вешать**: На тот же GameObject, где висит `TMP_InputField` или `InputField`.
* **Поля в Инспекторе**:
  - `TargetId`: Например, `"auth.username_input"`.
* **Как работает**: Автоматически подписывается на событие изменения строки. ИИ сначала шлёт `click { testId: 'auth.username_input' }` для установки фокуса и каретки, а затем шлёт `type_text { text: 'МойТекст' }`.

### 6. Скролл-панели (`UrdtUiScrollTarget`)
* **Куда вешать**: На GameObject со `ScrollRect` (обычно называется `ScrollView` или `ScrollArea`).
* **Поля в Инспекторе**:
  - `TargetId`: Например, `"shop.items_scroll"`.
* **Как работает**: Вычисляет размеры `viewport` и `content`. Если ИИ видит, что целевая кнопка имеет координаты вне экрана (например, $Y < 0$), ИИ делает `swipe` этого `UrdtUiScrollTarget` в направлении `"up"` или `"down"`, пока кнопка не окажется внутри вьюпорта!

---

## 4. Как расставлять маячки: 3 способа

### Способ 1. В редакторе Unity через Inspector (для существующих сцен)
1. Выделите нужный объект в Hierarchy (например, кнопку `StartButton`).
2. Нажмите `Add Component` в Инспекторе.
3. Введите `Urdt Ui Button Target` и нажмите Enter.
4. Введите `TargetId` (например, `"menu.btn_start"`).
5. Поле `Selectable` привяжется автоматически!

### Способ 2. В Prefab'ах UI (Рекомендуемый для продакшена)
* Откройте префаб кнопки/окна в режиме Prefab Mode.
* Повесьте соответствующий `UrdtUi*Target` прямо в префаб.
* Все экземпляры префаба, создаваемые динамически во время игры, **автоматически получат маячок и зарегистрируются в URDT**.

### Способ 3. Программно через C# (для процедурного UI или билдеров)
```csharp
using KBP.URDT.Inspect;
using UnityEngine;
using UnityEngine.UI;

// Добавление кнопки в коде:
Button myButton = myButtonObj.GetComponent<Button>();
UrdtUiButtonTarget beacon = myButtonObj.AddComponent<UrdtUiButtonTarget>();
beacon.ConfigureButton(
    targetId: "hud.btn_attack",
    activeWindow: "window_battle",
    activeModule: "combat",
    module: "ui",
    supportedCommands: new[] { UrdtCommandType.Inspect, UrdtCommandType.Query, UrdtCommandType.Click },
    selectable: myButton,
    clickResult: "attack_executed"
);
```

---

## 5. Антипаттерны при расстановке маячков (Чего делать НЕЛЬЗЯ)

1. ❌ **НЕ вешайте ссылки на маяки в игровые контроллеры**:
   Контроллер `MyGameController` **не должен** объявлять `public UrdtUiButtonTarget _myTarget;`. Маяк работает сам по себе.
2. ❌ **НЕ вешайте `UrdtUiButtonTarget` на слайдер или панель**:
   Каждый компонент должен соответствовать реальному элементу Unity.
3. ❌ **НЕ создавайте рандомные динамические GUID в TargetId**:
   `TargetId` должен быть стабильным и понятным человеку и ИИ (например, `"menu.settings"`, а не `"btn_98234-df-234"`).
4. ❌ **НЕ забывайте про `UrdtUiWindowTarget` на окнах**:
   Без маяка окна ИИ не сможет понять, какое окно сейчас на переднем плане и не перекрыт ли интерфейс модальным диалогом.
