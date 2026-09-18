# Обработка пользовательского ввода в GameComponent

Этот справочник описывает стандарты перехвата ввода (кликов, тачей, перетаскивания) на уровне визуального представления и его трансляции в логический слой.

---

## 1. Основной принцип: Компонент глух и нем

`GameComponent` перехватывает низкоуровневые события Unity (координаты мыши, тачи, столкновения физики) и без всякой интерпретации отправляет их в `LogicSystem` через C# `Action` или `IObservable` (UniRx).

Компонент **не должен**:
- Проверять, разрешен ли клик в данный момент.
- Менять состояние игры (например, увеличивать счетчик очков).
- Проигрывать звуки победы или запускать другие компоненты.

---

## 2. Перехват кликов и тачей (EventSystem)

Для корректной обработки кликов в Unity 2D/3D или UI всегда используйте интерфейсы `UnityEngine.EventSystems`:

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KBP.CORE.MODULES;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    public class InteractiveButtonComponent : GameComponent<InteractiveButtonComponent>, 
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        // Событие для LogicSystem
        public Action OnClicked;
        public Action<bool> OnPressedStateChanged;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Просто транслируем факт клика
            OnClicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPressedStateChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnPressedStateChanged?.Invoke(false);
        }

        private void OnDestroy()
        {
            OnClicked = null;
            OnPressedStateChanged = null;
        }
    }
}
```

---

## 3. Обработка Drag & Drop (Перетаскивание)

При реализации механики Drag & Drop компонент должен преобразовывать экранные координаты мыши/пальца в мировые координаты сцены (или локальные координаты Canvas) и передавать их системе.

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KBP.CORE.MODULES;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    public class DraggableItemComponent : GameComponent<DraggableItemComponent>,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action OnDragStarted;
        public Action<Vector2> OnDragProgress; // Передает целевую позицию в мировых координатах
        public Action OnDragEnded;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnDragStarted?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Переводим позицию курсора на экране в мировые координаты
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(eventData.position);
            OnDragProgress?.Invoke(new Vector2(worldPos.x, worldPos.y));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnDragEnded?.Invoke();
        }

        // Метод вызывается системой для плавного или мгновенного перемещения объекта в пространстве
        public void UpdatePosition(Vector2 newPosition)
        {
            transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);
        }

        private void OnDestroy()
        {
            OnDragStarted = null;
            OnDragProgress = null;
            OnDragEnded = null;
        }
    }
}
```

---

## 4. Настройка сцены для работы ввода

Чтобы события `EventSystem` доходили до вашего компонента, необходимо соблюдение условий:
1. **На сцене присутствует EventSystem** (обычно создается глобально фреймворком).
2. **На камере висит PhysicsRaycaster / Physics2DRaycaster** (для объектов сцены с коллайдерами) или **GraphicRaycaster** (для UI Canvas).
3. **Объект содержит Collider2D / Collider** с включенной галочкой `Is Trigger` (для 2D/3D сцены) или включен параметр `Raycast Target` (для UI элементов).
4. **Правильный физический слой (Layer)**: Убедитесь, что объект находится на слое, который сканируется Raycaster-ом камеры (например, слой `Clickable` или `Default`).
