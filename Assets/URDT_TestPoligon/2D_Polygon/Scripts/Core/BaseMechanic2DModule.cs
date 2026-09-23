using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Базовый класс для 2D-модулей механик полигона.
    /// Предоставляет унифицированную логику стейт-машины, событий и отчетов о прогрессе.
    ///
    /// Этапы: механика состоит из <see cref="StageCount"/> этапов (по умолчанию 1). Наследник читает
    /// <see cref="CurrentStage"/> в своём Initialize() и настраивает сложность. Когда наследник сообщает о
    /// победе (NotifyCompleted / CompleteMechanic / NotifyProgress(max)), на промежуточном этапе база
    /// показывает «Этап N пройден», повышает этап и перезапускает поле через ResetMechanic(); механика
    /// считается завершённой только после последнего этапа.
    ///
    /// Провалы: наследник вызывает <see cref="FailStage"/> — прогресс текущего этапа сбрасывается, поле
    /// перезапускается на том же этапе. Кнопка «Сброс» хоста возвращает на первый этап.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class BaseMechanic2DModule : MonoBehaviour, IMechanic2DModule
    {
        public const float STAGE_TRANSITION_SECONDS = 1.2f;
        public const float FAIL_RESTART_SECONDS = 1.0f;

        [Header("Метаданные механики")]
        [SerializeField] private string _mechanicId = "M00_Unknown";
        [SerializeField] private string _title = "Неизвестная механика";
        [TextArea(2, 4)]
        [SerializeField] protected string _instruction = "Выполните задачу механики.";

        protected bool _isCompleted;
        protected float _progressNormalized;

        private int _stage = 1;
        private int _fails;
        private bool _inTransition;
        private string _lastFailReason = string.Empty;

        public string MechanicId => _mechanicId;
        public string Title => _title;
        /// <summary>Инструкция текущего этапа (наследник может переопределить <see cref="GetStageInstruction"/>).</summary>
        public string Instruction => GetStageInstruction(_stage);
        public bool IsCompleted => _isCompleted;
        /// <summary>Общий прогресс механики с учётом этапов: ((этап-1) + прогресс этапа) / число этапов.</summary>
        public float ProgressNormalized => _progressNormalized;

        /// <summary>Число этапов механики (1..3).</summary>
        public virtual int StageCount => 1;
        /// <summary>Текущий этап, начиная с 1.</summary>
        public int CurrentStage => _stage;
        /// <summary>Сколько раз игрок провалил этап с начала механики.</summary>
        public int FailCount => _fails;
        /// <summary>Причина последнего провала (пусто, если провалов не было).</summary>
        public string LastFailReason => _lastFailReason;
        /// <summary>Идёт пауза между этапами или перезапуск после провала — ввод игнорируется.</summary>
        public bool IsInTransition => _inTransition;

        public event Action<IMechanic2DModule> OnCompleted;
        public event Action<IMechanic2DModule, float> OnProgressChanged;
        /// <summary>Этап пройден (аргумент — номер пройденного этапа).</summary>
        public event Action<BaseMechanic2DModule, int> OnStageCleared;
        /// <summary>Этап провален (аргумент — причина).</summary>
        public event Action<BaseMechanic2DModule, string> OnStageFailed;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>Инструкция для этапа; по умолчанию одна на все этапы.</summary>
        protected virtual string GetStageInstruction(int stage) => _instruction;

        public virtual void Initialize()
        {
            _isCompleted = false;
            _inTransition = false;
            Urdt2DBeaconUtility.InstrumentHierarchy(this);
            Urdt2DEntityInstrumentation.Attach(this);
            ApplyLocalProgress(0f);
        }

        public virtual void ResetMechanic()
        {
            Initialize();
        }

        /// <summary>Полный сброс механики на первый этап (кнопка «Сброс», повторный запуск).</summary>
        public void RestartFromFirstStage()
        {
            StopAllCoroutines();
            _stage = 1;
            _fails = 0;
            _lastFailReason = string.Empty;
            ResetMechanic();
        }

        protected void SetProgress(float normalized)
        {
            ApplyLocalProgress(normalized);
        }

        protected void CompleteMechanic()
        {
            NotifyCompleted();
        }

        protected void NotifyProgress(float current, float max)
        {
            if (max <= 0f) max = 1f;
            float local = Mathf.Clamp01(current / max);
            ApplyLocalProgress(local);
            if (local >= 1f && !_isCompleted)
            {
                NotifyCompleted();
            }
        }

        /// <summary>Победа на текущем этапе: переход к следующему этапу или завершение механики.</summary>
        protected void NotifyCompleted()
        {
            if (_isCompleted || _inTransition) return;
            if (_stage < StageCount)
            {
                ApplyLocalProgress(1f);
                StartCoroutine(StageClearedRoutine());
                return;
            }
            _isCompleted = true;
            _progressNormalized = 1f;
            Urdt2DBeaconUtility.SyncModuleState(this);
            OnProgressChanged?.Invoke(this, 1f);
            OnCompleted?.Invoke(this);
            Debug.Log($"<color=#00FF99>[2D Polygon] Механика '{MechanicId} - {Title}' УСПЕШНО ЗАВЕРШЕНА!</color>");
        }

        /// <summary>
        /// Провал этапа: прогресс этапа сбрасывается, поле перезапускается на том же этапе через
        /// <see cref="FAIL_RESTART_SECONDS"/> секунд. Повторные вызовы во время перезапуска игнорируются.
        /// </summary>
        protected void FailStage(string reason)
        {
            if (_isCompleted || _inTransition) return;
            _fails++;
            _lastFailReason = reason ?? string.Empty;
            Debug.Log($"<color=#FF5544>[2D Polygon] {MechanicId}: этап {_stage} провален — {reason}</color>");
            OnStageFailed?.Invoke(this, _lastFailReason);
            StartCoroutine(FailRestartRoutine());
        }

        private IEnumerator StageClearedRoutine()
        {
            _inTransition = true;
            int cleared = _stage;
            Debug.Log($"<color=#00FF99>[2D Polygon] {MechanicId}: этап {cleared}/{StageCount} пройден</color>");
            OnStageCleared?.Invoke(this, cleared);
            yield return new WaitForSeconds(STAGE_TRANSITION_SECONDS);
            _stage = Mathf.Min(StageCount, cleared + 1);
            ResetMechanic();
        }

        private IEnumerator FailRestartRoutine()
        {
            _inTransition = true;
            ApplyLocalProgress(0f);
            yield return new WaitForSeconds(FAIL_RESTART_SECONDS);
            ResetMechanic();
        }

        /// <summary>
        /// Свободная позиция для объекта, созданного на этапе: ближайшая к <paramref name="preferred"/> точка
        /// (по спирали), где рядом нет другого интерактивного соседа ближе <paramref name="minDistance"/> и которая
        /// лежит внутри родителя. Клон поверх существующего предмета перехватывает клики — так делать нельзя.
        /// </summary>
        protected static Vector2 FindFreeAnchoredPosition(RectTransform self, Vector2 preferred, float minDistance = 120f)
        {
            return FindFreeAnchoredPosition(self, preferred, minDistance, null);
        }

        /// <summary>
        /// Как выше, но дополнительно избегает <paramref name="reserved"/> — позиций, куда объекты ещё только едут
        /// (например, домашние позиции предметов после перемешивания, пока идёт анимация возврата).
        /// </summary>
        protected static Vector2 FindFreeAnchoredPosition(RectTransform self, Vector2 preferred, float minDistance, System.Collections.Generic.IList<Vector2> reserved)
        {
            RectTransform parent = self != null ? self.parent as RectTransform : null;
            if (parent == null) return preferred;
            for (int ring = 0; ring < 14; ring++)
            {
                int steps = ring == 0 ? 1 : ring * 8;
                for (int k = 0; k < steps; k++)
                {
                    float ang = k * Mathf.PI * 2f / steps;
                    Vector2 cand = preferred + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (ring * minDistance * 0.5f);
                    if (IsFreeSpot(parent, self, cand, minDistance) && !IsReserved(reserved, cand, minDistance)) return cand;
                }
            }
            return preferred;
        }

        private static bool IsReserved(System.Collections.Generic.IList<Vector2> reserved, Vector2 pos, float minDistance)
        {
            if (reserved == null) return false;
            for (int i = 0; i < reserved.Count; i++) if (Vector2.Distance(reserved[i], pos) < minDistance) return true;
            return false;
        }

        private static bool IsFreeSpot(RectTransform parent, RectTransform self, Vector2 pos, float minDistance)
        {
            Rect r = parent.rect;
            Vector2 local = pos;   // anchoredPosition ≈ локальная позиция при центральных якорях
            if (r.width > 0f && (local.x < r.xMin + minDistance * 0.4f || local.x > r.xMax - minDistance * 0.4f
                || local.y < r.yMin + minDistance * 0.4f || local.y > r.yMax - minDistance * 0.4f)) return false;
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform c = parent.GetChild(i) as RectTransform;
                if (c == null || c == self || !c.gameObject.activeInHierarchy) continue;
                if (c.GetComponent<IBeginDragHandler>() == null && c.GetComponent<IPointerDownHandler>() == null
                    && c.GetComponent<IPointerClickHandler>() == null) continue;
                if (Vector2.Distance(c.anchoredPosition, pos) < minDistance) return false;
            }
            return true;
        }

        private void ApplyLocalProgress(float local)
        {
            int count = Mathf.Max(1, StageCount);
            _progressNormalized = Mathf.Clamp01(((_stage - 1) + Mathf.Clamp01(local)) / count);
            Urdt2DBeaconUtility.SyncModuleState(this);
            OnProgressChanged?.Invoke(this, _progressNormalized);
        }
    }
}
