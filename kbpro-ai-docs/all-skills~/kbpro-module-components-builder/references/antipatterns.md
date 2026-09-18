# Антипаттерны при разработке GameComponent

Этот справочник описывает частые ошибки и запрещенные практики при написании визуальных компонентов `GameComponent` в архитектуре KBPro.

---

## 1. Размещение бизнес-логики и условий победы

### ❌ НЕПРАВИЛЬНО
```csharp
public class CoinChestComponent : GameComponent<CoinChestComponent>, IPointerClickHandler
{
    [SerializeField] private int _coinsRequired = 5;
    private int _coinsCollected;

    public void OnPointerClick(PointerEventData eventData)
    {
        _coinsCollected++;
        // Перемешивание логики подсчета и визуала
        if (_coinsCollected >= _coinsRequired)
        {
            PlayOpenAnimation();
            Constants.RaiseModuleComplete(); // Прямой вызов завершения
        }
    }
}
```

### ❓ Почему это плохо
Нарушает принцип SRP. Визуальный компонент жестко завязан на правила игры. Если геймдизайнер решит изменить условия победы (например, сделать сундук открываемым только ключом), вам придется переписывать MonoBehaviour код компонента, а не логическую C#-систему.

###  ПРАВИЛЬНО
Компонент транслирует клик, а система считает монеты и решает, когда открывать сундук и завершать модуль.
```csharp
// Компонент
public class CoinChestComponent : GameComponent<CoinChestComponent>, IPointerClickHandler
{
    public Action OnChestClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnChestClicked?.Invoke();
    }

    public void PlayOpenAnimation() { /* ... */ }
}
```

---

## 2. Использование устаревшего класса UnityEngine.UI.Text

### ❌ НЕПРАВИЛЬНО
```csharp
using UnityEngine.UI;

public class ScoreHUDComponent : GameComponent<ScoreHUDComponent>
{
    // Запрещено использовать стандартный UI Text
    [SerializeField] private Text _scoreText; 
}
```

### ❓ Почему это плохо
Стандартный компонент `Text` в Unity устарел, имеет плохую производительность и ограниченные возможности форматирования/локализации по сравнению с TextMeshPro.

###  ПРАВИЛЬНО
Всегда используйте `TextMeshPro` (или абстрактный базовый класс `TMP_Text`).
```csharp
using TMPro;

public class ScoreHUDComponent : GameComponent<ScoreHUDComponent>
{
    [SerializeField] private TMP_Text _scoreText; // Правильно

    public void SetScore(int score)
    {
        _scoreText.text = score.ToString();
    }
}
```

---

## 3. Отсутствие очистки C# подписок в OnDestroy

### ❌ НЕПРАВИЛЬНО
```csharp
public class InteractiveToolComponent : GameComponent<InteractiveToolComponent>
{
    public Action OnToolUsed;

    // Метод OnDestroy отсутствует или не очищает OnToolUsed
}
```

### ❓ Почему это плохо
Если логическая система подписалась на событие `OnToolUsed`, а затем сцена сменилась или префаб уничтожился, ссылка на логическую систему может зависнуть в памяти в виде "утечки памяти" (garbage collector не сможет удалить систему, так как на неё ссылается мертвый MonoBehaviour через событие).

###  ПРАВИЛЬНО
Всегда обнуляйте все публичные `Action` события в методе `OnDestroy()` или `OnDisable()`.
```csharp
private void OnDestroy()
{
    OnToolUsed = null; // Гарантирует разрыв связей при уничтожении
}
```

---

## 4. Прямой поиск сервисов (ServiceLocator / LazySrv) в компонентах

### ❌ НЕПРАВИЛЬНО
```csharp
public class SoundButtonComponent : GameComponent<SoundButtonComponent>, IPointerClickHandler
{
    private LazySrv<ISoundSystem> _sound = new LazySrv<ISoundSystem>();

    public void OnPointerClick(PointerEventData eventData)
    {
        // Непосредственное проигрывание звука компонентом
        _sound.Value.PlaySound("click", MixerType.Sound);
    }
}
```

### ❓ Почему это плохо
По правилам KBPro, доступ к внешним службам и DI должен происходить исключительно в логическом слое (`LogicSystem` или `AbstractGameModule`). Компоненты должны оставаться максимально изолированными визуальными контейнерами.

###  ПРАВИЛЬНО
Компонент выбрасывает событие клика, а логическая система воспроизводит звук. Это позволяет легко переопределять звуки или отключать их в зависимости от логического состояния игры.
