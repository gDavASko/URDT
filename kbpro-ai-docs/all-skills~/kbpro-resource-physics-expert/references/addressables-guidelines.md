# Руководство по работе с Addressables

Этот справочник описывает стандарты загрузки, инстанциирования и высвобождения ресурсов Addressables с помощью асинхронного фреймворка `UniTask`.

---

## 1. Базовый паттерн загрузки и освобождения ресурсов

Каждая асинхронная загрузка ресурса через `Addressables` возвращает дескриптор `AsyncOperationHandle`. ИИ обязан сохранять этот дескриптор в приватном поле класса для последующего освобождения.

- **Запрещено** отбрасывать возвращаемый `AsyncOperationHandle`.
- **Запрещено** вызывать `Addressables.Release` на невалидном или уже освобожденном дескрипторе.
- Всегда используйте токен отмены `CancellationToken` при асинхронном ожидании.

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressableAssetLoader : System.IDisposable
{
    private AsyncOperationHandle<GameObject> _prefabHandle;
    private GameObject _instantiatedObject;

    public async UniTask LoadAndSpawnAsync(string assetKey, Transform parent, CancellationToken ct)
    {
        // 1. Загрузка ассета префаба
        _prefabHandle = Addressables.LoadAssetAsync<GameObject>(assetKey);
        
        // Ожидание с поддержкой отмены задачи
        await _prefabHandle.Task.AsUniTask().AttachExternalCancellation(ct);

        if (ct.IsCancellationRequested) return;

        if (_prefabHandle.Status == AsyncOperationStatus.Succeeded)
        {
            // 2. Инстанцирование объекта на сцене
            _instantiatedObject = Object.Instantiate(_prefabHandle.Result, parent);
        }
    }

    public void Dispose()
    {
        // 3. Уничтожение созданного на сцене объекта
        if (_instantiatedObject != null)
        {
            Object.Destroy(_instantiatedObject);
        }

        // 4. Освобождение дескриптора загрузки префаба
        if (_prefabHandle.IsValid())
        {
            Addressables.Release(_prefabHandle);
        }
    }
}
```

---

## 2. Групповая загрузка по меткам (Labels)

Если модулю необходимо загрузить несколько однотипных ресурсов (например, набор инструментов), загружайте их пакетом по метке (Label):

```csharp
private AsyncOperationHandle<IList<Sprite>> _spritesHandle;

public async UniTask LoadSpritesByLabelAsync(string label, CancellationToken ct)
{
    _spritesHandle = Addressables.LoadAssetsAsync<Sprite>(label, null);
    await _spritesHandle.Task.AsUniTask().AttachExternalCancellation(ct);
}
```

---

## 3. Инстанцирование через Addressables.InstantiateAsync

Вместо `Object.Instantiate` вы можете использовать встроенное инстанциирование Addressables, но правила освобождения остаются такими же строгими:

```csharp
private AsyncOperationHandle<GameObject> _instanceHandle;

public async UniTask SpawnAsync(string key, CancellationToken ct)
{
    _instanceHandle = Addressables.InstantiateAsync(key);
    await _instanceHandle.Task.AsUniTask().AttachExternalCancellation(ct);
}

public void Dispose()
{
    if (_instanceHandle.IsValid())
    {
        // Addressables.ReleaseInstance автоматически уничтожит GameObject
        // и уменьшит счетчик ссылок загруженного ассета
        Addressables.Release(_instanceHandle); 
    }
}
```
