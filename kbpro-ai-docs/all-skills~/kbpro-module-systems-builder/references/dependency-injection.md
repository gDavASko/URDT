# Внедрение зависимостей (DI) в LogicSystem

Этот справочник описывает механизмы инъекции зависимостей (Dependency Injection) в логических системах платформы KBPro.

---

## 1. Внедрение визуальных компонентов (`[InjectComponent]`)

`[InjectComponent]` — это атрибут фреймворка, который автоматически внедряет ссылку на соответствующий `GameComponent`, находящийся в префабе модуля.

- Инъекция происходит через рефлексию **перед** вызовом метода `Initialize()`.
- Поля должны быть приватными.
- Допускается инъекция одиночного компонента или массива компонентов (`MyComponent[]`).

```csharp
using KBP.CORE;

public class TargetSystem : LogicSystem
{
    // Одиночный компонент
    [InjectComponent] private TargetSpotComponent _spotComponent = null;

    // Массив компонентов (все компоненты этого типа на префабе)
    [InjectComponent] private TargetSpotComponent[] _allSpots = null;
}
```
> [!IMPORTANT]
> Для успешного внедрения все компоненты должны быть зарегистрированы в списке `_componentProviders` в инспекторе оркестратора модуля.

---

## 2. Связывание систем между собой (`[InjectSystems]`)

Если одной логической системе нужно обратиться к другой системе внутри этого же модуля, используется метод конструирования, помеченный атрибутом `[InjectSystems]`.

- Этот метод автоматически вызывается фреймворком при инициализации модуля.
- Вы можете передать в параметры метода любые другие логические системы, зарегистрированные в оркестраторе модуля.

```csharp
using KBP.CORE;

public class CleaningFlowSystem : LogicSystem
{
    private DirtCounterSystem _counterSystem;
    private GameTimerSystem _timerSystem;

    [InjectSystems]
    private void Construct(DirtCounterSystem counterSystem, GameTimerSystem timerSystem)
    {
        _counterSystem = counterSystem;
        _timerSystem = timerSystem;
    }
}
```

---

## 3. Обращение к глобальным службам (`LazySrv<T>`)

Для взаимодействия с глобальными системами игры (звуковая система, аналитика, локализация, пулинг) используется ленивый сервис `LazySrv<T>`.

- **Запрещено** использовать статические вызовы `ServiceLocator.GetService<T>()` в конструкторах или полях.
- `LazySrv<T>` создает легкую обертку. Обращение к сервису происходит через свойство `.Value`.
- Все объявленные поля `LazySrv<T>` должны освобождаться через вызов `.Dispose()` в методе `Dispose()` системы.

```csharp
using KBP.CORE;

public class AudioLogicSystem : LogicSystem
{
    // Ленивая инициализация сервиса звуков
    private readonly LazySrv<ISoundSystem> _soundSystem = new();

    public void PlayActionSound()
    {
        // Разрешение зависимости при первом обращении
        _soundSystem.Value.PlaySound("action_click", MixerType.Sound);
    }

    public override void Dispose()
    {
        // Обязательная очистка во избежание утечек памяти
        _soundSystem.Dispose();
        
        base.Dispose();
    }
}
```
