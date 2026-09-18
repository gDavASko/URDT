# Предотвращение утечек в Async & Reactive коде — KBPro

Асинхронные задачи (`UniTask`) и реактивные подписки (`UniRx`) при некорректном использовании могут оставаться в памяти бесконечно долго, вызывая скрытые утечки памяти, фоновые ошибки и снижение производительности. Ниже приведена инструкция по поиску и предотвращению таких утечек.

## Чек-лист аудита кода на утечки

При ревью кода проверяйте каждый асинхронный и реактивный вызов по следующим пунктам:

### 1. Проверка CancellationTokenSource
* [ ] Для каждого создаваемого экземпляра `CancellationTokenSource` реализован гарантированный вызов `.Cancel()` и `.Dispose()` при завершении жизненного цикла объекта.
* [ ] Поле `CancellationTokenSource` сбрасывается в `null` после утилизации, чтобы избежать повторного обращения.
* [ ] Блок очистки защищен конструкцией `try/catch` или `try/finally` в случае асинхронных инициализаций.

### 2. Проверка UniTask методов
* [ ] Каждый метод с сигнатурой `async UniTask` или `async UniTask<T>` имеет параметр `CancellationToken ct`.
* [ ] Все операции ожидания (`await`) внутри метода передают этот токен отмены дальше.
* [ ] Вызовы `UniTask.Delay`, `UniTask.WaitUntil`, `UniTask.WaitWhile` обязательно содержат параметр `cancellationToken: ct`.
* [ ] Нет вызовов `async void` (заменены на `async UniTaskVoid` с вызовом `.Forget()`).
* [ ] При обращении к объектам Unity (особенно `MonoBehaviour`) после `await` стоит проверка на `this == null` или `destroyCancellationToken.IsCancellationRequested`.

### 3. Проверка UniRx подписок
* [ ] Каждый вызов `.Subscribe()` завершается методом `.AddTo(...)` или регистрируется в `CompositeDisposable`.
* [ ] `CompositeDisposable` очищается через `.Clear()` или `.Dispose()` в методе `Dispose()` (для чистых классов) или `OnDestroy()` (для MonoBehaviour).
* [ ] Подписки на статические события или глобальные сервисы обязательно утилизируются.

---

## Паттерны безопасной очистки ресурсов

### Шаблон CancellationTokenSource в LogicSystem
```csharp
public class MyLogicSystem : LogicSystem, IDisposable
{
    private CancellationTokenSource _cts;

    public override void Initialize()
    {
        base.Initialize();
        _cts = new CancellationTokenSource();
        
        // Запуск фонового процесса
        ProcessGameLoopAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid ProcessGameLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
                // Игровая логика...
            }
        }
        catch (OperationCanceledException)
        {
            // Ожидаемая отмена операции
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public override void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        base.Dispose();
    }
}
```

### Предотвращение утечек при работе с UniRx в MonoBehaviour
```csharp
public class MyGameComponent : MonoBehaviour
{
    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    private void Awake()
    {
        // Подписка привязана к CompositeDisposable, который очищается при уничтожении объекта
        MessageBroker.Default.Receive<MyEvent>()
            .Subscribe(OnEventReceived)
            .AddTo(_disposables);
    }

    private void OnDestroy()
    {
        _disposables.Dispose(); // Гарантирует отписку от MessageBroker
    }
}
```
> [!IMPORTANT]
> Если MonoBehaviour уничтожается, вызов `_disposables.Dispose()` в `OnDestroy()` предотвращает утечку подписки на синглтон/глобальный брокер сообщений `MessageBroker.Default`.
