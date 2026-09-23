using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M05_TimelineSequencer
{
    /// <summary>
    /// Механика #5: Сборка исполняемой очереди команд (Timeline Sequencer).
    /// Три этапа: больше фишек-помех и повышенная цена ошибки исполнения.
    /// Провал: запуск программы, содержащей сбойную фишку (X), либо бот не достиг цели.
    /// </summary>
    public class M05_TimelineSequencerMechanic : BaseMechanic2DModule
    {
        [Header("Таймлайн и фишки")]
        [SerializeField] private CommandSlot[] _timelineSlots = null;
        [SerializeField] private CommandChip[] _chips = null;
        [SerializeField] private Button _btnExecute = null;

        [Header("Исполнитель (Аватар) и трек")]
        [SerializeField] private RectTransform _avatarTransform = null;
        [SerializeField] private RectTransform _goalTransform = null;
        [SerializeField] private Vector2 _avatarStartPosition = new Vector2(-200f, 35f);

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private Coroutine _executionRoutine;
        private bool _isRunning = false;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();

        public override int StageCount => 3;
        public bool IsRunning => _isRunning;

        protected override void Awake()
        {
            base.Awake();
            if (_btnExecute != null) _btnExecute.onClick.AddListener(OnExecuteClicked);
        }

        private void OnDestroy()
        {
            if (_btnExecute != null) _btnExecute.onClick.RemoveListener(OnExecuteClicked);
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Соберите маршрут «Вперёд → Поворот → Вперёд» и нажмите ПУСК. Не используйте сбойные фишки (X).";
                case 2: return "Этап 2/3. Добавились лишние сбойные фишки. Запуск программы со сбойной командой — провал.";
                default: return "Этап 3/3. Максимум помех. Малейшая ошибка порядка отправит бота мимо цели — этап начнётся заново.";
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_executionRoutine != null)
            {
                StopCoroutine(_executionRoutine);
                _executionRoutine = null;
            }

            _isRunning = false;

            // Убрать клонированные лишние фишки прошлого этапа
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

            if (_timelineSlots != null)
            {
                foreach (var s in _timelineSlots)
                {
                    if (s != null) s.ClearSlot();
                }
            }

            if (_chips != null)
            {
                foreach (var c in _chips)
                {
                    if (c != null)
                    {
                        c.transform.localScale = Vector3.one;
                        c.ResetChip();
                    }
                }
            }

            if (_avatarTransform != null)
            {
                _avatarTransform.anchoredPosition = _avatarStartPosition;
                _avatarTransform.localRotation = Quaternion.identity;
            }

            SpawnStageExtras(CurrentStage);

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            if (stage < 2 || _chips == null) return;

            CommandChip junkTemplate = null;
            foreach (var c in _chips)
            {
                if (c != null && c.IsJunk) { junkTemplate = c; break; }
            }
            if (junkTemplate == null) return;

            int extra = stage == 2 ? 1 : 2;
            for (int i = 0; i < extra; i++)
            {
                var clone = Instantiate(junkTemplate, junkTemplate.transform.parent);
                clone.name = $"Cmd_Glitch_Extra_S{stage}_{i + 1}";
                clone.SetJunk(true);
                var rt = clone.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = FindFreeAnchoredPosition(rt, junkTemplate.GetComponent<RectTransform>().anchoredPosition + new Vector2(70f + i * 60f, -20f));
                    clone.SetHomePosition(rt.anchoredPosition);
                }
                _spawnedExtras.Add(clone.gameObject);
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void OnExecuteClicked()
        {
            if (_isRunning || _isCompleted || IsInTransition) return;
            if (_timelineSlots == null || _timelineSlots.Length < 3) return;

            for (int i = 0; i < 3; i++)
            {
                if (!_timelineSlots[i].IsOccupied)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FFCC00>Заполните все 3 слота очереди перед запуском!</color>";
                    }
                    return;
                }
            }

            _executionRoutine = StartCoroutine(ExecuteTimelineRoutine());
        }

        private IEnumerator ExecuteTimelineRoutine()
        {
            _isRunning = true;
            if (_instructionText != null) _instructionText.text = "<color=#00CCFF>Исполнение программы...</color>";

            Vector2 curPos = _avatarStartPosition;
            Vector2 forwardDir = Vector2.right;
            bool glitchTriggered = false;

            for (int i = 0; i < 3; i++)
            {
                CommandChip chip = _timelineSlots[i].AssignedChip;
                if (chip == null) yield break;

                float stepProgress = (float)(i + 1) / 3f;
                SetProgress(stepProgress * 0.8f);

                if (chip.IsJunk || chip.CommandType == CommandType.Glitch)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF5555>Сбой программы! Использована сбойная команда (X). Этап провален.</color>";
                    }
                    _isRunning = false;
                    glitchTriggered = true;
                    FailStage("Сбойная фишка (X) в программе");
                    yield break;
                }

                if (chip.CommandType == CommandType.Forward)
                {
                    float stepDist = (forwardDir == Vector2.down) ? 70f : 140f;
                    Vector2 targetPos = curPos + forwardDir * stepDist;
                    yield return MoveAvatarRoutine(curPos, targetPos, 0.45f);
                    curPos = targetPos;
                }
                else if (chip.CommandType == CommandType.Turn)
                {
                    forwardDir = Vector2.down;
                    yield return RotateAvatarRoutine(-90f, 0.35f);
                }

                yield return new WaitForSecondsRealtime(0.15f);
            }

            if (glitchTriggered) yield break;

            if (_goalTransform != null && _avatarTransform != null)
            {
                float dist = Vector2.Distance(_avatarTransform.anchoredPosition, _goalTransform.anchoredPosition);
                if (dist <= 60f)
                {
                    SetProgress(1f);
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Цель достигнута! Программа выполнена.</color>";
                    }
                }
                else
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF9900>Бот прошёл мимо цели! Этап провален.</color>";
                    }
                    _isRunning = false;
                    FailStage("Программа не привела бота к цели");
                    yield break;
                }
            }

            _isRunning = false;
        }

        private IEnumerator MoveAvatarRoutine(Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _avatarTransform.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _avatarTransform.anchoredPosition = to;
        }

        private IEnumerator RotateAvatarRoutine(float targetZ, float duration)
        {
            float elapsed = 0f;
            Quaternion fromRot = _avatarTransform.localRotation;
            Quaternion toRot = Quaternion.Euler(0f, 0f, targetZ);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _avatarTransform.localRotation = Quaternion.Slerp(fromRot, toRot, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _avatarTransform.localRotation = toRot;
        }
    }
}
