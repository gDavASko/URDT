# Примеры C# LogicSystems

В этом справочнике приведены готовые шаблоны логических систем (`LogicSystem`), соответствующих стандартам архитектуры KBPro.

---

## 1. Базовая система обработки шага (Flow / State System)

Используется для обработки одиночного шага (например, клик по кнопке, простая анимация, завершение).

```csharp
using System;
using KBP.CORE;
using KBP.CORE.MODULES;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    [Serializable]
    public class SimpleClickFlowSystem : LogicSystem
    {
        // Внедрение визуального компонента
        [InjectComponent] private ClickableSpotComponent _clickableSpot = null;

        // Внешние сервисы
        private readonly LazySrv<ISoundSystem> _sound = new();

        private bool _isInitialized;

        public override void Initialize()
        {
            // Подписка на действия вью-слоя
            _clickableSpot.OnSpotClicked += HandleSpotClicked;
            
            base.Initialize(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }

        public override void Start()
        {
            base.Start(); // ОБЯЗАТЕЛЬНО ПЕРВЫМ
            
            _isInitialized = true;
            _clickableSpot.SetSpotVisible(true);
        }

        private void HandleSpotClicked()
        {
            if (!_isInitialized) return;

            // Воспроизведение звука клика
            _sound.Value.PlaySound("click_sfx", MixerType.Sound);
            
            // Скрытие активной точки
            _clickableSpot.SetSpotVisible(false);

            // Оповещение оркестратора модуля о завершении работы системы
            _onComplete?.Invoke();
        }

        public override void Dispose()
        {
            // Отписка во избежание утечек
            if (_clickableSpot != null)
            {
                _clickableSpot.OnSpotClicked -= HandleSpotClicked;
            }

            // Высвобождение сервисов
            _sound.Dispose();

            base.Dispose(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }
    }
}
```

---

## 2. Система с покадровым обновлением и паузой (IUpdatable, ISuspendable)

Подходит для систем с отсчетом времени (таймеры), перемещением объектов по формулам или слежением за физикой.

```csharp
using System;
using KBP.CORE;
using UnityEngine;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    [Serializable]
    public class CountdownTimerSystem : LogicSystem, IUpdatable, ISuspendable
    {
        [InjectComponent] private ModuleHUDComponent _hudComponent = null;

        [SerializeField] private float _totalDuration = 30f;

        public Action OnTimerExpired;

        private float _timeRemaining;
        private bool _isTimerRunning;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Start()
        {
            base.Start();
            
            _timeRemaining = _totalDuration;
            _isTimerRunning = true;
            
            _hudComponent.UpdateTimer(_timeRemaining);
        }

        // Вызывается автоматически каждый кадр через TimerService
        public void OnUpdate()
        {
            if (!_isTimerRunning) return;

            _timeRemaining -= Time.deltaTime;
            _timeRemaining = Mathf.Max(0f, _timeRemaining);

            // Обновляем визуальный интерфейс
            _hudComponent.UpdateTimer(_timeRemaining);

            if (_timeRemaining <= 0.001f)
            {
                _isTimerRunning = false;
                OnTimerExpired?.Invoke();
                _onComplete?.Invoke();
            }
        }

        // Реализация ISuspendable для интеграции с паузой
        public void Suspend()
        {
            _isTimerRunning = false;
        }

        public void Resume()
        {
            _isTimerRunning = true;
        }

        public override void Dispose()
        {
            OnTimerExpired = null;
            base.Dispose();
        }
    }
}
```

---

## 3. Корневая система со сложной структурой подсистем (Stage/Helper Pattern)

Если система выполняет слишком много задач, её код разделяют на чистые C#-хелперы, инстанцируемые в `Initialize()`. Это предотвращает перегрузку инспектора Unity десятками мелких `LogicSystem`.

```csharp
using System;
using KBP.CORE;
using UnityEngine;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    [Serializable]
    public class ComplexGameSystem : LogicSystem
    {
        [InjectComponent] private ClickableSpotComponent[] _spots = null;
        [InjectComponent] private ModuleHUDComponent _hud = null;

        // Чистые C# подсистемы
        private SpotManagerHelper _spotManager;
        private ScoreTrackerHelper _scoreTracker;

        public override void Initialize()
        {
            // Инстанцируем чистые хелперы, передавая им зависимости
            _scoreTracker = new ScoreTrackerHelper();
            _spotManager = new SpotManagerHelper(_spots, HandleSpotProcessed);

            // Подписываемся на события подсистем
            _scoreTracker.OnScoreChanged += HandleScoreChanged;

            base.Initialize(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }

        public override void Start()
        {
            base.Start();
            
            _spotManager.StartTracking();
            _hud.SetProgress(0, _spots.Length);
        }

        private void HandleSpotProcessed()
        {
            _scoreTracker.AddScore(1);
        }

        private void HandleScoreChanged(int currentScore)
        {
            _hud.SetProgress(currentScore, _spots.Length);

            if (currentScore >= _spots.Length)
            {
                _onComplete?.Invoke();
            }
        }

        public override void Dispose()
        {
            if (_scoreTracker != null)
            {
                _scoreTracker.OnScoreChanged -= HandleScoreChanged;
                _scoreTracker.Dispose();
            }

            if (_spotManager != null)
            {
                _spotManager.Dispose();
            }

            base.Dispose();
        }
    }

    // --- ЧИСТЫЕ C# ВСПЕМОГАТЕЛЬНЫЕ КЛАССЫ (HELPERS) ---

    public class SpotManagerHelper : IDisposable
    {
        private readonly ClickableSpotComponent[] _spots;
        private readonly Action _onSpotProcessed;

        public SpotManagerHelper(ClickableSpotComponent[] spots, Action onSpotProcessed)
        {
            _spots = spots;
            _onSpotProcessed = onSpotProcessed;
        }

        public void StartTracking()
        {
            foreach (var spot in _spots)
            {
                spot.OnSpotClicked += HandleSpotClicked;
            }
        }

        private void HandleSpotClicked()
        {
            _onSpotProcessed?.Invoke();
        }

        public void Dispose()
        {
            foreach (var spot in _spots)
            {
                if (spot != null)
                {
                    spot.OnSpotClicked -= HandleSpotClicked;
                }
            }
        }
    }

    public class ScoreTrackerHelper : IDisposable
    {
        public Action<int> OnScoreChanged;
        public int Score { get; private set; }

        public void AddScore(int amount)
        {
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }

        public void Dispose()
        {
            OnScoreChanged = null;
        }
    }
}
```
