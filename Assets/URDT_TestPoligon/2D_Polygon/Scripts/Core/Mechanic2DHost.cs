using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Главный хост-контроллер 2D-полигона механик.
    /// Управляет каталогом тестов, сквозным автопрогоном, экраном прохождения,
    /// 2-секундным поздравительным оверлеем победы и навигацией в меню.
    /// </summary>
    [DisallowMultipleComponent]
    public class Mechanic2DHost : MonoBehaviour
    {
        [Header("Панели")]
        [SerializeField] private GameObject _catalogPanel = null;
        [SerializeField] private GameObject _playPanel = null;

        [Header("UI Каталога")]
        [SerializeField] private Button _btnCatalogBack = null;
        [SerializeField] private Button _btnRunSequential = null;
        [SerializeField] private RectTransform _catalogContent = null;

        [Header("UI Игрового Экрана - Навигация")]
        [SerializeField] private Button _btnPlayMainMenu = null;
        [SerializeField] private Button _btnPlayCatalog = null;
        [SerializeField] private TMP_Text _modeBadgeText = null;
        [SerializeField] private Sprite _navButtonSprite = null;

        [Header("UI элементов заголовка и статуса")]
        [SerializeField] private TMP_Text _titleText = null;
        [SerializeField] private TMP_Text _instructionText = null;
        [SerializeField] private TMP_Text _statusBadgeText = null;
        [SerializeField] private TMP_Text _progressText = null;
        [SerializeField] private Image _statusBadgeBg = null;

        [Header("Контейнер игровой зоны")]
        [SerializeField] private RectTransform _playAreaContainer = null;

        [Header("Кнопки управления в тесте")]
        [SerializeField] private Button _btnPrev = null;
        [SerializeField] private Button _btnNext = null;
        [SerializeField] private Button _btnReset = null;

        [Header("Эффект победы / Поздравление")]
        [SerializeField] private GameObject _victoryOverlay = null;
        [SerializeField] private TMP_Text _victoryTitleText = null;
        [SerializeField] private TMP_Text _victorySubText = null;

        [Header("Каталог механик полигона")]
        [SerializeField] private BaseMechanic2DModule[] _mechanicPrefabs = null;
        [SerializeField] private Sprite[] _mechanicThumbnails = null;

        private int _currentIndex = 0;
        private BaseMechanic2DModule _activeInstance = null;
        private bool _isSequentialMode = false;
        private Coroutine _autoAdvanceCoroutine = null;
        private bool _catalogBuilt = false;

        public int CurrentIndex => _currentIndex;
        public int TotalCount => _mechanicPrefabs != null ? _mechanicPrefabs.Length : 0;
        public BaseMechanic2DModule ActiveMechanic => _activeInstance;
        public bool IsSequentialMode => _isSequentialMode;

        private void Awake()
        {
            EnsureRuntimeHierarchy();
            BindNavigationButtons();

            if (_btnPrev != null) _btnPrev.onClick.AddListener(OnPrevClicked);
            if (_btnNext != null) _btnNext.onClick.AddListener(OnNextClicked);
            if (_btnReset != null) _btnReset.onClick.AddListener(OnResetClicked);
        }

        private void OnEnable()
        {
            EnsureRuntimeHierarchy();
            BindNavigationButtons();
            // При входе в 2D-окно из главного меню показываем каталог выбора тестов
            ShowCatalog();
        }

        private void OnDisable()
        {
            StopAutoAdvance();
            CleanupActiveInstance();
        }

        private bool IsPointInsideRect(Component comp, Vector2 screenPoint, Camera cam)
        {
            if (comp == null || !comp.gameObject.activeInHierarchy) return false;
            RectTransform rt = comp.transform as RectTransform;
            if (rt == null) return false;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, cam))
                return true;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, Mathf.Min(corners[1].x, Mathf.Min(corners[2].x, corners[3].x)));
            float maxX = Mathf.Max(corners[0].x, Mathf.Max(corners[1].x, corners[2].x, corners[3].x));
            float minY = Mathf.Min(corners[0].y, Mathf.Min(corners[1].y, Mathf.Min(corners[2].y, corners[3].y)));
            float maxY = Mathf.Max(corners[0].y, Mathf.Max(corners[1].y, corners[2].y, corners[3].y));

            return screenPoint.x >= minX && screenPoint.x <= maxX && screenPoint.y >= minY && screenPoint.y <= maxY;
        }

        private void Update()
        {
            // 1. Аварийный выход в главное меню по клавише Escape
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[Mechanic2DHost] Escape key pressed -> ReturnToMainMenu");
                ReturnToMainMenu();
                return;
            }

            // 2. Возврат в каталог уровней по Backspace / Tab / M / C
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) || UnityEngine.Input.GetKeyDown(KeyCode.Tab) || UnityEngine.Input.GetKeyDown(KeyCode.M) || UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                if (_playPanel != null && _playPanel.activeSelf)
                {
                    Debug.Log("[Mechanic2DHost] Catalog hotkey pressed -> ShowCatalog");
                    ShowCatalog();
                    return;
                }
                else
                {
                    ReturnToMainMenu();
                    return;
                }
            }

            // 3. Прямой перехват кликов мыши/тачей в экранных координатах (100% защита от блокировки Raycast)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = UnityEngine.Input.mousePosition;
                Canvas c = GetComponentInParent<Canvas>();
                Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;

                if (_playPanel != null && _playPanel.activeSelf)
                {
                    // Кнопка В главное меню
                    if (IsPointInsideRect(_btnPlayMainMenu, mousePos, cam))
                    {
                        Debug.Log("[Mechanic2DHost] Direct screen click on _btnPlayMainMenu -> ReturnToMainMenu");
                        ReturnToMainMenu();
                        return;
                    }

                    // Кнопка Каталог тестов / Список уровней
                    if (IsPointInsideRect(_btnPlayCatalog, mousePos, cam))
                    {
                        Debug.Log("[Mechanic2DHost] Direct screen click on _btnPlayCatalog -> ShowCatalog");
                        ShowCatalog();
                        return;
                    }

                    // Клик на заголовок теста или бейдж статуса (пользователь нажал на надпись теста!)
                    if (IsPointInsideRect(_titleText, mousePos, cam) || IsPointInsideRect(_statusBadgeBg, mousePos, cam))
                    {
                        Debug.Log("[Mechanic2DHost] Direct screen click on Title/StatusBadge -> ShowCatalog");
                        ShowCatalog();
                        return;
                    }
                }
                else if (_catalogPanel != null && _catalogPanel.activeSelf)
                {
                    if (IsPointInsideRect(_btnCatalogBack, mousePos, cam))
                    {
                        Debug.Log("[Mechanic2DHost] Direct screen click on _btnCatalogBack -> ReturnToMainMenu");
                        ReturnToMainMenu();
                        return;
                    }
                }
            }
        }

        public void SetMechanicPrefabs(BaseMechanic2DModule[] prefabs)
        {
            _mechanicPrefabs = prefabs;
            _currentIndex = 0;
            _catalogBuilt = false;
            if (isActiveAndEnabled)
            {
                ShowCatalog();
            }
        }

        #region Navigation
        /// <summary>
        /// Открывает окно каталога со всеми доступными тестами.
        /// </summary>
        public void ShowCatalog()
        {
            StopAutoAdvance();
            CleanupActiveInstance();
            _isSequentialMode = false;

            if (_playPanel != null) _playPanel.SetActive(false);
            if (_catalogPanel != null) _catalogPanel.SetActive(true);

            BindNavigationButtons();
            _catalogBuilt = false;
            BuildCatalogCardsIfEmpty();
        }

        /// <summary>
        /// Показывает панель прохождения механики.
        /// </summary>
        private void ShowPlayPanel()
        {
            if (_catalogPanel != null) _catalogPanel.SetActive(false);
            if (_playPanel != null) _playPanel.SetActive(true);
            BindNavigationButtons();
        }

        /// <summary>
        /// Запускает одиночный тест выбранной механики.
        /// </summary>
        public void LaunchSingleMechanic(int index)
        {
            StopAutoAdvance();
            _isSequentialMode = false;
            ShowPlayPanel();
            LoadMechanic(index);
        }

        /// <summary>
        /// Запускает сквозной автопрогон тестов один за другим (с 1 по последний).
        /// </summary>
        public void LaunchSequentialMode()
        {
            StopAutoAdvance();
            _isSequentialMode = true;
            ShowPlayPanel();
            LoadMechanic(0);
        }

        /// <summary>
        /// Возвращает игрока в главное меню полигона с тройной гарантией срабатывания.
        /// </summary>
        public void ReturnToMainMenu()
        {
            Debug.Log("[Mechanic2DHost] ReturnToMainMenu invoked!");
            StopAutoAdvance();
            CleanupActiveInstance();

            if (_playPanel != null) _playPanel.SetActive(false);
            if (_catalogPanel != null) _catalogPanel.SetActive(true);

            bool handled = false;

            // 1. Попытка через синглтон контроллера полигона
            try
            {
                if (UrdtUiTestPoligonController.Instance != null)
                {
                    UrdtUiTestPoligonController.Instance.ShowMainMenu();
                    handled = true;
                }
                else
                {
                    var controller = FindObjectOfType<UrdtUiTestPoligonController>();
                    if (controller != null)
                    {
                        controller.ShowMainMenu();
                        handled = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Mechanic2DHost] Exception calling ShowMainMenu on controller: {ex.Message}");
            }

            // 2. Гарантированный fallback: прямое переключение окон в Canvas иерархии
            Transform mainMenu = transform.parent != null ? transform.parent.Find("MainMenuWindow") : null;
            if (mainMenu == null) mainMenu = FindInChildren(transform.root, "MainMenuWindow");
            if (mainMenu != null)
            {
                mainMenu.gameObject.SetActive(true);
                gameObject.SetActive(false);
                handled = true;
            }

            if (!handled)
            {
                gameObject.SetActive(false);
            }
        }
        #endregion

        #region Mechanic Lifecycle
        public void LoadMechanic(int index)
        {
            if (_mechanicPrefabs == null || _mechanicPrefabs.Length == 0) return;

            _currentIndex = Mathf.Clamp(index, 0, _mechanicPrefabs.Length - 1);
            CleanupActiveInstance();

            BaseMechanic2DModule prefab = _mechanicPrefabs[_currentIndex];
            if (prefab == null) return;

            Transform parent = _playAreaContainer != null ? _playAreaContainer : transform;
            _activeInstance = Instantiate(prefab, parent);
            _activeInstance.transform.localPosition = Vector3.zero;
            _activeInstance.transform.localScale = Vector3.one;

            RectTransform rt = _activeInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            foreach (Transform t in _activeInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t.localScale == Vector3.zero)
                {
                    t.localScale = Vector3.one;
                }
            }

            // Снимаем блокировку raycastTarget со всех неинтерактивных текстов и плашек внутри механики
            foreach (var tmp in _activeInstance.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.GetComponentInParent<Button>() == null)
                {
                    tmp.raycastTarget = false;
                }
            }

            _activeInstance.OnCompleted += HandleMechanicCompleted;
            _activeInstance.OnProgressChanged += HandleMechanicProgress;
            _activeInstance.Initialize();

            UpdateHeaderUI();
            DisableNonButtonRaycasts();
        }

        private void CleanupActiveInstance()
        {
            if (_activeInstance != null)
            {
                _activeInstance.OnCompleted -= HandleMechanicCompleted;
                _activeInstance.OnProgressChanged -= HandleMechanicProgress;
                Destroy(_activeInstance.gameObject);
                _activeInstance = null;
            }
        }

        /// <summary>
        /// Вызывается при успешном прохождении любой механики:
        /// запускает 2-секундный поздравительный эффектик,
        /// затем либо идет в меню 2D-механик (одиночный), либо запускает следующий модуль (каскад).
        /// </summary>
        private void HandleMechanicCompleted(IMechanic2DModule module)
        {
            UpdateStatusUI(true);

            StopAutoAdvance();
            _autoAdvanceCoroutine = StartCoroutine(VictoryCelebrationRoutine());
        }

        private IEnumerator VictoryCelebrationRoutine()
        {
            EnsureVictoryOverlay();

            if (_victoryOverlay != null)
            {
                _victoryOverlay.transform.SetAsLastSibling();
                _victoryOverlay.SetActive(true);

                Transform cardTr = _victoryOverlay.transform.Find("VictoryCard");
                if (cardTr == null && _victoryOverlay.transform.childCount > 0)
                {
                    cardTr = _victoryOverlay.transform.GetChild(0);
                }

                if (_victoryTitleText != null)
                {
                    _victoryTitleText.text = "ТЕСТ УСПЕШНО ПРОЙДЕН!";
                    _victoryTitleText.color = new Color(0.2f, 1f, 0.55f);
                }

                if (_victorySubText != null)
                {
                    if (_isSequentialMode)
                    {
                        if (_currentIndex < TotalCount - 1)
                        {
                            string nextTitle = _mechanicPrefabs != null && _currentIndex + 1 < _mechanicPrefabs.Length && _mechanicPrefabs[_currentIndex + 1] != null
                                ? _mechanicPrefabs[_currentIndex + 1].Title
                                : $"Тест #{_currentIndex + 2}";
                            _victorySubText.text = $"Следующий модуль: [{_currentIndex + 2:D2}/{TotalCount:D2}] {nextTitle}";
                        }
                        else
                        {
                            _victoryTitleText.text = $"ВСЕ {TotalCount} МЕХАНИКИ УСПЕШНО ПРОЙДЕНЫ!";
                            _victorySubText.text = "Поздравляем с завершением полигона 2D!";
                        }
                    }
                    else
                    {
                        _victorySubText.text = "Возврат в каталог 2D-механик...";
                    }
                }

                // Анимация эффектика ровно 2.0 секунды (punch scale + pulse)
                float duration = 2.0f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime > 0 ? Time.unscaledDeltaTime : 0.016f;
                    float t = Mathf.Clamp01(elapsed / duration);

                    if (cardTr != null)
                    {
                        float scale;
                        if (t < 0.25f)
                        {
                            scale = Mathf.Lerp(0.65f, 1.08f, t / 0.25f);
                        }
                        else if (t < 0.45f)
                        {
                            scale = Mathf.Lerp(1.08f, 0.97f, (t - 0.25f) / 0.2f);
                        }
                        else if (t < 0.6f)
                        {
                            scale = Mathf.Lerp(0.97f, 1.0f, (t - 0.45f) / 0.15f);
                        }
                        else
                        {
                            scale = 1.0f + Mathf.Sin((t - 0.6f) * Mathf.PI * 4f) * 0.015f;
                        }
                        cardTr.localScale = new Vector3(scale, scale, 1f);
                    }

                    yield return null;
                }

                _victoryOverlay.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(2.0f);
            }

            _autoAdvanceCoroutine = null;

            // Логика перехода:
            // 1. Если каскад (автопрогон) -> следующий модуль, либо каталог если конец
            // 2. Если одиночная -> в меню 2D-механик (каталог)
            if (_isSequentialMode)
            {
                if (_currentIndex < TotalCount - 1)
                {
                    LoadMechanic(_currentIndex + 1);
                }
                else
                {
                    ShowCatalog();
                }
            }
            else
            {
                ShowCatalog();
            }
        }

        private void StopAutoAdvance()
        {
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }

            if (_victoryOverlay != null)
            {
                _victoryOverlay.SetActive(false);
            }
        }

        private void HandleMechanicProgress(IMechanic2DModule module, float progress)
        {
            if (_progressText != null)
            {
                _progressText.text = $"Прогресс: {(progress * 100f):F0}%";
            }
        }

        private void UpdateHeaderUI()
        {
            if (_activeInstance == null) return;

            if (_modeBadgeText != null)
            {
                if (_isSequentialMode)
                {
                    _modeBadgeText.text = $"РЕЖИМ: АВТОПРОГОН [{_currentIndex + 1}/{TotalCount}]";
                    _modeBadgeText.color = new Color(0.35f, 0.85f, 1f);
                }
                else
                {
                    _modeBadgeText.text = "РЕЖИМ: ОДИНОЧНЫЙ ТЕСТ";
                    _modeBadgeText.color = new Color(0.7f, 0.8f, 0.9f);
                }
            }

            if (_titleText != null)
            {
                _titleText.text = $"[{_currentIndex + 1:D2}/{TotalCount:D2}] {_activeInstance.Title}";
            }

            if (_instructionText != null)
            {
                _instructionText.text = _activeInstance.Instruction;
            }

            UpdateStatusUI(_activeInstance.IsCompleted);

            if (_btnPrev != null) _btnPrev.interactable = _currentIndex > 0;
            if (_btnNext != null) _btnNext.interactable = _currentIndex < TotalCount - 1;
        }

        private void UpdateStatusUI(bool completed)
        {
            if (_statusBadgeText != null)
            {
                _statusBadgeText.text = completed ? "ЗАВЕРШЕНО" : "В ПРОЦЕССЕ";
                _statusBadgeText.color = completed ? new Color(0.1f, 1f, 0.5f) : new Color(1f, 0.8f, 0.2f);
            }

            if (_statusBadgeBg != null)
            {
                _statusBadgeBg.color = completed
                    ? new Color(0.1f, 0.55f, 0.3f, 0.65f)
                    : new Color(0.55f, 0.4f, 0.1f, 0.5f);
            }

            if (_progressText != null && _activeInstance != null)
            {
                _progressText.text = completed
                    ? "Прогресс: 100% (Успех!)"
                    : $"Прогресс: {(_activeInstance.ProgressNormalized * 100f):F0}%";
            }
        }

        private void OnPrevClicked()
        {
            if (_currentIndex > 0)
            {
                LoadMechanic(_currentIndex - 1);
            }
        }

        private void OnNextClicked()
        {
            if (_currentIndex < TotalCount - 1)
            {
                LoadMechanic(_currentIndex + 1);
            }
        }

        private void OnResetClicked()
        {
            if (_activeInstance != null)
            {
                _activeInstance.ResetMechanic();
                UpdateStatusUI(false);
            }
        }
        #endregion

        #region Catalog Builder
        /// <summary>
        /// Динамически формирует карточки тестов в каталоге, если они еще не созданы.
        /// </summary>
        public void BuildCatalogCardsIfEmpty()
        {
            if (_catalogContent == null || _mechanicPrefabs == null || _mechanicPrefabs.Length == 0) return;
            if (_catalogBuilt && _catalogContent.childCount >= _mechanicPrefabs.Length) return;

            for (int c = _catalogContent.childCount - 1; c >= 0; c--)
            {
                Destroy(_catalogContent.GetChild(c).gameObject);
            }

            for (int i = 0; i < _mechanicPrefabs.Length; i++)
            {
                BaseMechanic2DModule prefab = _mechanicPrefabs[i];
                if (prefab == null) continue;

                int capturedIndex = i;
                CreateCatalogCard(prefab, capturedIndex);
            }

            _catalogBuilt = true;
        }

        private void CreateCatalogCard(BaseMechanic2DModule prefab, int index)
        {
            GameObject cardObj = new GameObject($"Card_{index + 1:D2}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Outline));
            cardObj.transform.SetParent(_catalogContent, false);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

            Outline cardOut = cardObj.GetComponent<Outline>();
            cardOut.effectColor = new Color(0.16f, 0.24f, 0.35f, 0.55f);
            cardOut.effectDistance = new Vector2(1f, -1f);

            LayoutElement cardLe = cardObj.GetComponent<LayoutElement>();
            cardLe.preferredHeight = 74f;
            cardLe.minHeight = 74f;
            cardLe.flexibleWidth = 1f;

            HorizontalLayoutGroup cardHlg = cardObj.AddComponent<HorizontalLayoutGroup>();
            cardHlg.padding = new RectOffset(12, 14, 8, 8);
            cardHlg.spacing = 14f;
            cardHlg.childAlignment = TextAnchor.MiddleLeft;
            cardHlg.childControlHeight = true;
            cardHlg.childControlWidth = true; // Контролирует ширину: дает инфо-колонке растянуться, а кнопке встать вправо!
            cardHlg.childForceExpandHeight = false;
            cardHlg.childForceExpandWidth = false;

            // 1. ЭСКИЗ УРОВНЯ (Превью-панель: миниатюра/иконка уровня + номер)
            GameObject thumbBox = new GameObject("ThumbnailBox", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            thumbBox.transform.SetParent(cardObj.transform, false);

            LayoutElement thumbLe = thumbBox.GetComponent<LayoutElement>();
            thumbLe.preferredWidth = 74f;
            thumbLe.minWidth = 74f;
            thumbLe.preferredHeight = 58f;
            thumbLe.minHeight = 58f;
            thumbLe.flexibleWidth = 0f;

            Image thumbBg = thumbBox.GetComponent<Image>();
            thumbBg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);

            Outline thumbOut = thumbBox.GetComponent<Outline>();
            thumbOut.effectColor = new Color(0.18f, 0.38f, 0.55f, 0.75f);
            thumbOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Иконка механики внутри превью-бокса
            Sprite thumbSpr = (_mechanicThumbnails != null && index < _mechanicThumbnails.Length) ? _mechanicThumbnails[index] : null;
            if (thumbSpr != null)
            {
                GameObject iconObj = new GameObject("LevelIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(thumbBox.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(0f, 7f);
                iconRt.sizeDelta = new Vector2(36f, 36f);
                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = thumbSpr;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // Номер уровня в нижней части превью-бокса
            GameObject numObj = new GameObject("LevelNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(thumbBox.transform, false);
            RectTransform numRt = numObj.GetComponent<RectTransform>();
            numRt.anchorMin = new Vector2(0f, 0f);
            numRt.anchorMax = new Vector2(1f, 0f);
            numRt.pivot = new Vector2(0.5f, 0f);
            numRt.anchoredPosition = new Vector2(0f, 2f);
            numRt.sizeDelta = new Vector2(0f, 18f);
            TextMeshProUGUI numTmp = numObj.GetComponent<TextMeshProUGUI>();
            numTmp.text = $"{index + 1:D2}";
            numTmp.fontSize = 13;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.alignment = TextAlignmentOptions.Center;
            numTmp.color = new Color(0.35f, 0.85f, 1f);
            numTmp.raycastTarget = false;

            // 2. ИНФО-КОЛОНКА (ОПИСАНИЕ НА ВСЮ ДОСТУПНУЮ ДЛИНУ)
            GameObject infoCol = new GameObject("InfoColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            infoCol.transform.SetParent(cardObj.transform, false);

            LayoutElement infoLe = infoCol.GetComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f; // РАСТЯГИВАЕТСЯ НА ВСЮ СВОБОДНУЮ ШИРИНУ КАРТОЧКИ!
            infoLe.minWidth = 150f;

            VerticalLayoutGroup infoVlg = infoCol.GetComponent<VerticalLayoutGroup>();
            infoVlg.spacing = 3f;
            infoVlg.childAlignment = TextAnchor.MiddleLeft;
            infoVlg.childControlHeight = true;
            infoVlg.childControlWidth = true;
            infoVlg.childForceExpandHeight = false;
            infoVlg.childForceExpandWidth = true;

            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(infoCol.transform, false);
            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = prefab.Title;
            titleTmp.fontSize = 15;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = false;
            titleTmp.textWrappingMode = TextWrappingModes.Normal;

            GameObject descObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(infoCol.transform, false);
            TextMeshProUGUI descTmp = descObj.GetComponent<TextMeshProUGUI>();
            descTmp.text = prefab.Instruction;
            descTmp.fontSize = 12;
            descTmp.color = new Color(0.7f, 0.8f, 0.9f, 0.9f);
            descTmp.raycastTarget = false;
            descTmp.textWrappingMode = TextWrappingModes.Normal;

            // 3. КНОПКА ЗАПУСКА (ПРИЖАТА ВПРАВО)
            GameObject btnObj = new GameObject("LaunchButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow), typeof(LayoutElement));
            btnObj.transform.SetParent(cardObj.transform, false);

            LayoutElement btnLe = btnObj.GetComponent<LayoutElement>();
            btnLe.preferredWidth = 115f;
            btnLe.minWidth = 115f;
            btnLe.preferredHeight = 44f;
            btnLe.minHeight = 44f;
            btnLe.flexibleWidth = 0f; // Фиксированная ширина - всегда прижата к правому краю

            Image btnImg = btnObj.GetComponent<Image>();
            if (_navButtonSprite != null)
            {
                btnImg.sprite = _navButtonSprite;
                btnImg.type = Image.Type.Sliced;
                btnImg.color = Color.white;
            }
            else
            {
                btnImg.color = new Color(0.08f, 0.50f, 0.85f, 1f);
            }
            btnImg.raycastTarget = true;

            Outline btnOut = btnObj.GetComponent<Outline>();
            btnOut.effectColor = new Color(0.2f, 0.75f, 1f, 0.9f);
            btnOut.effectDistance = new Vector2(1.5f, -1.5f);

            Shadow btnShadow = btnObj.GetComponent<Shadow>();
            btnShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            btnShadow.effectDistance = new Vector2(1f, -2f);

            GameObject btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRt = btnTextObj.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;
            TextMeshProUGUI btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
            btnTmp.text = "Запуск";
            btnTmp.fontSize = 15;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
            btnTmp.raycastTarget = false;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.15f, 0.65f, 1f, 1f);
            cb.pressedColor = new Color(0.05f, 0.35f, 0.65f, 1f);
            btn.colors = cb;

            btn.onClick.AddListener(() => LaunchSingleMechanic(index));
        }
        #endregion

        #region Hierarchy Guarantee
        private void FixLooseStatusBadge()
        {
            if (_statusBadgeBg == null)
            {
                Transform badgeTr = transform.Find("StatusBadge");
                if (badgeTr != null) _statusBadgeBg = badgeTr.GetComponent<Image>();
            }

            if (_statusBadgeBg != null && _playPanel != null)
            {
                Transform navTr = _playPanel.transform.Find("PlayTopNav");
                if (navTr != null && _statusBadgeBg.transform.parent != navTr)
                {
                    _statusBadgeBg.transform.SetParent(navTr, false);
                }
            }
        }

        /// <summary>
        /// Гарантирует аккуратную двухпанельную структуру без наслоений UI и квадратиков emoji.
        /// </summary>
        private void EnsureRuntimeHierarchy()
        {
            FixLooseStatusBadge();

            if (_catalogPanel != null && _playPanel != null && _catalogContent != null)
            {
                BindNavigationButtons();
                return;
            }

            Transform catTr = transform.Find("CatalogPanel");
            Transform playTr = transform.Find("PlayPanel");

            if (catTr != null) _catalogPanel = catTr.gameObject;
            if (playTr != null) _playPanel = playTr.gameObject;

            if (_playPanel == null)
            {
                _playPanel = new GameObject("PlayPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
                _playPanel.transform.SetParent(transform, false);

                RectTransform playRt = _playPanel.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.offsetMin = Vector2.zero;
                playRt.offsetMax = Vector2.zero;

                VerticalLayoutGroup playVlg = _playPanel.GetComponent<VerticalLayoutGroup>();
                playVlg.padding = new RectOffset(4, 4, 4, 4);
                playVlg.spacing = 4f;
                playVlg.childControlHeight = true;
                playVlg.childControlWidth = true;
                playVlg.childForceExpandHeight = false;
                playVlg.childForceExpandWidth = true;

                // 1. Единый компактный тулбар навигации и статуса (высота 40)
                GameObject navObj = new GameObject("PlayTopNav", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                navObj.transform.SetParent(_playPanel.transform, false);
                LayoutElement nLe = navObj.GetComponent<LayoutElement>();
                nLe.minHeight = 40f;
                nLe.preferredHeight = 40f;
                nLe.flexibleHeight = 0f;
                nLe.layoutPriority = 10;

                HorizontalLayoutGroup nHlg = navObj.GetComponent<HorizontalLayoutGroup>();
                nHlg.padding = new RectOffset(12, 12, 3, 3);
                nHlg.spacing = 10f;
                nHlg.childAlignment = TextAnchor.MiddleCenter;
                nHlg.childControlHeight = true;
                nHlg.childControlWidth = true;
                nHlg.childForceExpandHeight = true;
                nHlg.childForceExpandWidth = false;

                // Кнопка В главное меню
                GameObject btnMenu = CreateSimpleButton(navObj.transform, "BtnPlayMainMenu", "◄  В главное меню", 175f, 38f, new Color(0.10f, 0.48f, 0.90f, 1f), new Color(0.35f, 0.95f, 1f, 1f), ReturnToMainMenu);
                _btnPlayMainMenu = btnMenu.GetComponent<Button>();

                // Кнопка Каталог тестов / Список уровней
                GameObject btnCat = CreateSimpleButton(navObj.transform, "BtnPlayCatalog", "≡  Список уровней", 165f, 38f, new Color(0.38f, 0.22f, 0.85f, 1f), new Color(0.78f, 0.58f, 1f, 1f), ShowCatalog);
                _btnPlayCatalog = btnCat.GetComponent<Button>();

                // Заголовок уровня прямо в тулбаре
                if (_titleText != null)
                {
                    _titleText.transform.SetParent(navObj.transform, false);
                    LayoutElement ttLe = _titleText.GetComponent<LayoutElement>();
                    if (ttLe == null) ttLe = _titleText.gameObject.AddComponent<LayoutElement>();
                    ttLe.flexibleWidth = 1f;
                    ttLe.minWidth = 180f;
                    ttLe.preferredHeight = 38f;
                    _titleText.alignment = TextAlignmentOptions.Center;
                    _titleText.fontSize = 17;
                    _titleText.fontStyle = FontStyles.Bold;
                }

                // Бейдж статуса в тулбаре
                if (_statusBadgeBg != null)
                {
                    _statusBadgeBg.transform.SetParent(navObj.transform, false);
                    LayoutElement sbLe = _statusBadgeBg.GetComponent<LayoutElement>();
                    if (sbLe == null) sbLe = _statusBadgeBg.gameObject.AddComponent<LayoutElement>();
                    sbLe.preferredWidth = 135f;
                    sbLe.preferredHeight = 32f;
                    sbLe.flexibleWidth = 0f;
                    sbLe.flexibleHeight = 0f;
                }

                // Бейдж режима в самом правом конце тулбара
                GameObject mbObj = new GameObject("ModeBadge", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                mbObj.transform.SetParent(navObj.transform, false);
                LayoutElement mbLe = mbObj.GetComponent<LayoutElement>();
                mbLe.preferredWidth = 140f;
                mbLe.preferredHeight = 32f;
                mbLe.flexibleWidth = 0f;
                _modeBadgeText = mbObj.GetComponent<TextMeshProUGUI>();
                _modeBadgeText.text = "ОДИНОЧНЫЙ ТЕСТ";
                _modeBadgeText.fontSize = 12;
                _modeBadgeText.fontStyle = FontStyles.Bold;
                _modeBadgeText.alignment = TextAlignmentOptions.Right;
                _modeBadgeText.color = new Color(0.35f, 0.85f, 1f);

                // 2. Инструкция (высота 20)
                if (_instructionText != null)
                {
                    _instructionText.transform.SetParent(_playPanel.transform, false);
                    LayoutElement itLe = _instructionText.GetComponent<LayoutElement>();
                    if (itLe == null) itLe = _instructionText.gameObject.AddComponent<LayoutElement>();
                    itLe.minHeight = 20f;
                    itLe.preferredHeight = 20f;
                    itLe.flexibleHeight = 0f;
                    itLe.flexibleWidth = 1f;
                    _instructionText.fontSize = 13;
                    _instructionText.alignment = TextAlignmentOptions.Center;
                    _instructionText.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
                }

                // 3. Игровая зона (высота 390-420, без маски чтобы ничего не срезалось)
                if (_playAreaContainer != null)
                {
                    _playAreaContainer.transform.SetParent(_playPanel.transform, false);
                    RectMask2D mask = _playAreaContainer.GetComponent<RectMask2D>();
                    if (mask != null) Destroy(mask);

                    LayoutElement paLe = _playAreaContainer.GetComponent<LayoutElement>();
                    if (paLe == null) paLe = _playAreaContainer.gameObject.AddComponent<LayoutElement>();
                    paLe.flexibleHeight = 1f;
                    paLe.minHeight = 240f;
                        paLe.preferredHeight = 310f;
                }

                // 4. Нижняя панель управления (высота 36)
                GameObject bottomObj = new GameObject("PlayBottomControls", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                bottomObj.transform.SetParent(_playPanel.transform, false);
                LayoutElement pbLe = bottomObj.GetComponent<LayoutElement>();
                pbLe.minHeight = 36f;
                pbLe.preferredHeight = 36f;
                pbLe.flexibleHeight = 0f;
                pbLe.layoutPriority = 10;

                HorizontalLayoutGroup pbHlg = bottomObj.GetComponent<HorizontalLayoutGroup>();
                pbHlg.spacing = 10f;
                pbHlg.childAlignment = TextAnchor.MiddleCenter;
                pbHlg.childControlHeight = true;
                pbHlg.childControlWidth = true;
                pbHlg.childForceExpandHeight = true;
                pbHlg.childForceExpandWidth = false;
                pbHlg.childControlHeight = true;
                pbHlg.childControlWidth = true;
                pbHlg.childForceExpandHeight = true;
                pbHlg.childForceExpandWidth = false;

                if (_btnPrev == null)
                {
                    GameObject btnPrev = CreateSimpleButton(bottomObj.transform, "BtnPrev", "Предыдущий", 140f, 38f, new Color(0.12f, 0.16f, 0.22f, 1f));
                    _btnPrev = btnPrev.GetComponent<Button>();
                    _btnPrev.onClick.AddListener(OnPrevClicked);
                }
                else
                {
                    _btnPrev.transform.SetParent(bottomObj.transform, false);
                }

                if (_btnReset != null)
                {
                    _btnReset.transform.SetParent(bottomObj.transform, false);
                    LayoutElement rLe = _btnReset.GetComponent<LayoutElement>();
                    if (rLe != null) { rLe.preferredWidth = 120f; rLe.preferredHeight = 38f; }
                    TMP_Text rTxt = _btnReset.GetComponentInChildren<TMP_Text>();
                    if (rTxt != null) rTxt.text = "Сброс";
                }

                if (_btnNext != null)
                {
                    _btnNext.transform.SetParent(bottomObj.transform, false);
                    LayoutElement nxtLe = _btnNext.GetComponent<LayoutElement>();
                    if (nxtLe != null) { nxtLe.preferredWidth = 140f; nxtLe.preferredHeight = 38f; }
                    TMP_Text nTxt = _btnNext.GetComponentInChildren<TMP_Text>();
                    if (nTxt != null) nTxt.text = "Следующий";
                }

                GameObject progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                progObj.transform.SetParent(bottomObj.transform, false);
                LayoutElement pLe = progObj.GetComponent<LayoutElement>();
                pLe.flexibleWidth = 1f;
                pLe.preferredHeight = 38f;
                _progressText = progObj.GetComponent<TextMeshProUGUI>();
                _progressText.text = "Прогресс: 0%";
                _progressText.fontSize = 14;
                _progressText.alignment = TextAlignmentOptions.Right;
                _progressText.color = new Color(0.8f, 0.85f, 0.9f);
            }

            if (_catalogPanel == null)
            {
                _catalogPanel = new GameObject("CatalogPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
                _catalogPanel.transform.SetParent(transform, false);

                RectTransform catRt = _catalogPanel.GetComponent<RectTransform>();
                catRt.anchorMin = Vector2.zero;
                catRt.anchorMax = Vector2.one;
                catRt.offsetMin = Vector2.zero;
                catRt.offsetMax = Vector2.zero;

                VerticalLayoutGroup catVlg = _catalogPanel.GetComponent<VerticalLayoutGroup>();
                catVlg.padding = new RectOffset(8, 8, 8, 8);
                catVlg.spacing = 10f;
                catVlg.childControlHeight = true;
                catVlg.childControlWidth = true;
                catVlg.childForceExpandHeight = false;
                catVlg.childForceExpandWidth = true;

                // CatalogHeader
                GameObject catHeader = new GameObject("CatalogHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                catHeader.transform.SetParent(_catalogPanel.transform, false);
                LayoutElement chLe = catHeader.GetComponent<LayoutElement>();
                chLe.preferredHeight = 54f;
                chLe.flexibleHeight = 0f;

                HorizontalLayoutGroup chHlg = catHeader.GetComponent<HorizontalLayoutGroup>();
                chHlg.spacing = 14f;
                chHlg.childAlignment = TextAnchor.MiddleCenter;
                chHlg.childControlHeight = true;
                chHlg.childControlWidth = true;
                chHlg.childForceExpandHeight = true;
                chHlg.childForceExpandWidth = false;

                // BtnCatalogMainMenu (без emoji)
                GameObject btnCatBack = CreateSimpleButton(catHeader.transform, "BtnCatalogMainMenu", "Главное меню", 180f, 46f, new Color(0.18f, 0.24f, 0.32f, 1f));
                _btnCatalogBack = btnCatBack.GetComponent<Button>();
                _btnCatalogBack.onClick.AddListener(ReturnToMainMenu);

                // CatalogTitle
                GameObject catTitle = new GameObject("CatalogTitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                catTitle.transform.SetParent(catHeader.transform, false);
                LayoutElement ctLe = catTitle.GetComponent<LayoutElement>();
                ctLe.flexibleWidth = 1f;
                ctLe.preferredHeight = 46f;
                TextMeshProUGUI ctTmp = catTitle.GetComponent<TextMeshProUGUI>();
                ctTmp.text = $"Каталог 2D-механик ({TotalCount} тестов)";
                ctTmp.fontSize = 20;
                ctTmp.fontStyle = FontStyles.Bold;
                ctTmp.alignment = TextAlignmentOptions.Center;
                ctTmp.color = Color.white;

                // BtnRunSequential (без emoji)
                GameObject btnSeq = CreateSimpleButton(catHeader.transform, "BtnRunSequential", $"Автопрогон всех тестов (1 -> {TotalCount})", 300f, 46f, new Color(0.05f, 0.58f, 0.4f, 1f));
                _btnRunSequential = btnSeq.GetComponent<Button>();
                _btnRunSequential.onClick.AddListener(LaunchSequentialMode);

                // CatalogScrollView
                GameObject catScroll = new GameObject("CatalogScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                catScroll.transform.SetParent(_catalogPanel.transform, false);
                LayoutElement csLe = catScroll.GetComponent<LayoutElement>();
                csLe.flexibleHeight = 1f;
                csLe.minHeight = 350f;
                Image csImg = catScroll.GetComponent<Image>();
                csImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
                ScrollRect csSr = catScroll.GetComponent<ScrollRect>();
                csSr.horizontal = false;
                csSr.vertical = true;

                // Viewport
                GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                vp.transform.SetParent(catScroll.transform, false);
                RectTransform vpRt = vp.GetComponent<RectTransform>();
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.offsetMin = new Vector2(4, 4);
                vpRt.offsetMax = new Vector2(-4, -4);
                csSr.viewport = vpRt;

                // Content
                GameObject cnt = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                cnt.transform.SetParent(vp.transform, false);
                RectTransform cntRt = cnt.GetComponent<RectTransform>();
                cntRt.anchorMin = new Vector2(0f, 1f);
                cntRt.anchorMax = new Vector2(1f, 1f);
                cntRt.pivot = new Vector2(0.5f, 1f);
                cntRt.offsetMin = Vector2.zero;
                cntRt.offsetMax = Vector2.zero;
                csSr.content = cntRt;
                _catalogContent = cntRt;

                VerticalLayoutGroup cntVlg = cnt.GetComponent<VerticalLayoutGroup>();
                cntVlg.padding = new RectOffset(8, 8, 8, 8);
                cntVlg.spacing = 8f;
                cntVlg.childControlHeight = true;
                cntVlg.childControlWidth = true;
                cntVlg.childForceExpandHeight = false;
                cntVlg.childForceExpandWidth = true;

                ContentSizeFitter cntCsf = cnt.GetComponent<ContentSizeFitter>();
                cntCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                cntCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            EnsureVictoryOverlay();

            _catalogPanel.SetActive(true);
            _playPanel.SetActive(false);
        }

        /// <summary>
        /// Создает красивый поздравительный оверлей победы по центру экрана.
        /// </summary>
        private void EnsureVictoryOverlay()
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Transform parentTr = rootCanvas != null ? rootCanvas.transform : transform;

            if (_victoryOverlay != null)
            {
                if (_victoryOverlay.transform.parent != parentTr)
                {
                    _victoryOverlay.transform.SetParent(parentTr, false);
                }
                return;
            }

            Transform existing = parentTr.Find("VictoryOverlay");
            if (existing == null) existing = transform.Find("VictoryOverlay");
            if (existing != null)
            {
                _victoryOverlay = existing.gameObject;
                _victoryOverlay.transform.SetParent(parentTr, false);

                LayoutElement le = _victoryOverlay.GetComponent<LayoutElement>();
                if (le == null) le = _victoryOverlay.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                RectTransform ert = _victoryOverlay.GetComponent<RectTransform>();
                ert.anchorMin = Vector2.zero;
                ert.anchorMax = Vector2.one;
                ert.offsetMin = Vector2.zero;
                ert.offsetMax = Vector2.zero;

                _victoryTitleText = _victoryOverlay.transform.Find("VictoryCard/Title")?.GetComponent<TMP_Text>();
                _victorySubText = _victoryOverlay.transform.Find("VictoryCard/SubText")?.GetComponent<TMP_Text>();
                return;
            }

            _victoryOverlay = new GameObject("VictoryOverlay", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _victoryOverlay.transform.SetParent(parentTr, false);

            LayoutElement voLe = _victoryOverlay.GetComponent<LayoutElement>();
            voLe.ignoreLayout = true;

            RectTransform voRt = _victoryOverlay.GetComponent<RectTransform>();
            voRt.anchorMin = Vector2.zero;
            voRt.anchorMax = Vector2.one;
            voRt.offsetMin = Vector2.zero;
            voRt.offsetMax = Vector2.zero;

            Image voBg = _victoryOverlay.GetComponent<Image>();
            voBg.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);

            // Карточка по центру
            GameObject card = new GameObject("VictoryCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(_victoryOverlay.transform, false);

            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 210f);
            cardRt.anchoredPosition = Vector2.zero;

            Image cardImg = card.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.13f, 0.19f, 0.98f);

            VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            // Звезды
            GameObject starsObj = new GameObject("Stars", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            starsObj.transform.SetParent(card.transform, false);
            LayoutElement sLe = starsObj.GetComponent<LayoutElement>();
            sLe.preferredHeight = 36f;
            TextMeshProUGUI starsTmp = starsObj.GetComponent<TextMeshProUGUI>();
            starsTmp.text = "[ * * * ]";
            starsTmp.fontSize = 24;
            starsTmp.fontStyle = FontStyles.Bold;
            starsTmp.alignment = TextAlignmentOptions.Center;
            starsTmp.color = new Color(1f, 0.85f, 0.2f);

            // Заголовок победы
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleObj.transform.SetParent(card.transform, false);
            LayoutElement tLe = titleObj.GetComponent<LayoutElement>();
            tLe.preferredHeight = 44f;
            _victoryTitleText = titleObj.GetComponent<TextMeshProUGUI>();
            _victoryTitleText.text = "ТЕСТ УСПЕШНО ПРОЙДЕН!";
            _victoryTitleText.fontSize = 22;
            _victoryTitleText.fontStyle = FontStyles.Bold;
            _victoryTitleText.alignment = TextAlignmentOptions.Center;
            _victoryTitleText.color = new Color(0.2f, 1f, 0.55f);

            // Подзаголовок
            GameObject subObj = new GameObject("SubText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            subObj.transform.SetParent(card.transform, false);
            LayoutElement subLe = subObj.GetComponent<LayoutElement>();
            subLe.preferredHeight = 32f;
            _victorySubText = subObj.GetComponent<TextMeshProUGUI>();
            _victorySubText.text = "Возврат в каталог 2D-механик...";
            _victorySubText.fontSize = 15;
            _victorySubText.alignment = TextAlignmentOptions.Center;
            _victorySubText.color = new Color(0.7f, 0.85f, 1f);

            _victoryOverlay.SetActive(false);
        }

        private void BindNavigationButtons()
        {
            if (_playPanel != null)
            {
                Transform navTr = _playPanel.transform.Find("PlayTopNav");
                if (navTr != null)
                {
                    GraphicRaycaster navGr = navTr.GetComponent<GraphicRaycaster>();
                    if (navGr != null) Destroy(navGr);
                    Canvas navCanvas = navTr.GetComponent<Canvas>();
                    if (navCanvas != null) Destroy(navCanvas);

                    LayoutElement pnLe = navTr.GetComponent<LayoutElement>();
                    if (pnLe != null)
                    {
                        pnLe.minHeight = 44f;
                        pnLe.preferredHeight = 44f;
                        pnLe.flexibleHeight = 0f;
                        pnLe.layoutPriority = 10;
                    }

                    HorizontalLayoutGroup pnHlg = navTr.GetComponent<HorizontalLayoutGroup>();
                    if (pnHlg != null)
                    {
                        pnHlg.padding = new RectOffset(12, 12, 3, 3);
                        pnHlg.spacing = 10f;
                        pnHlg.childAlignment = TextAnchor.MiddleCenter;
                        pnHlg.childControlWidth = true;
                        pnHlg.childControlHeight = true;
                        pnHlg.childForceExpandWidth = false;
                        pnHlg.childForceExpandHeight = true;
                    }

                    // Переносим Title и StatusBadge в PlayTopNav, если они лежали в отдельной полосе PlayHeaderInfo / PlayInfoRow
                    Transform phiTr = _playPanel.transform.Find("PlayHeaderInfo");
                    if (phiTr == null) phiTr = _playPanel.transform.Find("PlayInfoRow");
                    if (phiTr != null)
                    {
                        Transform tChild = phiTr.Find("World2DSuiteWindow_Title");
                        if (tChild != null) tChild.SetParent(navTr, false);
                        Transform bChild = phiTr.Find("StatusBadge");
                        if (bChild != null) bChild.SetParent(navTr, false);
                        Destroy(phiTr.gameObject);
                    }

                    if (_btnPlayMainMenu == null)
                    {
                        Transform tr = navTr.Find("BtnPlayMainMenu");
                        if (tr != null) _btnPlayMainMenu = tr.GetComponent<Button>();
                    }
                    if (_btnPlayCatalog == null)
                    {
                        Transform tr = navTr.Find("BtnPlayCatalog");
                        if (tr != null) _btnPlayCatalog = tr.GetComponent<Button>();
                    }
                    if (_titleText == null)
                    {
                        Transform tr = navTr.Find("World2DSuiteWindow_Title");
                        if (tr != null) _titleText = tr.GetComponent<TMP_Text>();
                    }
                    if (_statusBadgeBg == null)
                    {
                        Transform tr = navTr.Find("StatusBadge");
                        if (tr != null) _statusBadgeBg = tr.GetComponent<Image>();
                    }
                    if (_modeBadgeText == null)
                    {
                        Transform tr = navTr.Find("ModeBadge");
                        if (tr != null) _modeBadgeText = tr.GetComponent<TMP_Text>();
                    }

                    if (_btnPlayMainMenu != null) _btnPlayMainMenu.transform.SetSiblingIndex(0);
                    if (_btnPlayCatalog != null) _btnPlayCatalog.transform.SetSiblingIndex(1);
                    if (_titleText != null)
                    {
                        _titleText.transform.SetParent(navTr, false);
                        _titleText.transform.SetSiblingIndex(2);
                        LayoutElement ttLe = _titleText.GetComponent<LayoutElement>();
                        if (ttLe == null) ttLe = _titleText.gameObject.AddComponent<LayoutElement>();
                        ttLe.flexibleWidth = 1f;
                        ttLe.minWidth = 180f;
                        ttLe.preferredHeight = 38f;
                        _titleText.alignment = TextAlignmentOptions.Center;
                        _titleText.fontSize = 17;
                    }
                    if (_statusBadgeBg != null)
                    {
                        _statusBadgeBg.transform.SetParent(navTr, false);
                        _statusBadgeBg.transform.SetSiblingIndex(3);
                        LayoutElement sbLe = _statusBadgeBg.GetComponent<LayoutElement>();
                        if (sbLe == null) sbLe = _statusBadgeBg.gameObject.AddComponent<LayoutElement>();
                        sbLe.preferredWidth = 135f;
                        sbLe.preferredHeight = 32f;
                        sbLe.flexibleWidth = 0f;
                        sbLe.flexibleHeight = 0f;
                    }
                    if (_modeBadgeText != null)
                    {
                        _modeBadgeText.transform.SetParent(navTr, false);
                        _modeBadgeText.transform.SetSiblingIndex(4);
                        LayoutElement mbLe = _modeBadgeText.GetComponent<LayoutElement>();
                        if (mbLe == null) mbLe = _modeBadgeText.gameObject.AddComponent<LayoutElement>();
                        mbLe.preferredWidth = 140f;
                        mbLe.preferredHeight = 32f;
                        mbLe.flexibleWidth = 0f;
                        _modeBadgeText.fontSize = 12;
                        _modeBadgeText.alignment = TextAlignmentOptions.Right;
                    }
                }

                if (_instructionText != null)
                {
                    LayoutElement hiLe = _instructionText.GetComponent<LayoutElement>();
                    if (hiLe == null) hiLe = _instructionText.gameObject.AddComponent<LayoutElement>();
                    hiLe.minHeight = 20f;
                    hiLe.preferredHeight = 20f;
                    hiLe.flexibleHeight = 0f;
                    _instructionText.fontSize = 13;
                }

                if (_playAreaContainer != null)
                {
                    // Убираем RectMask2D, чтобы склад мостиков, фишки, палитры и плашки инвентаря внизу НЕ срезались маской!
                    RectMask2D mask = _playAreaContainer.GetComponent<RectMask2D>();
                    if (mask != null) Destroy(mask);

                    LayoutElement paLe = _playAreaContainer.GetComponent<LayoutElement>();
                    if (paLe != null)
                    {
                        paLe.minHeight = 390f;
                        paLe.preferredHeight = 420f;
                        paLe.flexibleHeight = 1f;
                    }
                }

                Transform pbTr = _playPanel.transform.Find("PlayBottomControls");
                if (pbTr != null)
                {
                    LayoutElement pbLe = pbTr.GetComponent<LayoutElement>();
                    if (pbLe != null)
                    {
                        pbLe.minHeight = 36f;
                        pbLe.preferredHeight = 36f;
                        pbLe.flexibleHeight = 0f;
                        pbLe.layoutPriority = 10;
                    }
                }
            }

            if (_catalogPanel != null)
            {
                Transform catHeaderTr = _catalogPanel.transform.Find("CatalogHeader");
                if (catHeaderTr != null)
                {
                    GraphicRaycaster catGr = catHeaderTr.GetComponent<GraphicRaycaster>();
                    if (catGr != null) Destroy(catGr);
                    Canvas catCanvas = catHeaderTr.GetComponent<Canvas>();
                    if (catCanvas != null) Destroy(catCanvas);
                }

                if (_btnCatalogBack == null)
                {
                    Transform tr = _catalogPanel.transform.Find("CatalogHeader/BtnCatalogMainMenu");
                    if (tr == null) tr = transform.Find("World2DSuiteWindow_BackButton");
                    if (tr != null) _btnCatalogBack = tr.GetComponent<Button>();
                }
                if (_btnRunSequential == null)
                {
                    Transform tr = _catalogPanel.transform.Find("CatalogHeader/BtnRunSequential");
                    if (tr != null) _btnRunSequential = tr.GetComponent<Button>();
                }
            }

            // Настраиваем сочный визуальный стиль, контур, тень, спрайт и кликабельность
            SetupButtonVisuals(_btnPlayMainMenu, "◄  В главное меню", new Color(0.10f, 0.48f, 0.90f, 1f), new Color(0.35f, 0.95f, 1f, 1f), ReturnToMainMenu);
            SetupButtonVisuals(_btnPlayCatalog, "≡  Список уровней", new Color(0.38f, 0.22f, 0.85f, 1f), new Color(0.78f, 0.58f, 1f, 1f), ShowCatalog);
            SetupButtonVisuals(_btnCatalogBack, "◄  В главное меню", new Color(0.10f, 0.48f, 0.90f, 1f), new Color(0.35f, 0.95f, 1f, 1f), ReturnToMainMenu);
            SetupButtonVisuals(_btnRunSequential, $">>  Автопрогон всех тестов (1 -> {TotalCount})", new Color(0.08f, 0.6f, 0.42f, 1f), new Color(0.25f, 1f, 0.65f, 0.95f), LaunchSequentialMode);

            // Кликабельный заголовок уровня: при нажатии на название или статус открывается каталог уровней
            if (_titleText != null)
            {
                Button titleBtn = _titleText.GetComponent<Button>();
                if (titleBtn == null) titleBtn = _titleText.gameObject.AddComponent<Button>();
                titleBtn.onClick.RemoveAllListeners();
                titleBtn.onClick.AddListener(ShowCatalog);

                NavButtonClickHandler titleNav = _titleText.GetComponent<NavButtonClickHandler>();
                if (titleNav == null) titleNav = _titleText.gameObject.AddComponent<NavButtonClickHandler>();
                titleNav.OnClicked = ShowCatalog;

                _titleText.raycastTarget = true;
            }

            if (_statusBadgeBg != null)
            {
                Button badgeBtn = _statusBadgeBg.GetComponent<Button>();
                if (badgeBtn == null) badgeBtn = _statusBadgeBg.gameObject.AddComponent<Button>();
                badgeBtn.onClick.RemoveAllListeners();
                badgeBtn.onClick.AddListener(ShowCatalog);

                NavButtonClickHandler badgeNav = _statusBadgeBg.GetComponent<NavButtonClickHandler>();
                if (badgeNav == null) badgeNav = _statusBadgeBg.gameObject.AddComponent<NavButtonClickHandler>();
                badgeNav.OnClicked = ShowCatalog;

                _statusBadgeBg.raycastTarget = true;
            }

            DisableNonButtonRaycasts();
        }

        private static Sprite _cachedProceduralNavSprite = null;

        private Sprite GetOrCreateNavSprite()
        {
            if (_navButtonSprite != null) return _navButtonSprite;
#if UNITY_EDITOR
            _navButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Btn_Nav_Pill.png");
            if (_navButtonSprite != null) return _navButtonSprite;
#endif
            if (_cachedProceduralNavSprite != null) return _cachedProceduralNavSprite;

            int w = 64, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] colors = new Color[w * h];
            Color cBorder = new Color(0.2f, 0.9f, 1f, 1f);
            Color cBgTop = new Color(0.12f, 0.38f, 0.68f, 0.95f);
            Color cBgBottom = new Color(0.06f, 0.16f, 0.32f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float ty = (float)y / (h - 1);
                Color rowBg = Color.Lerp(cBgBottom, cBgTop, ty);
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x <= 2 || x >= w - 3 || y <= 2 || y >= h - 3;
                    colors[y * w + x] = isBorder ? cBorder : rowBg;
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            _cachedProceduralNavSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            return _cachedProceduralNavSprite;
        }

        private void SetupButtonVisuals(Button btn, string label, Color bgColor, Color outlineColor, System.Action onClickAction = null)
        {
            if (btn == null) return;

            // КРИТИЧНО: если Image отсутствует, кнопка невидима и GraphicRaycaster не может по ней кликнуть!
            Image img = btn.GetComponent<Image>();
            if (img == null) img = btn.gameObject.AddComponent<Image>();
            btn.targetGraphic = img;

            Sprite sp = GetOrCreateNavSprite();
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.color = bgColor != default ? bgColor : Color.white;
            }
            else
            {
                img.color = bgColor;
            }
            img.raycastTarget = true;

            LayoutElement btnLe = btn.GetComponent<LayoutElement>();
            if (btnLe != null)
            {
                btnLe.minHeight = 42f;
                btnLe.minWidth = 175f;
                btnLe.layoutPriority = 10;
            }

            Outline outline = btn.GetComponent<Outline>();
            if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = btn.GetComponent<Shadow>();
            if (shadow == null) shadow = btn.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -3f);

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var tmps = btn.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps)
            {
                t.raycastTarget = false; // КРИТИЧНО: дочерний текст не должен блокировать raycast кнопки!
                t.fontStyle = FontStyles.Bold;
                t.fontSize = 15;
                t.alignment = TextAlignmentOptions.Center;
                t.color = Color.white;
                if (!string.IsNullOrEmpty(label)) t.text = label;

                if (onClickAction != null)
                {
                    var textHandler = t.GetComponent<NavButtonClickHandler>();
                    if (textHandler == null) textHandler = t.gameObject.AddComponent<NavButtonClickHandler>();
                    textHandler.OnClicked = onClickAction;
                }
            }

            if (onClickAction != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClickAction());

                var navHandler = btn.GetComponent<NavButtonClickHandler>();
                if (navHandler == null) navHandler = btn.gameObject.AddComponent<NavButtonClickHandler>();
                navHandler.OnClicked = onClickAction;
            }
        }

        private GameObject CreateSimpleButton(Transform parent, string name, string label, float width, float height, Color bgColor, Color outlineColor = default, System.Action onClickAction = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(Shadow), typeof(NavButtonClickHandler));
            obj.transform.SetParent(parent, false);

            LayoutElement le = obj.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            le.layoutPriority = 10;

            Image img = obj.GetComponent<Image>();
            Sprite sp = GetOrCreateNavSprite();
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.color = bgColor != default ? bgColor : Color.white;
            }
            else
            {
                img.color = bgColor;
            }
            img.raycastTarget = true;

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = img;

            Outline outline = obj.GetComponent<Outline>();
            outline.effectColor = outlineColor != default ? outlineColor : new Color(0.25f, 0.95f, 1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = obj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -3f);

            GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(NavButtonClickHandler));
            lbl.transform.SetParent(obj.transform, false);
            RectTransform lRt = lbl.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = lbl.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // Текст не блокирует клики!

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClickAction != null)
            {
                btn.onClick.AddListener(() => onClickAction());
                NavButtonClickHandler handler = obj.GetComponent<NavButtonClickHandler>();
                if (handler != null) handler.OnClicked = onClickAction;
                NavButtonClickHandler textHandler = lbl.GetComponent<NavButtonClickHandler>();
                if (textHandler != null) textHandler.OnClicked = onClickAction;
            }

            return obj;
        }

        private void DisableNonButtonRaycasts()
        {
            if (_playAreaContainer != null)
            {
                Image paImg = _playAreaContainer.GetComponent<Image>();
                if (paImg != null) paImg.raycastTarget = false;
            }

            if (_playPanel != null)
            {
                foreach (var tmp in _playPanel.GetComponentsInChildren<TMP_Text>(true))
                {
                    tmp.raycastTarget = false;
                }

                foreach (var img in _playPanel.GetComponentsInChildren<Image>(true))
                {
                    if (img.GetComponent<Button>() == null && (img.name.Contains("Badge") || img.name.Contains("Info") || img.name.Contains("TopNav") || img.name.Contains("Header") || img.name.Contains("Title") || img.name.Contains("Area") || img.name.Contains("Panel")))
                    {
                        img.raycastTarget = false;
                    }
                }
            }

            if (_catalogPanel != null)
            {
                foreach (var tmp in _catalogPanel.GetComponentsInChildren<TMP_Text>(true))
                {
                    tmp.raycastTarget = false;
                }

                foreach (var img in _catalogPanel.GetComponentsInChildren<Image>(true))
                {
                    if (img.GetComponent<Button>() == null && (img.name.Contains("Badge") || img.name.Contains("Info") || img.name.Contains("Header") || img.name.Contains("Title") || img.name.Contains("Panel")))
                    {
                        img.raycastTarget = false;
                    }
                }
            }
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = FindInChildren(parent.GetChild(i), name);
                if (child != null) return child;
            }
            return null;
        }
        #endregion
    }

    /// <summary>
    /// Гарантированный обработчик кликов для навигационных кнопок (тройная страховка EventSystem).
    /// </summary>
    public class NavButtonClickHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        public System.Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[NavButtonClickHandler] OnPointerClick on {gameObject.name}");
            OnClicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }
    }
}

