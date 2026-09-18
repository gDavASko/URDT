# Примеры C# GameComponents

В этом справочнике приведены готовые шаблоны визуальных компонентов (`GameComponent`), соответствующих стандартам архитектуры KBPro.

---

## 1. Кликабельный объект сцены (Interactive Clickable)

Подходит для 2D/3D объектов в игровом мире (например, кнопки, активные точки, скрытые предметы).

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KBP.CORE.MODULES;
using DG.Tweening;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    public class ClickableSpotComponent : GameComponent<ClickableSpotComponent>, 
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer _spotSprite;
        [SerializeField] private ParticleSystem _clickParticle;

        [Header("Animation Settings")]
        [SerializeField] private float _punchScaleAmount = 0.15f;
        [SerializeField] private float _animationDuration = 0.2f;

        // Событие, перехватываемое LogicSystem
        public Action OnSpotClicked;

        private Vector3 _originalScale;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void SetSpotVisible(bool visible)
        {
            _spotSprite.enabled = visible;
        }

        public void PlayPulseAnimation()
        {
            // Используем DOTween с привязкой к жизненному циклу объекта SetLink
            transform.DOComplete();
            transform.DOPunchScale(Vector3.one * _punchScaleAmount, _animationDuration, 5, 0.5f)
                .SetLink(gameObject);
        }

        // --- Обработка ввода EventSystem ---

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_clickParticle != null)
            {
                _clickParticle.Play();
            }

            PlayPulseAnimation();
            
            // Отправляем сигнал логической системе
            OnSpotClicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Эффект легкого нажатия (затемнение спрайта)
            _spotSprite.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _spotSprite.color = Color.white;
        }

        private void OnDestroy()
        {
            // Обязательное обнуление для предотвращения утечек
            OnSpotClicked = null;
        }
    }
}
```

---

## 2. Локальная UI панель HUD (TextMeshPro & Slider)

Подходит для индикации прогресса, счета, таймера и локальных UI-элементов внутри модуля.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.CORE.MODULES;
using DG.Tweening;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    public class ModuleHUDComponent : GameComponent<ModuleHUDComponent>
    {
        [Header("UI Controls")]
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TMP_Text _progressPercentText;
        [SerializeField] private Slider _progressBar;

        public void UpdateTimer(float seconds)
        {
            int mins = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            _timerText.text = string.Format("{0:00}:{1:00}", mins, secs);
        }

        public void SetProgress(float current, float max)
        {
            float ratio = Mathf.Clamp01(current / max);
            
            // Плавное заполнение слайдера через DOTween
            _progressBar.DOComplete();
            _progressBar.DOValue(ratio, 0.25f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);

            int percent = Mathf.RoundToInt(ratio * 100f);
            _progressPercentText.text = $"{percent}%";
        }

        public void ShowHUD(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
```

---

## 3. Компонент свободного перетаскивания (Drag-and-Drop Tool)

Подходит для перетаскивания инструментов, элементов пазла, шестеренок и т.д.

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KBP.CORE.MODULES;
using DG.Tweening;

namespace KBP.RAILWAY_COW.EXAMPLE
{
    public class DragToolComponent : GameComponent<DragToolComponent>,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag Settings")]
        [SerializeField] private float _dragZDepth = 0f; // Глубина Z в мировых координатах
        [SerializeField] private SpriteRenderer _toolIcon;

        public Action OnDragStart;
        public Action<Vector2> OnDragging;
        public Action OnDragEnd;

        private Camera _mainCamera;
        private Vector3 _startPosition;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _startPosition = transform.position;
        }

        public void ReturnToStart(float duration = 0.4f)
        {
            // Плавное возвращение на исходную позицию при отмене
            transform.DOMove(_startPosition, duration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        // --- Реализация интерфейсов EventSystem ---

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.DOKill(); // Прерываем твины возврата
            _toolIcon.sortingOrder = 100; // Поднимаем над остальными объектами
            OnDragStart?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector3 screenPos = new Vector3(eventData.position.x, eventData.position.y, _mainCamera.WorldToScreenPoint(transform.position).z);
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);
            
            Vector2 targetPos = new Vector2(worldPos.x, worldPos.y);
            
            // Транслируем координаты в систему
            OnDragging?.Invoke(targetPos);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _toolIcon.sortingOrder = 5; // Возвращаем исходный слой отрисовки
            OnDragEnd?.Invoke();
        }

        public void SetPosition(Vector2 position)
        {
            // Устанавливаем позицию объекта на сцене
            transform.position = new Vector3(position.x, position.y, _dragZDepth);
        }

        private void OnDestroy()
        {
            OnDragStart = null;
            OnDragging = null;
            OnDragEnd = null;
        }
    }
}
```
