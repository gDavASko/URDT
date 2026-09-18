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
    /// Задача: составить очередь команд для робота: Вперед -> Поворот -> Вперед.
    /// Нажать «ПУСК» для пошагового выполнения программы и достижения финишного флажка.
    /// Мешающие факторы:
    /// 1. Сбойная фишка со знаком 'X' (Cmd_Glitch_Junk) - вызывает ошибку исполнения.
    /// 2. Ограниченное число слотов таймлайна (строго 3 шага).
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

        protected override void Awake()
        {
            base.Awake();
            if (_btnExecute != null) _btnExecute.onClick.AddListener(OnExecuteClicked);
        }

        private void OnDestroy()
        {
            if (_btnExecute != null) _btnExecute.onClick.RemoveListener(OnExecuteClicked);
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

            if (_instructionText != null)
            {
                _instructionText.text = "Соберите маршрут: 1. Вперед -> 2. Поворот -> 3. Вперед, затем нажмите «ПУСК».";
            }

            SetProgress(0f);
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void OnExecuteClicked()
        {
            if (_isRunning || _isCompleted) return;

            // Проверяем заполненность всех 3 слотов
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
                        _instructionText.text = "<color=#FF5555>Сбой программы! Обнаружена поврежденная команда (X).</color>";
                    }
                    _isRunning = false;
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
                    // Поворот вниз (на 90 градусов по часовой стрелке из направления вправо)
                    forwardDir = Vector2.down;
                    yield return RotateAvatarRoutine(-90f, 0.35f);
                }

                yield return new WaitForSecondsRealtime(0.15f);
            }

            // Проверка достижения цели
            if (_goalTransform != null && _avatarTransform != null)
            {
                float dist = Vector2.Distance(_avatarTransform.anchoredPosition, _goalTransform.anchoredPosition);
                if (dist <= 60f)
                {
                    SetProgress(1f);
                    CompleteMechanic();
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Цель достигнута! Программа выполнена безупречно.</color>";
                    }
                }
                else
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#FF9900>Бот не достиг цели! Проверьте порядок команд и нажмите «Сброс».</color>";
                    }
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
