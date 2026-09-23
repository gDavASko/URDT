using System;
using UnityEngine;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Базовый класс для 2D-модулей механик полигона.
    /// Предоставляет унифицированную логику стейт-машины, событий и отчетов о прогрессе.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class BaseMechanic2DModule : MonoBehaviour, IMechanic2DModule
    {
        [Header("Метаданные механики")]
        [SerializeField] private string _mechanicId = "M00_Unknown";
        [SerializeField] private string _title = "Неизвестная механика";
        [TextArea(2, 4)]
        [SerializeField] protected string _instruction = "Выполните задачу механики.";

        protected bool _isCompleted;
        protected float _progressNormalized;

        public string MechanicId => _mechanicId;
        public string Title => _title;
        public string Instruction => _instruction;
        public bool IsCompleted => _isCompleted;
        public float ProgressNormalized => _progressNormalized;

        public event Action<IMechanic2DModule> OnCompleted;
        public event Action<IMechanic2DModule, float> OnProgressChanged;

        protected virtual void Awake()
        {
            Initialize();
        }

        public virtual void Initialize()
        {
            _isCompleted = false;
            _progressNormalized = 0f;
            Urdt2DBeaconUtility.InstrumentHierarchy(this);
            Urdt2DEntityInstrumentation.Attach(this);
            OnProgressChanged?.Invoke(this, 0f);
        }

        public virtual void ResetMechanic()
        {
            Initialize();
        }

        protected void SetProgress(float normalized)
        {
            _progressNormalized = Mathf.Clamp01(normalized);
            Urdt2DBeaconUtility.SyncModuleState(this);
            OnProgressChanged?.Invoke(this, _progressNormalized);
        }

        protected void CompleteMechanic()
        {
            NotifyCompleted();
        }

        protected void NotifyProgress(float current, float max)
        {
            if (max <= 0f) max = 1f;
            _progressNormalized = Mathf.Clamp01(current / max);
            Urdt2DBeaconUtility.SyncModuleState(this);
            OnProgressChanged?.Invoke(this, _progressNormalized);

            if (_progressNormalized >= 1f && !_isCompleted)
            {
                NotifyCompleted();
            }
        }

        protected void NotifyCompleted()
        {
            if (_isCompleted) return;
            _isCompleted = true;
            _progressNormalized = 1f;
            Urdt2DBeaconUtility.SyncModuleState(this);
            OnProgressChanged?.Invoke(this, 1f);
            OnCompleted?.Invoke(this);
            Debug.Log($"<color=#00FF99>[2D Polygon] Механика '{MechanicId} - {Title}' УСПЕШНО ЗАВЕРШЕНА!</color>");
        }
    }
}
