# Примеры C# кода (Module Code Examples)

В этом справочнике приведены золотые стандарты написания C# кода для игрового модуля KBPro на примере модуля **WindowCleaning**.

---

## 1. Слой данных (Module Input & Output)

```csharp
using System;
using KBP.CORE.MODULES;
using UnityEngine;

namespace KBP.RAILWAY_COW.WINDOW_CLEANING
{
    [Serializable]
    public sealed class WindowCleaningInput : ModuleInput
    {
        [Tooltip("Начальная сложность очистки (скорость стирания грязи)")]
        [SerializeField] private float _scrubSensitivity = 1f;

        public float ScrubSensitivity => _scrubSensitivity;

        public override ModuleInput Clone()
        {
            return new WindowCleaningInput { _scrubSensitivity = this._scrubSensitivity };
        }
    }

    [Serializable]
    public sealed class WindowCleaningOutput : ModuleOutput
    {
        [Tooltip("Итоговое время, затраченное игроком на чистку окон")]
        [SerializeField] private float _timeTakenSeconds;

        public float TimeTakenSeconds
        {
            get => _timeTakenSeconds;
            set => _timeTakenSeconds = value;
        }

        public override ModuleOutput Clone()
        {
            return new WindowCleaningOutput { _timeTakenSeconds = this._timeTakenSeconds };
        }
    }
}
```

---

## 2. Визуальный слой (GameComponent)

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KBP.CORE.MODULES;
using DG.Tweening;

namespace KBP.RAILWAY_COW.WINDOW_CLEANING
{
    public class WindowCleaningComponent : GameComponent<WindowCleaningComponent>, 
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer[] _dirtZones;
        [SerializeField] private ParticleSystem _sparkleEffect;

        // События для логической системы
        public Action OnScrubStarted;
        public Action OnScrubStopped;
        public Action<int, Vector2> OnScrubDrag; // Передает индекс зоны и позицию

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public void SetZoneDirtAlpha(int zoneIndex, float alpha)
        {
            if (zoneIndex >= 0 && zoneIndex < _dirtZones.Length)
            {
                Color color = _dirtZones[zoneIndex].color;
                color.a = Mathf.Clamp01(alpha);
                _dirtZones[zoneIndex].color = color;
            }
        }

        public void PlaySparkleEffect()
        {
            if (_sparkleEffect != null)
            {
                _sparkleEffect.Play();
            }
        }

        // --- Обработка ввода Unity EventSystem ---

        public void OnPointerDown(PointerEventData eventData)
        {
            OnScrubStarted?.Invoke();
            ProcessScrub(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnScrubStopped?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            ProcessScrub(eventData);
        }

        private void ProcessScrub(PointerEventData eventData)
        {
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(eventData.position);
            Vector2 localPos = transform.InverseTransformPoint(worldPos);

            // Пример простой логики: определяем, в какой квадрант (зону) попал палец
            int zoneIndex = DetermineZoneByLocalPosition(localPos);
            if (zoneIndex != -1)
            {
                OnScrubDrag?.Invoke(zoneIndex, localPos);
            }
        }

        private int DetermineZoneByLocalPosition(Vector2 localPos)
        {
            // Упрощенное деление на 4 зоны (квадранты)
            if (localPos.x < 0 && localPos.y >= 0) return 0; // Верхний левый
            if (localPos.x >= 0 && localPos.y >= 0) return 1; // Верхний правый
            if (localPos.x < 0 && localPos.y < 0) return 2; // Нижний левый
            if (localPos.x >= 0 && localPos.y < 0) return 3; // Нижний правый
            return -1;
        }

        private void OnDestroy()
        {
            // Очистка событий C#
            OnScrubStarted = null;
            OnScrubStopped = null;
            OnScrubDrag = null;
        }
    }
}
```

---

## 3. Логический слой (LogicSystem)

```csharp
using KBP.CORE;
using UnityEngine;
using System;

namespace KBP.RAILWAY_COW.WINDOW_CLEANING
{
    public class WindowCleaningFlowSystem : LogicSystem, IUpdatable
    {
        // Инъекция компонента с префаба модуля
        [InjectComponent] private WindowCleaningComponent _cleaningComponent = null;

        // Внедрение зависимостей через LazySrv
        private LazySrv<ISoundSystem> _soundSystem = new LazySrv<ISoundSystem>();

