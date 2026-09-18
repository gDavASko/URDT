# Пример плана создания модуля (Module Creation Plan)

Этот документ представляет собой шаблон и пример заполнения **Module Creation Plan**, который разработчик обязан составить на **ЭТАПЕ-0** и согласовать с пользователем перед написанием C# кода.

---

# Module Creation Plan: Чистка окон (WindowCleaning)

## 1. Описание задачи (ТЗ)
Игрок должен помыть грязные окна поезда. На экране есть грязное окно, разделенное на 4 зоны. Игрок зажимает левую кнопку мыши (или палец на экране) и водит по окну. При прохождении по грязной зоне процент грязи в ней уменьшается. При полной очистке всех 4 зон модуль завершается со звуком победы. В HUD отображается процент очистки окна (0-100%).

## 2. Архитектура и распределение ответственности

### 2.1 GameComponents (Представление / MonoBehaviour)
Все визуальные компоненты располагаются на сцене в иерархии префаба модуля.

- **`WindowCleaningComponent`**:
  - Хранит ссылки на `SpriteRenderer` окна, массив грязевых масок, `Collider2D` для отслеживания ввода.
  - Подписывается на события мыши/тача через `IPointerDownHandler`, `IPointerUpHandler`, `IDragHandler`.
  - Предоставляет события:
    - `Action<Vector2> OnDragProgress` — передает позицию тача в локальных координатах.
  - Методы для системы:
    - `SetDirtAlpha(int zoneIndex, float alpha)` — меняет прозрачность грязи в конкретной зоне.
    - `PlaySparkleAnimation()` — проигрывает эффект блеска при завершении.

- **`WindowCleaningHUDComponent`**:
  - Хранит ссылку на `TextMeshProUGUI` и `Slider` для индикации общего процента.
  - Методы для системы:
    - `UpdatePercent(float percent)` — обновляет слайдер и текст (например, "Очищено: 85%").

### 2.2 LogicSystems (Бизнес-логика / Чистый C#)
Чистые классы, не наследующие `MonoBehaviour`.

- **`WindowCleaningFlowSystem`**:
  - Основной координатор логики чистки.
  - Подписывается на `OnDragProgress` от `WindowCleaningComponent`.
  - Рассчитывает дистанцию до зон, уменьшает процент грязи в зонах.
  - Хранит состояние: массив уровней чистоты зон `float[] _zonesCleanliness` (от 0f до 1f).
  - Вызывает событие `_onComplete` при достижении 100% очистки.

- **`WindowCleaningHUDSystem`**:
  - Обновляет HUD на основе изменения чистоты зон.
  - Вычисляет средний процент чистоты окна.
  - Передает данные в `WindowCleaningHUDComponent`.

### 2.3 GameModule (Оркестратор / MonoBehaviour)
- **`WindowCleaningModule`**:
  - Регистрирует системы `WindowCleaningFlowSystem` и `WindowCleaningHUDSystem`.
  - Управляет открытием UI-окна `Constants.UICOMPONENT.GAME`.
  - Воспроизводит фоновую музыку.
  - Завершает модуль через вызов `OnComplete()`.

---

## 3. Файловая структура модуля

Все скрипты размещаются в пространстве имен `KBP.RAILWAY_COW.WINDOW_CLEANING`:

- **Скрипты**:
  - `Assets/Railway-cow/Scripts/Modules/WindowCleaning/WindowCleaningModule.cs` — Оркестратор
  - `Assets/Railway-cow/Scripts/Modules/WindowCleaning/Components/WindowCleaningComponent.cs` — Компонент окна
  - `Assets/Railway-cow/Scripts/Modules/WindowCleaning/Components/WindowCleaningHUDComponent.cs` — Компонент HUD
  - `Assets/Railway-cow/Scripts/Modules/WindowCleaning/Systems/WindowCleaningFlowSystem.cs` — Логика очистки
  - `Assets/Railway-cow/Scripts/Modules/WindowCleaning/Systems/WindowCleaningHUDSystem.cs` — Логика HUD
- **Префаб**:
  - `Assets/Railway-cow/Prefabs/Modules/WindowCleaningModule.prefab`
- **Конфигурация**:
  - Регистрация в `SOConstantsContainer` под ключом `WINDOW_CLEANING`.
  - Настройка в `SOGameModuleSettings.asset` в сценарии `WINDOW_CLEANING`.

---

## 4. Контракт данных (Module I/O)
*В данной задаче передача данных из других нод сценария не требуется. Используются дефолтные `EmptyModuleInput` и `EmptyModuleOutput`.*

---

## 5. Внешние зависимости и ресурсы
- **Звуки**:
  - Эффект трения стекла: `"sfx_window_scrub"` (проигрывается циклично во время драга).
  - Звук победы: `"sfx_window_clean_success"`.
  - Получение через `LazySrv<ISoundSystem>`.
- **UI**:
  - Использование глобального HUD окна `Constants.UICOMPONENT.GAME`.

---

## 6. План ручной верификации
1. Собрать префаб модуля с настроенными `systems` и `componentProviders`.
2. Запустить Play Mode через кнопку **Play Module** в инспекторе `WindowCleaningModule`.
3. Убедиться, что при вождении мышкой по окну грязь визуально исчезает, а слайдер в HUD плавно заполняется до 100%.
4. Проверить воспроизведение цикличного звука при драге и его остановку при прекращении драга.
5. Убедиться, что по достижении 100% срабатывает анимация блеска, воспроизводится звук победы, и спустя 1.5 секунды модуль завершает работу.
6. Выйти из Play Mode, проверить консоль на отсутствие ошибок и предупреждений.
