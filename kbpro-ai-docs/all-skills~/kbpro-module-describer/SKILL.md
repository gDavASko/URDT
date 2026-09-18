---
name: kbpro-module-describer
description: "Document module to reproduction-grade reference. Triggers: задокументируй модуль X, опиши игровой модуль, сделай reference, describe module."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
required_reading:
  - references/reproduction-quality-bar.md
  - references/source-reading-checklist.md
  - references/kbpro-primitives-glossary.md
  - examples/Example_reference.md
known_risks:
  - Описание-обзор без reproduction-grade частей (нет 3bis Контракта интеграции / 3.8 Карты инъекций / 3.10 Скелета классов / 7 Adaptation Guide) — по нему нельзя воссоздать модуль.
  - Додумывание поведения или сигнатур API вместо чтения кода (нужно ставить [НЕИЗВЕСТНО — уточнить]; сигнатуры примитивов сверять по kbpro-primitives-glossary.md / исходнику).
  - Неполный список систем/компонентов/полей конфига или пропуск инъекций → воспроизводимость ломается.
  - Копирование целых файлов кода вместо ключевых фрагментов (скелет 3.10 — только сигнатуры со свёрнутыми телами).
  - Сохранение reference вне ModuleExamples/<категория>/ или отсутствие записи в README.
---

# KBPro Module Describer

## Purpose

Создать **reproduction-grade** описание существующего игрового модуля KBPro в виде `*_reference.md`
по протоколу HowToDescribeModule.md (v2.0). Главная цель — не просто обзор, а описание, **достаточное чтобы по нему собрать похожий модуль без открытия исходников** (reproduction test).

## When To Use

* «Задокументируй модуль X» / «сделай reference для модуля».
* После реализации модуля (до PR) или при code review чужого модуля.
* Когда нужно пополнить базу образцов `ModuleExamples/` для создания модулей по подобию.

## Do Not Use

* Для **создания** нового модуля — этот скилл только описывает существующий. Создание ведут builder-скиллы (см. ниже) + `HowToCreateModule.md` / `HowToAutoGenerateModule_ForAI.md`.
* Для генерации Unity YAML (.prefab/.asset) текстом.

## Связанные скиллы (создание модуля по этому reference)

Adaptation Guide (секция 7) маршрутизирует на специализированные builder-скиллы — описание само не строит код, а указывает, чем строить:

| Что строится | Скилл |
|--------------|-------|
| GameComponent (вью) | `kbpro-module-components-builder` |
| LogicSystem (логика) | `kbpro-module-systems-builder` |
| Класс модуля (AbstractGameModule) | `kbpro-node-module-builder` |
| ModuleInput / ModuleOutput | `kbpro-module-input-builder`, `kbpro-module-output-builder` |
| DI / LazySrv / сервисы | `kbpro-di-expert` |
| Сценарий (граф/ноды/связи) | `kbpro-scenario-graph-builder`, `kbpro-scenario-node-builder`, `kbpro-scenario-link-builder` |
| Префаб + провайдеры | `unity-prefab-builder` |
| UI HUD | `unity-ui-mvp-builder` |
| Анимации / VFX | `unity-animation-visuals-expert`, `unity-vfx-particle-author` |
| Аудит lifecycle / проверка | `kbpro-lifecycle-auditor`, `unity-playmode-validation` |

> Поэтому reference обязан давать builder-скиллам всё нужное на вход: 3.10 скелет (для components/systems/node-builder), 3.8 карта инъекций (для prefab-builder), 3bis Constants/IO (для node/input/output/scenario builders).

## Core Rules

1. **Reproduction test — критерий готовности.** Описание готово только если по нему + `HowToCreateModule.md` можно воссоздать похожий рабочий модуль и полностью настроенный префаб сцены БЕЗ исходников. См. [reproduction-quality-bar.md](file://references/reproduction-quality-bar.md).
2. **Сначала прочитать ВЕСЬ код модуля и структуру префаба** (Module/System/Component/SO/prefab с точными настройками инспектора) — затем писать. Не додумывать: непонятное → `[НЕИЗВЕСТНО — уточнить]`.
3. **Обязательны reproduction-секции**: 3.6 полный конфиг, 3.7 префаб с детальной иерархией GameObjects, типами прикрепленных компонентов и их сериализованными свойствами/значениями/ссылками, 3.8 карта `[InjectComponent] ↔ _componentProviders`, 3.9 порядок `base.*`, **3.10 скелет классов (сигнатуры со свёрнутыми телами)**, 3bis Контракт интеграции (Constants/ModuleInput-Output/**переиспользуемые примитивы**/запуск), 7 Adaptation Guide (таблица замен + «взять as-is»). Эталон заполнения — [examples/Example_reference.md](file://examples/Example_reference.md).
4. **Многоэтапные модули описываются по секциям этапов/файлам этапов**, каждый из которых содержит собственную детальную раскладку GameObjects префаба, которые относятся к этому этапу, их настройки и инъекции.
5. **Мермейд-диаграммы**: Минимум 2 Mermaid-диаграммы (flowchart/sequence + gantt).
6. **Имя и место**: `[ModuleName]_reference.md` в `ModuleExamples/<категория>/`; добавить строку в `ModuleExamples/README.md`.
7. **Только ключевые фрагменты кода** (3–20 строк); скелет классов (3.10) — только объявления, тела свёрнуты `// ...`.
8. **Сигнатуры примитивов — не выдумывать**: сверять по [references/kbpro-primitives-glossary.md](file://references/kbpro-primitives-glossary.md) или исходнику; читать код по [references/source-reading-checklist.md](file://references/source-reading-checklist.md).

## Workflow

1. **Изучи код модуля целиком** по [source-reading-checklist.md](file://references/source-reading-checklist.md): `*Module.cs`, все `*System.cs`, `*Component.cs`, SO-конфиги, структуру prefab. Зафиксируй системы, компоненты, инъекции, события, конфиг-поля, Constants, переиспользуемые примитивы.
2. **Определи класс задач** модуля (для Adaptation Guide) и похожие модули в `ModuleExamples/`.
3. **Заполни шаблон** из HowToDescribeModule.md (эталон — `examples/Example_reference.md`): метаданные → 1 Plain → 2 Флоу (+flowchart) → 3 Структура (3.1–3.10) → 3bis Контракт интеграции → 4 Взаимодействие (sequence) → 5 Время (gantt) → 6 Заметки → 7 Adaptation Guide.
4. **Прогон reproduction test**: проверь, что Constants, инъекции, конфиг, способ запуска и таблица замен достаточны для воссоздания.
5. **Сохрани** `[ModuleName]_reference.md` (UTF-8 с BOM) в `ModuleExamples/<категория>/`, добавь строку в `README.md`.
6. **Проверь чеклист** «Правило 6» из HowToDescribeModule.md.

## Output Format

* Один файл `[ModuleName]_reference.md` по шаблону HowToDescribeModule.md (все секции, ≥2 Mermaid).
* Обновлённая строка в `ModuleExamples/README.md`.
* Краткий отчёт: что задокументировано, какие места помечены `[НЕИЗВЕСТНО]`, прошёл ли reproduction test.

## Test Prompts

1. «Задокументируй модуль `HousesFoundationModule` как reference — чтобы по нему можно было собрать похожий модуль фундамента под новое ТЗ.»
2. «Сделай reproduction-grade reference для модуля диагностики: с контрактом интеграции, картой инъекций и Adaptation Guide.»
3. «Проверь существующий `TrainCleaningModule_reference.md` на reproduction test и дополни недостающие секции (3bis/3.8/7).»
