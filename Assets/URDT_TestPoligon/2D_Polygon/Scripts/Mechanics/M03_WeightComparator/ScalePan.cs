using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M03_WeightComparator
{
    /// <summary>
    /// Чаша рычажных весов. Учитывает находящиеся на ней гири и суммирует общую массу.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class ScalePan : MonoBehaviour
    {
        [Header("Идентификация чаши")]
        [SerializeField] private string _panId = "right";
        [SerializeField] private float _baseMass = 0f;
        [SerializeField] private bool _isReferencePan = false;
        [SerializeField] private bool _acceptsDrop = true;
        [SerializeField] private TMP_Text _massDisplay = null;
        [SerializeField] private Transform _itemsAnchor = null;

        private RectTransform _rectTransform;
        private readonly List<WeightItem> _placedWeights = new List<WeightItem>();
        private float _currentMass;

        public string PanId => _panId;
        public float BaseMass => _baseMass;
        public bool IsReferencePan => _isReferencePan;
        public bool AcceptsDrop => _acceptsDrop;
        public float CurrentMass => _currentMass;
        public IReadOnlyList<WeightItem> PlacedWeights => _placedWeights;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public Transform ItemsAnchor => _itemsAnchor != null ? _itemsAnchor : transform;

        public event Action<ScalePan, float> OnMassChanged;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            // Self-healing: if LeftPan, guarantee reference mass and visual weight
            if (_isReferencePan || _panId.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _isReferencePan = true;
                _acceptsDrop = false;
                if (_baseMass <= 0f) _baseMass = 15f;
                EnsureReferenceVisual();
            }
            else
            {
                _isReferencePan = false;
                _acceptsDrop = true;
                _baseMass = 0f;
            }

            RecalculateMass();
        }

        private void EnsureReferenceVisual()
        {
            Transform anchor = ItemsAnchor;
            if (anchor.Find("ReferenceWeight15kg") == null)
            {
                GameObject refObj = new GameObject("ReferenceWeight15kg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                refObj.transform.SetParent(anchor, false);
                RectTransform rwRt = refObj.GetComponent<RectTransform>();
                rwRt.anchoredPosition = new Vector2(0f, 16f);
                rwRt.sizeDelta = new Vector2(65f, 65f);

                UnityEngine.UI.Image img = refObj.GetComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Image myImg = GetComponent<UnityEngine.UI.Image>();
                if (myImg != null && myImg.sprite != null)
                {
                    img.sprite = myImg.sprite;
                }
                img.color = new Color(0.95f, 0.8f, 0.25f, 1f);
                img.raycastTarget = false;

                GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(refObj.transform, false);
                RectTransform lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(2f, 2f);
                lblRt.offsetMax = new Vector2(-2f, -10f);
                TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
                lblTmp.fontSize = 14;
                lblTmp.fontStyle = FontStyles.Bold;
                lblTmp.alignment = TextAlignmentOptions.Center;
                lblTmp.text = "15 кг";
                lblTmp.color = Color.white;
                lblTmp.raycastTarget = false;
            }
        }

        public void SetBaseMass(float mass)
        {
            _baseMass = mass;
            RecalculateMass();
        }

        public void ResetPan()
        {
            _placedWeights.Clear();
            RecalculateMass();
        }

        public void AddWeight(WeightItem item)
        {
            if (item != null && !_placedWeights.Contains(item))
            {
                _placedWeights.Add(item);
                RecalculateMass();
                ArrangeWeights();
            }
        }

        public void RemoveWeight(WeightItem item)
        {
            if (item != null && _placedWeights.Remove(item))
            {
                RecalculateMass();
                ArrangeWeights();
            }
        }

        public void ArrangeWeights()
        {
            int count = _placedWeights.Count;
            if (count == 0) return;

            float spacing = 46f;
            float startX = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var w = _placedWeights[i];
                if (w != null)
                {
                    Vector2 localPos = new Vector2(startX + i * spacing, 22f);
                    w.SnapToPanLocal(this, localPos);
                }
            }
        }

        private void RecalculateMass()
        {
            _currentMass = _baseMass;
            foreach (var w in _placedWeights)
            {
                if (w != null) _currentMass += w.Mass;
            }
            UpdateDisplay();
            OnMassChanged?.Invoke(this, _currentMass);
        }

        private void UpdateDisplay()
        {
            if (_massDisplay != null)
            {
                if (_isReferencePan)
                {
                    _massDisplay.text = $"<b>{_currentMass:F0} кг</b>";
                    _massDisplay.color = new Color(0.2f, 1f, 0.7f, 1f);
                }
                else
                {
                    _massDisplay.text = $"{_currentMass:F0} кг";
                    _massDisplay.color = new Color(0.9f, 0.95f, 1f, 1f);
                }
            }
        }
    }
}
