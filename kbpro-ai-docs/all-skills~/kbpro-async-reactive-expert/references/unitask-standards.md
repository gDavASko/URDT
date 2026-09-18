# Стандарты UniTask — KBPro

Использование UniTask вместо стандартных Coroutine и .NET Task/Task<T> в проектах Unity обеспечивает высокую производительность (без аллокаций) и удобство написания асинхронного кода.

## Основные правила UniTask

### 1. Передача CancellationToken
Каждый асинхронный метод, возвращающий `UniTask` или `UniTask<T>`, **обязан** принимать `CancellationToken ct` в качестве последнего параметра.
```csharp
public async UniTask<MyResult> DoSomethingAsync(int param, CancellationToken ct)
{
    // ...
}
```

### 2. Отслеживание токена при ожиданиях
Любая асинхронная операция ожидания (`await`) должна передавать этот токен дальше:
```csharp
await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
await UniTask.WaitUntil(() => _isReady, cancellationToken: ct);
```

### 3. Использование правильного CancellationToken
* **В MonoBehaviour:** Используйте `this.GetCancellationTokenOnDestroy()`. Это гарантирует, что асинхронная операция прекратится сразу при уничтожении игрового объекта.
* **В LogicSystem / Orchestrator:** Используйте токен, передаваемый от управляющего жизненным циклом класса, или токен из `CancellationTokenSource`, который отменяется в методе `Dispose()`.

### 4. Асинхронные методы в фоновом режиме (Fire-and-Forget)
* Используйте `async UniTaskVoid` только для обработчиков событий верхнего уровня (например, нажатие кнопки UI).
* При вызове таких методов обязательно вызывайте метод расширения `.Forget()` для глушения предупреждений компилятора:
```csharp
public void OnButtonClick()
{
    DoSomethingAsync(this.GetCancellationTokenOnDestroy()).Forget();
}

private async UniTaskVoid DoSomethingAsync(CancellationToken ct)
{
    try
    {
        await _myService.LoadDataAsync(ct);
    }
    catch (OperationCanceledException)
    {
        // Ожидаемое поведение при отмене
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
    }
}
```

### 5. Безопасность при обращении к объектам Unity
После любого `await` в методах MonoBehaviour необходимо проверять, не уничтожен ли сам объект (`this == null` или `destroyCancellationToken.IsCancellationRequested`), прежде чем обращаться к компонентам Unity:
```csharp
await UniTask.Delay(500, cancellationToken: ct);
if (this == null) return; // Проверка на уничтожение MonoBehaviour
_renderer.material.color = Color.red;
```

### 6. Именование методов
Все методы, возвращающие `UniTask` или `UniTaskVoid`, должны оканчиваться суффиксом `Async` (например, `LoadAssetsAsync`, `PlayAnimationAsync`).

## Антипаттерны при использовании UniTask
* **Использование `async void`:** Всегда используйте `async UniTaskVoid` для асинхронных методов без возвращаемого значения, запускаемых по принципу «выстрелил и забыл». Использование `async void` приводит к неотлавливаемым исключениям, которые крашат приложение.
* **Использование стандартного `System.Threading.Tasks.Task`:** Стандартный `Task` выделяет память под объект задачи в куче при каждом вызове. В Unity для горячих путей это неприемлемо.
* **Ожидание без CancellationToken:** Ожидание (`await`) без передачи токена отмены приводит к зависанию задач в памяти после выхода из сцены или уничтожения объекта (утечка памяти и фоновые ошибки).
