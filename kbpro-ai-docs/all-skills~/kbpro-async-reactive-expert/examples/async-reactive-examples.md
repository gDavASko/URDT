# Примеры кода Async & Reactive — KBPro

Этот справочник содержит эталонные C# примеры кода (Golden Samples) для типичных задач, связанных с асинхронностью и реактивностью в KBPro.

## Пример 1: Превращение Coroutine в UniTask с поддержкой отмены

### До (Классическая Coroutine Unity)
```csharp
private Coroutine _processCoroutine;

public void StartProcess()
{
    StopProcess();
    _processCoroutine = StartCoroutine(ProcessRoutine());
}

public void StopProcess()
{
    if (_processCoroutine != null)
    {
        StopCoroutine(_processCoroutine);
        _processCoroutine = null;
    }
}

private IEnumerator ProcessRoutine()
{
    yield return new WaitForSeconds(1f);
    Debug.Log("Step 1 done");
    yield return new WaitForSeconds(2f);
    Debug.Log("Step 2 done");
    _renderer.material.color = Color.green;
}
```

### После (Реализация на UniTask)
```csharp
private CancellationTokenSource _processCts;

public void StartProcess()
{
    StopProcess();
    _processCts = new CancellationTokenSource();
    
    // Передаем токен для корректного прерывания задачи
    ProcessAsync(_processCts.Token).Forget();
}

public void StopProcess()
{
    if (_processCts != null)
    {
        _processCts.Cancel();
        _processCts.Dispose();
        _processCts = null;
    }
}

private async UniTaskVoid ProcessAsync(CancellationToken ct)
{
    try
    {
        // Ожидание без выделения памяти
        await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
        Debug.Log("Step 1 done");

        await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);
        Debug.Log("Step 2 done");

        // Безопасно обращаемся к Unity объекту после асинхронных задержек
        if (this == null) return;
        _renderer.material.color = Color.green;
    }
    catch (OperationCanceledException)
    {
        Debug.Log("Process was canceled gracefully.");
    }
    catch (Exception ex)
    {
        Debug.LogException(ex);
    }
}
```

---

## Пример 2: Реактивное свойство здоровья и его привязка к UI

Этот пример демонстрирует отделение состояния (модели) от отображения (UI) с использованием UniRx.

### Логический класс состояния (Score/Health Model)
```csharp
using UniRx;

public class PlayerHealthModel
{
    private readonly ReactiveProperty<float> _health = new ReactiveProperty<float>(100f);
    
    // Экспонируем свойство только для чтения наружу
    public IReadOnlyReactiveProperty<float> Health => _health;

    public void TakeDamage(float damage)
    {
        _health.Value = Mathf.Max(0f, _health.Value - damage);
    }

    public void Heal(float amount)
    {
        _health.Value = Mathf.Min(100f, _health.Value + amount);
    }
}
```

### UI Вью-компонент (Health UI View)
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class HealthView : MonoBehaviour
{
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private TMP_Text _healthText;
    
    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    public void Bind(PlayerHealthModel model)
    {
        // Очищаем предыдущие привязки, если они были
        _disposables.Clear();

        // Подписываемся на реактивное свойство модели
        model.Health
            .Subscribe(healthValue =>
            {
                // Обновляем слайдер
                _healthSlider.value = healthValue;
                
                // Обновляем текст (используем TextMeshPro)
                _healthText.text = $"HP: {healthValue:F0}/100";
            })
            .AddTo(_disposables);
            
        // Анимация тряски при падении здоровья ниже 20%
        model.Health
            .Select(hp => hp < 20f)
            .DistinctUntilChanged()
            .Where(isLow => isLow)
            .Subscribe(_ => PlayShakeAnimation())
            .AddTo(_disposables);
    }

    private void PlayShakeAnimation()
    {
        // Логика визуального эффекта тряски
    }

    private void OnDestroy()
    {
        // Гарантированная отписка при уничтожении UI элемента
        _disposables.Dispose();
    }
}
```
