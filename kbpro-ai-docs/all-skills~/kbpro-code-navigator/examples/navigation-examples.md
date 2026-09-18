﻿# Примеры отчетов навигации — KBPro

Этот справочник содержит примеры заполнения отчета `Navigation Summary`, который ИИ-ассистент обязан выдать после завершения исследования кодовой базы.

## Шаблон отчета

```markdown
## Navigation Summary

- **Task surface:** [Область задачи, например: модуль очистки зубов, система звуков]
- **Relevant wiki/docs:** [Ссылки на изученные вики-файлы, например: file:///path/to/principals.md]
- **Relevant code/assets:** [Ссылки на файлы исходного кода и префабы, например: [TrainCleaningModule.cs](file:///path/to/TrainCleaningModule.cs)]
- **Existing pattern:** [Используемый паттерн: DI через LazySrv, EventBus сообщения, компоненты]
- **Risks:** [Риски: возможные утечки памяти в Dispose, циклическая зависимость, отсутствующие скрипты]
- **Recommended next skill:** [Рекомендуемый специализированный навык для реализации, например: kbpro-module-systems-builder]
- **Confidence:** [Уровень уверенности: Высокий / Средний (с указанием причин сомнений)]
```

---

## Пример 1: Анализ багов в логической системе

```markdown
## Navigation Summary

- **Task surface:** Логика завершения этапа модуля чистки (зубной камень).
- **Relevant wiki/docs:** [architecture-map.md](file:///e:/UnityProjects/IRI/dentistry-cow/kbpro-ai-docs/kbpro-wiki/wiki/maps/architecture-map.md), [principals.md](file:///e:/UnityProjects/IRI/dentistry-cow/kbpro-ai-docs/kbpro-wiki/raw/principals.md)
- **Relevant code/assets:** [StoneCleaningSystem.cs](file:///e:/UnityProjects/IRI/dentistry-cow/Assets/Core/Modules/StoneCleaning/Systems/StoneCleaningSystem.cs)
- **Existing pattern:** Чистая C# логическая система `LogicSystem`, тики через `IUpdatable`, подписка на события вью-компонента через Action.
- **Risks:** Обнаружена подписка на события компонента без удаления в `Dispose()`. При перезапуске модуля возможна утечка памяти.
- **Recommended next skill:** `kbpro-lifecycle-auditor` (для исправления утечки)
- **Confidence:** Высокий. Проблема локализована в методах Initialize/Dispose.
```

---

## Пример 2: Проектирование нового UI-окна

```markdown
## Navigation Summary

- **Task surface:** Добавление всплывающего окна подтверждения выхода в главное меню.
- **Relevant wiki/docs:** [gameplay-product-map.md](file:///e:/UnityProjects/IRI/dentistry-cow/kbpro-ai-docs/kbpro-wiki/wiki/maps/gameplay-product-map.md)
- **Relevant code/assets:** [UIPConfirmationWindow.cs](file:///e:/UnityProjects/IRI/dentistry-cow/Assets/Core/UI/Confirmation/UIPConfirmationWindow.cs), префаб `ConfirmationWindow.prefab`
- **Existing pattern:** MVP-паттерн UI на базе классов `UIPWindow` и `UIVWindow`.
- **Risks:** Необходима проверка на использование `TextMeshPro` на кнопках префаба; привязка параметров открытия через `IUIShowParams`.
- **Recommended next skill:** `unity-ui-mvp-builder`
- **Confidence:** Высокий. Новое окно будет создано по аналогии с окном `UIPSettingsWindow`.
```
