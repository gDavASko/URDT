# Антипаттерны при работе с ресурсами и физикой

Этот справочник описывает частые ошибки при работе с ассетами Addressables и физикой в проектах Unity.

---

## 1. Забытое или неправильное высвобождение ресурсов Addressables

### ❌ НЕПРАВИЛЬНО
```csharp
public async UniTask SpawnTool()
{
    // Ошибка: мы загрузили ассет, но дескриптор (handle) не сохранен
    // Мы никак не сможем выгрузить его из видеопамяти при закрытии сцены!
    var prefab = await Addressables.LoadAssetAsync<GameObject>("DrillTool").Task;
    Object.Instantiate(prefab);
}
```

### ❓ Почему это плохо
Addressables использует внутренний счетчик ссылок (Ref Count) для каждого ассета. Загрузка без сохранения дескриптора (`AsyncOperationHandle`) приводит к тому, что ресурс зависает в памяти навечно. Повторные переходы между сценами приведут к утечке оперативной и видеопамяти (VRAM), что вызовет падение приложения на мобильных устройствах.

###  ПРАВИЛЬНО
Всегда сохраняйте дескриптор в поле класса и вызывайте `Addressables.Release` в `Dispose` или `OnDestroy`.
```csharp
private AsyncOperationHandle<GameObject> _drillHandle;

public async UniTask SpawnTool(CancellationToken ct)
{
    _drillHandle = Addressables.LoadAssetAsync<GameObject>("DrillTool");
    await _drillHandle.Task.AsUniTask().AttachExternalCancellation(ct);
    
    if (_drillHandle.Status == AsyncOperationStatus.Succeeded)
        Object.Instantiate(_drillHandle.Result);
}

public void Dispose()
{
    if (_drillHandle.IsValid())
        Addressables.Release(_drillHandle);
}
```

---

## 2. Перемещение физических тел через Transform

### ❌ НЕПРАВИЛЬНО
```csharp
private void FixedUpdate()
{
    // Ошибка: прямое изменение позиции через transform ломает расчеты PhysX
    transform.position = transform.position + transform.forward * Time.fixedDeltaTime;
}
```

### ❓ Почему это плохо
Прямое изменение `transform.position` на объекте с `Rigidbody` телепортирует объект в пространстве, вместо того чтобы двигать его. Это приводит к тому, что объект проходит сквозь другие коллайдеры (туннелирование), физический движок тратит лишние CPU-циклы на принудительную перегрузку дерева коллизий, а рендеринг объекта начинает дергаться.

###  ПРАВИЛЬНО
Используйте `Rigidbody.MovePosition` в FixedUpdate:
```csharp
[SerializeField] private Rigidbody _rb;

private void FixedUpdate()
{
    Vector3 targetPosition = _rb.position + transform.forward * Time.fixedDeltaTime;
    _rb.MovePosition(targetPosition);
}
```

---

## 3. Выделение памяти (Garbage) при физических проверках в Update

### ❌ НЕПРАВИЛЬНО
```csharp
private void Update()
{
    // Метод OverlapCircleAll создает и возвращает новый массив объектов Collider2D КАЖДЫЙ КАДР.
    // Это приводит к образованию сотен мегабайт мусора в куче в минуту.
    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 5f);
    foreach (var hit in hits)
    {
        ProcessHit(hit);
    }
}
```

###  ПРАВИЛЬНО
Используйте pre-allocated буфер и `NonAlloc` версию метода:
```csharp
private readonly Collider2D[] _hitsBuffer = new Collider2D[8];

private void Update()
{
    int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, 5f, _hitsBuffer);
    for (int i = 0; i < hitCount; i++)
    {
        ProcessHit(_hitsBuffer[i]);
    }
}
```

---

## 4. Проверка тегов при столкновениях

### ❌ НЕПРАВИЛЬНО
```csharp
private void OnCollisionEnter2D(Collision2D collision)
{
    // Ошибка: медленное сравнение строк, а также лишние вычисления 
    // столкновений, которые можно было отключить на уровне матрицы
    if (collision.gameObject.tag == "Player")
    {
        ApplyDamage();
    }
}
```

###  ПРАВИЛЬНО
Настройте матрицу столкновений по слоям (Physics Layer Matrix) и проверяйте слои (это быстрее) или используйте сравнение через `CompareTag` (которое оптимизировано в Unity по сравнению с прямым `==` на строках).
```csharp
private void OnCollisionEnter2D(Collision2D collision)
{
    if (collision.gameObject.CompareTag("Player"))
    {
        ApplyDamage();
    }
}
```
