using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M24_TargetElimination
{
    /// <summary>
    /// Механика #24: Прямое поражение движущихся точечных целей (Target Elimination).
    /// Задача: кликать по всплывающим мыльным пузырям и лопнуть 6 штук.
    /// Мешающие факторы:
    /// 1. Всплывающая шипастая бомба [X] — при клике взрывается и отнимает 2 очка прогресса.
    /// 2. Непрерывное движение целей снизу вверх с разной скоростью.
    /// </summary>
    public class M24_TargetEliminationMechanic : BaseMechanic2DModule
    {
        [Header("Контейнер спавна")]
        [SerializeField] private RectTransform _spawnContainer = null;

        [Header("Спрайты")]
        [SerializeField] private Sprite _bubbleSprite = null;
        [SerializeField] private Sprite _hazardBombSprite = null;

        [Header("Параметры механики")]
        [SerializeField] private int _targetPops = 6;
        [SerializeField] private float _spawnInterval = 0.85f;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _scoreText = null;
        [SerializeField] private TMP_Text _instructionText = null;

        private int _poppedCount = 0;
        private float _spawnTimer = 0f;
        private List<PoppableTarget> _pool = new List<PoppableTarget>();

        private static readonly int[] StageTarget = new int[] { 6, 8, 10 };
        private static readonly float[] StageSpawn = new float[] { 0.85f, 0.7f, 0.55f };
        private static readonly float[] StageHazardChance = new float[] { 0.25f, 0.35f, 0.42f };

        public override int StageCount => 3;
        public int PoppedCount => _poppedCount;
        public int PopTarget => _targetPops;

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnDestroy()
        {
            ClearPool();
        }

        public override void Initialize()
        {
            base.Initialize();
            _poppedCount = 0;
            _spawnTimer = 0.2f;

            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageTarget.Length - 1);
            _targetPops = StageTarget[idx];
            _spawnInterval = StageSpawn[idx];

            foreach (var t in _pool)
            {
                if (t != null) t.gameObject.SetActive(false);
            }

            UpdateUI();
            SetProgress(0f);
        }

        protected override string GetStageInstruction(int stage)
        {
            int idx = Mathf.Clamp(stage - 1, 0, StageTarget.Length - 1);
            switch (stage)
            {
                case 1: return $"Этап 1/3. Лопните {StageTarget[idx]} пузырей. Красные бомбы [X] — штраф −2.";
                case 2: return $"Этап 2/3. Пузырей больше ({StageTarget[idx]}), пузыри чаще, а клик по бомбе [X] сразу сбрасывает этап.";
                case 3: return $"Этап 3/3. {StageTarget[idx]} пузырей, максимальный темп. Любая бомба [X] — сброс.";
                default: return _instruction;
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isCompleted || IsInTransition) return;

            _spawnTimer -= Time.unscaledDeltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;
                SpawnTarget();
            }
        }

        private void SpawnTarget()
        {
            if (_spawnContainer == null) return;

            PoppableTarget target = GetOrCreateTarget();
            int idx = Mathf.Clamp(CurrentStage - 1, 0, StageHazardChance.Length - 1);
            bool isHazard = UnityEngine.Random.value < StageHazardChance[idx];
            float randomX = UnityEngine.Random.Range(-220f, 220f);
            Vector2 startPos = new Vector2(randomX, -180f);
            float speed = UnityEngine.Random.Range(90f, 160f);
            Sprite spr = isHazard ? _hazardBombSprite : _bubbleSprite;

            target.Spawn(isHazard, startPos, speed, spr);
        }

        private PoppableTarget GetOrCreateTarget()
        {
            foreach (var t in _pool)
            {
                if (t != null && !t.gameObject.activeSelf) return t;
            }

            GameObject obj = new GameObject("PoppableTarget", typeof(RectTransform), typeof(Image), typeof(PoppableTarget));
            obj.transform.SetParent(_spawnContainer, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(65f, 65f);

            PoppableTarget target = obj.GetComponent<PoppableTarget>();
            target.OnPopped += HandleTargetPopped;
            _pool.Add(target);
            return target;
        }

        private void HandleTargetPopped(PoppableTarget target)
        {
            if (_isCompleted || IsInTransition) return;

            if (target.IsHazardBomb)
            {
                if (CurrentStage >= 2)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF3333>Клик по бомбе [X]! Этап сброшен.</color>";
                    }
                    FailStage("клик по бомбе [X]");
                    return;
                }
                // Взрыв бомбы [X] на этапе 1 — штраф прогресса
                _poppedCount = Mathf.Max(0, _poppedCount - 2);
                UpdateUI();
                float prog = Mathf.Clamp01((float)_poppedCount / _targetPops);
                SetProgress(prog);

                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#FF3333>ВЗРЫВ БОМБЫ [X]! Штраф -2 очка! Не лопайте красные бомбы!</color>";
                }
                return;
            }

            // Успешный клик по пузырю
            _poppedCount++;
            UpdateUI();

            float progress = Mathf.Clamp01((float)_poppedCount / _targetPops);
            SetProgress(progress);

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#00FF99>Пузырь лопнут! ({_poppedCount}/{_targetPops})</color>";
            }

            if (_poppedCount >= _targetPops)
            {
                CompleteMechanic();
                foreach (var t in _pool) if (t != null) t.gameObject.SetActive(false);

                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>Отличная меткость! Все цели успешно поражены!</color>";
                }
            }
        }

        private void UpdateUI()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"Лопнуто: {_poppedCount} / {_targetPops}";
            }
        }

        private void ClearPool()
        {
            foreach (var t in _pool)
            {
                if (t != null)
                {
                    t.OnPopped -= HandleTargetPopped;
                    Destroy(t.gameObject);
                }
            }
            _pool.Clear();
        }
    }
}
