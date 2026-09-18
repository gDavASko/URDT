# Примеры Dependency Injection (DI) в C#

В этом справочнике приведены готовые шаблоны создания, регистрации и потребления сервисов в соответствии со стандартами KBPro.

---

## 1. Шаблон создания и регистрации нового сервиса

### Шаг 1: Определение интерфейса
```csharp
using KBP.CORE;
using System;

namespace KBP.RAILWAY_COW.SERVICES
{
    public interface IPlayerScoreService : IService
    {
        int CurrentScore { get; }
        void AddPoints(int amount);
        void ResetScore();
    }
}
```

### Шаг 2: Реализация сервиса
```csharp
using System;
using KBP.CORE;
using UnityEngine;

namespace KBP.RAILWAY_COW.SERVICES
{
    public class PlayerScoreService : IPlayerScoreService
    {
        // Ключ-тип, под которым сервис будет храниться в ServiceLocator
        public Type RegisterType => typeof(IPlayerScoreService);

        public int CurrentScore { get; private set; }

        public void Initialize()
        {
            CurrentScore = 0;
            Debug.Log("[ScoreService] Инициализирован.");
        }

        public void AddPoints(int amount)
        {
            CurrentScore += amount;
        }

        public void ResetScore()
        {
            CurrentScore = 0;
        }

        public void Dispose()
        {
            // Очистка ресурсов, если есть
            Debug.Log("[ScoreService] Освобожден.");
        }
    }
}
```

### Шаг 3: Регистрация сервиса в Bootstrap (ServiceAccessor)
Обычно сервисы регистрируются автоматически на основе конфигураций ScriptableObject, но вы можете сделать это и вручную:
```csharp
var scoreService = new PlayerScoreService();
scoreService.Initialize();

// Регистрация в глобальном локаторе
ServiceLocator.Register<IPlayerScoreService>(scoreService);
```

---

## 2. Потребление сервиса через ленивый LazySrv

```csharp
using System;
using KBP.CORE;
using UnityEngine;

namespace KBP.RAILWAY_COW.GAMEPLAY
{
    public class GameCompletionFlow : IDisposable
    {
        // Объявление оберток ленивых сервисов
        private readonly LazySrv<IPlayerScoreService> _scoreService = new();
        private readonly LazySrv<ISoundSystem> _soundSystem = new();

        public void CompleteGameStep()
        {
            // Обращение к сервисам через .Value
            _scoreService.Value.AddPoints(10);
            
            int current = _scoreService.Value.CurrentScore;
            Debug.Log($"Текущие очки: {current}");

            _soundSystem.Value.PlaySound("success_sfx", MixerType.Sound);
        }

        public void Dispose()
        {
            // Обязательная очистка в Dispose класса
            _scoreService.Dispose();
            _soundSystem.Dispose();
        }
    }
}
```

---

## 3. Настройка связей в VContainer / Zenject (при их использовании)

Если в проекте для некоторых модулей применяется Zenject или VContainer (вместо или поверх `ServiceLocator`):

### Пример Zenject Installer:
```csharp
using Zenject;

namespace KBP.RAILWAY_COW.DI
{
    public class ModuleDependenciesInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // Привязка интерфейса к конкретной реализации в рамках синглтона области видимости
            Container
                .Bind<IPlayerScoreService>()
                .To<PlayerScoreService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
```
> [!NOTE]
> В проектах KBPro с Zenject/VContainer отдавайте предпочтение **Constructor Injection** (инъекции через конструктор) перед инъекцией через поля `[Inject]`, так как это упрощает изолированное тестирование.
