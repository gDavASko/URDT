# Антипаттерны при разработке AbstractGameModule

Этот справочник описывает частые ошибки и нарушения архитектурных стандартов при написании классов-оркестраторов `AbstractGameModule`.

---

## 1. Написание игровой логики прямо в модуле

### ❌ НЕПРАВИЛЬНО
```csharp
public class MyGameModule : AbstractGameModule
{
    [SerializeField] private float _gameTimer = 30f;
    private int _score;

    private void Update()
    {
        // Запрещено писать логику тиков и игрового процесса прямо в модуле!
        _gameTimer -= Time.deltaTime;
        if (_gameTimer <= 0)
        {
            OnComplete();
        }
    }

    public void AddScore()
    {
        _score++;
    }
}
```

### ❓ Почему это плохо
Нарушает принцип SRP и концепцию архитектуры KBPro. Класс модуля должен быть исключительно легким оркестратором (диспетчером), координирующим работу логических систем и внешнего UI. Вся бизнес-логика обязана находиться в классах `LogicSystem`.

###  ПРАВИЛЬНО
Модуль делегирует запуск и обновление логическим системам, а сам только подписывается на событие завершения.
```csharp
public class MyGameModule : AbstractGameModule
{
    private MyTimerSystem _timerSystem;

    public override void Initialize()
    {
        _timerSystem = GetSystem<MyTimerSystem>();
        _timerSystem.OnTimerExpired += OnComplete;

        base.Initialize();
    }

    public override void Dispose()
    {
        if (_timerSystem != null)
            _timerSystem.OnTimerExpired -= OnComplete;

        base.Dispose();
    }
}
```

---

## 2. Обращение к RuntimeContext в конструкторе класса

### ❌ НЕПРАВИЛЬНО
```csharp
public class MyGameModule : AbstractGameModule
{
    private MyModuleInput _input;

    public MyGameModule()
    {
        // Ошибка: RuntimeContext еще не проинициализирован во время вызова конструктора!
        _input = RuntimeContext.ModuleInput as MyModuleInput; 
    }
}
```

### ❓ Почему это плохо
Объект `RuntimeContext` выделяется и связывается с модулем непосредственно перед вызовом `Initialize()`. Попытка обратиться к нему в конструкторе вызовет `NullReferenceException`.

###  ПРАВИЛЬНО
Считывайте данные строго внутри перегрузки метода `Initialize()`.
```csharp
public override void Initialize()
{
    var input = RuntimeContext.ModuleInput as MyModuleInput;
    if (input != null)
    {
        // Применяем настройки к системам
    }

    base.Initialize();
}
```

---

## 3. Модификация входных параметров (ModuleInput)

### ❌ НЕПРАВИЛЬНО
```csharp
public override void Initialize()
{
    var input = RuntimeContext.ModuleInput as MyModuleInput;
    if (input != null)
    {
        // Ошибка: Входные параметры должны быть строго read-only!
        input.DrillSpeed = 100f; 
    }

    base.Initialize();
}
```

### ❓ Почему это плохо
Класс `ModuleInput` представляет собой неизменяемые входные данные (контракт), переданные от предыдущих шагов ScenarioGraph. Прямое изменение этих параметров может сломать логику переходов в графе и усложнить отладку.

###  ПРАВИЛЬНО
Если вам нужно изменить значение во время выполнения, скопируйте его в локальное поле логической системы.

---

## 4. Подписка на EventBus без отписки в Dispose()

### ❌ НЕПРАВИЛЬНО
```csharp
public class MyGameModule : AbstractGameModule
{
    private EventBinding<EventScriptBreak> _eBreak;

    public override void Initialize()
    {
        _eBreak = new EventBinding<EventScriptBreak>(OnBreak);
        EventBus<EventScriptBreak>.Register(_eBreak); // Подписка сделана

        base.Initialize();
    }

    // Метод Dispose() отсутствует или не содержит Unregister
}
```

### ❓ Почему это плохо
Глобальная шина событий `EventBus` хранит статические ссылки на биндинги. Если модуль уничтожается, а биндинг не отписан, объект модуля останется висеть в памяти, вызывая утечку памяти. Кроме того, при возникновении события уничтоженный модуль попытается выполниться, что приведет к фатальным исключениям.

###  ПРАВИЛЬНО
Всегда очищайте подписки в `Dispose()`.
```csharp
public override void Dispose()
{
    EventBus<EventScriptBreak>.Unregister(_eBreak);
    base.Dispose();
}
```