        private float[] _zonesDirt = new float[] { 1f, 1f, 1f, 1f }; // 1.0 - полностью грязное
        private bool _isScrubbing;
        private bool _isCompleted;
        private float _sensitivity = 1f;
        private float _timer;

        public float TotalCleanlinessPercent { get; private set; }

        public void SetupSensitivity(float sensitivity)
        {
            _sensitivity = sensitivity;
        }

        public override void Initialize()
        {
            // Подписка на события визуального компонента
            _cleaningComponent.OnScrubStarted += HandleScrubStarted;
            _cleaningComponent.OnScrubStopped += HandleScrubStopped;
            _cleaningComponent.OnScrubDrag += HandleScrubDrag;

            base.Initialize(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }

        public override void Start()
        {
            _timer = 0f;
            base.Start();
        }

        public void OnUpdate()
        {
            if (_isCompleted) return;

            _timer += Time.deltaTime;
        }

        private void HandleScrubStarted()
        {
            if (_isCompleted) return;

            _isScrubbing = true;
            // Запустить цикличный звук трения стекла
            _soundSystem.Value.PlaySound("sfx_window_scrub", isLoop: true, volume: 0.8f, MixerType.Sound);
        }

        private void HandleScrubStopped()
        {
            _isScrubbing = false;
            // Остановить звук трения
            _soundSystem.Value.StopSound("sfx_window_scrub");
        }

        private void HandleScrubDrag(int zoneIndex, Vector2 localPos)
        {
            if (!_isScrubbing || _isCompleted) return;

            // Уменьшаем грязь в указанной зоне на основе чувствительности и времени кадра
            if (zoneIndex >= 0 && zoneIndex < _zonesDirt.Length)
            {
                _zonesDirt[zoneIndex] -= Time.deltaTime * 0.5f * _sensitivity;
                _zonesDirt[zoneIndex] = Mathf.Max(0f, _zonesDirt[zoneIndex]);
                
                // Передаем изменения обратно в визуальный слой
                _cleaningComponent.SetZoneDirtAlpha(zoneIndex, _zonesDirt[zoneIndex]);
            }

            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            float totalDirt = 0f;
            foreach (float dirt in _zonesDirt)
            {
                totalDirt += dirt;
            }

            TotalCleanlinessPercent = (1f - (totalDirt / _zonesDirt.Length)) * 100f;

            if (totalDirt <= 0.01f)
            {
                _isCompleted = true;
                HandleScrubStopped();
                
                // Воспроизвести звук победы
                _soundSystem.Value.PlaySound("sfx_window_clean_success", isLoop: false, volume: 1f, MixerType.Sound);
                
                // Запустить блеск на компоненте
                _cleaningComponent.PlaySparkleEffect();

                // Сигнализируем оркестратору о завершении
                _onComplete?.Invoke();
            }
        }

        public float GetTimeTaken() => _timer;

        public override void Dispose()
        {
            // Отписка от событий во избежание утечек
            if (_cleaningComponent != null)
            {
                _cleaningComponent.OnScrubStarted -= HandleScrubStarted;
                _cleaningComponent.OnScrubStopped -= HandleScrubStopped;
                _cleaningComponent.OnScrubDrag -= HandleScrubDrag;
            }

            // Обязательное освобождение LazySrv
            _soundSystem.Dispose();

            base.Dispose(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }
    }
}
```

---

## 4. Оркестратор модуля (AbstractGameModule)

```csharp
using KBP.CORE;
using KBP.CORE.EVENTS;
using UnityEngine;

namespace KBP.RAILWAY_COW.WINDOW_CLEANING
{
    public class WindowCleaningModule : AbstractGameModule
    {
        // Кэшированная ссылка на логическую систему
        private WindowCleaningFlowSystem _flowSystem;

        // Входные/выходные данные (Module IO)
        private WindowCleaningInput _moduleInput;
        private WindowCleaningOutput _moduleOutput;

