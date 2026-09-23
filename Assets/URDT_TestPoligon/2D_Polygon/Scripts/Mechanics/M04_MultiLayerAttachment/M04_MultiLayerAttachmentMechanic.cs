using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M04_MultiLayerAttachment
{
    /// <summary>
    /// Механика #4: Многослойная сборка робота с внутренними шагами.
    /// Стадии URDT-этапов: этапы 1-3 — три последовательные сборки с растущей сложностью.
    /// Внутри одной сборки два "шага" (Шаг 1 — база, Шаг 2 — внешние модули); это лишь части одного игрового этапа.
    /// Провал: установка бракованной детали или попытка нарушения порядка на 2-3 этапах.
    /// </summary>
    public class M04_MultiLayerAttachmentMechanic : BaseMechanic2DModule
    {
        [Header("Компоненты сборки")]
        [SerializeField] private AttachmentSlot[] _slots = null;
        [SerializeField] private AttachmentPart[] _parts = null;
        [SerializeField] private GameObject _chassisBase = null;
        [SerializeField] private GameObject _stage1Tray = null;
        [SerializeField] private GameObject _stage2Tray = null;

        [Header("UI обратная связь")]
        [SerializeField] private TMP_Text _instructionText = null;

        private int _currentPhase = 1;                 // Внутренний шаг сборки (1 или 2)
        private int _currentInstalledLayer = 0;
        private const int TOTAL_TARGET_LAYERS = 4;
        private Coroutine _transitionRoutine;
        private readonly List<GameObject> _spawnedExtras = new List<GameObject>();

        public override int StageCount => 3;
        public int CurrentPhase => _currentPhase;
        public int CurrentInstalledLayer => _currentInstalledLayer;

        protected override void Awake()
        {
            base.Awake();
            // EnsureTwoPhaseSetup и BindEvents уже вызваны через base.Awake -> Initialize.
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (_slots == null || _slots.Length < 4 || _stage1Tray == null)
            {
                Initialize();
            }
        }

        protected override string GetStageInstruction(int stage)
        {
            switch (stage)
            {
                case 1: return "Этап 1/3. Сборка робота: Шаг 1 — база (Ядро → Броня), Шаг 2 — внешние модули (Шлем → Энергоблок). Не берите бракованные детали (X).";
                case 2: return "Этап 2/3. Сборка второго корпуса. Добавились лишние бракованные детали. Установка бракованной детали — провал этапа.";
                default: return "Этап 3/3. Финальная сборка. Любое нарушение порядка монтажа или бракованная деталь — этап начнётся заново.";
            }
        }

        private void BindEvents()
        {
            if (_parts != null)
            {
                foreach (var p in _parts)
                {
                    if (p != null)
                    {
                        p.OnPartSnapped += HandlePartSnapped;
                        p.OnPartFailed += HandlePartFailed;
                    }
                }
            }
            foreach (var extra in _spawnedExtras)
            {
                if (extra == null) continue;
                var ap = extra.GetComponent<AttachmentPart>();
                if (ap != null)
                {
                    ap.OnPartSnapped += HandlePartSnapped;
                    ap.OnPartFailed += HandlePartFailed;
                }
            }
        }

        private void UnbindEvents()
        {
            if (_parts != null)
            {
                foreach (var p in _parts)
                {
                    if (p != null)
                    {
                        p.OnPartSnapped -= HandlePartSnapped;
                        p.OnPartFailed -= HandlePartFailed;
                    }
                }
            }
            foreach (var extra in _spawnedExtras)
            {
                if (extra == null) continue;
                var ap = extra.GetComponent<AttachmentPart>();
                if (ap != null)
                {
                    ap.OnPartSnapped -= HandlePartSnapped;
                    ap.OnPartFailed -= HandlePartFailed;
                }
            }
        }

        private void EnsureTwoPhaseSetup()
        {
            if (_instructionText == null)
            {
                _instructionText = GetComponentInChildren<TMP_Text>();
            }

            if (_chassisBase == null)
            {
                var stationTr = transform.Find("ChassisStation");
                if (stationTr != null) _chassisBase = stationTr.gameObject;
            }

            Transform chassisTr = _chassisBase != null ? _chassisBase.transform : transform;

            AttachmentSlot slotCore = FindOrCreateSlot(chassisTr, "Socket_Core", 1, 1, 0, new Vector2(0f, -10f), new Vector2(90f, 90f));
            AttachmentSlot slotArmor = FindOrCreateSlot(chassisTr, "Socket_Armor", 1, 2, 1, new Vector2(0f, -10f), new Vector2(125f, 125f));
            AttachmentSlot slotHead = FindOrCreateSlot(chassisTr, "Socket_Head", 2, 3, 2, new Vector2(0f, 85f), new Vector2(95f, 95f));
            AttachmentSlot slotBattery = FindOrCreateSlot(chassisTr, "Socket_Battery", 2, 4, 3, new Vector2(0f, -80f), new Vector2(85f, 75f));

            _slots = new AttachmentSlot[] { slotCore, slotArmor, slotHead, slotBattery };

            Transform tray1Tr = transform.Find("Stage1Tray");
            if (tray1Tr == null)
            {
                Transform oldTray = transform.Find("PartsTray");
                if (oldTray != null)
                {
                    oldTray.name = "Stage1Tray";
                    tray1Tr = oldTray;
                }
                else
                {
                    GameObject t1Obj = new GameObject("Stage1Tray", typeof(RectTransform));
                    t1Obj.transform.SetParent(transform, false);
                    tray1Tr = t1Obj.transform;
                    RectTransform rt = t1Obj.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(0f, -120f);
                    rt.sizeDelta = new Vector2(640f, 120f);
                }
            }
            _stage1Tray = tray1Tr.gameObject;

            Transform tray2Tr = transform.Find("Stage2Tray");
            if (tray2Tr == null)
            {
                GameObject t2Obj = new GameObject("Stage2Tray", typeof(RectTransform));
                t2Obj.transform.SetParent(transform, false);
                tray2Tr = t2Obj.transform;
                RectTransform rt = t2Obj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -120f);
                rt.sizeDelta = new Vector2(640f, 120f);
            }
            _stage2Tray = tray2Tr.gameObject;

            AttachmentPart pCore = FindOrCreatePart(tray1Tr, "Part_Core", 1, 1, "Энергоядро", false, new Vector2(-200f, 0f), new Vector2(85f, 85f), new Color(0f, 0.8f, 1f, 1f));
            AttachmentPart pArmor = FindOrCreatePart(tray1Tr, "Part_Armor", 1, 2, "Бронекорпус", false, new Vector2(0f, 0f), new Vector2(100f, 100f), new Color(1f, 0.6f, 0f, 1f));
            AttachmentPart pJunk1 = FindOrCreatePart(tray1Tr, "Part_Junk1", 1, 99, "Дефектный блок", true, new Vector2(200f, 0f), new Vector2(80f, 80f), new Color(0.9f, 0.2f, 0.2f, 1f));

            AttachmentPart pHead = FindOrCreatePart(tray2Tr, "Part_Head", 2, 3, "Сенсорный шлем", false, new Vector2(-200f, 0f), new Vector2(90f, 90f), new Color(0.8f, 0.3f, 1f, 1f));
            AttachmentPart pBattery = FindOrCreatePart(tray2Tr, "Part_Battery", 2, 4, "Энергоблок", false, new Vector2(0f, 0f), new Vector2(85f, 85f), new Color(0f, 1f, 0.7f, 1f));
            AttachmentPart pJunk2 = FindOrCreatePart(tray2Tr, "Part_Junk2", 2, 99, "Сломанный чип", true, new Vector2(200f, 0f), new Vector2(80f, 80f), new Color(0.9f, 0.2f, 0.2f, 1f));

            _parts = new AttachmentPart[] { pCore, pArmor, pJunk1, pHead, pBattery, pJunk2 };
        }

        private AttachmentSlot FindOrCreateSlot(Transform parent, string name, int stage, int layer, int prereq, Vector2 pos, Vector2 size)
        {
            Transform t = parent.Find(name);
            GameObject obj;
            if (t == null)
            {
                obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AttachmentSlot));
                obj.transform.SetParent(parent, false);
            }
            else
            {
                obj = t.gameObject;
            }

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            if (img == null) img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.5f, 0.3f);

            AttachmentSlot slot = obj.GetComponent<AttachmentSlot>();
            if (slot == null) slot = obj.AddComponent<AttachmentSlot>();
            slot.SetStageAndLayer(stage, layer, prereq, name);

            return slot;
        }

        private AttachmentPart FindOrCreatePart(Transform parent, string name, int stage, int layer, string partName, bool isJunk, Vector2 homePos, Vector2 size, Color fallbackColor)
        {
            AttachmentPart part = null;
            AttachmentPart[] existingParts = GetComponentsInChildren<AttachmentPart>(true);
            foreach (var ep in existingParts)
            {
                if (ep != null && (ep.gameObject.name == name || (isJunk && ep.IsJunk && ep.Stage == stage)))
                {
                    part = ep;
                    break;
                }
            }

            GameObject obj;
            if (part == null)
            {
                obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AttachmentPart));
                obj.transform.SetParent(parent, false);
                part = obj.GetComponent<AttachmentPart>();
                Image img = obj.GetComponent<Image>();
                img.color = fallbackColor;

