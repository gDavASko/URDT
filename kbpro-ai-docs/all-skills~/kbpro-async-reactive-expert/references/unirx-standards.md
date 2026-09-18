# Стандарты UniRx — KBPro

Реактивное программирование на базе UniRx позволяет связывать состояние игры (модели данных) с представлением (UI/анимации) через потоки данных. Для избежания утечек памяти и непредсказуемого поведения необходимо строго следовать правилам жизненного цикла подписок.

## Основные правила UniRx

### 1. Жизненный цикл подписок
Каждый вызов метода `.Subscribe()` **обязан** заканчиваться привязкой к жизненному циклу объекта:
* **Использование `.AddTo(gameObject)` / `.AddTo(this)`:** Для MonoBehaviour подписок используйте метод расширения `.AddTo`, передавая ссылку на компонент или игровой объект.
* **Использование `CompositeDisposable`:** Для чистых C# классов (таких как `LogicSystem` или `AbstractGameModule`), не наследующих MonoBehaviour, используйте `CompositeDisposable` для накопления подписок и их очистки в методе `Dispose()`.

```csharp
private readonly CompositeDisposable _disposables = new CompositeDisposable();

public void Initialize()
{
    _health.ValueProperty
        .Subscribe(OnHealthChanged)
        .AddTo(_disposables);
}

public void Dispose()
{
    _disposables.Clear(); // Очищает все накопленные подписки
}
```

### 2. Реактивные свойства (ReactiveProperty)
* Для объявления наблюдаемого состояния используйте `ReactiveProperty<T>` или `ReadOnlyReactiveProperty<T>`.
* Инкапсулируйте изменяемые свойства! Изменяйте значение внутри логики, а наружу отдавайте только для чтения:
```csharp
private readonly ReactiveProperty<int> _score = new ReactiveProperty<int>(0);
public IReadOnlyReactiveProperty<int> Score => _score; // Безопасно для внешних подписок
```

### 3. Операторы фильтрации и оптимизации
Не перегружайте поток событий лишними вызовами. Используйте встроенные операторы UniRx:
* `ThrottleFirst(TimeSpan)` — игнорирует повторные вызовы в течение времени (полезно для защиты от дабл-кликов по кнопкам).
* `DistinctUntilChanged()` — пропускает событие дальше только при изменении самого значения.
* `Where(predicate)` — фильтрует события на входе.

```csharp
_button.OnClickAsObservable()
    .ThrottleFirst(TimeSpan.FromMilliseconds(500)) // Защита от спама кнопкой
    .Subscribe(_ => DoAction())
    .AddTo(this);
```

### 4. Создание событий через Subject
Если вам нужно передавать события без хранения состояния (как в `ReactiveProperty`), используйте `Subject<T>`. Также инкапсулируйте его интерфейс:
```csharp
private readonly Subject<Unit> _onComplete = new Subject<Unit>();
public IObservable<Unit> OnComplete => _onComplete;
```

## Антипаттерны при работе с UniRx
* **Брошенные подписки:** Оставление вызова `.Subscribe(...)` без завершающего `.AddTo()` или добавления в `CompositeDisposable`. Такая подписка продолжает жить в памяти и выполнять код даже после уничтожения объекта или смены сцены, что приводит к NullReferenceException и утечкам памяти.
* **Прямое изменение ReactiveProperty из вью-классов:** Вью-компоненты (`GameComponent`) должны только подписываться на изменение состояния. Менять значения свойств имеет право только логический слой (`LogicSystem`/оркестратор).
* **Смешивание EventBus и UniRx:** Не используйте UniRx для глобальной шины событий (EventBus). В KBPro для глобального обмена сообщениями используется типизированная шина событий без UniRx (`EventBus<T>`).
