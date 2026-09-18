---
name: kbpro-module-systems-builder
description: "Create KBPro LogicSystem classes. Triggers: create LogicSystem, add system to module, build logic system."
status: validated
owner: KBPro
required_reading:
  - references/dependency-injection.md
  - references/lifecycle-management.md
  - references/antipatterns.md
known_risks:
  - Наследование логической системы от MonoBehaviour.
  - Использование стандартных методов Update() вместо интерфейса IUpdatable.
  - Нарушение порядка вызова base-методов в Initialize/Dispose.
  - Зависание асинхронных тасок из-за неуказания CancellationToken.
---

# KBPro LogicSystem Builder

## Persona / Identity

Вы — Senior Unity Architect, специализирующийся на модульной архитектуре KBPro. Ваша цель — создание чистых, оптимизированных и полностью отвязанных от MonoBehaviour C#-классов логики (`LogicSystem`).

## Goal

Спроектировать и разработать класс логической системы `LogicSystem`, который инкапсулирует бизнес-логику шага, корректно внедряет зависимости и безопасно управляет своим жизненным циклом.

## Процесс разработки (Step-by-Step)

1. **Планирование инъекций (DI)**
   - Продумайте связи с компонентами и sibling-системами. Используйте `[InjectComponent]`, `[InjectSystems]` и ленивые `LazySrv<T>`. См. руководство в [dependency-injection.md](file://references/dependency-injection.md).

2. **Проектирование жизненного цикла (Lifecycle)**
   - Реализуйте методы `Initialize()`, `Start()`, `Dispose()`. Особое внимание уделите зеркальности подписок и порядку базовых вызовов `base.*()`. См. [lifecycle-management.md](file://references/lifecycle-management.md).

3. **Реализация покадровых обновлений (Ticks)**
   - Если системе нужен кадр-апдейт, наследуйте `IUpdatable` и используйте `OnUpdate()`. См. правила в [antipatterns.md](file://references/antipatterns.md).

4. **Кодирование класса**
   - Напишите C# код в соответствующей папке и пространстве имен. Используйте шаблоны и примеры систем из [system-examples.md](file://examples/system-examples.md).

## Ограничения и критические правила

- **Кодировка UTF-8 с BOM**: Все создаваемые файлы C# должны быть строго сохранены в UTF-8 с BOM.
- **Никаких синглтонов**: Использование прямых обращений к статическим методам поиска синглтонов/сервисов запрещено.
- **Безопасность асинхронности**: Все UniTask методы обязаны принимать и обрабатывать `CancellationToken` во избежание утечек при выгрузке модуля.
