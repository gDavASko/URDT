# Связывание данных (Module I/O Binding) в AbstractGameModule

Этот справочник описывает стандарты привязки входных и выходных параметров к игровым модулям в ScenarioGraph.

---

## 1. Атрибут `[ModuleIO]`

Игровой модуль, поддерживающий ScenarioGraph, должен явно объявить типы своих входных и выходных данных с помощью атрибута `[ModuleIO]`.

- Атрибут накладывается на сам класс модуля.
- Первым параметром передается тип входного класса (наследник `ModuleInput`).
- Вторым параметром передается тип выходного класса (наследник `ModuleOutput`).

```csharp
using KBP.CORE.MODULES;

namespace KBP.DENTISTRY_COW.EXAMPLE
{
    [ModuleIO(typeof(ExampleInput), typeof(ExampleOutput))]
    public sealed class ExampleModule : AbstractGameModule
    {
        // Класс модуля
    }
}
```

---

## 2. Разрешение контекста данных (`RuntimeContext`)

Входные данные, переданные в ноду ScenarioGraph, автоматически разрешаются `ModuleStateMachine` перед запуском модуля.

- Ссылка на входные данные доступна через `RuntimeContext.ModuleInput`.
- Ссылка на выходной контейнер доступна через `RuntimeContext.ModuleOutput`.
- **Запрещено** обращаться к `RuntimeContext` в конструкторе класса, так как он заполняется непосредственно перед фазой `Initialize()`.

```csharp
private ExampleInput _input;
private ExampleOutput _output;

public override void Initialize()
{
    // Безопасное приведение типов в Initialize
    _input = RuntimeContext.ModuleInput as ExampleInput;
    _output = RuntimeContext.ModuleOutput as ExampleOutput;

    // Применяем входные параметры к логическим системам
    if (_input != null)
    {
        GetSystem<ExampleSystem>().SetupSpeed(_input.Speed);
    }

    base.Initialize();
}
```

---

## 3. Обновление входов во время выполнения (`IInputUpdateAware`)

Если входные параметры в графе могут динамически изменяться в процессе работы модуля (например, внешние триггеры или параметры сценария):

1. Реализуйте интерфейс `IInputUpdateAware` на классе модуля.
2. Фреймворк вызовет метод `SyncInput()`, когда данные обновятся.

```csharp
using KBP.CORE.MODULES;

namespace KBP.DENTISTRY_COW.EXAMPLE
{
    public sealed class ExampleModule : AbstractGameModule, IInputUpdateAware
    {
        public void SyncInput()
        {
            // Считываем обновленные параметры
            var freshInput = RuntimeContext.ModuleInput as ExampleInput;
            if (freshInput != null)
            {
                GetSystem<ExampleSystem>().UpdateSpeed(freshInput.Speed);
            }
        }
    }
}
```

---

## 4. Запись результатов на выходе (OnComplete)

Перед вызовом `OnComplete()` модуль обязан заполнить поля выходного DTO, чтобы передать результаты выполнения дальше по графу.

```csharp
private void HandleFlowFinished(bool isVictory)
{
    // Заполняем выход
    if (_output != null)
    {
        _output.IsVictory = isVictory;
        _output.Score = GetSystem<ExampleSystem>().CurrentScore;
    }

    // Завершаем модуль
    OnComplete();
}
```