        public override void Initialize()
        {
            // 1. Получаем систему из списка систем модуля
            _flowSystem = GetSystem<WindowCleaningFlowSystem>();

            // 2. Подписываемся на завершение логической системы
            _flowSystem.OnComplete(HandleModuleComplete);

            // 3. Считываем и применяем входные параметры (ModuleIO)
            if (TryGetInput<WindowCleaningInput>(out var input))
            {
                _moduleInput = input;
                _flowSystem.SetupSensitivity(_moduleInput.ScrubSensitivity);
            }

            // 4. Инициализируем выходной класс
            _moduleOutput = new WindowCleaningOutput();

            // 5. Вызываем инициализацию базового класса (запуск DI и Initialize в системах)
            base.Initialize(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }

        public override void Startable()
        {
            base.Startable(); // ОБЯЗАТЕЛЬНО ПЕРВЫМ (вызывает Start() систем)

            // Открываем HUD
            EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.GAME));
        }

        private void HandleModuleComplete()
        {
            // Заполняем выходные данные
            if (_moduleOutput != null && _flowSystem != null)
            {
                _moduleOutput.TimeTakenSeconds = _flowSystem.GetTimeTaken();
                WriteOutput(_moduleOutput); // Записываем результаты в контекст
            }

            OnComplete();
        }

        public override void OnComplete()
        {
            // Закрываем HUD
            EventBus<EventHideComponent>.Raise(new EventHideComponent(Constants.UICOMPONENT.GAME));

            base.OnComplete(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }

        public override void Dispose()
        {
            // Любая локальная очистка и отписки
            
            base.Dispose(); // ОБЯЗАТЕЛЬНО ПОСЛЕДНИМ
        }
    }
}
```

---

## 5. Скрипт сборки префаба (Editor Auto-Builder)

Этот скрипт размещается во временной папке `Editor/` модуля и позволяет собрать иерархию и настроить все ссылки в инспекторе одной кнопкой, после чего самоуничтожается.

```csharp
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using KBP.CORE.MODULES;
using KBP.RAILWAY_COW.WINDOW_CLEANING;

namespace KBP.RAILWAY_COW.WINDOW_CLEANING.EDITOR
{
    public static class WindowCleaningAutoBuilder
    {
        [MenuItem("KBPro/Build/Assemble WindowCleaning Module")]
        public static void AssembleModule()
        {
            // 1. Создаем корневой объект
            GameObject rootGo = new GameObject("WindowCleaningModule");
            var module = rootGo.AddComponent<WindowCleaningModule>();

            // 2. Создаем визуальный дочерний объект
            GameObject visualGo = new GameObject("WindowCleaningComponent");
            visualGo.transform.SetParent(rootGo.transform);
            var component = visualGo.AddComponent<WindowCleaningComponent>();

            // 3. Настраиваем логические системы в оркестраторе модуля
            // Добавление SerializeReference систем
            var systemsList = new System.Collections.Generic.List<LogicSystem>
            {
                new WindowCleaningFlowSystem()
            };
            
            // Используем рефлексию или прямой доступ к сериализуемому полю _systems
            var systemsField = typeof(AbstractGameModule).GetField("_systems", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (systemsField != null)
            {
                systemsField.SetValue(module, systemsList);
            }

            // 4. Настраиваем Component Providers
            var provider = visualGo.AddComponent<LinkedComponentProvider>();
            var providersList = new System.Collections.Generic.List<ComponentProvider> { provider };
            
            var providersField = typeof(AbstractGameModule).GetField("_componentProviders", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (providersField != null)
            {
                providersField.SetValue(module, providersList);
            }

            // 5. Сохраняем префаб
            string folderPath = "Assets/Railway-cow/Prefabs/Modules";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string prefabPath = Path.Combine(folderPath, "WindowCleaningModule.prefab");
            PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            
            // Уничтожаем временную сцену сборки
            Object.DestroyImmediate(rootGo);

            Debug.Log($"[AutoBuilder] Успешно собран и сохранен префаб: {prefabPath}");
            
            // 6. Самоудаление скрипта сборщика
            DeleteBuilderScript();
        }

        private static void DeleteBuilderScript()
        {
            string scriptPath = "Assets/Railway-cow/Scripts/Modules/WindowCleaning/Editor/WindowCleaningAutoBuilder.cs";
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
                File.Delete(scriptPath + ".meta");
                AssetDatabase.Refresh();
                Debug.Log("[AutoBuilder] Скрипт сборщика успешно удален во избежание захламления проекта.");
            }
        }
    }
}
#endif
```
> [!NOTE]
> Перед использованием скрипта сборщика убедитесь, что в Unity Editor настроена правильная иерархия папок и регенерированы скрипты через `AssetDatabase.Refresh()`.