#if UNITY_EDITOR
                if (name == "Part_Battery")
                {
                    Sprite batSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Robot_Battery.png");
                    if (batSpr != null)
                    {
                        img.sprite = batSpr;
                        img.color = Color.white;
                    }
                }
                else if (name == "Part_Junk2")
                {
                    Sprite junkSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Robot_BrokenPart_Junk.png");
                    if (junkSpr != null)
                    {
                        img.sprite = junkSpr;
                        img.color = Color.white;
                    }
                }
#endif
            }
            else
            {
                obj = part.gameObject;
                if (part.transform.parent != parent && !part.IsLocked)
                {
                    part.transform.SetParent(parent, false);
                }
            }

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            part.InitializePart(stage, layer, partName, isJunk, homePos);
            part.SetHomePosition(homePos);

            return part;
        }

        public override void Initialize()
        {
            base.Initialize();
            UnbindEvents();
            EnsureTwoPhaseSetup();

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _currentPhase = 1;
            _currentInstalledLayer = 0;

            // Убрать клоны предыдущего этапа
            for (int i = _spawnedExtras.Count - 1; i >= 0; i--)
            {
                if (_spawnedExtras[i] != null) Destroy(_spawnedExtras[i]);
            }
            _spawnedExtras.Clear();

            if (_stage1Tray != null) _stage1Tray.SetActive(true);
            if (_stage2Tray != null) _stage2Tray.SetActive(false);

            if (_slots != null)
            {
                foreach (var s in _slots)
                {
                    if (s != null) s.ResetSlot();
                }
            }

            if (_parts != null)
            {
                foreach (var p in _parts)
                {
                    if (p != null)
                    {
                        p.transform.localScale = Vector3.one;
                        p.ResetPart();
                    }
                }
            }

            SpawnStageExtras(CurrentStage);
            BindEvents();

            if (_instructionText != null)
            {
                _instructionText.text = $"<b>Этап {CurrentStage}/{StageCount}, Шаг 1:</b> установите Ядро → Броню. Не используйте дефектный блок (X)!";
            }

            SetProgress(0f);
        }

        private void SpawnStageExtras(int stage)
        {
            if (stage < 2) return;

            AttachmentPart junkTemplate1 = null;
            AttachmentPart junkTemplate2 = null;
            if (_parts != null)
            {
                foreach (var p in _parts)
                {
                    if (p == null) continue;
                    if (p.IsJunk && p.Stage == 1 && junkTemplate1 == null) junkTemplate1 = p;
                    else if (p.IsJunk && p.Stage == 2 && junkTemplate2 == null) junkTemplate2 = p;
                }
            }

            int extraJunk = stage == 2 ? 1 : 2;
            for (int i = 0; i < extraJunk; i++)
            {
                if (junkTemplate1 != null)
                {
                    var clone = Instantiate(junkTemplate1, junkTemplate1.transform.parent);
                    clone.name = $"Part_Junk1_Extra_S{stage}_{i + 1}";
                    var rt = clone.GetComponent<RectTransform>();
                    rt.anchoredPosition = FindFreeAnchoredPosition(rt, junkTemplate1.GetComponent<RectTransform>().anchoredPosition + new Vector2(60f + i * 50f, 40f));
                    clone.InitializePart(1, 99, "Дефектный блок", true, rt.anchoredPosition);
                    _spawnedExtras.Add(clone.gameObject);
                }
                if (junkTemplate2 != null)
                {
                    var clone = Instantiate(junkTemplate2, junkTemplate2.transform.parent);
                    clone.name = $"Part_Junk2_Extra_S{stage}_{i + 1}";
                    var rt = clone.GetComponent<RectTransform>();
                    rt.anchoredPosition = FindFreeAnchoredPosition(rt, junkTemplate2.GetComponent<RectTransform>().anchoredPosition + new Vector2(60f + i * 50f, 40f));
                    clone.InitializePart(2, 99, "Сломанный чип", true, rt.anchoredPosition);
                    _spawnedExtras.Add(clone.gameObject);
                }
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        public void HighlightSlotForPart(AttachmentPart part)
        {
            if (part == null || _slots == null) return;

            if (part.IsJunk)
            {
                ClearSlotHighlights();
                return;
            }

            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                bool isTarget = slot.Stage == part.Stage && slot.LayerIndex == part.LayerIndex && !slot.IsInstalled;
                slot.SetHighlighted(isTarget);
            }
        }

        public void ClearSlotHighlights()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
            {
                if (slot != null && !slot.IsInstalled)
                {
                    slot.SetHighlighted(false);
                }
            }
        }

        private void HandlePartSnapped(AttachmentPart part, AttachmentSlot slot)
        {
            _currentInstalledLayer = Mathf.Max(_currentInstalledLayer, part.LayerIndex);
            float progress = (float)_currentInstalledLayer / TOTAL_TARGET_LAYERS;
            SetProgress(progress);

            ClearSlotHighlights();

            if (_currentPhase == 1)
            {
                if (_currentInstalledLayer == 1)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Ядро установлено!</color> Теперь установите бронекорпус поверх ядра.";
                    }
                }
                else if (_currentInstalledLayer == 2)
                {
                    if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
                    _transitionRoutine = StartCoroutine(TransitionToPhase2Routine());
                }
            }
            else if (_currentPhase == 2)
            {
                if (_currentInstalledLayer == 3)
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = "<color=#00FF99>Шлем смонтирован!</color> Теперь установите энергоблок.";
                    }
                }
                else if (_currentInstalledLayer >= 4)
                {
                    CompleteMechanic();
                    if (_instructionText != null && !IsInTransition)
                    {
                        _instructionText.text = $"<color=#00FF99>Этап {CurrentStage} пройден!</color>";
                    }
                }
            }
        }

        private IEnumerator TransitionToPhase2Routine()
        {
            if (_instructionText != null)
            {
                _instructionText.text = "<color=#00FFFF>★ Шаг 1 завершен! Загрузка деталей Шага 2...</color>";
            }

            yield return new WaitForSecondsRealtime(1.2f);

            _currentPhase = 2;
            if (_stage1Tray != null) _stage1Tray.SetActive(false);
            if (_stage2Tray != null) _stage2Tray.SetActive(true);

            if (_instructionText != null)
            {
                _instructionText.text = $"<b>Этап {CurrentStage}/{StageCount}, Шаг 2:</b> установите Шлем → Энергоблок.";
            }
            _transitionRoutine = null;
        }

        private void HandlePartFailed(AttachmentPart part, string errorReason)
        {
            if (IsInTransition) return;
            ClearSlotHighlights();

            // Провал на 2-3 этапах при попытке установить бракованную деталь или нарушении порядка.
            if (CurrentStage >= 2 && part != null)
            {
                if (part.IsJunk || errorReason.Contains("порядок"))
                {
                    if (_instructionText != null)
                    {
                        _instructionText.text = $"<color=#FF5555>{errorReason} — этап начнётся заново.</color>";
                    }
                    FailStage(errorReason);
                    return;
                }
            }

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF6666>{errorReason}</color>";
            }
        }
    }
}
