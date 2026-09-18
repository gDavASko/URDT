# KBPro Primitives Glossary — переиспользуемые блоки фреймворка

> Чтобы reference был **actionable**: когда в описании упомянут примитив (`LazySrv`, `EventBus`, `DragLogicSystem` и т.п.), адаптирующий агент должен знать его API. Подтверждённые сигнатуры — из `HowToCreateModule.md`. Для примитивов, помеченных «(уточни в исходнике)», обязательно сверить сигнатуру в коде/`EventBus.md`/`ServiceLocator.md` перед использованием — НЕ выдумывать.

## Базовые классы

| Класс | Назначение | Ключевое API |
|-------|-----------|--------------|
| `AbstractGameModule` | MonoBehaviour-оркестратор модуля | `override Initialize()/Startable()/OnComplete()/Dispose()`; `GetSystem<T>()`; поля `_systems`, `_componentProviders`, `_delayToComplete` |
| `InRunnerGameModule` | Модуль внутри Runner | + `await WaitForRunnerReadyAsync()` в `Startable()` |
| `LogicSystem` | Чистый C# слой логики | `override Initialize()/Start()/Dispose()`; поле-коллбек `_onComplete`; метод `OnComplete(Action)` для подписки модулем |
| `GameComponent<T>` | MonoBehaviour-вью (T = сам класс) | только `[SerializeField]` ссылки + простые публичные методы + `Action` события |
| `IUpdatable` | Тик без Unity Update | `void OnUpdate()` — фреймворк сам подпишет на `TimerService.OnTimerUpdate` |

## DI и сервисы

| Примитив | API | Заметки |
|----------|-----|---------|
| `[InjectComponent]` | атрибут на поле `LogicSystem` (одиночный или `[]`) | заполняется фреймворком ПЕРЕД `Initialize()`; требует регистрации компонента в `_componentProviders` |
| `LazySrv<TService>` | `new LazySrv<IService>()`; `.Value.Method(...)`; `.Dispose()` в `Dispose()` | НЕ использовать `ServiceLocator.GetService<T>()` напрямую |
| `LinkedComponentProvider` | провайдер для `_componentProviders`, хранит ссылку на `GameComponent` | без него `[InjectComponent]` → null |
| `GetSystem<T>()` | в `Initialize()` модуля — получить систему из `_systems` | не `[InjectComponent]` для систем |

## События и UI

| Примитив | API |
|----------|-----|
| `EventBus<TEvent>` | `.Raise(new TEvent(...))`; `.Register(binding)`; `.Unregister(binding)` |
| `EventBinding<TEvent>` | `new EventBinding<TEvent>(handler)` — хранить полем, регистрировать в Initialize, снимать в Dispose |
| Показ/скрытие UI | `EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.GAME))`; парный `EventHideComponent` |
| Константы | `Constants.MODULES.*`, `Constants.UICOMPONENT.*`, `Constants.SOUNDS.*` (генерируются из `SOConstantsContainer`) |

## Звук, анимации, твины

| Примитив | API | Заметки |
|----------|-----|---------|
| `ISoundSystem` (через LazySrv) | `PlaySound(id, isLoop, volume, MixerType.Sound)` | id — из Constants/строка |
| `ISoundAccessor` / `_music` | `SafePlay()` / `SafeStop()` | музыка модуля |
| DOTween | `.SetLink(gameObject)` для авто-kill; `SafeKill()` / `MakeSequence()` в `Dispose()` | прямой `.Kill()` — допустимо, но `SafeKill` предпочтительнее |

## Примитивы геймплея (уточни сигнатуру в исходнике перед использованием)

| Примитив | Назначение | Где смотреть |
|----------|-----------|--------------|
| `DragLogicSystem` | drag-взаимодействие (перетаскивание объектов/инструментов) | исходник системы; примеры — `TrainCleaning*`-references |
| `TutorDragToTargets` / `TutorDragByPath` + `EventStartTutor/Pause/Continue/Stop` | подсказки-туторы | `*VoiceSystem`/stage-контроллеры в references |
| `ICameraService` | доступ к камере, зум/пан (Cinemachine) | `ServiceLocator.md`, `TrainCleaningCameraSystem` |
| `ModuleStageApi` | контракт завершения под-модуля: `IsModuleComplete = true` | `ModuleOrchestrator.md` |
| `IsolatedGameModuleService` | `Run(Constants.MODULES.XXX)` — запуск модуля | как запускается модуль |

## Жёсткие правила (из HowToCreateModule.md)
- `base.Initialize()`/`OnComplete()`/`Dispose()` — **последними**; `base.Startable()` — **первым**.
- Нет `Update()` в системах (только `IUpdatable.OnUpdate`), нет `FindObjectOfType`, нет хардкод-строк, нет прямых ссылок на другие модули.
- Каждый `EventBus.Register` → парный `Unregister`; каждый `LazySrv` → `Dispose`.
