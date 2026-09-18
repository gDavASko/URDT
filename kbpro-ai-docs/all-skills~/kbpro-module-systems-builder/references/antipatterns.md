# Антипаттерны при разработке LogicSystem

Этот справочник описывает критические ошибки проектирования логических систем в рамках архитектуры KBPro.

---

## 1. Наследование от MonoBehaviour

### ❌ НЕПРАВИЛЬНО
```csharp
using UnityEngine;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    // Системы не должны быть MonoBehaviour компонентами!
    public class TrafficLightSystem : MonoBehaviour 
    {
        // Неверный контракт
    }
}
```

### ❓ Почему это плохо
Суть разделения SOLID/SRP в KBPro состоит в том, что бизнес-логика должна быть отделена от визуального представления Unity. Наследование системы от MonoBehaviour приводит к хаотичному смешиванию логики и визуала, засоряет сцену лишними компонентами и усложняет тестирование.

###  ПРАВИЛЬНО
Наследуйте класс строго от `LogicSystem`.
```csharp
using KBP.CORE;

public class TrafficLightSystem : LogicSystem
{
    // Чистый C# класс, управляемый оркестратором модуля
}
```

---

## 2. Использование стандартного метода Update() без интерфейса

### ❌ НЕПРАВИЛЬНО
```csharp
public class SpawnSystem : LogicSystem
{
    // Этот метод не будет вызываться Unity, так как класс не является MonoBehaviour
    private void Update()
    {
        // Логика не работает
    }
}
```

### ❓ Почему это плохо
Метод `Update()` является магическим методом Unity, который работает только на компонентах сцены. Логическая система не управляется движком Unity напрямую, поэтому такой метод никогда не запустится.

###  ПРАВИЛЬНО
Реализуйте интерфейс `IUpdatable` и метод `OnUpdate()`.
```csharp
using KBP.CORE;

public class SpawnSystem : LogicSystem, IUpdatable
{
    public void OnUpdate()
    {
        // Метод вызывается фреймворком каждый кадр
    }
}
```

---

## 3. Игнорирование CancellationToken в UniTask методах

При запуске асинхронных операций (например, ожидания завершения анимации или задержки спавна) крайне важно передавать CancellationToken.

### ❌ НЕПРАВИЛЬНО
```csharp
public async UniTask VoidSpawnDelay()
{
    // Зависание таски: если модуль будет уничтожен (вызван Dispose),
    // этот метод все равно проснется через 2 секунды и попытается выполнить логику
    await UniTask.Delay(TimeSpan.FromSeconds(2));
    
    _viewComponent.SpawnNextItem(); // NullReferenceException или баг на сцене
}
```

### ❓ Почему это плохо
Если игрок выйдет из модуля во время асинхронного ожидания, объект модуля и его компоненты будут уничтожены (`Destroy`). Проснувшийся через 2 секунды метод попытается обратиться к уже несуществующим объектам, что вызовет ошибки и утечки памяти.

###  ПРАВИЛЬНО
Получайте `CancellationToken` от модуля или используйте токен уничтожения MonoBehaviour-компонента (`GetCancellationTokenOnDestroy()`).
```csharp
using Cysharp.Threading.Tasks;
using System.Threading;

public async UniTask VoidSpawnDelay(CancellationToken cancellationToken)
{
    // Безопасное ожидание: при вызове Dispose таска отменится автоматически
    await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);
    
    if (cancellationToken.IsCancellationRequested) return;

    _viewComponent.SpawnNextItem();
}
```

---

## 4. Нарушение порядка очистки ресурсов в Dispose()

### ❌ НЕПРАВИЛЬНО
```csharp
public override void Dispose()
{
    // base.Dispose() уничтожает дочерние связи.
    // Если вызвать его первым, вы не сможете безопасно отписаться от компонентов
    base.Dispose(); 
    
    _viewComponent.OnSpotClicked -= HandleSpotClick; // Вызовет ошибку, если ссылки уже занулены
}
```

###  ПРАВИЛЬНО
```csharp
public override void Dispose()
{
    if (_viewComponent != null)
    {
        _viewComponent.OnSpotClicked -= HandleSpotClick;
    }

    _soundSystem.Dispose();

    base.Dispose(); // Строго ПОСЛЕДНИМ
}
```
