using System.Collections.Generic;
using System.IO;
using KBP.URDT.TestPoligon.Mechanics2D.Core;
using KBP.URDT.TestPoligon.Mechanics2D.M01_SnapToSlot;
using KBP.URDT.TestPoligon.Mechanics2D.M02_ContainerSorting;
using KBP.URDT.TestPoligon.Mechanics2D.M03_WeightComparator;
using KBP.URDT.TestPoligon.Mechanics2D.M04_MultiLayerAttachment;
using KBP.URDT.TestPoligon.Mechanics2D.M05_TimelineSequencer;
using KBP.URDT.TestPoligon.Mechanics2D.M06_NodePairing;
using KBP.URDT.TestPoligon.Mechanics2D.M07_WobbleAndSnap;
using KBP.URDT.TestPoligon.Mechanics2D.M08_WaypointTracking;
using KBP.URDT.TestPoligon.Mechanics2D.M09_CoverageAccumulator;
using KBP.URDT.TestPoligon.Mechanics2D.M10_TimedAccumulator;
using KBP.URDT.TestPoligon.Mechanics2D.M11_ConeEmitter;
using KBP.URDT.TestPoligon.Mechanics2D.M12_AngularDeltaTracker;
using KBP.URDT.TestPoligon.Mechanics2D.M13_LaneSwitcherRunner;
using KBP.URDT.TestPoligon.Mechanics2D.M14_SlingshotImpulser;
using KBP.URDT.TestPoligon.Mechanics2D.M15_TimingInterceptor;
using KBP.URDT.TestPoligon.Mechanics2D.M16_RhythmPhaseDetector;
using KBP.URDT.TestPoligon.Mechanics2D.M17_TugOfWarBalance;
using KBP.URDT.TestPoligon.Mechanics2D.M18_GraphFlowClosure;
using KBP.URDT.TestPoligon.Mechanics2D.M19_FloodFillColoring;
using KBP.URDT.TestPoligon.Mechanics2D.M20_VerticalStacking;
using KBP.URDT.TestPoligon.Mechanics2D.M21_ReactionProbe;
using KBP.URDT.TestPoligon.Mechanics2D.M22_GridPathfinding;
using KBP.URDT.TestPoligon.Mechanics2D.M23_StencilReveal;
using KBP.URDT.TestPoligon.Mechanics2D.M24_TargetElimination;
using KBP.URDT.TestPoligon.Mechanics2D.M27_PhysicsCarHills;
using KBP.URDT.TestPoligon.Mechanics2D.M28_DualBridgeRoute;
using KBP.URDT.TestPoligon.Mechanics2D.M29_ContourCutting;
using KBP.URDT.TestPoligon.Mechanics2D.M30_CharacterDressUp;
using KBP.URDT.TestPoligon.Mechanics2D.M31_LiquidFilling;
using KBP.URDT.TestPoligon.Mechanics2D.M32_AirplaneStarGlider;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Editor
{
    /// <summary>
    /// Автоматизированный строитель префабов и сцены 2D-полигона механик (32 механики).
    /// Создает монолитные префабы с правильной привязкой компонентов, спрайтов и иерархии.
    /// </summary>
    public static class UrdtMechanics2DBuilder
    {
        private const string BASE_DIR = "Assets/URDT_TestPoligon/2D_Polygon";
        private const string PREFABS_DIR = BASE_DIR + "/Prefabs";
        private const string SPRITES_DIR = BASE_DIR + "/Sprites";
        private const string M01_PREFAB_PATH = PREFABS_DIR + "/M01_SnapToSlot.prefab";
        private const string M02_PREFAB_PATH = PREFABS_DIR + "/M02_ContainerSorting.prefab";
        private const string M03_PREFAB_PATH = PREFABS_DIR + "/M03_WeightComparator.prefab";
        private const string M04_PREFAB_PATH = PREFABS_DIR + "/M04_MultiLayerAttachment.prefab";
        private const string M05_PREFAB_PATH = PREFABS_DIR + "/M05_TimelineSequencer.prefab";
        private const string M06_PREFAB_PATH = PREFABS_DIR + "/M06_NodePairing.prefab";
        private const string M07_PREFAB_PATH = PREFABS_DIR + "/M07_WobbleAndSnap.prefab";
        private const string M08_PREFAB_PATH = PREFABS_DIR + "/M08_WaypointTracking.prefab";
        private const string M09_PREFAB_PATH = PREFABS_DIR + "/M09_CoverageAccumulator.prefab";
        private const string M10_PREFAB_PATH = PREFABS_DIR + "/M10_TimedAccumulator.prefab";
        private const string M11_PREFAB_PATH = PREFABS_DIR + "/M11_ConeEmitter.prefab";
        private const string M12_PREFAB_PATH = PREFABS_DIR + "/M12_AngularDeltaTracker.prefab";
        private const string M13_PREFAB_PATH = PREFABS_DIR + "/M13_LaneSwitcherRunner.prefab";
        private const string M14_PREFAB_PATH = PREFABS_DIR + "/M14_SlingshotImpulser.prefab";
        private const string M15_PREFAB_PATH = PREFABS_DIR + "/M15_TimingInterceptor.prefab";
        private const string M16_PREFAB_PATH = PREFABS_DIR + "/M16_RhythmPhaseDetector.prefab";
        private const string M17_PREFAB_PATH = PREFABS_DIR + "/M17_TugOfWarBalance.prefab";
        private const string M18_PREFAB_PATH = PREFABS_DIR + "/M18_GraphFlowClosure.prefab";
        private const string M19_PREFAB_PATH = PREFABS_DIR + "/M19_FloodFillColoring.prefab";
        private const string M20_PREFAB_PATH = PREFABS_DIR + "/M20_VerticalStacking.prefab";
        private const string M21_PREFAB_PATH = PREFABS_DIR + "/M21_ReactionProbe.prefab";
        private const string M22_PREFAB_PATH = PREFABS_DIR + "/M22_GridPathfinding.prefab";
        private const string M23_PREFAB_PATH = PREFABS_DIR + "/M23_StencilReveal.prefab";
        private const string M24_PREFAB_PATH = PREFABS_DIR + "/M24_TargetElimination.prefab";
        private const string M25_PREFAB_PATH = PREFABS_DIR + "/M25_LaneSwitcherTapHalves.prefab";
        private const string M26_PREFAB_PATH = PREFABS_DIR + "/M26_LaneSwitcherDirectDrag.prefab";
        private const string M27_PREFAB_PATH = PREFABS_DIR + "/M27_PhysicsCarHills.prefab";
        private const string M28_PREFAB_PATH = PREFABS_DIR + "/M28_DualBridgeRoute.prefab";
        private const string M29_PREFAB_PATH = PREFABS_DIR + "/M29_ContourCutting.prefab";
        private const string M30_PREFAB_PATH = PREFABS_DIR + "/M30_CharacterDressUp.prefab";
        private const string M31_PREFAB_PATH = PREFABS_DIR + "/M31_LiquidFilling.prefab";
        private const string M32_PREFAB_PATH = PREFABS_DIR + "/M32_AirplaneStarGlider.prefab";
        private const string SCENE_PATH = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";

        [MenuItem("Tools/URDT 2D Polygon/Build All 2D Mechanics and Setup Scene")]
        public static void BuildAllAndSetup()
        {
            EnsureDirectories();
            List<GameObject> prefabs = new List<GameObject>();

            prefabs.Add(BuildM01SnapToSlotPrefab());
            prefabs.Add(BuildM02ContainerSortingPrefab());
            prefabs.Add(BuildM03WeightComparatorPrefab());
            prefabs.Add(BuildM04MultiLayerAttachmentPrefab());
            prefabs.Add(BuildM05TimelineSequencerPrefab());
            prefabs.Add(BuildM06NodePairingPrefab());
            prefabs.Add(BuildM07WobbleAndSnapPrefab());
            prefabs.Add(BuildM08WaypointTrackingPrefab());
            prefabs.Add(BuildM09CoverageAccumulatorPrefab());
            prefabs.Add(BuildM10TimedAccumulatorPrefab());
            prefabs.Add(BuildM11ConeEmitterPrefab());
            prefabs.Add(BuildM12AngularDeltaTrackerPrefab());
            prefabs.Add(BuildM13LaneSwitcherRunnerPrefab());
            prefabs.Add(BuildM14SlingshotImpulserPrefab());
            prefabs.Add(BuildM15TimingInterceptorPrefab());
            prefabs.Add(BuildM16RhythmPhaseDetectorPrefab());
            prefabs.Add(BuildM17TugOfWarBalancePrefab());
            prefabs.Add(BuildM18GraphFlowClosurePrefab());
            prefabs.Add(BuildM19FloodFillColoringPrefab());
            prefabs.Add(BuildM20VerticalStackingPrefab());
            prefabs.Add(BuildM21ReactionProbePrefab());
            prefabs.Add(BuildM22GridPathfindingPrefab());
            prefabs.Add(BuildM23StencilRevealPrefab());
            prefabs.Add(BuildM24TargetEliminationPrefab());
            prefabs.Add(BuildM25LaneSwitcherTapHalvesPrefab());
            prefabs.Add(BuildM26LaneSwitcherDirectDragPrefab());
            prefabs.Add(BuildM27PhysicsCarHillsPrefab());
            prefabs.Add(BuildM28DualBridgeRoutePrefab());
            prefabs.Add(BuildM29ContourCuttingPrefab());
            prefabs.Add(BuildM30CharacterDressUpPrefab());
            prefabs.Add(BuildM31LiquidFillingPrefab());
            prefabs.Add(BuildM32AirplaneStarGliderPrefab());

            Setup2DSuiteInScene(prefabs.ToArray());
            Debug.Log($"<color=#00FF99>[2D Polygon Builder] Успешно собрано {prefabs.Count} префабов механик и настроена сцена URDT_TestPoligon_UI!</color>");
            UrdtMechanics2DValidator.ValidateAllMechanics();
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PREFABS_DIR)) Directory.CreateDirectory(PREFABS_DIR);
            if (!Directory.Exists(SPRITES_DIR)) Directory.CreateDirectory(SPRITES_DIR);
        }
#region M01: Snap-to-Slot
        public static GameObject BuildM01SnapToSlotPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M01_SnapToSlot", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite slotCircleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Slot_Circle.png");
            Sprite slotSquareSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Slot_Square.png");
            Sprite slotTriangleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Slot_Triangle.png");

            Sprite itemCircleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Circle.png");
            Sprite itemSquareSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Square.png");
            Sprite itemTriangleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Triangle.png");
            Sprite itemJunkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Junk.png");
            Sprite obstacleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Obstacle_Barrier.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Перетащите цветные детали в соответствующие гнезда. Избегайте мусора (X)!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject slotsContainer = new GameObject("SlotsTargetArea", typeof(RectTransform));
            slotsContainer.transform.SetParent(root.transform, false);
            RectTransform slotsRt = slotsContainer.GetComponent<RectTransform>();
            slotsRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotsRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotsRt.anchoredPosition = new Vector2(0f, 55f);
            slotsRt.sizeDelta = new Vector2(560f, 105f);

            SnapSlot slot1 = CreateM01Slot(slotsContainer.transform, "Slot_Circle", "circle", "Круг", new Vector2(-160f, 0f), slotCircleSpr);
            SnapSlot slot2 = CreateM01Slot(slotsContainer.transform, "Slot_Square", "square", "Квадрат", new Vector2(0f, 0f), slotSquareSpr);
            SnapSlot slot3 = CreateM01Slot(slotsContainer.transform, "Slot_Triangle", "triangle", "Треугольник", new Vector2(160f, 0f), slotTriangleSpr);

            GameObject obstacleObj = new GameObject("Obstacle_Barrier", typeof(RectTransform), typeof(Image));
            obstacleObj.transform.SetParent(root.transform, false);
            RectTransform obsRt = obstacleObj.GetComponent<RectTransform>();
            obsRt.anchorMin = new Vector2(0.5f, 0.5f);
            obsRt.anchorMax = new Vector2(0.5f, 0.5f);
            obsRt.anchoredPosition = new Vector2(0f, -5f);
            obsRt.sizeDelta = new Vector2(620f, 12f);
            Image obsImg = obstacleObj.GetComponent<Image>();
            obsImg.sprite = obstacleSpr;
            obsImg.color = Color.white;

            GameObject trayContainer = new GameObject("ItemsSpawnTray", typeof(RectTransform));
            trayContainer.transform.SetParent(root.transform, false);
            RectTransform trayRt = trayContainer.GetComponent<RectTransform>();
            trayRt.anchorMin = new Vector2(0.5f, 0.5f);
            trayRt.anchorMax = new Vector2(0.5f, 0.5f);
            trayRt.anchoredPosition = new Vector2(0f, -70f);
            trayRt.sizeDelta = new Vector2(620f, 85f);

            SnapDraggableItem item1 = CreateM01Item(trayContainer.transform, "Item_Circle", "circle", false, new Vector2(-210f, 0f), itemCircleSpr);
            SnapDraggableItem item2 = CreateM01Item(trayContainer.transform, "Item_Square", "square", false, new Vector2(-70f, 0f), itemSquareSpr);
            SnapDraggableItem item3 = CreateM01Item(trayContainer.transform, "Item_Triangle", "triangle", false, new Vector2(70f, 0f), itemTriangleSpr);
            SnapDraggableItem itemJunk = CreateM01Item(trayContainer.transform, "Item_Junk_Defective", "junk", true, new Vector2(210f, 0f), itemJunkSpr);

            M01_SnapToSlotMechanic mechanic = root.AddComponent<M01_SnapToSlotMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M01_SnapToSlot";
            so.FindProperty("_title").stringValue = "Механика #1: Drag & Drop Snap-to-Slot";
            so.FindProperty("_instruction").stringValue = "Перетащите подходящие геометрические детали в соответствующие пазы. Остерегайтесь бракованной детали (знак X)!";
            so.FindProperty("_obstacleBarrier").objectReferenceValue = obstacleObj;
            so.FindProperty("_localInstructionText").objectReferenceValue = instrTmp;

            SerializedProperty slotsProp = so.FindProperty("_slots");
            slotsProp.arraySize = 3;
            slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = slot1;
            slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = slot2;
            slotsProp.GetArrayElementAtIndex(2).objectReferenceValue = slot3;

            SerializedProperty itemsProp = so.FindProperty("_items");
            itemsProp.arraySize = 4;
            itemsProp.GetArrayElementAtIndex(0).objectReferenceValue = item1;
            itemsProp.GetArrayElementAtIndex(1).objectReferenceValue = item2;
            itemsProp.GetArrayElementAtIndex(2).objectReferenceValue = item3;
            itemsProp.GetArrayElementAtIndex(3).objectReferenceValue = itemJunk;

            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M01_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M01_SnapToSlot сохранен: {M01_PREFAB_PATH}</color>");
            return prefab;
        }

        private static SnapSlot CreateM01Slot(Transform parent, string name, string slotId, string label, Vector2 position, Sprite sprite)
        {
            GameObject slotObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SnapSlot));
            slotObj.transform.SetParent(parent, false);
            RectTransform rt = slotObj.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(105f, 105f);

            Image img = slotObj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;

            GameObject borderObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(slotObj.transform, false);
            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-4f, -4f);
            borderRt.offsetMax = new Vector2(4f, 4f);
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.3f, 0.4f, 0.5f, 0.4f);
            borderImg.raycastTarget = false;

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(slotObj.transform, false);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -6f);
            labelRt.sizeDelta = new Vector2(140f, 22f);
            TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 13;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            SnapSlot slot = slotObj.GetComponent<SnapSlot>();
            SerializedObject so = new SerializedObject(slot);
            so.FindProperty("_slotId").stringValue = slotId;
            so.FindProperty("_slotImage").objectReferenceValue = img;
            so.FindProperty("_highlightBorder").objectReferenceValue = borderImg;
            so.FindProperty("_label").objectReferenceValue = labelTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slot;
        }

        private static SnapDraggableItem CreateM01Item(Transform parent, string name, string itemId, bool isJunk, Vector2 position, Sprite sprite)
        {
            GameObject itemObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SnapDraggableItem));
            itemObj.transform.SetParent(parent, false);
            RectTransform rt = itemObj.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(85f, 85f);

            Image img = itemObj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.raycastTarget = true;

            SnapDraggableItem item = itemObj.GetComponent<SnapDraggableItem>();
            item.SetHomePosition(position);

            SerializedObject so = new SerializedObject(item);
            var idProp = so.FindProperty("_itemId");
            if (idProp != null) idProp.stringValue = itemId;
            var junkProp = so.FindProperty("_isJunk");
            if (junkProp != null) junkProp.boolValue = isJunk;
            var imgProp = so.FindProperty("_itemImage");
            if (imgProp != null) imgProp.objectReferenceValue = img;
            var snapProp = so.FindProperty("_snapThreshold");
            if (snapProp != null) snapProp.floatValue = 120f;
            var homeProp = so.FindProperty("_homeAnchoredPosition");
            if (homeProp != null) homeProp.vector2Value = position;
            so.ApplyModifiedPropertiesWithoutUndo();

            return item;
        }
        #endregion

        #region M02: Container Sorting
        public static GameObject BuildM02ContainerSortingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M02_ContainerSorting", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite bRedSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Bucket_Red.png");
            Sprite bBlueSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Bucket_Blue.png");
            Sprite appleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Apple.png");
            Sprite berrySpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Berry.png");
            Sprite stoneSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Stone_Junk.png");
            Sprite barrierSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Obstacle_Barrier.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Разложите красные яблоки и синие ягоды в соответствующие корзины.";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject contArea = new GameObject("ContainersArea", typeof(RectTransform));
            contArea.transform.SetParent(root.transform, false);
            RectTransform caRt = contArea.GetComponent<RectTransform>();
            caRt.anchorMin = new Vector2(0.5f, 0.5f);
            caRt.anchorMax = new Vector2(0.5f, 0.5f);
            caRt.anchoredPosition = new Vector2(0f, 60f);
            caRt.sizeDelta = new Vector2(600f, 125f);

            SortContainer redBucket = CreateM02Container(contArea.transform, "Bucket_Red", "red", 2, new Vector2(-150f, 0f), bRedSpr, "Красные (0/2)");
            SortContainer blueBucket = CreateM02Container(contArea.transform, "Bucket_Blue", "blue", 2, new Vector2(150f, 0f), bBlueSpr, "Синие (0/2)");

            GameObject barrierObj = new GameObject("Obstacle_Barrier", typeof(RectTransform), typeof(Image));
            barrierObj.transform.SetParent(root.transform, false);
            RectTransform barRt = barrierObj.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0.5f);
            barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = new Vector2(0f, -8f);
            barRt.sizeDelta = new Vector2(640f, 12f);
            Image barImg = barrierObj.GetComponent<Image>();
            barImg.sprite = barrierSpr;

            GameObject tray = new GameObject("SpawnTray", typeof(RectTransform));
            tray.transform.SetParent(root.transform, false);
            RectTransform trRt = tray.GetComponent<RectTransform>();
            trRt.anchorMin = new Vector2(0.5f, 0.5f);
            trRt.anchorMax = new Vector2(0.5f, 0.5f);
            trRt.anchoredPosition = new Vector2(0f, -85f);
            trRt.sizeDelta = new Vector2(620f, 90f);

            SortDraggableItem apple1 = CreateM02Item(tray.transform, "Item_Apple_1", "red", false, new Vector2(-220f, 0f), appleSpr);
            SortDraggableItem apple2 = CreateM02Item(tray.transform, "Item_Apple_2", "red", false, new Vector2(-110f, 0f), appleSpr);
            SortDraggableItem junkStone = CreateM02Item(tray.transform, "Item_Stone_Junk", "stone", true, new Vector2(0f, 0f), stoneSpr);
            SortDraggableItem berry1 = CreateM02Item(tray.transform, "Item_Berry_1", "blue", false, new Vector2(110f, 0f), berrySpr);
            SortDraggableItem berry2 = CreateM02Item(tray.transform, "Item_Berry_2", "blue", false, new Vector2(220f, 0f), berrySpr);

            M02_ContainerSortingMechanic mechanic = root.AddComponent<M02_ContainerSortingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M02_ContainerSorting";
            so.FindProperty("_title").stringValue = "Механика #2: Сортировка по контейнерам (Bucket Sorting)";
            so.FindProperty("_instruction").stringValue = "Разложите красные яблоки и синие ягоды в соответствующие корзины. Камень (X) не помещайте!";
            so.FindProperty("_localInstructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_obstacleBarrier").objectReferenceValue = barrierObj;

            var contsProp = so.FindProperty("_containers");
            contsProp.arraySize = 2;
            contsProp.GetArrayElementAtIndex(0).objectReferenceValue = redBucket;
            contsProp.GetArrayElementAtIndex(1).objectReferenceValue = blueBucket;

            var itemsProp = so.FindProperty("_items");
            itemsProp.arraySize = 5;
            itemsProp.GetArrayElementAtIndex(0).objectReferenceValue = apple1;
            itemsProp.GetArrayElementAtIndex(1).objectReferenceValue = apple2;
            itemsProp.GetArrayElementAtIndex(2).objectReferenceValue = junkStone;
            itemsProp.GetArrayElementAtIndex(3).objectReferenceValue = berry1;
            itemsProp.GetArrayElementAtIndex(4).objectReferenceValue = berry2;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M02_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M02_ContainerSorting сохранен: {M02_PREFAB_PATH}</color>");
            return prefab;
        }

        private static SortContainer CreateM02Container(Transform parent, string name, string typeId, int reqCount, Vector2 pos, Sprite sprite, string label)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SortContainer));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(130f, 120f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            GameObject borderObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(obj.transform, false);
            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-4f, -4f);
            borderRt.offsetMax = new Vector2(4f, 4f);
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.3f, 0.4f, 0.5f, 0.4f);
            borderImg.raycastTarget = false;

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(obj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.pivot = new Vector2(0.5f, 1f);
            lblRt.anchoredPosition = new Vector2(0f, -6f);
            lblRt.sizeDelta = new Vector2(160f, 22f);
            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.text = label;
            lblTmp.fontSize = 13;
            lblTmp.alignment = TextAlignmentOptions.Center;

            SortContainer container = obj.GetComponent<SortContainer>();
            SerializedObject so = new SerializedObject(container);
            so.FindProperty("_acceptedTypeId").stringValue = typeId;
            so.FindProperty("_requiredCount").intValue = reqCount;
            so.FindProperty("_containerImage").objectReferenceValue = img;
            so.FindProperty("_highlightBorder").objectReferenceValue = borderImg;
            so.FindProperty("_countLabel").objectReferenceValue = lblTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            return container;
        }

        private static SortDraggableItem CreateM02Item(Transform parent, string name, string typeId, bool isJunk, Vector2 pos, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SortDraggableItem));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(95f, 95f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            SortDraggableItem item = obj.GetComponent<SortDraggableItem>();
            item.SetHomePosition(pos);

            SerializedObject so = new SerializedObject(item);
            so.FindProperty("_itemTypeId").stringValue = typeId;
            so.FindProperty("_isJunk").boolValue = isJunk;
            so.FindProperty("_itemImage").objectReferenceValue = img;
            var homeProp = so.FindProperty("_homeAnchoredPosition");
            if (homeProp != null) homeProp.vector2Value = pos;
            so.ApplyModifiedPropertiesWithoutUndo();

            return item;
        }
        #endregion

        #region M03: Weight Comparator
        public static GameObject BuildM03WeightComparatorPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M03_WeightComparator", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite beamSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Scale_Beam.png");
            Sprite panSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Scale_Pan.png");
            Sprite w10Spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Weight_10kg.png");
            Sprite w5Spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Weight_5kg.png");
            Sprite w2Spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Weight_2kg.png");
            Sprite wJunkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Weight_Junk.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Уравновесьте весы. Наберите ровно 15 кг на правой чаше (10кг + 5кг).";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject standObj = new GameObject("ScaleStand", typeof(RectTransform), typeof(Image));
            standObj.transform.SetParent(root.transform, false);
            RectTransform standRt = standObj.GetComponent<RectTransform>();
            standRt.anchoredPosition = new Vector2(0f, 30f);
            standRt.sizeDelta = new Vector2(26f, 85f);
            Image standImg = standObj.GetComponent<Image>();
            standImg.color = new Color(0.4f, 0.45f, 0.5f, 0.85f);

            GameObject beamObj = new GameObject("ScaleBeam", typeof(RectTransform), typeof(Image));
            beamObj.transform.SetParent(root.transform, false);
            RectTransform beamRt = beamObj.GetComponent<RectTransform>();
            beamRt.anchoredPosition = new Vector2(0f, 70f);
            beamRt.sizeDelta = new Vector2(380f, 24f);
            Image beamImg = beamObj.GetComponent<Image>();
            beamImg.sprite = beamSpr;

            ScalePan leftPan = CreateM03ScalePan(beamRt, "LeftPan", "left", 15f, new Vector2(-155f, -40f), panSpr, w10Spr);
            ScalePan rightPan = CreateM03ScalePan(beamRt, "RightPan", "right", 0f, new Vector2(155f, -40f), panSpr, null);

            GameObject tray = new GameObject("WeightsTray", typeof(RectTransform));
            tray.transform.SetParent(root.transform, false);
            RectTransform trayRt = tray.GetComponent<RectTransform>();
            trayRt.anchoredPosition = new Vector2(0f, -75f);
            trayRt.sizeDelta = new Vector2(620f, 85f);

            WeightItem w10 = CreateM03WeightItem(tray.transform, "Weight_10kg", 10f, false, new Vector2(-180f, 0f), w10Spr);
            WeightItem w5 = CreateM03WeightItem(tray.transform, "Weight_5kg", 5f, false, new Vector2(-60f, 0f), w5Spr);
            WeightItem w3 = CreateM03WeightItem(tray.transform, "Weight_3kg", 3f, false, new Vector2(60f, 0f), w2Spr);
            WeightItem wJunk = CreateM03WeightItem(tray.transform, "Weight_Junk", 0f, true, new Vector2(180f, 0f), wJunkSpr);

            M03_WeightComparatorMechanic mechanic = root.AddComponent<M03_WeightComparatorMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M03_WeightComparator";
            so.FindProperty("_title").stringValue = "Механика #3: Балансировка массы / Весовой компаратор";
            so.FindProperty("_instruction").stringValue = "Уравновесьте весы. Наберите ровно 15 кг на правой чаше (10кг + 5кг). Избегайте гири-пустышки (X)!";
            so.FindProperty("_instructionLabel").objectReferenceValue = instrTmp;
            so.FindProperty("_beamTransform").objectReferenceValue = beamRt;
            so.FindProperty("_leftPan").objectReferenceValue = leftPan;
            so.FindProperty("_rightPan").objectReferenceValue = rightPan;
            so.FindProperty("_targetMass").floatValue = 15f;

            var wProp = so.FindProperty("_weights");
            wProp.arraySize = 4;
            wProp.GetArrayElementAtIndex(0).objectReferenceValue = w10;
            wProp.GetArrayElementAtIndex(1).objectReferenceValue = w5;
            wProp.GetArrayElementAtIndex(2).objectReferenceValue = w3;
            wProp.GetArrayElementAtIndex(3).objectReferenceValue = wJunk;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M03_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M03_WeightComparator сохранен: {M03_PREFAB_PATH}</color>");
            return prefab;
        }

        private static ScalePan CreateM03ScalePan(Transform parent, string name, string panId, float baseMass, Vector2 pos, Sprite sprite, Sprite refWeightSprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScalePan));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(140f, 55f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            GameObject anchorObj = new GameObject("ItemsAnchor", typeof(RectTransform));
            anchorObj.transform.SetParent(obj.transform, false);
            RectTransform anchorRt = anchorObj.GetComponent<RectTransform>();
            anchorRt.anchoredPosition = new Vector2(0f, 15f);

            if (baseMass > 0f && refWeightSprite != null)
            {
                GameObject refWeightObj = new GameObject("ReferenceWeight15kg", typeof(RectTransform), typeof(Image));
                refWeightObj.transform.SetParent(anchorObj.transform, false);
                RectTransform rwRt = refWeightObj.GetComponent<RectTransform>();
                rwRt.anchoredPosition = new Vector2(0f, 15f);
                rwRt.sizeDelta = new Vector2(68f, 68f);
                Image rwImg = refWeightObj.GetComponent<Image>();
                rwImg.sprite = refWeightSprite;
                rwImg.color = new Color(0.95f, 0.8f, 0.25f, 1f);
                rwImg.raycastTarget = false;

                GameObject rwLblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                rwLblObj.transform.SetParent(refWeightObj.transform, false);
                RectTransform rwLblRt = rwLblObj.GetComponent<RectTransform>();
                rwLblRt.anchorMin = Vector2.zero;
                rwLblRt.anchorMax = Vector2.one;
                rwLblRt.offsetMin = new Vector2(2f, 2f);
                rwLblRt.offsetMax = new Vector2(-2f, -10f);
                TextMeshProUGUI rwLblTmp = rwLblObj.GetComponent<TextMeshProUGUI>();
                rwLblTmp.fontSize = 14;
                rwLblTmp.fontStyle = FontStyles.Bold;
                rwLblTmp.alignment = TextAlignmentOptions.Center;
                rwLblTmp.text = "15 кг";
                rwLblTmp.color = Color.white;
                rwLblTmp.raycastTarget = false;
            }

            GameObject lblObj = new GameObject("MassLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(obj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.pivot = new Vector2(0.5f, 1f);
            lblRt.anchoredPosition = new Vector2(0f, -4f);
            lblRt.sizeDelta = new Vector2(140f, 24f);
            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.fontSize = 14;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.text = $"{baseMass:F0} кг";
            lblTmp.color = baseMass > 0f ? new Color(0.2f, 1f, 0.7f, 1f) : new Color(0.9f, 0.95f, 1f, 1f);

            ScalePan pan = obj.GetComponent<ScalePan>();
            SerializedObject so = new SerializedObject(pan);
            so.FindProperty("_panId").stringValue = panId;
            so.FindProperty("_baseMass").floatValue = baseMass;
            so.FindProperty("_isReferencePan").boolValue = baseMass > 0f;
            so.FindProperty("_acceptsDrop").boolValue = baseMass == 0f;
            so.FindProperty("_massDisplay").objectReferenceValue = lblTmp;
            so.FindProperty("_itemsAnchor").objectReferenceValue = anchorRt;
            so.ApplyModifiedPropertiesWithoutUndo();
            pan.SetBaseMass(baseMass);

            return pan;
        }

        private static WeightItem CreateM03WeightItem(Transform parent, string name, float mass, bool isJunk, Vector2 pos, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(WeightItem));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(85f, 85f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            GameObject lblObj = new GameObject("MassLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(obj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(4f, 4f);
            lblRt.offsetMax = new Vector2(-4f, -12f);
            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.fontSize = 15;
            lblTmp.fontStyle = FontStyles.Bold;
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.raycastTarget = false;
            if (isJunk)
            {
                lblTmp.text = "<color=#FFAA44>0 кг</color>\n<size=11><color=#FF6666>(X)</color></size>";
            }
            else
            {
                lblTmp.text = $"<b>{mass:F0} кг</b>";
            }
            lblTmp.color = Color.white;

            WeightItem item = obj.GetComponent<WeightItem>();
            item.SetHomePosition(pos);

            SerializedObject so = new SerializedObject(item);
            so.FindProperty("_mass").floatValue = mass;
            so.FindProperty("_isJunk").boolValue = isJunk;
            so.FindProperty("_massLabel").objectReferenceValue = lblTmp;
            var homeProp = so.FindProperty("_homeAnchoredPosition");
            if (homeProp != null) homeProp.vector2Value = pos;
            so.ApplyModifiedPropertiesWithoutUndo();

            item.InitializeWeight(mass, isJunk);

            return item;
        }
        #endregion

        #region M04: Multi-layer Attachment
        [MenuItem("Tools/URDT 2D Polygon/Rebuild M04 Prefab")]
        public static void MenuBuildM04Prefab()
        {
            BuildM04MultiLayerAttachmentPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameObject BuildM04MultiLayerAttachmentPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M04_MultiLayerAttachment", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite chassisSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Chassis.png");
            Sprite coreSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Core.png");
            Sprite armorSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Armor.png");
            Sprite headSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Head.png");
            Sprite batterySpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Battery.png");
            Sprite junkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_BrokenPart_Junk.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "<b>Этап 1/2:</b> Смонтируйте базу робота (1. Ядро -> 2. Броня). Не используйте дефектный блок (X)!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject station = new GameObject("ChassisStation", typeof(RectTransform), typeof(Image));
            station.transform.SetParent(root.transform, false);
            RectTransform stRt = station.GetComponent<RectTransform>();
            stRt.anchoredPosition = new Vector2(0f, 46f);
            stRt.sizeDelta = new Vector2(170f, 170f);
            Image stImg = station.GetComponent<Image>();
            stImg.sprite = chassisSpr;

            AttachmentSlot slot1 = CreateM04Slot(station.transform, "Socket_Core", 1, 1, 0, new Vector2(0f, -10f), new Vector2(90f, 90f));
            AttachmentSlot slot2 = CreateM04Slot(station.transform, "Socket_Armor", 1, 2, 1, new Vector2(0f, -10f), new Vector2(125f, 125f));
            AttachmentSlot slot3 = CreateM04Slot(station.transform, "Socket_Head", 2, 3, 2, new Vector2(0f, 85f), new Vector2(95f, 95f));
            AttachmentSlot slot4 = CreateM04Slot(station.transform, "Socket_Battery", 2, 4, 3, new Vector2(0f, -80f), new Vector2(85f, 75f));

            // Лоток Этапа 1 (Внутренняя база)
            GameObject stage1Tray = new GameObject("Stage1Tray", typeof(RectTransform));
            stage1Tray.transform.SetParent(root.transform, false);
            RectTransform tray1Rt = stage1Tray.GetComponent<RectTransform>();
            tray1Rt.anchoredPosition = new Vector2(0f, -75f);
            tray1Rt.sizeDelta = new Vector2(620f, 85f);

            AttachmentPart partCore = CreateM04Part(stage1Tray.transform, "Part_Core", 1, 1, "Энергоядро", false, new Vector2(-200f, 0f), coreSpr, new Vector2(85f, 85f));
            AttachmentPart partArmor = CreateM04Part(stage1Tray.transform, "Part_Armor", 1, 2, "Бронекорпус", false, new Vector2(0f, 0f), armorSpr, new Vector2(100f, 100f));
            AttachmentPart partJunk1 = CreateM04Part(stage1Tray.transform, "Part_Junk1", 1, 99, "Дефектный блок", true, new Vector2(200f, 0f), junkSpr, new Vector2(80f, 80f));

            // Лоток Этапа 2 (Внешние модули)
            GameObject stage2Tray = new GameObject("Stage2Tray", typeof(RectTransform));
            stage2Tray.transform.SetParent(root.transform, false);
            RectTransform tray2Rt = stage2Tray.GetComponent<RectTransform>();
            tray2Rt.anchoredPosition = new Vector2(0f, -75f);
            tray2Rt.sizeDelta = new Vector2(620f, 85f);

            AttachmentPart partHead = CreateM04Part(stage2Tray.transform, "Part_Head", 2, 3, "Сенсорный шлем", false, new Vector2(-200f, 0f), headSpr, new Vector2(90f, 90f));
            AttachmentPart partBattery = CreateM04Part(stage2Tray.transform, "Part_Battery", 2, 4, "Энергоблок", false, new Vector2(0f, 0f), batterySpr, new Vector2(85f, 85f));
            AttachmentPart partJunk2 = CreateM04Part(stage2Tray.transform, "Part_Junk2", 2, 99, "Сломанный чип", true, new Vector2(200f, 0f), junkSpr, new Vector2(80f, 80f));
            stage2Tray.SetActive(false);

            M04_MultiLayerAttachmentMechanic mechanic = root.AddComponent<M04_MultiLayerAttachmentMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M04_MultiLayerAttachment";
            so.FindProperty("_title").stringValue = "Механика #4: Сборка многокомпонентной иерархии (2 Этапа)";
            so.FindProperty("_instruction").stringValue = "Смонтируйте узлы робота: Этап 1: Ядро -> Броня. Этап 2: Шлем -> Энергоблок. Подсветка сокетов при захвате детали!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_chassisBase").objectReferenceValue = station;
            so.FindProperty("_stage1Tray").objectReferenceValue = stage1Tray;
            so.FindProperty("_stage2Tray").objectReferenceValue = stage2Tray;

            var slotsProp = so.FindProperty("_slots");
            slotsProp.arraySize = 4;
            slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = slot1;
            slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = slot2;
            slotsProp.GetArrayElementAtIndex(2).objectReferenceValue = slot3;
            slotsProp.GetArrayElementAtIndex(3).objectReferenceValue = slot4;

            var partsProp = so.FindProperty("_parts");
            partsProp.arraySize = 6;
            partsProp.GetArrayElementAtIndex(0).objectReferenceValue = partCore;
            partsProp.GetArrayElementAtIndex(1).objectReferenceValue = partArmor;
            partsProp.GetArrayElementAtIndex(2).objectReferenceValue = partJunk1;
            partsProp.GetArrayElementAtIndex(3).objectReferenceValue = partHead;
            partsProp.GetArrayElementAtIndex(4).objectReferenceValue = partBattery;
            partsProp.GetArrayElementAtIndex(5).objectReferenceValue = partJunk2;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M04_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M04_MultiLayerAttachment сохранен: {M04_PREFAB_PATH}</color>");
            return prefab;
        }

        private static AttachmentSlot CreateM04Slot(Transform parent, string name, int stage, int layer, int prereq, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AttachmentSlot));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.5f, 0.3f);

            AttachmentSlot slot = obj.GetComponent<AttachmentSlot>();
            SerializedObject so = new SerializedObject(slot);
            so.FindProperty("_stage").intValue = stage;
            so.FindProperty("_layerIndex").intValue = layer;
            so.FindProperty("_requiredPrerequisiteLayer").intValue = prereq;
            so.FindProperty("_slotName").stringValue = name;
            so.FindProperty("_slotOutline").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slot;
        }

        private static AttachmentPart CreateM04Part(Transform parent, string name, int stage, int layer, string partName, bool isJunk, Vector2 pos, Sprite sprite, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AttachmentPart));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            AttachmentPart part = obj.GetComponent<AttachmentPart>();
            part.SetHomePosition(pos);

            SerializedObject so = new SerializedObject(part);
            so.FindProperty("_stage").intValue = stage;
            so.FindProperty("_layerIndex").intValue = layer;
            so.FindProperty("_partName").stringValue = partName;
            so.FindProperty("_isJunk").boolValue = isJunk;
            var homeProp = so.FindProperty("_homeAnchoredPosition");
            if (homeProp != null) homeProp.vector2Value = pos;
            so.ApplyModifiedPropertiesWithoutUndo();

            return part;
        }
        #endregion

        #region M05: Timeline Sequencer
        public static GameObject BuildM05TimelineSequencerPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M05_TimelineSequencer", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite fwdSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Cmd_Forward.png");
            Sprite turnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Cmd_Turn.png");
            Sprite fwdDownSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Cmd_Forward_Down.png");
            if (fwdDownSpr == null) fwdDownSpr = fwdSpr;
            Sprite glitchSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Cmd_Glitch_Junk.png");
            Sprite botSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Avatar_Bot.png");
            Sprite flagSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Goal_Flag.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Соберите маршрут: 1. Вперед -> 2. Поворот -> 3. Вперед, затем нажмите «ПУСК».";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject track = new GameObject("TrackArea", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            RectTransform trackRt = track.GetComponent<RectTransform>();
            trackRt.anchoredPosition = new Vector2(0f, 65f);
            trackRt.sizeDelta = new Vector2(540f, 105f);
            Image trImg = track.GetComponent<Image>();
            trImg.color = new Color(0.12f, 0.16f, 0.22f, 0.7f);

            // Визуальная направляющая дорожка маршрута
            GameObject hLine = new GameObject("GuidePath_H", typeof(RectTransform), typeof(Image));
            hLine.transform.SetParent(track.transform, false);
            RectTransform hlRt = hLine.GetComponent<RectTransform>();
            hlRt.anchoredPosition = new Vector2(-130f, 35f);
            hlRt.sizeDelta = new Vector2(140f, 6f);
            hLine.GetComponent<Image>().color = new Color(0.15f, 0.75f, 1f, 0.35f);

            GameObject vLine = new GameObject("GuidePath_V", typeof(RectTransform), typeof(Image));
            vLine.transform.SetParent(track.transform, false);
            RectTransform vlRt = vLine.GetComponent<RectTransform>();
            vlRt.anchoredPosition = new Vector2(-60f, 0f);
            vlRt.sizeDelta = new Vector2(6f, 70f);
            vLine.GetComponent<Image>().color = new Color(0.15f, 0.75f, 1f, 0.35f);

            GameObject botObj = new GameObject("Avatar_Bot", typeof(RectTransform), typeof(Image));
            botObj.transform.SetParent(track.transform, false);
            RectTransform botRt = botObj.GetComponent<RectTransform>();
            botRt.anchoredPosition = new Vector2(-200f, 35f);
            botRt.sizeDelta = new Vector2(64f, 64f);
            Image botImg = botObj.GetComponent<Image>();
            botImg.sprite = botSpr;

            GameObject flagObj = new GameObject("Goal_Flag", typeof(RectTransform), typeof(Image));
            flagObj.transform.SetParent(track.transform, false);
            RectTransform flagRt = flagObj.GetComponent<RectTransform>();
            flagRt.anchoredPosition = new Vector2(-60f, -35f);
            flagRt.sizeDelta = new Vector2(64f, 64f);
            Image flagImg = flagObj.GetComponent<Image>();
            flagImg.sprite = flagSpr;

            GameObject timelineRow = new GameObject("TimelineRow", typeof(RectTransform));
            timelineRow.transform.SetParent(root.transform, false);
            RectTransform tRowRt = timelineRow.GetComponent<RectTransform>();
            tRowRt.anchoredPosition = new Vector2(0f, -8f);
            tRowRt.sizeDelta = new Vector2(580f, 65f);

            CommandSlot s0 = CreateM05Slot(timelineRow.transform, "Slot_0", 0, new Vector2(-150f, 0f));
            CommandSlot s1 = CreateM05Slot(timelineRow.transform, "Slot_1", 1, new Vector2(-30f, 0f));
            CommandSlot s2 = CreateM05Slot(timelineRow.transform, "Slot_2", 2, new Vector2(90f, 0f));

            GameObject runBtnObj = new GameObject("Button_Execute", typeof(RectTransform), typeof(Image), typeof(Button));
            runBtnObj.transform.SetParent(timelineRow.transform, false);
            RectTransform rbRt = runBtnObj.GetComponent<RectTransform>();
            rbRt.anchoredPosition = new Vector2(210f, 0f);
            rbRt.sizeDelta = new Vector2(100f, 50f);
            Image rbImg = runBtnObj.GetComponent<Image>();
            rbImg.color = new Color(0.1f, 0.75f, 0.35f, 1f);
            Button runBtn = runBtnObj.GetComponent<Button>();

            GameObject rbText = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            rbText.transform.SetParent(runBtnObj.transform, false);
            RectTransform rbtRt = rbText.GetComponent<RectTransform>();
            rbtRt.anchorMin = Vector2.zero;
            rbtRt.anchorMax = Vector2.one;
            rbtRt.offsetMin = Vector2.zero;
            rbtRt.offsetMax = Vector2.zero;
            TextMeshProUGUI rbtTmp = rbText.GetComponent<TextMeshProUGUI>();
            rbtTmp.text = "ПУСК ▶";
            rbtTmp.fontSize = 17;
            rbtTmp.alignment = TextAlignmentOptions.Center;
            rbtTmp.color = Color.white;

            GameObject tray = new GameObject("ChipsTray", typeof(RectTransform));
            tray.transform.SetParent(root.transform, false);
            RectTransform trayRt = tray.GetComponent<RectTransform>();
            trayRt.anchoredPosition = new Vector2(0f, -75f);
            trayRt.sizeDelta = new Vector2(600f, 65f);

            CommandChip chipFwd1 = CreateM05Chip(tray.transform, "Chip_Fwd_1", CommandType.Forward, "Вперед →", false, new Vector2(-200f, 0f), fwdSpr);
            CommandChip chipTurn = CreateM05Chip(tray.transform, "Chip_Turn", CommandType.Turn, "Поворот ↷", false, new Vector2(-65f, 0f), turnSpr);
            CommandChip chipFwd2 = CreateM05Chip(tray.transform, "Chip_Fwd_2", CommandType.Forward, "Вперед ↓", false, new Vector2(70f, 0f), fwdDownSpr);
            CommandChip chipGlitch = CreateM05Chip(tray.transform, "Chip_Glitch_Junk", CommandType.Glitch, "Сбой [X]", true, new Vector2(205f, 0f), glitchSpr);

            M05_TimelineSequencerMechanic mechanic = root.AddComponent<M05_TimelineSequencerMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M05_TimelineSequencer";
            so.FindProperty("_title").stringValue = "Механика #5: Сборка исполняемой очереди команд";
            so.FindProperty("_instruction").stringValue = "Составьте маршрут: Вперед -> Поворот -> Вперед. Запустите программу кнопкой «ПУСК». Не ставьте фишку со сбоем (X)!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_btnExecute").objectReferenceValue = runBtn;
            so.FindProperty("_avatarTransform").objectReferenceValue = botRt;
            so.FindProperty("_goalTransform").objectReferenceValue = flagRt;
            so.FindProperty("_avatarStartPosition").vector2Value = new Vector2(-200f, 35f);

            var sProp = so.FindProperty("_timelineSlots");
            sProp.arraySize = 3;
            sProp.GetArrayElementAtIndex(0).objectReferenceValue = s0;
            sProp.GetArrayElementAtIndex(1).objectReferenceValue = s1;
            sProp.GetArrayElementAtIndex(2).objectReferenceValue = s2;

            var cProp = so.FindProperty("_chips");
            cProp.arraySize = 4;
            cProp.GetArrayElementAtIndex(0).objectReferenceValue = chipFwd1;
            cProp.GetArrayElementAtIndex(1).objectReferenceValue = chipTurn;
            cProp.GetArrayElementAtIndex(2).objectReferenceValue = chipFwd2;
            cProp.GetArrayElementAtIndex(3).objectReferenceValue = chipGlitch;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M05_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M05_TimelineSequencer сохранен: {M05_PREFAB_PATH}</color>");
            return prefab;
        }

        private static CommandSlot CreateM05Slot(Transform parent, string name, int step, Vector2 pos)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CommandSlot));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(100f, 70f);

            Image img = obj.GetComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.35f, 0.6f);

            GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(obj.transform, false);
            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-3f, -3f);
            borderRt.offsetMax = new Vector2(3f, 3f);
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.4f, 0.5f, 0.6f, 0.4f);
            borderImg.raycastTarget = false;

            GameObject lblObj = new GameObject("StepLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(obj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 1f);
            lblRt.anchorMax = new Vector2(1f, 1f);
            lblRt.pivot = new Vector2(0.5f, 0f);
            lblRt.anchoredPosition = new Vector2(0f, 4f);
            lblRt.sizeDelta = new Vector2(100f, 18f);
            TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
            lblTmp.text = $"Шаг {step + 1}";
            lblTmp.fontSize = 12;
            lblTmp.alignment = TextAlignmentOptions.Center;

            CommandSlot slot = obj.GetComponent<CommandSlot>();
            SerializedObject so = new SerializedObject(slot);
            so.FindProperty("_stepIndex").intValue = step;
            so.FindProperty("_stepLabel").objectReferenceValue = lblTmp;
            so.FindProperty("_highlightBorder").objectReferenceValue = borderImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slot;
        }

        private static CommandChip CreateM05Chip(Transform parent, string name, CommandType type, string label, bool isJunk, Vector2 pos, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CommandChip));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(95f, 65f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            CommandChip chip = obj.GetComponent<CommandChip>();
            chip.SetHomePosition(pos);

            SerializedObject so = new SerializedObject(chip);
            so.FindProperty("_commandType").enumValueIndex = (int)type;
            so.FindProperty("_commandName").stringValue = label;
            so.FindProperty("_isJunk").boolValue = isJunk;
            var homeProp = so.FindProperty("_homeAnchoredPosition");
            if (homeProp != null) homeProp.vector2Value = pos;
            so.ApplyModifiedPropertiesWithoutUndo();

            return chip;
        }
        #endregion


        #region M06: Node Pairing
        public static GameObject BuildM06NodePairingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M06_NodePairing", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite pinRedSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pin_Red.png");
            Sprite pinBlueSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pin_Blue.png");
            Sprite pinGreenSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pin_Green.png");
            Sprite pinJunkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pin_Junk.png");
            Sprite wireSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wire_Texture.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Протяните провода от левых клемм к парным правым. Не замыкайте клемму (X)!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject wiresContainer = new GameObject("WiresContainer", typeof(RectTransform));
            wiresContainer.transform.SetParent(root.transform, false);
            RectTransform wcRt = wiresContainer.GetComponent<RectTransform>();
            wcRt.anchorMin = Vector2.zero;
            wcRt.anchorMax = Vector2.one;
            wcRt.offsetMin = Vector2.zero;
            wcRt.offsetMax = Vector2.zero;

            // Левая панель источников
            GameObject leftPanel = new GameObject("LeftTerminalBlock", typeof(RectTransform));
            leftPanel.transform.SetParent(root.transform, false);
            RectTransform lpRt = leftPanel.GetComponent<RectTransform>();
            lpRt.anchoredPosition = new Vector2(-220f, 0f);
            lpRt.sizeDelta = new Vector2(100f, 240f);

            NodePin srcA = CreateM06Pin(leftPanel.transform, "Pin_Src_A", "A", true, false, new Vector2(0f, 90f), pinRedSpr);
            NodePin srcB = CreateM06Pin(leftPanel.transform, "Pin_Src_B", "B", true, false, new Vector2(0f, 30f), pinBlueSpr);
            NodePin srcC = CreateM06Pin(leftPanel.transform, "Pin_Src_C", "C", true, false, new Vector2(0f, -30f), pinGreenSpr);
            NodePin srcJunk = CreateM06Pin(leftPanel.transform, "Pin_Src_Junk", "Junk", true, true, new Vector2(0f, -90f), pinJunkSpr);

            // Правая панель приемников
            GameObject rightPanel = new GameObject("RightTerminalBlock", typeof(RectTransform));
            rightPanel.transform.SetParent(root.transform, false);
            RectTransform rpRt = rightPanel.GetComponent<RectTransform>();
            rpRt.anchoredPosition = new Vector2(220f, 0f);
            rpRt.sizeDelta = new Vector2(100f, 240f);

            NodePin tgtB = CreateM06Pin(rightPanel.transform, "Pin_Tgt_B", "B", false, false, new Vector2(0f, 90f), pinBlueSpr);
            NodePin tgtC = CreateM06Pin(rightPanel.transform, "Pin_Tgt_C", "C", false, false, new Vector2(0f, 30f), pinGreenSpr);
            NodePin tgtA = CreateM06Pin(rightPanel.transform, "Pin_Tgt_A", "A", false, false, new Vector2(0f, -30f), pinRedSpr);
            NodePin tgtJunk = CreateM06Pin(rightPanel.transform, "Pin_Tgt_Junk", "Junk", false, true, new Vector2(0f, -90f), pinJunkSpr);

            M06_NodePairingMechanic mechanic = root.AddComponent<M06_NodePairingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M06_NodePairing";
            so.FindProperty("_title").stringValue = "Механика #6: Соединение графовых узлов эластичной связью";
            so.FindProperty("_instruction").stringValue = "Соедините контакты одинакового цвета. Не замыкайте дефектную клемму (X)!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_wiresContainer").objectReferenceValue = wiresContainer.transform;
            so.FindProperty("_wireSprite").objectReferenceValue = wireSpr;

            var srcProp = so.FindProperty("_sourcePins");
            srcProp.arraySize = 4;
            srcProp.GetArrayElementAtIndex(0).objectReferenceValue = srcA;
            srcProp.GetArrayElementAtIndex(1).objectReferenceValue = srcB;
            srcProp.GetArrayElementAtIndex(2).objectReferenceValue = srcC;
            srcProp.GetArrayElementAtIndex(3).objectReferenceValue = srcJunk;

            var tgtProp = so.FindProperty("_targetPins");
            tgtProp.arraySize = 4;
            tgtProp.GetArrayElementAtIndex(0).objectReferenceValue = tgtB;
            tgtProp.GetArrayElementAtIndex(1).objectReferenceValue = tgtC;
            tgtProp.GetArrayElementAtIndex(2).objectReferenceValue = tgtA;
            tgtProp.GetArrayElementAtIndex(3).objectReferenceValue = tgtJunk;

            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M06_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M06_NodePairing сохранен: {M06_PREFAB_PATH}</color>");
            return prefab;
        }

        private static NodePin CreateM06Pin(Transform parent, string name, string pairId, bool isSource, bool isJunk, Vector2 pos, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(NodePin));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(56f, 56f);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;

            NodePin pin = obj.GetComponent<NodePin>();
            SerializedObject so = new SerializedObject(pin);
            so.FindProperty("_pairId").stringValue = pairId;
            so.FindProperty("_isSource").boolValue = isSource;
            so.FindProperty("_isJunk").boolValue = isJunk;
            so.FindProperty("_pinImage").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();

            return pin;
        }
        #endregion

        #region M07: Wobble & Snap
        [MenuItem("Tools/URDT 2D Polygon/Rebuild M07 Prefab")]
        public static void RebuildM07Menu()
        {
            BuildM07WobbleAndSnapPrefab();
        }

        public static GameObject BuildM07WobbleAndSnapPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M07_WobbleAndSnap", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite toothSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tooth_Object.png");
            Sprite socketSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Gum_Socket.png");
            Sprite forcepsSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tool_Forceps.png");
            Sprite traySpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Instrument_Tray.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Возьмите щипцы со столика справа и наложите их на коронку зуба!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            // Прогресс-бар усталости связки
            GameObject pBarBg = new GameObject("FatigueBarBg", typeof(RectTransform), typeof(Image));
            pBarBg.transform.SetParent(root.transform, false);
            RectTransform pbRt = pBarBg.GetComponent<RectTransform>();
            pbRt.anchoredPosition = new Vector2(0f, 96f);
            pbRt.sizeDelta = new Vector2(300f, 18f);
            Image pbBgImg = pBarBg.GetComponent<Image>();
            pbBgImg.color = new Color(0.2f, 0.25f, 0.35f, 0.8f);

            GameObject pBarFill = new GameObject("FatigueBarFill", typeof(RectTransform), typeof(Image));
            pBarFill.transform.SetParent(pBarBg.transform, false);
            RectTransform pbfRt = pBarFill.GetComponent<RectTransform>();
            pbfRt.anchorMin = Vector2.zero;
            pbfRt.anchorMax = Vector2.one;
            pbfRt.offsetMin = Vector2.zero;
            pbfRt.offsetMax = Vector2.zero;
            Image pbfImg = pBarFill.GetComponent<Image>();
            pbfImg.color = new Color(1f, 0.75f, 0.2f, 0.95f);
            pbfImg.type = Image.Type.Filled;
            pbfImg.fillMethod = Image.FillMethod.Horizontal;
            pbfImg.fillAmount = 0f;

            GameObject pBarLbl = new GameObject("FatigueLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            pBarLbl.transform.SetParent(pBarBg.transform, false);
            RectTransform pblRt = pBarLbl.GetComponent<RectTransform>();
            pblRt.anchorMin = Vector2.zero;
            pblRt.anchorMax = Vector2.one;
            pblRt.offsetMin = Vector2.zero;
            pblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI pblTmp = pBarLbl.GetComponent<TextMeshProUGUI>();
            pblTmp.text = "Усталость связки: 0%";
            pblTmp.fontSize = 12;
            pblTmp.alignment = TextAlignmentOptions.Center;
            pblTmp.color = Color.white;

            // Стоматологический столик / лоток под щипцы (справа)
            GameObject trayObj = new GameObject("InstrumentTray", typeof(RectTransform), typeof(Image));
            trayObj.transform.SetParent(root.transform, false);
            RectTransform trayRt = trayObj.GetComponent<RectTransform>();
            trayRt.anchoredPosition = new Vector2(240f, 0f);
            trayRt.sizeDelta = new Vector2(150f, 210f);
            Image trayImg = trayObj.GetComponent<Image>();
            trayImg.sprite = traySpr;

            GameObject trayLblObj = new GameObject("TrayLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            trayLblObj.transform.SetParent(trayObj.transform, false);
            RectTransform tlRt = trayLblObj.GetComponent<RectTransform>();
            tlRt.anchorMin = new Vector2(0f, 1f);
            tlRt.anchorMax = new Vector2(1f, 1f);
            tlRt.pivot = new Vector2(0.5f, 1f);
            tlRt.anchoredPosition = new Vector2(0f, -8f);
            tlRt.sizeDelta = new Vector2(150f, 20f);
            TextMeshProUGUI tlTmp = trayLblObj.GetComponent<TextMeshProUGUI>();
            tlTmp.text = "ЛОТОК ИНСТРУМЕНТОВ";
            tlTmp.fontSize = 10;
            tlTmp.alignment = TextAlignmentOptions.Center;
            tlTmp.color = new Color(0.7f, 0.8f, 0.9f, 0.75f);

            // База-десна
            GameObject socketObj = new GameObject("GumSocket", typeof(RectTransform), typeof(Image));
            socketObj.transform.SetParent(root.transform, false);
            RectTransform sRt = socketObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(0f, -35f);
            sRt.sizeDelta = new Vector2(170f, 75f);
            Image sImg = socketObj.GetComponent<Image>();
            sImg.sprite = socketSpr;

            // Зуб с WobbleItem
            GameObject toothObj = new GameObject("ToothItem", typeof(RectTransform), typeof(Image), typeof(WobbleItem));
            toothObj.transform.SetParent(root.transform, false);
            RectTransform tRt = toothObj.GetComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(0f, 0f);
            tRt.sizeDelta = new Vector2(110f, 140f);
            Image tImg = toothObj.GetComponent<Image>();
            tImg.sprite = toothSpr;

            WobbleItem wobble = toothObj.GetComponent<WobbleItem>();
            SerializedObject wobbleSo = new SerializedObject(wobble);
            wobbleSo.FindProperty("_requiresForceps").boolValue = true;
            wobbleSo.ApplyModifiedPropertiesWithoutUndo();

            // Хирургические щипцы DentalForceps
            GameObject forcepsObj = new GameObject("DentalForceps", typeof(RectTransform), typeof(Image), typeof(DentalForcepsTool));
            forcepsObj.transform.SetParent(root.transform, false);
            RectTransform fRt = forcepsObj.GetComponent<RectTransform>();
            fRt.anchoredPosition = new Vector2(230f, 0f);
            fRt.sizeDelta = new Vector2(95f, 190f);
            Image fImg = forcepsObj.GetComponent<Image>();
            fImg.sprite = forcepsSpr;

            DentalForcepsTool forcepsTool = forcepsObj.GetComponent<DentalForcepsTool>();
            SerializedObject fSo = new SerializedObject(forcepsTool);
            fSo.FindProperty("_targetTooth").objectReferenceValue = wobble;
            fSo.FindProperty("_homeAnchoredPosition").vector2Value = new Vector2(230f, 0f);
            fSo.FindProperty("_gripOffsetOnTooth").vector2Value = new Vector2(0f, 65f);
            fSo.FindProperty("_snapAttachDistance").floatValue = 110f;
            fSo.ApplyModifiedPropertiesWithoutUndo();

            M07_WobbleAndSnapMechanic mechanic = root.AddComponent<M07_WobbleAndSnapMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M07_WobbleAndSnap";
            so.FindProperty("_title").stringValue = "Механика #7: Преодоление сопротивления пружины (Wobble & Snap)";
            so.FindProperty("_instruction").stringValue = "Наложите хирургические щипцы на зуб и раскачивайте его до ослабления связки, затем потяните вверх!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_wobbleItem").objectReferenceValue = wobble;
            so.FindProperty("_forcepsTool").objectReferenceValue = forcepsTool;
            so.FindProperty("_fatigueProgressBar").objectReferenceValue = pbfImg;
            so.FindProperty("_fatigueLabel").objectReferenceValue = pblTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M07_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M07_WobbleAndSnap сохранен с хирургическими щипцами: {M07_PREFAB_PATH}</color>");
            return prefab;
        }
        #endregion

        #region M08: Waypoint Tracking
        public static GameObject BuildM08WaypointTrackingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M08_WaypointTracking", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite sawSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tool_CircularSaw.png");
            Sprite boardSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wood_Board.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Проведите пилу через контрольные точки 1 -> 5. Избегайте опасного сучка (X)!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject boardObj = new GameObject("WoodBoard", typeof(RectTransform), typeof(Image));
            boardObj.transform.SetParent(root.transform, false);
            RectTransform bRt = boardObj.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, 0f);
            bRt.sizeDelta = new Vector2(500f, 95f);
            Image bImg = boardObj.GetComponent<Image>();
            bImg.sprite = boardSpr;

            // 5 контрольных точек
            RectTransform[] wpMarkers = new RectTransform[5];
            float[] xPositions = new float[] { -200f, -100f, 0f, 100f, 200f };
            for (int i = 0; i < 5; i++)
            {
                GameObject wp = new GameObject($"Waypoint_{i}", typeof(RectTransform), typeof(Image));
                wp.transform.SetParent(boardObj.transform, false);
                RectTransform wpRt = wp.GetComponent<RectTransform>();
                wpRt.anchoredPosition = new Vector2(xPositions[i], 0f);
                wpRt.sizeDelta = new Vector2(28f, 28f);
                Image wpImg = wp.GetComponent<Image>();
                wpImg.color = new Color(0.3f, 0.5f, 0.7f, 0.5f);
                wpMarkers[i] = wpRt;
            }

            // Опасный сучок (hazard)
            GameObject hazardObj = new GameObject("HazardKnot", typeof(RectTransform), typeof(Image));
            hazardObj.transform.SetParent(boardObj.transform, false);
            RectTransform hRt = hazardObj.GetComponent<RectTransform>();
            hRt.anchoredPosition = new Vector2(0f, -38f);
            hRt.sizeDelta = new Vector2(36f, 36f);
            Image hImg = hazardObj.GetComponent<Image>();
            hImg.color = new Color(0.95f, 0.2f, 0.2f, 0.85f);

            // Пила
            GameObject sawObj = new GameObject("ToolSaw", typeof(RectTransform), typeof(Image), typeof(WaypointTrackerTool));
            sawObj.transform.SetParent(root.transform, false);
            RectTransform sRt = sawObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(-200f, 10f);
            sRt.sizeDelta = new Vector2(75f, 75f);
            Image sImg = sawObj.GetComponent<Image>();
            sImg.sprite = sawSpr;

            WaypointTrackerTool tool = sawObj.GetComponent<WaypointTrackerTool>();
            SerializedObject toolSo = new SerializedObject(tool);
            toolSo.FindProperty("_startPosition").vector2Value = new Vector2(-200f, 10f);
            toolSo.ApplyModifiedPropertiesWithoutUndo();

            M08_WaypointTrackingMechanic mechanic = root.AddComponent<M08_WaypointTrackingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M08_WaypointTracking";
            so.FindProperty("_title").stringValue = "Механика #8: Трассировка пути по путевым точкам (Waypoint Tracking)";
            so.FindProperty("_instruction").stringValue = "Проведите инструмент по линии распила строго по контрольным точкам. Не заденьте гвоздь (X)!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_tool").objectReferenceValue = tool;
            so.FindProperty("_hazardObstacle").objectReferenceValue = hRt;

            var wpProp = so.FindProperty("_waypointMarkers");
            wpProp.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                wpProp.GetArrayElementAtIndex(i).objectReferenceValue = wpMarkers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M08_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M08_WaypointTracking сохранен: {M08_PREFAB_PATH}</color>");
            return prefab;
        }
        #endregion

        #region M09: Coverage Accumulator
        public static GameObject BuildM09CoverageAccumulatorPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M09_CoverageAccumulator", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite spongeSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tool_Sponge.png");
            Sprite dirtSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tile_Dirt.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Возьмите губку и круговыми движениями сотрите грязь (цель: 90%).";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            GameObject plateObj = new GameObject("MetalPlate", typeof(RectTransform), typeof(Image));
            plateObj.transform.SetParent(root.transform, false);
            RectTransform plRt = plateObj.GetComponent<RectTransform>();
            plRt.anchoredPosition = new Vector2(0f, 0f);
            plRt.sizeDelta = new Vector2(440f, 210f);
            Image plImg = plateObj.GetComponent<Image>();
            plImg.color = new Color(0.2f, 0.25f, 0.32f, 0.9f);

            // Сетка 5 x 4 ячеек грязи (всего 20)
            List<DirtCell> cells = new List<DirtCell>();
            int cols = 5;
            int rows = 4;
            float startX = -180f;
            float startY = 80f;
            float stepX = 90f;
            float stepY = -55f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    GameObject cObj = new GameObject($"DirtCell_{r}_{c}", typeof(RectTransform), typeof(Image), typeof(DirtCell));
                    cObj.transform.SetParent(plateObj.transform, false);
                    RectTransform cRt = cObj.GetComponent<RectTransform>();
                    cRt.anchoredPosition = new Vector2(startX + c * stepX, startY + r * stepY);
                    cRt.sizeDelta = new Vector2(75f, 48f);
                    Image cImg = cObj.GetComponent<Image>();
                    cImg.sprite = dirtSpr;

                    DirtCell cell = cObj.GetComponent<DirtCell>();
                    // 1 несмываемый дефект (в правом нижнем углу)
                    if (r == rows - 1 && c == cols - 1)
                    {
                        cell.SetHazard(true);
                    }
                    cells.Add(cell);
                }
            }

            GameObject spongeObj = new GameObject("ToolSponge", typeof(RectTransform), typeof(Image), typeof(SpongeBrush));
            spongeObj.transform.SetParent(root.transform, false);
            RectTransform spRt = spongeObj.GetComponent<RectTransform>();
            spRt.anchoredPosition = new Vector2(210f, -80f);
            spRt.sizeDelta = new Vector2(80f, 80f);
            Image spImg = spongeObj.GetComponent<Image>();
            spImg.sprite = spongeSpr;

            M09_CoverageAccumulatorMechanic mechanic = root.AddComponent<M09_CoverageAccumulatorMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M09_CoverageAccumulator";
            so.FindProperty("_title").stringValue = "Механика #9: Накопление покрытия / Стирание маски (Coverage Accumulator)";
            so.FindProperty("_instruction").stringValue = "Сотрите пятна грязи губкой. Несмываемый скол (красный X) очистить невозможно.";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_brush").objectReferenceValue = spongeObj.GetComponent<SpongeBrush>();

            var cellsProp = so.FindProperty("_dirtCells");
            cellsProp.arraySize = cells.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M09_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M09_CoverageAccumulator сохранен: {M09_PREFAB_PATH}</color>");
            return prefab;
        }
        #endregion

        #region M10: Timed Accumulator
        public static GameObject BuildM10TimedAccumulatorPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M10_TimedAccumulator", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite dialSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Gauge_Dial.png");
            Sprite needleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Gauge_Needle.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Pump.png");

            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(700f, 24f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Зажмите кнопку «НАКАЧКА» и отпустите в зеленой зоне (70-85%). Не перекачивайте!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            instrTmp.raycastTarget = false;

            // Циферблат манометра
            GameObject dialObj = new GameObject("GaugeDial", typeof(RectTransform), typeof(Image));
            dialObj.transform.SetParent(root.transform, false);
            RectTransform dRt = dialObj.GetComponent<RectTransform>();
            dRt.anchoredPosition = new Vector2(0f, 40f);
            dRt.sizeDelta = new Vector2(190f, 190f);
            Image dImg = dialObj.GetComponent<Image>();
            dImg.sprite = dialSpr;

            // Стрелка манометра (pivot у основания)
            GameObject needleObj = new GameObject("GaugeNeedle", typeof(RectTransform), typeof(Image));
            needleObj.transform.SetParent(dialObj.transform, false);
            RectTransform nRt = needleObj.GetComponent<RectTransform>();
            nRt.pivot = new Vector2(0.5f, 0.15f);
            nRt.anchoredPosition = Vector2.zero;
            nRt.sizeDelta = new Vector2(16f, 85f);
            Image nImg = needleObj.GetComponent<Image>();
            nImg.sprite = needleSpr;

            // Цифровой дисплей
            GameObject dispObj = new GameObject("PressureDisplay", typeof(RectTransform), typeof(TextMeshProUGUI));
            dispObj.transform.SetParent(dialObj.transform, false);
            RectTransform dpRt = dispObj.GetComponent<RectTransform>();
            dpRt.anchoredPosition = new Vector2(0f, -45f);
            dpRt.sizeDelta = new Vector2(100f, 30f);
            TextMeshProUGUI dpTmp = dispObj.GetComponent<TextMeshProUGUI>();
            dpTmp.text = "0%";
            dpTmp.fontSize = 20;
            dpTmp.alignment = TextAlignmentOptions.Center;
            dpTmp.color = Color.white;

            // Кнопка удержания (HoldButton)
            GameObject pumpObj = new GameObject("ButtonPump", typeof(RectTransform), typeof(Image), typeof(HoldButton));
            pumpObj.transform.SetParent(root.transform, false);
            RectTransform pRt = pumpObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, -75f);
            pRt.sizeDelta = new Vector2(160f, 44f);
            Image pImg = pumpObj.GetComponent<Image>();
            pImg.sprite = btnSpr;

            GameObject pLbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            pLbl.transform.SetParent(pumpObj.transform, false);
            RectTransform plRt = pLbl.GetComponent<RectTransform>();
            plRt.anchorMin = Vector2.zero;
            plRt.anchorMax = Vector2.one;
            plRt.offsetMin = Vector2.zero;
            plRt.offsetMax = Vector2.zero;
            TextMeshProUGUI plTmp = pLbl.GetComponent<TextMeshProUGUI>();
            plTmp.text = "НАКАЧКА ▶";
            plTmp.fontSize = 17;
            plTmp.alignment = TextAlignmentOptions.Center;
            plTmp.color = Color.white;

            HoldButton hb = pumpObj.GetComponent<HoldButton>();

            M10_TimedAccumulatorMechanic mechanic = root.AddComponent<M10_TimedAccumulatorMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M10_TimedAccumulator";
            so.FindProperty("_title").stringValue = "Механика #10: Временной интегратор с контролем диапазона (Timed Accumulator)";
            so.FindProperty("_instruction").stringValue = "Зажмите и удерживайте кнопку «НАКАЧКА». Отпустите в зелёном секторе (70-85%). Перекачка выше 90% приводит к взрыву!";
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_needleTransform").objectReferenceValue = nRt;
            so.FindProperty("_holdButton").objectReferenceValue = hb;
            so.FindProperty("_pressureDisplay").objectReferenceValue = dpTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M10_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M10_TimedAccumulator сохранен: {M10_PREFAB_PATH}</color>");
            return prefab;
        }
        #endregion

        
        #region M11: Cone Emitter
        public static GameObject BuildM11ConeEmitterPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M11_ConeEmitter", typeof(RectTransform), typeof(Image));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Image rootImg = root.GetComponent<Image>();
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;

            Sprite nozzleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Nozzle_Extinguisher.png");
            Sprite fireSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Target_Fire.png");
            Sprite hazardSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Obstacle_ElectricBox.png");
            Sprite coneSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Spray_Cone.png");

            GameObject instrObj = CreateInstruction(root.transform, "Зажмите и направляйте струю на очаги огня. Остерегайтесь электрощита (молния)!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Электрощит (препятствие / штраф)
            GameObject hazardObj = new GameObject("Obstacle_ElectricBox", typeof(RectTransform), typeof(Image));
            hazardObj.transform.SetParent(root.transform, false);
            RectTransform hzRt = hazardObj.GetComponent<RectTransform>();
            hzRt.anchoredPosition = new Vector2(80f, 0f);
            hzRt.sizeDelta = new Vector2(55f, 55f);
            Image hzImg = hazardObj.GetComponent<Image>();
            hzImg.sprite = hazardSpr;
            hzImg.raycastTarget = false;

            // Очаги огня
            Vector2[] firePositions = new Vector2[]
            {
                new Vector2(160f, 95f),
                new Vector2(160f, -95f),
                new Vector2(230f, 85f)
            };

            List<FireTarget> targets = new List<FireTarget>();
            for (int i = 0; i < firePositions.Length; i++)
            {
                GameObject fireObj = new GameObject($"FireTarget_{i + 1}", typeof(RectTransform), typeof(Image), typeof(FireTarget));
                fireObj.transform.SetParent(root.transform, false);
                RectTransform fRt = fireObj.GetComponent<RectTransform>();
                fRt.anchoredPosition = firePositions[i];
                fRt.sizeDelta = new Vector2(60f, 60f);
                Image fImg = fireObj.GetComponent<Image>();
                fImg.sprite = fireSpr;
                fImg.raycastTarget = false;

                // HP бар над огнем
                GameObject barBg = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
                barBg.transform.SetParent(fireObj.transform, false);
                RectTransform bRt = barBg.GetComponent<RectTransform>();
                bRt.anchoredPosition = new Vector2(0f, 38f);
                bRt.sizeDelta = new Vector2(50f, 8f);
                Image bImg = barBg.GetComponent<Image>();
                bImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                bImg.raycastTarget = false;

                GameObject barFill = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
                barFill.transform.SetParent(barBg.transform, false);
                RectTransform bfRt = barFill.GetComponent<RectTransform>();
                bfRt.anchorMin = Vector2.zero;
                bfRt.anchorMax = Vector2.one;
                bfRt.offsetMin = Vector2.zero;
                bfRt.offsetMax = Vector2.zero;
                Image bfImg = barFill.GetComponent<Image>();
                bfImg.color = new Color(0.2f, 0.85f, 1f, 1f);
                bfImg.type = Image.Type.Filled;
                bfImg.fillMethod = Image.FillMethod.Horizontal;
                bfImg.raycastTarget = false;

                FireTarget ft = fireObj.GetComponent<FireTarget>();
                SerializedObject ftSo = new SerializedObject(ft);
                ftSo.FindProperty("_maxHp").floatValue = 100f;
                ftSo.FindProperty("_hpBarFill").objectReferenceValue = bfImg;
                ftSo.ApplyModifiedPropertiesWithoutUndo();

                targets.Add(ft);
            }

            // Насадка распылителя
            GameObject nozzleObj = new GameObject("Nozzle", typeof(RectTransform), typeof(Image));
            nozzleObj.transform.SetParent(root.transform, false);
            RectTransform nRt = nozzleObj.GetComponent<RectTransform>();
            nRt.anchoredPosition = new Vector2(-190f, 0f);
            nRt.sizeDelta = new Vector2(65f, 40f);
            Image nImg = nozzleObj.GetComponent<Image>();
            nImg.sprite = nozzleSpr;
            nImg.raycastTarget = false;

            // Визуальный конус распыления струи
            GameObject coneObj = new GameObject("SprayConeVisual", typeof(RectTransform), typeof(Image));
            coneObj.transform.SetParent(nozzleObj.transform, false);
            RectTransform cRt = coneObj.GetComponent<RectTransform>();
            cRt.pivot = new Vector2(0f, 0.5f);
            cRt.anchoredPosition = new Vector2(25f, 0f);
            cRt.sizeDelta = new Vector2(480f, 160f);
            Image cImg = coneObj.GetComponent<Image>();
            cImg.sprite = coneSpr;
            cImg.color = Color.white;
            cImg.raycastTarget = false;
            coneObj.SetActive(false);

            M11_ConeEmitterMechanic mechanic = root.AddComponent<M11_ConeEmitterMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M11_ConeEmitter";
            so.FindProperty("_title").stringValue = "Механика #11: Наведение конуса воздействия с деградацией HP цели (Cone Emitter)";
            so.FindProperty("_instruction").stringValue = "Зажмите и направляйте струю огнетушителя на очаги пожара. Не попадайте на электрощит (молния)!";
            so.FindProperty("_nozzleTransform").objectReferenceValue = nRt;
            so.FindProperty("_sprayConeVisual").objectReferenceValue = cRt;
            so.FindProperty("_electricHazardBox").objectReferenceValue = hzRt;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_coneHalfAngle").floatValue = 18f;
            so.FindProperty("_maxRange").floatValue = 480f;
            so.FindProperty("_dps").floatValue = 65f;

            SerializedProperty targetsProp = so.FindProperty("_targets");
            targetsProp.arraySize = targets.Count;
            for (int i = 0; i < targets.Count; i++)
            {
                targetsProp.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M11_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M12: Angular Delta Tracker
        public static GameObject BuildM12AngularDeltaTrackerPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M12_AngularDeltaTracker", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite valveSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wheel_Valve.png");

            GameObject instrObj = CreateInstruction(root.transform, "Поверните рабочий вентиль слева на 2 полных оборота (720°). Вентиль справа заклинило [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Активный вентиль
            GameObject activeWheelObj = new GameObject("Valve_Active", typeof(RectTransform), typeof(Image), typeof(RotaryWheel));
            activeWheelObj.transform.SetParent(root.transform, false);
            RectTransform awRt = activeWheelObj.GetComponent<RectTransform>();
            awRt.anchoredPosition = new Vector2(-120f, 0f);
            awRt.sizeDelta = new Vector2(150f, 150f);
            Image awImg = activeWheelObj.GetComponent<Image>();
            awImg.sprite = valveSpr;
            RotaryWheel aw = activeWheelObj.GetComponent<RotaryWheel>();

            // Заклинивший вентиль [X]
            GameObject jammedWheelObj = new GameObject("Valve_Jammed_Junk", typeof(RectTransform), typeof(Image), typeof(RotaryWheel));
            jammedWheelObj.transform.SetParent(root.transform, false);
            RectTransform jwRt = jammedWheelObj.GetComponent<RectTransform>();
            jwRt.anchoredPosition = new Vector2(120f, 0f);
            jwRt.sizeDelta = new Vector2(150f, 150f);
            Image jwImg = jammedWheelObj.GetComponent<Image>();
            jwImg.sprite = valveSpr;
            jwImg.color = new Color(0.9f, 0.45f, 0.45f, 1f);
            RotaryWheel jw = jammedWheelObj.GetComponent<RotaryWheel>();
            SerializedObject jwSo = new SerializedObject(jw);
            jwSo.FindProperty("_isJammed").boolValue = true;
            jwSo.ApplyModifiedPropertiesWithoutUndo();

            // Дисплей оборотов
            GameObject dispObj = new GameObject("TurnsDisplay", typeof(RectTransform), typeof(TextMeshProUGUI));
            dispObj.transform.SetParent(root.transform, false);
            RectTransform dRt = dispObj.GetComponent<RectTransform>();
            dRt.anchoredPosition = new Vector2(0f, 92f);
            dRt.sizeDelta = new Vector2(240f, 30f);
            TextMeshProUGUI dTmp = dispObj.GetComponent<TextMeshProUGUI>();
            dTmp.text = "Обороты: 0.0 / 2.0";
            dTmp.fontSize = 18;
            dTmp.alignment = TextAlignmentOptions.Center;
            dTmp.color = Color.white;

            // Прогресс Бар
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -112f);
            bRt.sizeDelta = new Vector2(260f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0f, 0.85f, 1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0f;

            M12_AngularDeltaTrackerMechanic mechanic = root.AddComponent<M12_AngularDeltaTrackerMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M12_AngularDeltaTracker";
            so.FindProperty("_title").stringValue = "Механика #12: Трекер углового смещения (Angular Delta Tracker)";
            so.FindProperty("_instruction").stringValue = "Вращайте рабочий вентиль слева. Заклинивший вентиль справа [X] не вращается!";
            so.FindProperty("_activeWheel").objectReferenceValue = aw;
            so.FindProperty("_jammedWheel").objectReferenceValue = jw;
            so.FindProperty("_targetDegrees").floatValue = 720f;
            so.FindProperty("_turnsDisplay").objectReferenceValue = dTmp;
            so.FindProperty("_progressFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M12_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M13: Lane Switcher Runner
        public static GameObject BuildM13LaneSwitcherRunnerPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M13_LaneSwitcherRunner", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite avatarSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Avatar.png");
            Sprite coinSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Coin.png");
            Sprite barrierSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Barrier.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Tap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Переключайте полосы (кнопки или A/D). Соберите 5 монет и избегайте барьеров [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Дорожки
            GameObject lanesObj = new GameObject("LanesBackground", typeof(RectTransform), typeof(Image));
            lanesObj.transform.SetParent(root.transform, false);
            RectTransform lRt = lanesObj.GetComponent<RectTransform>();
            lRt.anchoredPosition = new Vector2(0f, 18f);
            lRt.sizeDelta = new Vector2(420f, 210f);
            Image lImg = lanesObj.GetComponent<Image>();
            lImg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);

            // Контейнер спавна
            GameObject spawnObj = new GameObject("SpawnContainer", typeof(RectTransform), typeof(RectMask2D));
            spawnObj.transform.SetParent(lanesObj.transform, false);
            RectTransform spRt = spawnObj.GetComponent<RectTransform>();
            spRt.anchorMin = Vector2.zero;
            spRt.anchorMax = Vector2.one;
            spRt.offsetMin = Vector2.zero;
            spRt.offsetMax = Vector2.zero;

            // Игрок
            GameObject playerObj = new GameObject("PlayerAvatar", typeof(RectTransform), typeof(Image));
            playerObj.transform.SetParent(spawnObj.transform, false);
            RectTransform pRt = playerObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, -70f);
            pRt.sizeDelta = new Vector2(46f, 46f);
            Image pImg = playerObj.GetComponent<Image>();
            pImg.sprite = avatarSpr;

            // Кнопки управления
            GameObject btnLeftObj = CreateTapButton(root.transform, "BtnLeft", "◀ ВЛЕВО", new Vector2(-140f, -88f), new Vector2(120f, 36f), btnSpr);
            GameObject btnRightObj = CreateTapButton(root.transform, "BtnRight", "ВПРАВО ▶", new Vector2(140f, -88f), new Vector2(120f, 36f), btnSpr);

            // Счет
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Собрано: 0 / 5";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            M13_LaneSwitcherRunnerMechanic mechanic = root.AddComponent<M13_LaneSwitcherRunnerMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_controlMode").enumValueIndex = (int)RunnerControlMode.Swipe;
            so.FindProperty("_mechanicId").stringValue = "M13_LaneSwitcherRunner";
            so.FindProperty("_title").stringValue = "Механика #13: Сдвиг по полосам — Свайпы (Lane Switcher: Swipe)";
            so.FindProperty("_instruction").stringValue = "Свайпайте влево или вправо по экрану для смены полосы. Соберите 5 монет и избегайте барьеров [X]!";
            so.FindProperty("_playerAvatar").objectReferenceValue = pRt;
            so.FindProperty("_spawnContainer").objectReferenceValue = spRt;
            so.FindProperty("_coinSprite").objectReferenceValue = coinSpr;
            so.FindProperty("_barrierSprite").objectReferenceValue = barrierSpr;
            so.FindProperty("_btnLeft").objectReferenceValue = btnLeftObj.GetComponent<Button>();
            so.FindProperty("_btnRight").objectReferenceValue = btnRightObj.GetComponent<Button>();
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_targetCoins").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M13_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M14: Slingshot Impulser
        public static GameObject BuildM14SlingshotImpulserPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M14_SlingshotImpulser", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite ballSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Slingshot_Ball.png");
            Sprite canSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Slingshot_TargetCan.png");
            Sprite barrierSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Obstacle_Barrier.png");

            GameObject instrObj = CreateInstruction(root.transform, "Оттяните снаряд назад и отпустите для выстрела. Сбейте 3 банки! Остерегайтесь колонны [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // База рогатки
            GameObject anchorObj = new GameObject("SlingshotAnchor", typeof(RectTransform));
            anchorObj.transform.SetParent(root.transform, false);
            RectTransform aRt = anchorObj.GetComponent<RectTransform>();
            aRt.anchoredPosition = new Vector2(-180f, -40f);

            // Индикатор траектории
            GameObject trajObj = new GameObject("TrajectoryIndicator", typeof(RectTransform), typeof(Image));
            trajObj.transform.SetParent(root.transform, false);
            RectTransform trRt = trajObj.GetComponent<RectTransform>();
            trRt.pivot = new Vector2(0f, 0.5f);
            trRt.anchoredPosition = aRt.anchoredPosition;
            trRt.sizeDelta = new Vector2(80f, 4f);
            Image trImg = trajObj.GetComponent<Image>();
            trImg.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            trajObj.SetActive(false);

            // Контейнер точек баллистической траектории
            GameObject trajDotsObj = new GameObject("TrajectoryDots", typeof(RectTransform));
            trajDotsObj.transform.SetParent(root.transform, false);
            RectTransform trajDotsRt = trajDotsObj.GetComponent<RectTransform>();
            trajDotsRt.anchorMin = Vector2.zero;
            trajDotsRt.anchorMax = Vector2.one;
            trajDotsRt.offsetMin = Vector2.zero;
            trajDotsRt.offsetMax = Vector2.zero;

            for (int i = 0; i < 22; i++)
            {
                GameObject dotObj = new GameObject($"Dot_{i:00}", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(trajDotsRt, false);
                RectTransform dRt = dotObj.GetComponent<RectTransform>();
                float factor = 1f - ((float)i / 22);
                float size = Mathf.Lerp(6f, 14f, factor);
                dRt.sizeDelta = new Vector2(size, size);
                Image dImg = dotObj.GetComponent<Image>();
                dImg.sprite = ballSpr;
                dImg.raycastTarget = false;
                Color col = new Color(1f, 0.85f, 0.25f, Mathf.Lerp(0.25f, 0.95f, factor));
                dImg.color = col;
                dotObj.SetActive(false);
            }

            // Снаряд
            GameObject ballObj = new GameObject("ProjectileBall", typeof(RectTransform), typeof(Image));
            ballObj.transform.SetParent(root.transform, false);
            RectTransform bRt = ballObj.GetComponent<RectTransform>();
            bRt.anchoredPosition = aRt.anchoredPosition;
            bRt.sizeDelta = new Vector2(40f, 40f);
            Image bImg = ballObj.GetComponent<Image>();
            bImg.sprite = ballSpr;

            // Препятствие-колонна [X]
            GameObject pillarObj = new GameObject("Obstacle_Pillar", typeof(RectTransform), typeof(Image));
            pillarObj.transform.SetParent(root.transform, false);
            RectTransform pilRt = pillarObj.GetComponent<RectTransform>();
            pilRt.anchoredPosition = new Vector2(30f, -20f);
            pilRt.sizeDelta = new Vector2(25f, 130f);
            Image pilImg = pillarObj.GetComponent<Image>();
            pilImg.sprite = barrierSpr;

            // Банки-мишени
            Vector2[] canPositions = new Vector2[]
            {
                new Vector2(170f, 50f),
                new Vector2(210f, -10f),
                new Vector2(170f, -70f)
            };
            SlingshotTargetCan[] canTargets = new SlingshotTargetCan[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject canObj = new GameObject($"TargetCan_{i + 1}", typeof(RectTransform), typeof(Image), typeof(SlingshotTargetCan));
                canObj.transform.SetParent(root.transform, false);
                RectTransform canRt = canObj.GetComponent<RectTransform>();
                canRt.anchoredPosition = canPositions[i];
                canRt.sizeDelta = new Vector2(45f, 55f);
                Image canImg = canObj.GetComponent<Image>();
                canImg.sprite = canSpr;
                canTargets[i] = canObj.GetComponent<SlingshotTargetCan>();
            }

            // Счет
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform sRt = scoreObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(0f, 96f);
            sRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI sTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            sTmp.text = "Сбито: 0 / 3";
            sTmp.fontSize = 18;
            sTmp.alignment = TextAlignmentOptions.Center;
            sTmp.color = Color.white;

            M14_SlingshotImpulserMechanic mechanic = root.AddComponent<M14_SlingshotImpulserMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M14_SlingshotImpulser";
            so.FindProperty("_title").stringValue = "Механика #14: Баллистический импульс с обратным вектором (Slingshot Impulser)";
            so.FindProperty("_instruction").stringValue = "Оттяните снаряд назад и отпустите для выстрела. Сбейте 3 банки! Остерегайтесь колонны [X]!";
            so.FindProperty("_projectileBall").objectReferenceValue = bRt;
            so.FindProperty("_slingshotAnchor").objectReferenceValue = aRt;
            so.FindProperty("_trajectoryIndicator").objectReferenceValue = trRt;
            so.FindProperty("_trajectoryDotsContainer").objectReferenceValue = trajDotsRt;
            so.FindProperty("_trajectoryPointCount").intValue = 22;
            so.FindProperty("_timeStep").floatValue = 0.04f;
            so.FindProperty("_trajectoryColor").colorValue = new Color(1f, 0.85f, 0.25f, 0.9f);

            SerializedProperty targetsProp = so.FindProperty("_targets");
            targetsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                targetsProp.GetArrayElementAtIndex(i).objectReferenceValue = canTargets[i];
            }

            so.FindProperty("_obstaclePillar").objectReferenceValue = pilRt;
            so.FindProperty("_maxPullDistance").floatValue = 90f;
            so.FindProperty("_launchForceMultiplier").floatValue = 7f;
            so.FindProperty("_gravity").floatValue = 250f;
            so.FindProperty("_scoreText").objectReferenceValue = sTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M14_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M15: Timing Interceptor
        public static GameObject BuildM15TimingInterceptorPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M15_TimingInterceptor", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite ballSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Interceptor_Ball.png");
            Sprite paddleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Interceptor_Paddle.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Tap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Нажмите «ОТРАЗИТЬ» в момент прохода мяча через зеленую линию. Не отбивайте фантомы [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Зона перехвата (зеленая вертикальная линия допуска)
            GameObject lineObj = new GameObject("InterceptorLine", typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(root.transform, false);
            RectTransform lRt = lineObj.GetComponent<RectTransform>();
            lRt.anchoredPosition = new Vector2(-100f, 0f);
            lRt.sizeDelta = new Vector2(35f, 200f);
            Image lImg = lineObj.GetComponent<Image>();
            lImg.color = new Color(0f, 1f, 0.5f, 0.35f);

            // Ракетка
            GameObject paddleObj = new GameObject("HitPaddle", typeof(RectTransform), typeof(Image));
            paddleObj.transform.SetParent(root.transform, false);
            RectTransform pRt = paddleObj.GetComponent<RectTransform>();
            pRt.pivot = new Vector2(0.5f, 0.2f);
            pRt.anchoredPosition = new Vector2(-100f, 0f);
            pRt.sizeDelta = new Vector2(30f, 80f);
            Image pImg = paddleObj.GetComponent<Image>();
            pImg.sprite = paddleSpr;

            // Мяч
            GameObject ballObj = new GameObject("MovingBall", typeof(RectTransform), typeof(Image));
            ballObj.transform.SetParent(root.transform, false);
            RectTransform bRt = ballObj.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(220f, 0f);
            bRt.sizeDelta = new Vector2(40f, 40f);
            Image bImg = ballObj.GetComponent<Image>();
            bImg.sprite = ballSpr;

            // Кнопка отбивания
            GameObject btnObj = CreateTapButton(root.transform, "BtnIntercept", "ОТРАЗИТЬ ->", new Vector2(0f, -100f), new Vector2(170f, 42f), btnSpr);
            Button btn = btnObj.GetComponent<Button>();

            // Счетчик серии
            GameObject streakObj = new GameObject("StreakText", typeof(RectTransform), typeof(TextMeshProUGUI));
            streakObj.transform.SetParent(root.transform, false);
            RectTransform sRt = streakObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(0f, 92f);
            sRt.sizeDelta = new Vector2(300f, 30f);
            TextMeshProUGUI sTmp = streakObj.GetComponent<TextMeshProUGUI>();
            sTmp.text = "Серия перехватов: 0 / 3";
            sTmp.fontSize = 18;
            sTmp.alignment = TextAlignmentOptions.Center;
            sTmp.color = Color.white;

            M15_TimingInterceptorMechanic mechanic = root.AddComponent<M15_TimingInterceptorMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M15_TimingInterceptor";
            so.FindProperty("_title").stringValue = "Механика #15: Перехват объекта во временном окне допуска (Timing Interceptor)";
            so.FindProperty("_instruction").stringValue = "Нажмите «ОТРАЗИТЬ» в момент прохода мяча через зеленую линию. Не отбивайте фантомы [X]!";
            so.FindProperty("_ballTransform").objectReferenceValue = bRt;
            so.FindProperty("_interceptorLine").objectReferenceValue = lRt;
            so.FindProperty("_hitPaddle").objectReferenceValue = pRt;
            so.FindProperty("_interceptButton").objectReferenceValue = btn;
            so.FindProperty("_targetLineX").floatValue = -100f;
            so.FindProperty("_tolerance").floatValue = 30f;
            so.FindProperty("_requiredStreak").intValue = 3;
            so.FindProperty("_streakText").objectReferenceValue = sTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_ballImage").objectReferenceValue = bImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M15_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M16: Rhythm Phase Detector
        public static GameObject BuildM16RhythmPhaseDetectorPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M16_RhythmPhaseDetector", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite leverSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Rhythm_LeverHandle.png");

            GameObject instrObj = CreateInstruction(root.transform, "Ритмично чередуйте: ЛЕВЫЙ -> ПРАВЫЙ рычаг (темп 0.3-1.1с). Не жмите средний сломанный [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchoredPosition = new Vector2(0f, 122f);
            instrRt.sizeDelta = new Vector2(650f, 24f);

            // Текст тактов (на отдельной строке, Y = 96)
            GameObject beatsObj = new GameObject("BeatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            beatsObj.transform.SetParent(root.transform, false);
            RectTransform bRt = beatsObj.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, 96f);
            bRt.sizeDelta = new Vector2(300f, 24f);
            TextMeshProUGUI bTmp = beatsObj.GetComponent<TextMeshProUGUI>();
            bTmp.text = "Ритм-такт: 0 / 8";
            bTmp.fontSize = 16;
            bTmp.fontStyle = FontStyles.Bold;
            bTmp.alignment = TextAlignmentOptions.Center;
            bTmp.color = Color.white;

            // Бак/резервуар заполнения (на отдельной строке, Y = 68)
            GameObject tankBg = new GameObject("TankBg", typeof(RectTransform), typeof(Image));
            tankBg.transform.SetParent(root.transform, false);
            RectTransform tRt = tankBg.GetComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(0f, 68f);
            tRt.sizeDelta = new Vector2(300f, 20f);
            Image tImg = tankBg.GetComponent<Image>();
            tImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject tankFill = new GameObject("TankFill", typeof(RectTransform), typeof(Image));
            tankFill.transform.SetParent(tankBg.transform, false);
            RectTransform tfRt = tankFill.GetComponent<RectTransform>();
            tfRt.anchorMin = Vector2.zero;
            tfRt.anchorMax = Vector2.one;
            tfRt.offsetMin = Vector2.zero;
            tfRt.offsetMax = Vector2.zero;
            Image tfImg = tankFill.GetComponent<Image>();
            tfImg.color = new Color(0.2f, 0.85f, 0.45f, 1f);
            tfImg.type = Image.Type.Filled;
            tfImg.fillMethod = Image.FillMethod.Horizontal;
            tfImg.fillAmount = 0f;

            // Метроном / стрелка такта (Y = 18)
            GameObject tickObj = new GameObject("MetronomeTick", typeof(RectTransform), typeof(Image));
            tickObj.transform.SetParent(root.transform, false);
            RectTransform tkRt = tickObj.GetComponent<RectTransform>();
            tkRt.pivot = new Vector2(0.5f, 0.1f);
            tkRt.anchoredPosition = new Vector2(0f, 18f);
            tkRt.sizeDelta = new Vector2(10f, 44f);
            Image tkImg = tickObj.GetComponent<Image>();
            tkImg.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Рычаги (Y = -60)
            GameObject btnLeft = CreateTapButton(root.transform, "BtnLeftLever", "ЛЕВЫЙ", new Vector2(-150f, -60f), new Vector2(110f, 65f), leverSpr);
            GameObject btnRight = CreateTapButton(root.transform, "BtnRightLever", "ПРАВЫЙ", new Vector2(150f, -60f), new Vector2(110f, 65f), leverSpr);
            GameObject btnJunk = CreateTapButton(root.transform, "BtnJunkLever", "[X] СЛОМАН", new Vector2(0f, -60f), new Vector2(90f, 55f), leverSpr);
            btnJunk.GetComponent<Image>().color = new Color(0.85f, 0.4f, 0.4f, 1f);

            M16_RhythmPhaseDetectorMechanic mechanic = root.AddComponent<M16_RhythmPhaseDetectorMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M16_RhythmPhaseDetector";
            so.FindProperty("_title").stringValue = "Механика #16: Синхронизация поочередных фаз движения (Rhythm Phase Detector)";
            so.FindProperty("_instruction").stringValue = "Ритмично чередуйте: ЛЕВЫЙ -> ПРАВЫЙ рычаг (темп 0.3-1.1с). Не жмите средний сломанный [X]!";
            so.FindProperty("_btnLeftLever").objectReferenceValue = btnLeft.GetComponent<Button>();
            so.FindProperty("_btnRightLever").objectReferenceValue = btnRight.GetComponent<Button>();
            so.FindProperty("_btnJunkCenterLever").objectReferenceValue = btnJunk.GetComponent<Button>();
            so.FindProperty("_tankFill").objectReferenceValue = tfImg;
            so.FindProperty("_beatsText").objectReferenceValue = bTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_metronomeTick").objectReferenceValue = tkRt;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M16_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M17: Tug-of-War Balance
        public static GameObject BuildM17TugOfWarBalancePrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M17_TugOfWarBalance", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite ropeSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tug_Rope.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Tap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Быстро тапайте «ТЯНИ!», чтобы перетянуть канат в зону победы. Не жмите скользкий узел [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Канат
            GameObject ropeObj = new GameObject("RopeVisual", typeof(RectTransform), typeof(Image));
            ropeObj.transform.SetParent(root.transform, false);
            RectTransform rpRt = ropeObj.GetComponent<RectTransform>();
            rpRt.anchoredPosition = new Vector2(0f, 40f);
            rpRt.sizeDelta = new Vector2(460f, 26f);
            Image rpImg = ropeObj.GetComponent<Image>();
            rpImg.sprite = ropeSpr;

            // Маркер центра каната
            GameObject markerObj = new GameObject("RopeMarker", typeof(RectTransform), typeof(Image));
            markerObj.transform.SetParent(ropeObj.transform, false);
            RectTransform mRt = markerObj.GetComponent<RectTransform>();
            mRt.anchoredPosition = Vector2.zero;
            mRt.sizeDelta = new Vector2(22f, 44f);
            Image mImg = markerObj.GetComponent<Image>();
            mImg.color = new Color(1f, 0.2f, 0.2f, 1f);

            // Прогресс бар баланса
            GameObject barBg = new GameObject("BalanceBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, 90f);
            bRt.sizeDelta = new Vector2(340f, 16f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("BalanceBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0.2f, 0.85f, 1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0.5f;

            // Кнопка тяги
            GameObject btnPull = CreateTapButton(root.transform, "BtnPull", "ТЯНИ! ▶", new Vector2(-110f, -65f), new Vector2(160f, 52f), btnSpr);
            // Ложная кнопка / скользкий узел [X]
            GameObject btnSlip = CreateTapButton(root.transform, "BtnSlipHazard", "[X] УЗЕЛ", new Vector2(130f, -65f), new Vector2(120f, 44f), btnSpr);
            btnSlip.GetComponent<Image>().color = new Color(0.85f, 0.4f, 0.4f, 1f);

            // Текст баланса
            GameObject balTextObj = new GameObject("BalanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            balTextObj.transform.SetParent(root.transform, false);
            RectTransform btRt = balTextObj.GetComponent<RectTransform>();
            btRt.anchoredPosition = new Vector2(0f, 96f);
            btRt.sizeDelta = new Vector2(300f, 30f);
            TextMeshProUGUI btTmp = balTextObj.GetComponent<TextMeshProUGUI>();
            btTmp.text = "Баланс сил: 50% (Цель: 92%)";
            btTmp.fontSize = 18;
            btTmp.alignment = TextAlignmentOptions.Center;
            btTmp.color = Color.white;

            M17_TugOfWarBalanceMechanic mechanic = root.AddComponent<M17_TugOfWarBalanceMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M17_TugOfWarBalance";
            so.FindProperty("_title").stringValue = "Механика #17: Аккумулятор интенсивности ввода с диссипацией (Tug-of-War Balance)";
            so.FindProperty("_instruction").stringValue = "Быстро тапайте «ТЯНИ!», чтобы перетянуть канат в зону победы. Не жмите скользкий узел [X]!";
            so.FindProperty("_btnPull").objectReferenceValue = btnPull.GetComponent<Button>();
            so.FindProperty("_btnSlipHazard").objectReferenceValue = btnSlip.GetComponent<Button>();
            so.FindProperty("_ropeMarker").objectReferenceValue = mRt;
            so.FindProperty("_ropeVisual").objectReferenceValue = rpRt;
            so.FindProperty("_balanceFill").objectReferenceValue = bfImg;
            so.FindProperty("_balanceText").objectReferenceValue = btTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M17_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M18: BFS Graph Flow Closure
        public static GameObject BuildM18GraphFlowClosurePrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M18_GraphFlowClosure", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite straightSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Straight.png");
            Sprite cornerSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Corner.png");
            Sprite sourceSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Source.png");
            Sprite sinkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Sink.png");
            Sprite junkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Junk_Broken.png");

            GameObject instrObj = CreateInstruction(root.transform, "Кликайте по трубам для поворота. Соедините Источник (слева-сверху) с Приемником (справа-снизу)!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // 3x3 сетка труб
            GameObject gridContainer = new GameObject("PipesGrid", typeof(RectTransform));
            gridContainer.transform.SetParent(root.transform, false);
            RectTransform gRt = gridContainer.GetComponent<RectTransform>();
            gRt.anchoredPosition = new Vector2(0f, -25f);
            gRt.sizeDelta = new Vector2(240f, 240f);

            PipeTile[] tiles = new PipeTile[9];
            PipeType[] types = new PipeType[]
            {
                PipeType.Source,   PipeType.Straight, PipeType.Corner,
                PipeType.Corner,   PipeType.BrokenJunk, PipeType.Corner,
                PipeType.Corner,   PipeType.Straight, PipeType.Sink
            };

            int[] initialRotations = new int[] { 0, 1, 2, 3, 0, 1, 2, 1, 0 };

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int index = y * 3 + x;
                    GameObject tObj = new GameObject($"PipeTile_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(PipeTile));
                    tObj.transform.SetParent(gridContainer.transform, false);
                    RectTransform trt = tObj.GetComponent<RectTransform>();
                    trt.anchoredPosition = new Vector2((x - 1) * 72f, (1 - y) * 72f);
                    trt.sizeDelta = new Vector2(68f, 68f);

                    Image img = tObj.GetComponent<Image>();
                    switch (types[index])
                    {
                        case PipeType.Source: img.sprite = sourceSpr; break;
                        case PipeType.Sink: img.sprite = sinkSpr; break;
                        case PipeType.Corner: img.sprite = cornerSpr; break;
                        case PipeType.BrokenJunk: img.sprite = junkSpr; break;
                        default: img.sprite = straightSpr; break;
                    }

                    PipeTile pt = tObj.GetComponent<PipeTile>();
                    pt.Setup(types[index], initialRotations[index]);
                    tiles[index] = pt;
                }
            }

            // Статус
            GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(root.transform, false);
            RectTransform stRt = statusObj.GetComponent<RectTransform>();
            stRt.anchoredPosition = new Vector2(0f, 96f);
            stRt.sizeDelta = new Vector2(350f, 24f);
            TextMeshProUGUI stTmp = statusObj.GetComponent<TextMeshProUGUI>();
            stTmp.text = "Заполнено узлов: 1 / 9";
            stTmp.fontSize = 17;
            stTmp.alignment = TextAlignmentOptions.Center;
            stTmp.color = Color.white;

            M18_GraphFlowClosureMechanic mechanic = root.AddComponent<M18_GraphFlowClosureMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M18_GraphFlowClosure";
            so.FindProperty("_title").stringValue = "Механика #18: Дискретный поворот топологического узла (BFS Graph Flow Closure)";
            so.FindProperty("_instruction").stringValue = "Кликайте по трубам для поворота. Соедините Источник (слева-сверху) с Приемником (справа-снизу)!";
            so.FindProperty("_gridWidth").intValue = 3;
            so.FindProperty("_gridHeight").intValue = 3;
            so.FindProperty("_straightSprite").objectReferenceValue = straightSpr;
            so.FindProperty("_cornerSprite").objectReferenceValue = cornerSpr;
            so.FindProperty("_sourceSprite").objectReferenceValue = sourceSpr;
            so.FindProperty("_sinkSprite").objectReferenceValue = sinkSpr;
            so.FindProperty("_junkSprite").objectReferenceValue = junkSpr;
            so.FindProperty("_statusText").objectReferenceValue = stTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;

            SerializedProperty tilesProp = so.FindProperty("_gridTiles");
            tilesProp.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M18_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M19: Flood Fill Coloring
        public static GameObject BuildM19FloodFillColoringPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M19_FloodFillColoring", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite frameSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Segment_Frame.png");
            Sprite redSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Color_Swatch_Red.png");
            Sprite greenSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Color_Swatch_Green.png");
            Sprite blueSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Color_Swatch_Blue.png");
            Sprite yellowSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Color_Swatch_Yellow.png");
            Sprite mudSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Color_Swatch_MudJunk.png");

            GameObject instrObj = CreateInstruction(root.transform, "Выберите цвет в палитре и раскрасьте сегменты по эталону. Не используйте грязь [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // 4 сегмента для раскраски
            ColoringSegment[] segments = new ColoringSegment[4];
            Vector2[] segPos = new Vector2[]
            {
                new Vector2(-65f, 55f), // Красный (1)
                new Vector2(65f, 55f),  // Зеленый (2)
                new Vector2(-65f, -25f), // Синий (3)
                new Vector2(65f, -25f)   // Желтый (4)
            };
            int[] expectedIds = new int[] { 1, 2, 3, 4 };

            for (int i = 0; i < 4; i++)
            {
                GameObject segObj = new GameObject($"Segment_{i + 1}", typeof(RectTransform), typeof(Image), typeof(ColoringSegment));
                segObj.transform.SetParent(root.transform, false);
                RectTransform sRt = segObj.GetComponent<RectTransform>();
                sRt.anchoredPosition = segPos[i];
                sRt.sizeDelta = new Vector2(110f, 90f);
                Image sImg = segObj.GetComponent<Image>();
                sImg.sprite = frameSpr;

                ColoringSegment cs = segObj.GetComponent<ColoringSegment>();
                SerializedObject csSo = new SerializedObject(cs);
                csSo.FindProperty("_expectedColorId").intValue = expectedIds[i];
                csSo.FindProperty("_fillImage").objectReferenceValue = sImg;
                csSo.ApplyModifiedPropertiesWithoutUndo();

                segments[i] = cs;
            }

            // Палитра
            GameObject btnRed = CreateSwatchButton(root.transform, "BtnRed", new Vector2(-140f, -92f), redSpr);
            GameObject btnGreen = CreateSwatchButton(root.transform, "BtnGreen", new Vector2(-70f, -92f), greenSpr);
            GameObject btnBlue = CreateSwatchButton(root.transform, "BtnBlue", new Vector2(0f, -92f), blueSpr);
            GameObject btnYellow = CreateSwatchButton(root.transform, "BtnYellow", new Vector2(70f, -92f), yellowSpr);
            GameObject btnMud = CreateSwatchButton(root.transform, "BtnMudJunk", new Vector2(140f, -92f), mudSpr);

            // Индикатор выбранного цвета
            GameObject actObj = new GameObject("ActiveColorIndicator", typeof(RectTransform), typeof(Image));
            actObj.transform.SetParent(root.transform, false);
            RectTransform aRt = actObj.GetComponent<RectTransform>();
            aRt.anchoredPosition = new Vector2(210f, -92f);
            aRt.sizeDelta = new Vector2(30f, 30f);
            Image aImg = actObj.GetComponent<Image>();
            aImg.color = new Color(1f, 0.3f, 0.3f, 1f);

            // Панель Эталона (Образец для раскраски)
            GameObject etalonObj = new GameObject("EtalonPanel", typeof(RectTransform), typeof(Image));
            etalonObj.transform.SetParent(root.transform, false);
            RectTransform etalonRt = etalonObj.GetComponent<RectTransform>();
            etalonRt.anchoredPosition = new Vector2(230f, 15f);
            etalonRt.sizeDelta = new Vector2(130f, 170f);
            Image etalonBg = etalonObj.GetComponent<Image>();
            etalonBg.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);

            GameObject titleObj = new GameObject("EtalonTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(etalonRt, false);
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(0f, 62f);
            tRt.sizeDelta = new Vector2(120f, 26f);
            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "ЭТАЛОН";
            titleTmp.fontSize = 15f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(1f, 0.85f, 0.25f, 1f);

            Vector2[] prevPos = new Vector2[]
            {
                new Vector2(-28f, 16f),
                new Vector2(28f, 16f),
                new Vector2(-28f, -32f),
                new Vector2(28f, -32f)
            };
            Color[] prevColors = new Color[]
            {
                new Color(1f, 0.3f, 0.3f, 1f),
                new Color(0.25f, 0.85f, 0.45f, 1f),
                new Color(0.2f, 0.55f, 1f, 1f),
                new Color(1f, 0.85f, 0.2f, 1f)
            };
            Image[] etalonPreviews = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject prevObj = new GameObject($"Preview_{i + 1}", typeof(RectTransform), typeof(Image));
                prevObj.transform.SetParent(etalonRt, false);
                RectTransform pRt = prevObj.GetComponent<RectTransform>();
                pRt.anchoredPosition = prevPos[i];
                pRt.sizeDelta = new Vector2(50f, 40f);
                Image pImg = prevObj.GetComponent<Image>();
                pImg.sprite = frameSpr;
                pImg.color = prevColors[i];
                pImg.raycastTarget = false;
                etalonPreviews[i] = pImg;
            }

            M19_FloodFillColoringMechanic mechanic = root.AddComponent<M19_FloodFillColoringMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M19_FloodFillColoring";
            so.FindProperty("_title").stringValue = "Механика #19: Присвоение идентификатора сегменту маски (Tag / Flood Fill)";
            so.FindProperty("_instruction").stringValue = "Выберите цвет в палитре и раскрасьте сегменты по эталону. Не используйте грязь [X]!";
            so.FindProperty("_btnRed").objectReferenceValue = btnRed.GetComponent<Button>();
            so.FindProperty("_btnGreen").objectReferenceValue = btnGreen.GetComponent<Button>();
            so.FindProperty("_btnBlue").objectReferenceValue = btnBlue.GetComponent<Button>();
            so.FindProperty("_btnYellow").objectReferenceValue = btnYellow.GetComponent<Button>();
            so.FindProperty("_btnJunkMud").objectReferenceValue = btnMud.GetComponent<Button>();
            so.FindProperty("_activeColorIndicator").objectReferenceValue = aImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_etalonContainer").objectReferenceValue = etalonRt;

            SerializedProperty prevsProp = so.FindProperty("_etalonPreviews");
            prevsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                prevsProp.GetArrayElementAtIndex(i).objectReferenceValue = etalonPreviews[i];
            }

            SerializedProperty segProp = so.FindProperty("_segments");
            segProp.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                segProp.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M19_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSwatchButton(Transform parent, string name, Vector2 pos, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(50f, 50f);
            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;
            return obj;
        }
        #endregion

        #region M20: Vertical Stacking
        public static GameObject BuildM20VerticalStackingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M20_VerticalStacking", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite baseSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Stack_Base.png");
            Sprite blockSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Stack_Block.png");
            Sprite junkBlockSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Stack_Block_Junk.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Tap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Нажмите «СБРОСИТЬ БЛОК» в момент пролета над башней. Постройте башню из 3 блоков!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Базовая платформа
            GameObject baseObj = new GameObject("BasePlatform", typeof(RectTransform), typeof(Image));
            baseObj.transform.SetParent(root.transform, false);
            RectTransform bsRt = baseObj.GetComponent<RectTransform>();
            bsRt.anchoredPosition = new Vector2(0f, -65f);
            bsRt.sizeDelta = new Vector2(200f, 40f);
            Image bsImg = baseObj.GetComponent<Image>();
            bsImg.sprite = baseSpr;

            // Качающаяся балка крана
            GameObject armObj = new GameObject("SwingingArm", typeof(RectTransform), typeof(Image));
            armObj.transform.SetParent(root.transform, false);
            RectTransform aRt = armObj.GetComponent<RectTransform>();
            aRt.anchoredPosition = new Vector2(0f, 85f);
            aRt.sizeDelta = new Vector2(16f, 30f);
            Image aImg = armObj.GetComponent<Image>();
            aImg.color = new Color(0.7f, 0.75f, 0.85f, 1f);

            // Активный сбрасываемый блок
            GameObject blockObj = new GameObject("ActiveBlock", typeof(RectTransform), typeof(Image));
            blockObj.transform.SetParent(root.transform, false);
            RectTransform blRt = blockObj.GetComponent<RectTransform>();
            blRt.anchoredPosition = new Vector2(0f, 65f);
            blRt.sizeDelta = new Vector2(110f, 45f);
            Image blImg = blockObj.GetComponent<Image>();
            blImg.sprite = blockSpr;

            // Кнопка сброса
            GameObject btnDrop = CreateTapButton(root.transform, "BtnDrop", "СБРОСИТЬ БЛОК", new Vector2(0f, -105f), new Vector2(180f, 38f), btnSpr);

            // Счетчик блоков
            GameObject stackTextObj = new GameObject("StackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stackTextObj.transform.SetParent(root.transform, false);
            RectTransform stRt = stackTextObj.GetComponent<RectTransform>();
            stRt.anchoredPosition = new Vector2(0f, 96f);
            stRt.sizeDelta = new Vector2(250f, 24f);
            TextMeshProUGUI stTmp = stackTextObj.GetComponent<TextMeshProUGUI>();
            stTmp.text = "Блоков в башне: 0 / 3";
            stTmp.fontSize = 18;
            stTmp.alignment = TextAlignmentOptions.Center;
            stTmp.color = Color.white;

            M20_VerticalStackingMechanic mechanic = root.AddComponent<M20_VerticalStackingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M20_VerticalStacking";
            so.FindProperty("_title").stringValue = "Механика #20: Своевременный сброс с проверкой устойчивости (Vertical Stacking)";
            so.FindProperty("_instruction").stringValue = "Нажмите «СБРОСИТЬ БЛОК» в момент пролета над башней. Постройте башню из 3 блоков!";
            so.FindProperty("_swingingArm").objectReferenceValue = aRt;
            so.FindProperty("_activeBlock").objectReferenceValue = blRt;
            so.FindProperty("_basePlatform").objectReferenceValue = bsRt;
            so.FindProperty("_btnDrop").objectReferenceValue = btnDrop.GetComponent<Button>();
            so.FindProperty("_standardBlockSprite").objectReferenceValue = blockSpr;
            so.FindProperty("_junkBlockSprite").objectReferenceValue = junkBlockSpr;
            so.FindProperty("_stackText").objectReferenceValue = stTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M20_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M21: Reaction Probe
        public static GameObject BuildM21ReactionProbePrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M21_ReactionProbe", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite holeSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Fishing_Hole.png");
            Sprite bobberSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Fishing_Bobber.png");
            Sprite exclamSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Fishing_Exclamation.png");
            Sprite bootSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Fishing_Junk_Boot.png");
            Sprite btnSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Button_Tap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Ждите поклёвки (знак «!» и рывок поплавка вниз) и моментально жмите «ПОДСЕЧЬ»!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Прорубь
            GameObject holeObj = new GameObject("FishingHole", typeof(RectTransform), typeof(Image));
            holeObj.transform.SetParent(root.transform, false);
            RectTransform hRt = holeObj.GetComponent<RectTransform>();
            hRt.anchoredPosition = new Vector2(0f, -10f);
            hRt.sizeDelta = new Vector2(160f, 160f);
            Image hImg = holeObj.GetComponent<Image>();
            hImg.sprite = holeSpr;

            // Мусорный сапог [X]
            GameObject bootObj = new GameObject("JunkBoot", typeof(RectTransform), typeof(Image));
            bootObj.transform.SetParent(root.transform, false);
            RectTransform btRt = bootObj.GetComponent<RectTransform>();
            btRt.anchoredPosition = new Vector2(135f, -30f);
            btRt.sizeDelta = new Vector2(60f, 60f);
            Image btImg = bootObj.GetComponent<Image>();
            btImg.sprite = bootSpr;

            // Поплавок
            GameObject bobberObj = new GameObject("Bobber", typeof(RectTransform), typeof(Image));
            bobberObj.transform.SetParent(holeObj.transform, false);
            RectTransform bbRt = bobberObj.GetComponent<RectTransform>();
            bbRt.anchoredPosition = Vector2.zero;
            bbRt.sizeDelta = new Vector2(40f, 65f);
            Image bbImg = bobberObj.GetComponent<Image>();
            bbImg.sprite = bobberSpr;

            // Знак поклевки (!)
            GameObject exclamObj = new GameObject("BiteExclamation", typeof(RectTransform), typeof(Image));
            exclamObj.transform.SetParent(holeObj.transform, false);
            RectTransform exRt = exclamObj.GetComponent<RectTransform>();
            exRt.anchoredPosition = new Vector2(0f, 65f);
            exRt.sizeDelta = new Vector2(50f, 50f);
            Image exImg = exclamObj.GetComponent<Image>();
            exImg.sprite = exclamSpr;
            exclamObj.SetActive(false);

            // Кнопка подсечки
            GameObject btnStrike = CreateTapButton(root.transform, "BtnStrike", "ПОДСЕЧЬ! ->", new Vector2(0f, -82f), new Vector2(160f, 38f), btnSpr);

            // Счет улова
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Поймано: 0 / 2";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            M21_ReactionProbeMechanic mechanic = root.AddComponent<M21_ReactionProbeMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M21_ReactionProbe";
            so.FindProperty("_title").stringValue = "Механика #21: Реакция на фазовый триггер поклевки (Reaction Time Probe)";
            so.FindProperty("_instruction").stringValue = "Ждите поклёвки (знак «!» и рывок поплавка вниз) и моментально жмите «ПОДСЕЧЬ»!";
            so.FindProperty("_bobber").objectReferenceValue = bbRt;
            so.FindProperty("_biteExclamation").objectReferenceValue = exRt;
            so.FindProperty("_junkBoot").objectReferenceValue = btRt;
            so.FindProperty("_btnStrike").objectReferenceValue = btnStrike.GetComponent<Button>();
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M21_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M22: Grid Pathfinding
        public static GameObject BuildM22GridPathfindingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M22_GridPathfinding", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite tileSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_Tile.png");
            Sprite robotSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_Robot.png");
            Sprite goalSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_Goal.png");
            Sprite obsSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_Obstacle.png");
            Sprite trapSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_GlitchTrap.png");

            GameObject instrObj = CreateInstruction(root.transform, "Нажимайте на соседние доступные ячейки, чтобы довести робота до зеленого флага!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Контейнер 5x5 сетки
            GameObject gridContainer = new GameObject("GridContainer", typeof(RectTransform));
            gridContainer.transform.SetParent(root.transform, false);
            RectTransform gRt = gridContainer.GetComponent<RectTransform>();
            gRt.anchoredPosition = new Vector2(0f, -25f);
            gRt.sizeDelta = new Vector2(240f, 240f);

            GridTileButton[] tiles = new GridTileButton[25];
            HashSet<Vector2Int> walls = new HashSet<Vector2Int>
            {
                new Vector2Int(1, 3), new Vector2Int(2, 3),
                new Vector2Int(2, 1), new Vector2Int(3, 1)
            };
            Vector2Int trapPos = new Vector2Int(1, 1);
            Vector2Int goalPos = new Vector2Int(4, 0);

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int index = y * 5 + x;
                    Vector2Int pos = new Vector2Int(x, y);

                    GameObject tObj = new GameObject($"Tile_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(GridTileButton));
                    tObj.transform.SetParent(gridContainer.transform, false);
                    RectTransform trt = tObj.GetComponent<RectTransform>();
                    trt.anchoredPosition = new Vector2((x - 2) * 44f, (2 - y) * 44f);
                    trt.sizeDelta = new Vector2(40f, 40f);

                    GridCellType cType = GridCellType.Walkable;
                    Sprite s = tileSpr;
                    Color tint = Color.white;

                    if (walls.Contains(pos))
                    {
                        cType = GridCellType.ObstacleWall;
                        s = obsSpr;
                    }
                    else if (pos == trapPos)
                    {
                        cType = GridCellType.GlitchTrapHazard;
                        s = trapSpr;
                    }
                    else if (pos == goalPos)
                    {
                        cType = GridCellType.Goal;
                        s = goalSpr;
                    }

                    Image tImg = tObj.GetComponent<Image>();
                    if (s != null) tImg.sprite = s;
                    tImg.color = tint;

                    GridTileButton gtb = tObj.GetComponent<GridTileButton>();
                    gtb.Setup(cType, x, y, s, tint);
                    tiles[index] = gtb;
                }
            }

            // Робот
            GameObject robotObj = new GameObject("RobotAvatar", typeof(RectTransform), typeof(Image));
            robotObj.transform.SetParent(gridContainer.transform, false);
            RectTransform rRt = robotObj.GetComponent<RectTransform>();
            rRt.anchoredPosition = tiles[4 * 5 + 0].RectTransform.anchoredPosition; // (0, 4)
            rRt.sizeDelta = new Vector2(36f, 36f);
            Image rImg = robotObj.GetComponent<Image>();
            rImg.sprite = robotSpr;

            // Текст шагов
            GameObject stepObj = new GameObject("StepText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stepObj.transform.SetParent(root.transform, false);
            RectTransform stRt = stepObj.GetComponent<RectTransform>();
            stRt.anchoredPosition = new Vector2(0f, 96f);
            stRt.sizeDelta = new Vector2(300f, 24f);
            TextMeshProUGUI stTmp = stepObj.GetComponent<TextMeshProUGUI>();
            stTmp.text = "Шаги: 0 | Позиция: (0,4)";
            stTmp.fontSize = 17;
            stTmp.alignment = TextAlignmentOptions.Center;
            stTmp.color = Color.white;

            M22_GridPathfindingMechanic mechanic = root.AddComponent<M22_GridPathfindingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M22_GridPathfinding";
            so.FindProperty("_title").stringValue = "Механика #22: Навигация сущности по дискретной тайловой сетке (Grid Pathfinding)";
            so.FindProperty("_instruction").stringValue = "Нажимайте на соседние доступные ячейки, чтобы довести робота до зеленого флага!";
            so.FindProperty("_gridSize").intValue = 5;
            so.FindProperty("_robotAvatar").objectReferenceValue = rRt;
            so.FindProperty("_stepText").objectReferenceValue = stTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;

            SerializedProperty tilesProp = so.FindProperty("_gridTiles");
            tilesProp.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M22_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M23: Stencil Reveal
        public static GameObject BuildM23StencilRevealPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M23_StencilReveal", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite lensSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Stencil_Lens.png");
            Sprite gemSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Hidden_Target_Gem.png");
            Sprite dustSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Hidden_Junk_Dust.png");

            GameObject instrObj = CreateInstruction(root.transform, "Перемещайте лупу по области, находите скрытые кристаллы и удерживайте 2 сек для сбора!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Темная зона исследования (размер 500x210, чтобы оставался простор для текста сверху)
            GameObject darkObj = new GameObject("DarkSearchArea", typeof(RectTransform), typeof(Image));
            darkObj.transform.SetParent(root.transform, false);
            RectTransform dkRt = darkObj.GetComponent<RectTransform>();
            dkRt.anchoredPosition = new Vector2(0f, -25f);
            dkRt.sizeDelta = new Vector2(500f, 210f);
            Image dkImg = darkObj.GetComponent<Image>();
            dkImg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
            dkImg.raycastTarget = true;

            // Скрытые кристаллы (внутри 500x210)
            Vector2[] gemPositions = new Vector2[]
            {
                new Vector2(-150f, 40f),
                new Vector2(140f, 30f),
                new Vector2(-40f, -45f)
            };

            List<HiddenRevealTarget> targets = new List<HiddenRevealTarget>();
            for (int i = 0; i < gemPositions.Length; i++)
            {
                GameObject gemObj = new GameObject($"HiddenGem_{i + 1}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HiddenRevealTarget));
                gemObj.transform.SetParent(darkObj.transform, false);
                RectTransform gRt = gemObj.GetComponent<RectTransform>();
                gRt.anchoredPosition = gemPositions[i];
                gRt.sizeDelta = new Vector2(50f, 50f);
                Image gImg = gemObj.GetComponent<Image>();
                gImg.sprite = gemSpr;
                gImg.raycastTarget = false;

                HiddenRevealTarget hrt = gemObj.GetComponent<HiddenRevealTarget>();
                hrt.SetRevealed(false);
                targets.Add(hrt);
            }

            // Мусорная пыль [X]
            GameObject dustObj = new GameObject("JunkDust", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HiddenRevealTarget));
            dustObj.transform.SetParent(darkObj.transform, false);
            RectTransform dRt = dustObj.GetComponent<RectTransform>();
            dRt.anchoredPosition = new Vector2(110f, -40f);
            dRt.sizeDelta = new Vector2(50f, 50f);
            Image dImg = dustObj.GetComponent<Image>();
            dImg.sprite = dustSpr;
            dImg.raycastTarget = false;
            HiddenRevealTarget dustHrt = dustObj.GetComponent<HiddenRevealTarget>();
            SerializedObject dustSo = new SerializedObject(dustHrt);
            dustSo.FindProperty("_isJunkDust").boolValue = true;
            dustSo.ApplyModifiedPropertiesWithoutUndo();
            dustHrt.SetRevealed(false);

            // Лупа (с возможностью прямого захвата и перетаскивания)
            GameObject lensObj = new GameObject("StencilLens", typeof(RectTransform), typeof(Image));
            lensObj.transform.SetParent(darkObj.transform, false);
            RectTransform lRt = lensObj.GetComponent<RectTransform>();
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(130f, 130f);
            Image lImg = lensObj.GetComponent<Image>();
            lImg.sprite = lensSpr;
            lImg.raycastTarget = true;

            // Круговой индикатор 2-секундного удержания на лупе
            GameObject ringObj = new GameObject("ProgressRing", typeof(RectTransform), typeof(Image));
            ringObj.transform.SetParent(lensObj.transform, false);
            RectTransform ringRt = ringObj.GetComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = new Vector2(6f, 6f);
            ringRt.offsetMax = new Vector2(-6f, -6f);
            Image ringImg = ringObj.GetComponent<Image>();
            ringImg.sprite = lensSpr;
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillAmount = 0f;
            ringImg.color = new Color(0.2f, 1f, 0.6f, 0.75f);
            ringImg.raycastTarget = false;

            // Счет кристаллов
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(280f, 24f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Найдено: 0 / 3";
            scTmp.fontSize = 17;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            M23_StencilRevealMechanic mechanic = root.AddComponent<M23_StencilRevealMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M23_StencilReveal";
            so.FindProperty("_title").stringValue = "Механика #23: Пространственная фильтрация триггеров лучом/окном (Stencil Reveal)";
            so.FindProperty("_instruction").stringValue = "Перемещайте лупу по области, находите скрытые кристаллы и удерживайте 2 сек для сбора!";
            so.FindProperty("_lensTransform").objectReferenceValue = lRt;
            so.FindProperty("_darkSearchArea").objectReferenceValue = dkRt;
            so.FindProperty("_lensProgressRing").objectReferenceValue = ringImg;
            so.FindProperty("_junkDust").objectReferenceValue = dustHrt;
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;

            SerializedProperty targetsProp = so.FindProperty("_targets");
            targetsProp.arraySize = targets.Count;
            for (int i = 0; i < targets.Count; i++)
            {
                targetsProp.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M23_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M24: Target Elimination
        public static GameObject BuildM24TargetEliminationPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M24_TargetElimination", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite bubbleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Target_Bubble.png");
            Sprite bombSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Target_Hazard_Bomb.png");

            GameObject instrObj = CreateInstruction(root.transform, "Кликайте по всплывающим мыльным пузырям (лопните 6 шт). Не трогайте красные бомбы [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Контейнер спавна пузырей с маской
            GameObject spawnContainer = new GameObject("SpawnContainer", typeof(RectTransform), typeof(RectMask2D));
            spawnContainer.transform.SetParent(root.transform, false);
            RectTransform spRt = spawnContainer.GetComponent<RectTransform>();
            spRt.anchoredPosition = new Vector2(0f, -5f);
            spRt.sizeDelta = new Vector2(550f, 260f);

            // Счет
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Лопнуто: 0 / 6";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            M24_TargetEliminationMechanic mechanic = root.AddComponent<M24_TargetEliminationMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M24_TargetElimination";
            so.FindProperty("_title").stringValue = "Механика #24: Прямое поражение движущихся точечных целей (Target Elimination)";
            so.FindProperty("_instruction").stringValue = "Кликайте по всплывающим мыльным пузырям (лопните 6 шт). Не трогайте красные бомбы [X]!";
            so.FindProperty("_spawnContainer").objectReferenceValue = spRt;
            so.FindProperty("_bubbleSprite").objectReferenceValue = bubbleSpr;
            so.FindProperty("_hazardBombSprite").objectReferenceValue = bombSpr;
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_targetPops").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M24_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M25: Lane Switcher Tap Halves
        public static GameObject BuildM25LaneSwitcherTapHalvesPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M25_LaneSwitcherTapHalves", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite avatarSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Avatar.png");
            Sprite coinSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Coin.png");
            Sprite barrierSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Barrier.png");

            GameObject instrObj = CreateInstruction(root.transform, "Нажимайте на левую или правую половину экрана для смены полосы. Соберите 5 монет и избегайте барьеров [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Дорожки
            GameObject lanesObj = new GameObject("LanesBackground", typeof(RectTransform), typeof(Image));
            lanesObj.transform.SetParent(root.transform, false);
            RectTransform lRt = lanesObj.GetComponent<RectTransform>();
            lRt.anchoredPosition = new Vector2(0f, 10f);
            lRt.sizeDelta = new Vector2(420f, 260f);
            Image lImg = lanesObj.GetComponent<Image>();
            lImg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);

            // Контейнер спавна
            GameObject spawnObj = new GameObject("SpawnContainer", typeof(RectTransform), typeof(RectMask2D));
            spawnObj.transform.SetParent(lanesObj.transform, false);
            RectTransform spRt = spawnObj.GetComponent<RectTransform>();
            spRt.anchorMin = Vector2.zero;
            spRt.anchorMax = Vector2.one;
            spRt.offsetMin = Vector2.zero;
            spRt.offsetMax = Vector2.zero;

            // Игрок
            GameObject playerObj = new GameObject("PlayerAvatar", typeof(RectTransform), typeof(Image));
            playerObj.transform.SetParent(spawnObj.transform, false);
            RectTransform pRt = playerObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, -90f);
            pRt.sizeDelta = new Vector2(50f, 50f);
            Image pImg = playerObj.GetComponent<Image>();
            pImg.sprite = avatarSpr;

            // Счет
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Собрано: 0 / 5";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            // Кнопки управления (не используются при тапах, но привязаны)
            GameObject btnLeftObj = new GameObject("BtnLeft_Optional", typeof(RectTransform), typeof(Button));
            btnLeftObj.transform.SetParent(root.transform, false);
            btnLeftObj.SetActive(false);
            GameObject btnRightObj = new GameObject("BtnRight_Optional", typeof(RectTransform), typeof(Button));
            btnRightObj.transform.SetParent(root.transform, false);
            btnRightObj.SetActive(false);

            M13_LaneSwitcherRunnerMechanic mechanic = root.AddComponent<M13_LaneSwitcherRunnerMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_controlMode").enumValueIndex = (int)RunnerControlMode.HalfScreenTap;
            so.FindProperty("_mechanicId").stringValue = "M25_LaneSwitcherTapHalves";
            so.FindProperty("_title").stringValue = "Механика #25: Сдвиг по полосам — Тапы по половинам (Lane Switcher: Half Taps)";
            so.FindProperty("_instruction").stringValue = "Нажимайте на левую или правую половину экрана для смены полосы. Соберите 5 монет и избегайте барьеров [X]!";
            so.FindProperty("_playerAvatar").objectReferenceValue = pRt;
            so.FindProperty("_spawnContainer").objectReferenceValue = spRt;
            so.FindProperty("_coinSprite").objectReferenceValue = coinSpr;
            so.FindProperty("_barrierSprite").objectReferenceValue = barrierSpr;
            so.FindProperty("_btnLeft").objectReferenceValue = btnLeftObj.GetComponent<Button>();
            so.FindProperty("_btnRight").objectReferenceValue = btnRightObj.GetComponent<Button>();
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_targetCoins").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M25_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M26: Lane Switcher Direct Drag
        public static GameObject BuildM26LaneSwitcherDirectDragPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M26_LaneSwitcherDirectDrag", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite avatarSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Avatar.png");
            Sprite coinSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Coin.png");
            Sprite barrierSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Barrier.png");

            GameObject instrObj = CreateInstruction(root.transform, "Зажмите приёмник и ведите его влево-вправо мышкой. Соберите 5 монет и избегайте барьеров [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Дорожки
            GameObject lanesObj = new GameObject("LanesBackground", typeof(RectTransform), typeof(Image));
            lanesObj.transform.SetParent(root.transform, false);
            RectTransform lRt = lanesObj.GetComponent<RectTransform>();
            lRt.anchoredPosition = new Vector2(0f, 10f);
            lRt.sizeDelta = new Vector2(420f, 260f);
            Image lImg = lanesObj.GetComponent<Image>();
            lImg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);

            // Контейнер спавна
            GameObject spawnObj = new GameObject("SpawnContainer", typeof(RectTransform), typeof(RectMask2D));
            spawnObj.transform.SetParent(lanesObj.transform, false);
            RectTransform spRt = spawnObj.GetComponent<RectTransform>();
            spRt.anchorMin = Vector2.zero;
            spRt.anchorMax = Vector2.one;
            spRt.offsetMin = Vector2.zero;
            spRt.offsetMax = Vector2.zero;

            // Игрок
            GameObject playerObj = new GameObject("PlayerAvatar", typeof(RectTransform), typeof(Image));
            playerObj.transform.SetParent(spawnObj.transform, false);
            RectTransform pRt = playerObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, -90f);
            pRt.sizeDelta = new Vector2(50f, 50f);
            Image pImg = playerObj.GetComponent<Image>();
            pImg.sprite = avatarSpr;

            // Счет
            GameObject scoreObj = new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 30f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Собрано: 0 / 5";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            // Кнопки управления (не используются при прямом вождении, но привязаны)
            GameObject btnLeftObj = new GameObject("BtnLeft_Optional", typeof(RectTransform), typeof(Button));
            btnLeftObj.transform.SetParent(root.transform, false);
            btnLeftObj.SetActive(false);
            GameObject btnRightObj = new GameObject("BtnRight_Optional", typeof(RectTransform), typeof(Button));
            btnRightObj.transform.SetParent(root.transform, false);
            btnRightObj.SetActive(false);

            M13_LaneSwitcherRunnerMechanic mechanic = root.AddComponent<M13_LaneSwitcherRunnerMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_controlMode").enumValueIndex = (int)RunnerControlMode.DirectDrag;
            so.FindProperty("_mechanicId").stringValue = "M26_LaneSwitcherDirectDrag";
            so.FindProperty("_title").stringValue = "Механика #26: Сдвиг по полосам — Вождение приёмника (Lane Switcher: Drag)";
            so.FindProperty("_instruction").stringValue = "Зажмите приёмник и ведите его влево-вправо мышкой. Соберите 5 монет и избегайте барьеров [X]!";
            so.FindProperty("_playerAvatar").objectReferenceValue = pRt;
            so.FindProperty("_spawnContainer").objectReferenceValue = spRt;
            so.FindProperty("_coinSprite").objectReferenceValue = coinSpr;
            so.FindProperty("_barrierSprite").objectReferenceValue = barrierSpr;
            so.FindProperty("_btnLeft").objectReferenceValue = btnLeftObj.GetComponent<Button>();
            so.FindProperty("_btnRight").objectReferenceValue = btnRightObj.GetComponent<Button>();
            so.FindProperty("_scoreText").objectReferenceValue = scTmp;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_targetCoins").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M26_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M27: Physics Car Hills
        public static GameObject BuildM27PhysicsCarHillsPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M27_PhysicsCarHills", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite flagSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Goal_Flag.png");
            Sprite valveSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wheel_Valve.png");
            Sprite chassisSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wood_Board.png");

            // Инструкция и телеметрия без наложений
            GameObject speedObj = new GameObject("SpeedometerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            speedObj.transform.SetParent(root.transform, false);
            RectTransform spRt = speedObj.GetComponent<RectTransform>();
            spRt.anchorMin = new Vector2(0f, 1f);
            spRt.anchorMax = new Vector2(0f, 1f);
            spRt.pivot = new Vector2(0f, 1f);
            spRt.anchoredPosition = new Vector2(24f, -10f);
            spRt.sizeDelta = new Vector2(200f, 26f);
            TextMeshProUGUI spTmp = speedObj.GetComponent<TextMeshProUGUI>();
            spTmp.text = "Скорость: 0 км/ч";
            spTmp.fontSize = 14;
            spTmp.fontStyle = FontStyles.Bold;
            spTmp.color = Color.white;
            spTmp.alignment = TextAlignmentOptions.Left;

            GameObject instrObj = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 1f);
            instrRt.anchorMax = new Vector2(0.5f, 1f);
            instrRt.pivot = new Vector2(0.5f, 1f);
            instrRt.anchoredPosition = new Vector2(0f, -38f);
            instrRt.sizeDelta = new Vector2(420f, 26f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "БАЛАНС: ГАЗ (D/->) / ТОРМОЗ (A/<-)";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.color = new Color(0.3f, 0.85f, 1f);
            instrTmp.alignment = TextAlignmentOptions.Center;

            GameObject distObj = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            distObj.transform.SetParent(root.transform, false);
            RectTransform dtRt = distObj.GetComponent<RectTransform>();
            dtRt.anchorMin = new Vector2(1f, 1f);
            dtRt.anchorMax = new Vector2(1f, 1f);
            dtRt.pivot = new Vector2(1f, 1f);
            dtRt.anchoredPosition = new Vector2(-24f, -10f);
            dtRt.sizeDelta = new Vector2(200f, 26f);
            TextMeshProUGUI dtTmp = distObj.GetComponent<TextMeshProUGUI>();
            dtTmp.text = "Дистанция: 0 / 2200 м";
            dtTmp.fontSize = 14;
            dtTmp.fontStyle = FontStyles.Bold;
            dtTmp.color = Color.white;
            dtTmp.alignment = TextAlignmentOptions.Right;

            // Контейнер трассы со скроллингом рельефа (дистанция 2200 м)
            GameObject trackObj = new GameObject("TrackContainer", typeof(RectTransform));
            trackObj.transform.SetParent(root.transform, false);
            RectTransform trRt = trackObj.GetComponent<RectTransform>();
            trRt.anchoredPosition = new Vector2(0f, 0f);
            trRt.sizeDelta = new Vector2(3000f, 300f);

            // Визуальные сегменты рельефа земли (110 сегментов от -350 до +2350)
            int slices = 110;
            float startX = -350f;
            float endX = 2350f;
            float step = (endX - startX) / (slices - 1);
            const float bottomY = -180f;
            for (int i = 0; i < slices; i++)
            {
                float x = startX + i * step;
                float y = M27_PhysicsCarHillsMechanic.GetTerrainHeight(x);
                GameObject sliceObj = new GameObject($"TerrainSlice_{i}", typeof(RectTransform), typeof(Image));
                sliceObj.transform.SetParent(trackObj.transform, false);
                RectTransform sRt = sliceObj.GetComponent<RectTransform>();
                float sliceH = Mathf.Max(20f, y - bottomY);
                sRt.anchoredPosition = new Vector2(x, bottomY + sliceH * 0.5f);
                sRt.sizeDelta = new Vector2(step + 3f, sliceH);
                Image sImg = sliceObj.GetComponent<Image>();
                sImg.color = new Color(0.16f, 0.52f, 0.25f, 0.95f);
                sImg.raycastTarget = false;
            }

            // Указатели пройденной дистанции каждые 500 метров
            float[] markers = new float[] { 500f, 1000f, 1500f, 2000f };
            foreach (float mx in markers)
            {
                GameObject signObj = new GameObject($"Sign_{mx}", typeof(RectTransform), typeof(Image));
                signObj.transform.SetParent(trackObj.transform, false);
                RectTransform sRt = signObj.GetComponent<RectTransform>();
                float my = M27_PhysicsCarHillsMechanic.GetTerrainHeight(mx);
                sRt.anchoredPosition = new Vector2(mx, my + 20f);
                sRt.sizeDelta = new Vector2(46f, 22f);
                Image sImg = signObj.GetComponent<Image>();
                sImg.color = new Color(0.1f, 0.18f, 0.28f, 0.9f);
                sImg.raycastTarget = false;

                GameObject signTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                signTxtObj.transform.SetParent(signObj.transform, false);
                RectTransform stRt = signTxtObj.GetComponent<RectTransform>();
                stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one;
                stRt.offsetMin = Vector2.zero; stRt.offsetMax = Vector2.zero;
                TextMeshProUGUI stTmp = signTxtObj.GetComponent<TextMeshProUGUI>();
                stTmp.text = $"{mx:F0}м";
                stTmp.fontSize = 11;
                stTmp.fontStyle = FontStyles.Bold;
                stTmp.alignment = TextAlignmentOptions.Center;
                stTmp.color = new Color(1f, 0.85f, 0.2f);
                stTmp.raycastTarget = false;
            }

            // Финишный флаг на отметке 2200 м
            GameObject flagObj = new GameObject("FinishFlag", typeof(RectTransform), typeof(Image));
            flagObj.transform.SetParent(trackObj.transform, false);
            RectTransform fRt = flagObj.GetComponent<RectTransform>();
            float flagY = M27_PhysicsCarHillsMechanic.GetTerrainHeight(2200f);
            fRt.anchoredPosition = new Vector2(2200f, flagY + 24f);
            fRt.sizeDelta = new Vector2(40f, 40f);
            Image fImg = flagObj.GetComponent<Image>();
            fImg.sprite = flagSpr;
            fImg.raycastTarget = false;

            // Машинка (CarRoot) - дочерний объект контейнера трассы
            GameObject carRootObj = new GameObject("CarRoot", typeof(RectTransform));
            carRootObj.transform.SetParent(trackObj.transform, false);
            RectTransform crRt = carRootObj.GetComponent<RectTransform>();
            crRt.sizeDelta = new Vector2(80f, 40f);

            // Кузов
            GameObject chassisObj = new GameObject("Chassis", typeof(RectTransform), typeof(Image));
            chassisObj.transform.SetParent(carRootObj.transform, false);
            RectTransform chRt = chassisObj.GetComponent<RectTransform>();
            chRt.anchoredPosition = new Vector2(0f, 12f);
            chRt.sizeDelta = new Vector2(74f, 26f);
            Image chImg = chassisObj.GetComponent<Image>();
            chImg.sprite = chassisSpr;
            chImg.color = new Color(0.95f, 0.35f, 0.15f, 1f);
            chImg.raycastTarget = false;

            // Кабина/стекло
            GameObject cabObj = new GameObject("Cockpit", typeof(RectTransform), typeof(Image));
            cabObj.transform.SetParent(chassisObj.transform, false);
            RectTransform cabRt = cabObj.GetComponent<RectTransform>();
            cabRt.anchoredPosition = new Vector2(6f, 16f);
            cabRt.sizeDelta = new Vector2(36f, 16f);
            Image cabImg = cabObj.GetComponent<Image>();
            cabImg.color = new Color(0.3f, 0.85f, 1f, 0.85f);
            cabImg.raycastTarget = false;

            // Заднее колесо
            GameObject rearWhObj = new GameObject("RearWheel", typeof(RectTransform), typeof(Image));
            rearWhObj.transform.SetParent(carRootObj.transform, false);
            RectTransform rwRt = rearWhObj.GetComponent<RectTransform>();
            rwRt.anchoredPosition = new Vector2(-30f, 0f);
            rwRt.sizeDelta = new Vector2(32f, 32f);
            Image rwImg = rearWhObj.GetComponent<Image>();
            rwImg.sprite = valveSpr;
            rwImg.color = new Color(0.2f, 0.25f, 0.3f, 1f);
            rwImg.raycastTarget = false;

            // Переднее колесо
            GameObject frontWhObj = new GameObject("FrontWheel", typeof(RectTransform), typeof(Image));
            frontWhObj.transform.SetParent(carRootObj.transform, false);
            RectTransform fwRt = frontWhObj.GetComponent<RectTransform>();
            fwRt.anchoredPosition = new Vector2(30f, 0f);
            fwRt.sizeDelta = new Vector2(32f, 32f);
            Image fwImg = frontWhObj.GetComponent<Image>();
            fwImg.sprite = valveSpr;
            fwImg.color = new Color(0.2f, 0.25f, 0.3f, 1f);
            fwImg.raycastTarget = false;

            // Кнопки Тормоз и Газ (по углам экрана внизу, не перекрывают холмы и машину!)
            GameObject btnBrakeObj = CreateTapButton(root.transform, "BtnBrake", "ТОРМОЗ (A)", Vector2.zero, new Vector2(140f, 44f), null);
            RectTransform bbRt = btnBrakeObj.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0f, 0f);
            bbRt.anchorMax = new Vector2(0f, 0f);
            bbRt.pivot = new Vector2(0f, 0f);
            bbRt.anchoredPosition = new Vector2(28f, 38f);
            Button btnBrake = btnBrakeObj.GetComponent<Button>();

            GameObject btnGasObj = CreateTapButton(root.transform, "BtnGas", "ГАЗ (D)", Vector2.zero, new Vector2(140f, 44f), null);
            RectTransform bgRt = btnGasObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(1f, 0f);
            bgRt.anchorMax = new Vector2(1f, 0f);
            bgRt.pivot = new Vector2(1f, 0f);
            bgRt.anchoredPosition = new Vector2(-28f, 38f);
            Button btnGas = btnGasObj.GetComponent<Button>();
            Image bgImg = btnGasObj.GetComponent<Image>();
            bgImg.color = new Color(0.15f, 0.75f, 0.35f, 1f);

            // Прогресс бар
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0f);
            bRt.anchorMax = new Vector2(0.5f, 0f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = new Vector2(0f, 42f);
            bRt.sizeDelta = new Vector2(260f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0f, 0.85f, 1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0f;

            M27_PhysicsCarHillsMechanic mechanic = root.AddComponent<M27_PhysicsCarHillsMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M27_PhysicsCarHills";
            so.FindProperty("_title").stringValue = "Механика #27: Машинка с холмиками с физикой (Physics Car on Hills)";
            so.FindProperty("_instruction").stringValue = "Преодолейте холмистую трассу! Зажимайте ГАЗ / ТОРМОЗ (или клавиши A/D / стрелки).";
            so.FindProperty("_carRoot").objectReferenceValue = crRt;
            so.FindProperty("_chassis").objectReferenceValue = chRt;
            so.FindProperty("_rearWheel").objectReferenceValue = rwRt;
            so.FindProperty("_frontWheel").objectReferenceValue = fwRt;
            so.FindProperty("_finishFlag").objectReferenceValue = fRt;
            so.FindProperty("_terrainContainer").objectReferenceValue = trRt;
            so.FindProperty("_btnGas").objectReferenceValue = btnGas;
            so.FindProperty("_btnBrake").objectReferenceValue = btnBrake;
            so.FindProperty("_speedometerText").objectReferenceValue = spTmp;
            so.FindProperty("_distanceText").objectReferenceValue = dtTmp;
            so.FindProperty("_progressBarFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_startX").floatValue = -200f;
            so.FindProperty("_finishX").floatValue = 2200f;
            so.FindProperty("_suspensionSpring").floatValue = 380f;
            so.FindProperty("_suspensionDamping").floatValue = 22f;
            so.FindProperty("_airTorque").floatValue = 420f;
            so.FindProperty("_engineForce").floatValue = 480f;
            so.FindProperty("_brakeForce").floatValue = 420f;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M27_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M28: Dual Bridge Route
        public static GameObject BuildM28DualBridgeRoutePrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M28_DualBridgeRoute", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite botSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Avatar.png");
            Sprite goalSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Grid_Goal.png");
            Sprite woodSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Wood_Board.png");

            // Инструкция внутри игровой зоны (аккуратный баннер, не перекрывающий верхнее меню)
            GameObject instrObj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instrObj.transform.SetParent(root.transform, false);
            RectTransform instrRt = instrObj.GetComponent<RectTransform>();
            instrRt.anchorMin = new Vector2(0.5f, 0.5f);
            instrRt.anchorMax = new Vector2(0.5f, 0.5f);
            instrRt.pivot = new Vector2(0.5f, 0.5f);
            instrRt.anchoredPosition = new Vector2(0f, 126f);
            instrRt.sizeDelta = new Vector2(650f, 22f);
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();
            instrTmp.text = "Установите оба мостика в разрывы путей (0/2). Остерегайтесь сломанного [X]!";
            instrTmp.fontSize = 13;
            instrTmp.fontStyle = FontStyles.Bold;
            instrTmp.alignment = TextAlignmentOptions.Center;
            instrTmp.color = new Color(0.85f, 0.92f, 1f);
            instrTmp.raycastTarget = false;

            // ==================== ТРАССА A (ВЕРХНЯЯ / СИНЯЯ) ====================
            // Платформа старта A
            GameObject pStartA = new GameObject("PlatformA_Start", typeof(RectTransform), typeof(Image), typeof(Outline));
            pStartA.transform.SetParent(root.transform, false);
            RectTransform psaRt = pStartA.GetComponent<RectTransform>();
            psaRt.anchoredPosition = new Vector2(-230f, 75f);
            psaRt.sizeDelta = new Vector2(140f, 24f);
            pStartA.GetComponent<Image>().color = new Color(0.12f, 0.28f, 0.48f, 1f);
            pStartA.GetComponent<Image>().raycastTarget = false;
            Outline psaOut = pStartA.GetComponent<Outline>();
            psaOut.effectColor = new Color(0.2f, 0.75f, 1f, 0.9f);
            psaOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Слот разрыва A
            GameObject slotAObj = new GameObject("SlotA", typeof(RectTransform), typeof(Image), typeof(Outline));
            slotAObj.transform.SetParent(root.transform, false);
            RectTransform slARt = slotAObj.GetComponent<RectTransform>();
            slARt.anchoredPosition = new Vector2(-80f, 75f);
            slARt.sizeDelta = new Vector2(110f, 24f);
            Image slAImg = slotAObj.GetComponent<Image>();
            slAImg.color = new Color(0.06f, 0.14f, 0.24f, 0.85f);
            slAImg.raycastTarget = false;
            Outline slAOut = slotAObj.GetComponent<Outline>();
            slAOut.effectColor = new Color(0f, 0.85f, 1f, 0.95f);
            slAOut.effectDistance = new Vector2(2f, -2f);

            GameObject slALbl = new GameObject("SlotALabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            slALbl.transform.SetParent(slotAObj.transform, false);
            RectTransform slALblRt = slALbl.GetComponent<RectTransform>();
            slALblRt.anchorMin = Vector2.zero; slALblRt.anchorMax = Vector2.one;
            slALblRt.offsetMin = Vector2.zero; slALblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI slATmp = slALbl.GetComponent<TextMeshProUGUI>();
            slATmp.text = "[ СЛОТ МОСТА A ]";
            slATmp.fontSize = 11;
            slATmp.fontStyle = FontStyles.Bold;
            slATmp.alignment = TextAlignmentOptions.Center;
            slATmp.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            slATmp.raycastTarget = false;

            // Платформа финиша A
            GameObject pEndA = new GameObject("PlatformA_End", typeof(RectTransform), typeof(Image), typeof(Outline));
            pEndA.transform.SetParent(root.transform, false);
            RectTransform peaRt = pEndA.GetComponent<RectTransform>();
            peaRt.anchoredPosition = new Vector2(120f, 75f);
            peaRt.sizeDelta = new Vector2(240f, 24f);
            pEndA.GetComponent<Image>().color = new Color(0.12f, 0.28f, 0.48f, 1f);
            pEndA.GetComponent<Image>().raycastTarget = false;
            Outline peaOut = pEndA.GetComponent<Outline>();
            peaOut.effectColor = new Color(0.2f, 0.75f, 1f, 0.9f);
            peaOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Бот A (Синий)
            GameObject botAObj = new GameObject("EntityA", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
            botAObj.transform.SetParent(root.transform, false);
            RectTransform baRt = botAObj.GetComponent<RectTransform>();
            baRt.anchoredPosition = new Vector2(-230f, 102f);
            baRt.sizeDelta = new Vector2(46f, 46f);
            Image baImg = botAObj.GetComponent<Image>();
            if (botSpr != null) baImg.sprite = botSpr;
            baImg.color = new Color(0.2f, 0.85f, 1f, 1f);
            baImg.raycastTarget = false;
            Outline baOut = botAObj.GetComponent<Outline>();
            baOut.effectColor = new Color(0.25f, 1f, 1f, 0.95f);
            baOut.effectDistance = new Vector2(2f, -2f);

            // Цель A (Флаг A)
            GameObject goalAObj = new GameObject("GoalA", typeof(RectTransform), typeof(Image));
            goalAObj.transform.SetParent(root.transform, false);
            RectTransform gaRt = goalAObj.GetComponent<RectTransform>();
            gaRt.anchoredPosition = new Vector2(200f, 102f);
            gaRt.sizeDelta = new Vector2(44f, 44f);
            Image gaImg = goalAObj.GetComponent<Image>();
            if (goalSpr != null) gaImg.sprite = goalSpr;
            gaImg.color = new Color(0.2f, 0.85f, 1f, 1f);
            gaImg.raycastTarget = false;

            // ==================== ТРАССА B (НИЖНЯЯ / ОРАНЖЕВАЯ) ====================
            // Платформа старта B
            GameObject pStartB = new GameObject("PlatformB_Start", typeof(RectTransform), typeof(Image), typeof(Outline));
            pStartB.transform.SetParent(root.transform, false);
            RectTransform psbRt = pStartB.GetComponent<RectTransform>();
            psbRt.anchoredPosition = new Vector2(-230f, 20f);
            psbRt.sizeDelta = new Vector2(140f, 24f);
            pStartB.GetComponent<Image>().color = new Color(0.48f, 0.25f, 0.08f, 1f);
            pStartB.GetComponent<Image>().raycastTarget = false;
            Outline psbOut = pStartB.GetComponent<Outline>();
            psbOut.effectColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            psbOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Слот разрыва B
            GameObject slotBObj = new GameObject("SlotB", typeof(RectTransform), typeof(Image), typeof(Outline));
            slotBObj.transform.SetParent(root.transform, false);
            RectTransform slBRt = slotBObj.GetComponent<RectTransform>();
            slBRt.anchoredPosition = new Vector2(0f, 20f);
            slBRt.sizeDelta = new Vector2(110f, 24f);
            Image slBImg = slotBObj.GetComponent<Image>();
            slBImg.color = new Color(0.24f, 0.12f, 0.04f, 0.85f);
            slBImg.raycastTarget = false;
            Outline slBOut = slotBObj.GetComponent<Outline>();
            slBOut.effectColor = new Color(1f, 0.65f, 0.2f, 0.95f);
            slBOut.effectDistance = new Vector2(2f, -2f);

            GameObject slBLbl = new GameObject("SlotBLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            slBLbl.transform.SetParent(slotBObj.transform, false);
            RectTransform slBLblRt = slBLbl.GetComponent<RectTransform>();
            slBLblRt.anchorMin = Vector2.zero; slBLblRt.anchorMax = Vector2.one;
            slBLblRt.offsetMin = Vector2.zero; slBLblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI slBTmp = slBLbl.GetComponent<TextMeshProUGUI>();
            slBTmp.text = "[ СЛОТ МОСТА B ]";
            slBTmp.fontSize = 11;
            slBTmp.fontStyle = FontStyles.Bold;
            slBTmp.alignment = TextAlignmentOptions.Center;
            slBTmp.color = new Color(1f, 0.7f, 0.3f, 0.9f);
            slBTmp.raycastTarget = false;

            // Платформа финиша B
            GameObject pEndB = new GameObject("PlatformB_End", typeof(RectTransform), typeof(Image), typeof(Outline));
            pEndB.transform.SetParent(root.transform, false);
            RectTransform pebRt = pEndB.GetComponent<RectTransform>();
            pebRt.anchoredPosition = new Vector2(160f, 20f);
            pebRt.sizeDelta = new Vector2(160f, 24f);
            pEndB.GetComponent<Image>().color = new Color(0.48f, 0.25f, 0.08f, 1f);
            pEndB.GetComponent<Image>().raycastTarget = false;
            Outline pebOut = pEndB.GetComponent<Outline>();
            pebOut.effectColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            pebOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Бот B (Оранжевый)
            GameObject botBObj = new GameObject("EntityB", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
            botBObj.transform.SetParent(root.transform, false);
            RectTransform bbRt = botBObj.GetComponent<RectTransform>();
            bbRt.anchoredPosition = new Vector2(-230f, 48f);
            bbRt.sizeDelta = new Vector2(46f, 46f);
            Image bbImg = botBObj.GetComponent<Image>();
            if (botSpr != null) bbImg.sprite = botSpr;
            bbImg.color = new Color(1f, 0.65f, 0.2f, 1f);
            bbImg.raycastTarget = false;
            Outline bbOut = botBObj.GetComponent<Outline>();
            bbOut.effectColor = new Color(1f, 0.75f, 0.3f, 0.95f);
            bbOut.effectDistance = new Vector2(2f, -2f);

            // Цель B (Флаг B)
            GameObject goalBObj = new GameObject("GoalB", typeof(RectTransform), typeof(Image));
            goalBObj.transform.SetParent(root.transform, false);
            RectTransform gbRt = goalBObj.GetComponent<RectTransform>();
            gbRt.anchoredPosition = new Vector2(200f, 48f);
            gbRt.sizeDelta = new Vector2(44f, 44f);
            Image gbImg = goalBObj.GetComponent<Image>();
            if (goalSpr != null) gbImg.sprite = goalSpr;
            gbImg.color = new Color(1f, 0.65f, 0.2f, 1f);
            gbImg.raycastTarget = false;

            // ==================== СКЛАД МОСТИКОВ (ИНВЕНТАРЬ) ====================
            GameObject toolboxObj = new GameObject("BridgeToolbox", typeof(RectTransform), typeof(Image), typeof(Outline));
            toolboxObj.transform.SetParent(root.transform, false);
            RectTransform tbRt = toolboxObj.GetComponent<RectTransform>();
            tbRt.anchoredPosition = new Vector2(0f, -80f);
            tbRt.sizeDelta = new Vector2(530f, 56f);
            Image tbImg = toolboxObj.GetComponent<Image>();
            tbImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            tbImg.raycastTarget = false;
            Outline tbOut = toolboxObj.GetComponent<Outline>();
            tbOut.effectColor = new Color(0.2f, 0.35f, 0.5f, 0.8f);
            tbOut.effectDistance = new Vector2(2f, -2f);

            GameObject tbHeaderObj = new GameObject("ToolboxHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            tbHeaderObj.transform.SetParent(toolboxObj.transform, false);
            RectTransform tbhRt = tbHeaderObj.GetComponent<RectTransform>();
            tbhRt.anchoredPosition = new Vector2(0f, 16f);
            tbhRt.sizeDelta = new Vector2(500f, 16f);
            TextMeshProUGUI tbhTmp = tbHeaderObj.GetComponent<TextMeshProUGUI>();
            tbhTmp.text = "СКЛАД МОСТИКОВ (ПЕРЕТЯНИТЕ ИСПРАВНЫЕ В СЛОТЫ A И B)";
            tbhTmp.fontSize = 11;
            tbhTmp.fontStyle = FontStyles.Bold;
            tbhTmp.alignment = TextAlignmentOptions.Center;
            tbhTmp.color = new Color(0.55f, 0.7f, 0.85f, 0.9f);
            tbhTmp.raycastTarget = false;

            // 3 слота инвентаря для идеального позиционирования мостиков
            GameObject slot1Obj = CreateInventorySlot(toolboxObj.transform, "Slot_Inv_1", new Vector2(-160f, -8f), "[ СЛОТ 1 ]", new Color(0.2f, 0.5f, 0.8f, 0.4f));
            GameObject slot2Obj = CreateInventorySlot(toolboxObj.transform, "Slot_Inv_2", new Vector2(0f, -8f), "[ СЛОТ 2 ]", new Color(0.2f, 0.5f, 0.8f, 0.4f));
            GameObject slot3Obj = CreateInventorySlot(toolboxObj.transform, "Slot_Inv_3", new Vector2(160f, -8f), "[ [X] СЛОМАННЫЙ ]", new Color(0.8f, 0.2f, 0.2f, 0.4f));

            // Мостик 1 (исправный)
            GameObject pl1Obj = new GameObject("Plank_1", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow), typeof(BridgePlank));
            pl1Obj.transform.SetParent(slot1Obj.transform, false);
            RectTransform pl1Rt = pl1Obj.GetComponent<RectTransform>();
            pl1Rt.anchoredPosition = Vector2.zero;
            pl1Rt.sizeDelta = new Vector2(110f, 26f);
            Image pl1Img = pl1Obj.GetComponent<Image>();
            pl1Img.color = new Color(0.72f, 0.44f, 0.2f, 1f);
            pl1Img.raycastTarget = true;
            Outline pl1Out = pl1Obj.GetComponent<Outline>();
            pl1Out.effectColor = new Color(0.95f, 0.75f, 0.35f, 1f);
            pl1Out.effectDistance = new Vector2(2f, -2f);
            BridgePlank pl1 = pl1Obj.GetComponent<BridgePlank>();
            pl1.SetBridgeId(1);
            pl1.SetBroken(false);
            pl1.SetInitialOrigin(slot1Obj.transform, Vector2.zero);

            SerializedObject soPl1 = new SerializedObject(pl1);
            soPl1.FindProperty("_initialParent").objectReferenceValue = slot1Obj.transform;
            soPl1.FindProperty("_initialPosition").vector2Value = Vector2.zero;
            soPl1.ApplyModifiedPropertiesWithoutUndo();

            // Мостик 2 (исправный)
            GameObject pl2Obj = new GameObject("Plank_2", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow), typeof(BridgePlank));
            pl2Obj.transform.SetParent(slot2Obj.transform, false);
            RectTransform pl2Rt = pl2Obj.GetComponent<RectTransform>();
            pl2Rt.anchoredPosition = Vector2.zero;
            pl2Rt.sizeDelta = new Vector2(110f, 26f);
            Image pl2Img = pl2Obj.GetComponent<Image>();
            pl2Img.color = new Color(0.72f, 0.44f, 0.2f, 1f);
            pl2Img.raycastTarget = true;
            Outline pl2Out = pl2Obj.GetComponent<Outline>();
            pl2Out.effectColor = new Color(0.95f, 0.75f, 0.35f, 1f);
            pl2Out.effectDistance = new Vector2(2f, -2f);
            BridgePlank pl2 = pl2Obj.GetComponent<BridgePlank>();
            pl2.SetBridgeId(2);
            pl2.SetBroken(false);
            pl2.SetInitialOrigin(slot2Obj.transform, Vector2.zero);

            SerializedObject soPl2 = new SerializedObject(pl2);
            soPl2.FindProperty("_initialParent").objectReferenceValue = slot2Obj.transform;
            soPl2.FindProperty("_initialPosition").vector2Value = Vector2.zero;
            soPl2.ApplyModifiedPropertiesWithoutUndo();

            // Мостик 3 (сломанный [X])
            GameObject pl3Obj = new GameObject("Plank_Broken_Junk", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow), typeof(BridgePlank));
            pl3Obj.transform.SetParent(slot3Obj.transform, false);
            RectTransform pl3Rt = pl3Obj.GetComponent<RectTransform>();
            pl3Rt.anchoredPosition = Vector2.zero;
            pl3Rt.sizeDelta = new Vector2(110f, 26f);
            Image pl3Img = pl3Obj.GetComponent<Image>();
            pl3Img.color = new Color(0.65f, 0.18f, 0.18f, 1f);
            pl3Img.raycastTarget = true;
            Outline pl3Out = pl3Obj.GetComponent<Outline>();
            pl3Out.effectColor = new Color(1f, 0.35f, 0.35f, 1f);
            pl3Out.effectDistance = new Vector2(2f, -2f);
            BridgePlank pl3 = pl3Obj.GetComponent<BridgePlank>();
            pl3.SetBridgeId(3);
            pl3.SetBroken(true);
            pl3.SetInitialOrigin(slot3Obj.transform, Vector2.zero);

            SerializedObject soPl3 = new SerializedObject(pl3);
            soPl3.FindProperty("_initialParent").objectReferenceValue = slot3Obj.transform;
            soPl3.FindProperty("_initialPosition").vector2Value = Vector2.zero;
            soPl3.ApplyModifiedPropertiesWithoutUndo();

            // Кнопка «ПУСК >>»
            GameObject btnStartObj = CreateTapButton(root.transform, "BtnStart", "ПУСК >>", new Vector2(0f, -32f), new Vector2(160f, 34f), null);
            Button btnStart = btnStartObj.GetComponent<Button>();
            btnStart.interactable = false;

            M28_DualBridgeRouteMechanic mechanic = root.AddComponent<M28_DualBridgeRouteMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M28_DualBridgeRoute";
            so.FindProperty("_title").stringValue = "Механика #28: Построение маршрута с мостиками (Dual Bridge Route)";
            so.FindProperty("_instruction").stringValue = "Перетащите исправные мостики в провалы путей и нажмите 'ПУСК'. Остерегайтесь сломанного мостика [X]!";
            so.FindProperty("_entityA").objectReferenceValue = baRt;
            so.FindProperty("_entityB").objectReferenceValue = bbRt;
            so.FindProperty("_goalA").objectReferenceValue = gaRt;
            so.FindProperty("_goalB").objectReferenceValue = gbRt;
            so.FindProperty("_slotA").objectReferenceValue = slARt;
            so.FindProperty("_slotB").objectReferenceValue = slBRt;
            so.FindProperty("_snapRadius").floatValue = 75f;
            so.FindProperty("_btnStart").objectReferenceValue = btnStart;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_progressFill").objectReferenceValue = pStartA.GetComponent<Image>();

            SerializedProperty planksProp = so.FindProperty("_planks");
            planksProp.arraySize = 3;
            planksProp.GetArrayElementAtIndex(0).objectReferenceValue = pl1;
            planksProp.GetArrayElementAtIndex(1).objectReferenceValue = pl2;
            planksProp.GetArrayElementAtIndex(2).objectReferenceValue = pl3;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M28_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M29: Contour Cutting
        public static GameObject BuildM29ContourCuttingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M29_ContourCutting", typeof(RectTransform), typeof(Image));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Image rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.01f); // для ловли Drag/Pointer событий

            Sprite sawSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Tool_CircularSaw.png");
            Sprite dotSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Circle.png");
            Sprite squareSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Square.png");

            GameObject instrObj = CreateInstruction(root.transform, "Ведите ножницами вдоль пунктирного контура звезды. Не выходите за пределы допуска!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Контейнер контура
            GameObject contourObj = new GameObject("ContourContainer", typeof(RectTransform));
            contourObj.transform.SetParent(root.transform, false);
            RectTransform cRt = contourObj.GetComponent<RectTransform>();
            cRt.anchoredPosition = new Vector2(0f, 12f);
            cRt.sizeDelta = new Vector2(340f, 340f);

            // Центральная вырезаемая звезда (формируется двумя наложенными квадратами 0 и 45 градусов)
            GameObject shapeObj = new GameObject("CutShapeVisual", typeof(RectTransform));
            shapeObj.transform.SetParent(contourObj.transform, false);
            RectTransform sRt = shapeObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = Vector2.zero;
            sRt.sizeDelta = new Vector2(200f, 200f);

            GameObject starPart1 = new GameObject("StarPart_1", typeof(RectTransform), typeof(Image));
            starPart1.transform.SetParent(shapeObj.transform, false);
            RectTransform sp1Rt = starPart1.GetComponent<RectTransform>();
            sp1Rt.anchoredPosition = Vector2.zero;
            sp1Rt.sizeDelta = new Vector2(130f, 130f);
            sp1Rt.localRotation = Quaternion.identity;
            Image sp1Img = starPart1.GetComponent<Image>();
            sp1Img.sprite = squareSpr;
            sp1Img.color = new Color(0.14f, 0.28f, 0.44f, 0.45f);
            sp1Img.raycastTarget = false;

            GameObject starPart2 = new GameObject("StarPart_2", typeof(RectTransform), typeof(Image));
            starPart2.transform.SetParent(shapeObj.transform, false);
            RectTransform sp2Rt = starPart2.GetComponent<RectTransform>();
            sp2Rt.anchoredPosition = Vector2.zero;
            sp2Rt.sizeDelta = new Vector2(130f, 130f);
            sp2Rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image sp2Img = starPart2.GetComponent<Image>();
            sp2Img.sprite = squareSpr;
            sp2Img.color = new Color(0.14f, 0.28f, 0.44f, 0.45f);
            sp2Img.raycastTarget = false;

            M29_ContourCuttingMechanic mechanic = root.AddComponent<M29_ContourCuttingMechanic>();

            // 16 вершин 8-конечной звезды
            int pointsCount = 16;
            float outerR = 120f;
            float innerR = 70f;
            Vector2[] pts = new Vector2[pointsCount];
            for (int i = 0; i < pointsCount; i++)
            {
                float angle = (i / (float)pointsCount) * Mathf.PI * 2f;
                float r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }

            // 1. Линии сегментов контура (16 отрезков звезды)
            for (int i = 0; i < pointsCount; i++)
            {
                Vector2 pA = pts[i];
                Vector2 pB = pts[(i + 1) % pointsCount];
                Vector2 mid = (pA + pB) * 0.5f;
                Vector2 diff = pB - pA;
                float dist = diff.magnitude;
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                GameObject segObj = new GameObject($"Segment_{i}", typeof(RectTransform), typeof(Image));
                segObj.transform.SetParent(contourObj.transform, false);
                RectTransform segRt = segObj.GetComponent<RectTransform>();
                segRt.anchoredPosition = mid;
                segRt.sizeDelta = new Vector2(dist, 4f);
                segRt.localRotation = Quaternion.Euler(0f, 0f, angle);

                Image segImg = segObj.GetComponent<Image>();
                segImg.color = new Color(0.35f, 0.65f, 0.95f, 0.45f);
                segImg.raycastTarget = false;

                mechanic.RegisterSegmentVisual(segImg);
            }

            // 2. Вершины звезды (16 точек)
            for (int i = 0; i < pointsCount; i++)
            {
                GameObject dotObj = new GameObject($"Point_{i}", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(contourObj.transform, false);
                RectTransform dRt = dotObj.GetComponent<RectTransform>();
                dRt.anchoredPosition = pts[i];
                dRt.sizeDelta = new Vector2(14f, 14f);
                Image dImg = dotObj.GetComponent<Image>();
                dImg.sprite = dotSpr;
                dImg.color = new Color(0.5f, 0.8f, 1f, 0.7f);
                dImg.raycastTarget = false;

                mechanic.RegisterVertexVisual(dImg);
            }

            // 3. Табличка "СТАРТ ▶"
            GameObject startBadge = new GameObject("StartBadge", typeof(RectTransform), typeof(Image));
            startBadge.transform.SetParent(contourObj.transform, false);
            RectTransform sbRt = startBadge.GetComponent<RectTransform>();
            sbRt.anchoredPosition = pts[0] + new Vector2(50f, 0f);
            sbRt.sizeDelta = new Vector2(80f, 26f);
            Image sbImg = startBadge.GetComponent<Image>();
            sbImg.color = new Color(0.08f, 0.72f, 0.4f, 0.95f);
            sbImg.raycastTarget = false;

            GameObject sbTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            sbTxtObj.transform.SetParent(startBadge.transform, false);
            RectTransform sbtRt = sbTxtObj.GetComponent<RectTransform>();
            sbtRt.anchorMin = Vector2.zero;
            sbtRt.anchorMax = Vector2.one;
            sbtRt.offsetMin = Vector2.zero;
            sbtRt.offsetMax = Vector2.zero;
            TextMeshProUGUI sbt = sbTxtObj.GetComponent<TextMeshProUGUI>();
            sbt.text = "СТАРТ ▶";
            sbt.fontSize = 12;
            sbt.fontStyle = FontStyles.Bold;
            sbt.alignment = TextAlignmentOptions.Center;
            sbt.color = Color.white;
            sbt.raycastTarget = false;

            // Инструмент реза (пила/ножницы)
            GameObject toolObj = new GameObject("ScissorTool", typeof(RectTransform), typeof(Image));
            toolObj.transform.SetParent(contourObj.transform, false);
            RectTransform tRt = toolObj.GetComponent<RectTransform>();
            tRt.anchoredPosition = pts[0];
            tRt.sizeDelta = new Vector2(44f, 44f);
            Image tImg = toolObj.GetComponent<Image>();
            tImg.sprite = sawSpr;
            tImg.color = new Color(1f, 0.85f, 0.2f, 1f);
            tImg.raycastTarget = false;

            // Прогресс Текст
            GameObject progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progObj.transform.SetParent(root.transform, false);
            RectTransform pRt = progObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, 96f);
            pRt.sizeDelta = new Vector2(250f, 26f);
            TextMeshProUGUI pTmp = progObj.GetComponent<TextMeshProUGUI>();
            pTmp.text = "Вырезано: 0%";
            pTmp.fontSize = 18;
            pTmp.alignment = TextAlignmentOptions.Center;
            pTmp.color = Color.white;

            // Прогресс Бар
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -112f);
            bRt.sizeDelta = new Vector2(260f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0f, 0.9f, 0.6f, 1f);
            bfImg.fillAmount = 0f;

            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M29_ContourCutting";
            so.FindProperty("_title").stringValue = "Механика #29: Вырезание по контуру (Contour Scissor Cutting)";
            so.FindProperty("_instruction").stringValue = "Ведите ножницами вдоль пунктирного контура звезды. Не выходите за пределы допуска!";
            so.FindProperty("_scissorTool").objectReferenceValue = tRt;
            so.FindProperty("_contourContainer").objectReferenceValue = cRt;
            so.FindProperty("_cutShapeVisual").objectReferenceValue = sRt;
            so.FindProperty("_startBadge").objectReferenceValue = startBadge;
            so.FindProperty("_progressText").objectReferenceValue = pTmp;
            so.FindProperty("_progressFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_toleranceRadius").floatValue = 65f;
            so.FindProperty("_stepAdvanceThreshold").floatValue = 24f;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M29_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M30: Character Dress Up
        public static GameObject BuildM30CharacterDressUpPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M30_CharacterDressUp", typeof(RectTransform));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            Sprite headSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Head.png");
            Sprite armorSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Armor.png");
            Sprite bootSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Fishing_Junk_Boot.png");
            Sprite coreSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Core.png");
            Sprite junkSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_BrokenPart_Junk.png");
            Sprite chassisSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Robot_Chassis.png");

            GameObject instrObj = CreateInstruction(root.transform, "Перетащите элементы экипировки на силуэт робота. Бракованный хлам [X] не подходит!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Силуэт персонажа слева
            GameObject mannequinObj = new GameObject("CharacterSilhouette", typeof(RectTransform), typeof(Image));
            mannequinObj.transform.SetParent(root.transform, false);
            RectTransform mqRt = mannequinObj.GetComponent<RectTransform>();
            mqRt.anchoredPosition = new Vector2(-110f, -5f);
            mqRt.sizeDelta = new Vector2(160f, 220f);
            Image mqImg = mannequinObj.GetComponent<Image>();
            mqImg.sprite = chassisSpr;
            mqImg.color = new Color(0.2f, 0.26f, 0.35f, 0.85f);
            mqImg.raycastTarget = false;

            // Слоты на силуэте
            GameObject slotHeadObj = new GameObject("Slot_Head", typeof(RectTransform), typeof(Image));
            slotHeadObj.transform.SetParent(mannequinObj.transform, false);
            RectTransform shRt = slotHeadObj.GetComponent<RectTransform>();
            shRt.anchoredPosition = new Vector2(0f, 65f);
            shRt.sizeDelta = new Vector2(52f, 52f);
            Image shImg = slotHeadObj.GetComponent<Image>();
            shImg.color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
            shImg.raycastTarget = false;

            GameObject slotBodyObj = new GameObject("Slot_Body", typeof(RectTransform), typeof(Image));
            slotBodyObj.transform.SetParent(mannequinObj.transform, false);
            RectTransform sbRt = slotBodyObj.GetComponent<RectTransform>();
            sbRt.anchoredPosition = new Vector2(0f, 10f);
            sbRt.sizeDelta = new Vector2(65f, 65f);
            Image sbImg = slotBodyObj.GetComponent<Image>();
            sbImg.color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
            sbImg.raycastTarget = false;

            GameObject slotFeetObj = new GameObject("Slot_Feet", typeof(RectTransform), typeof(Image));
            slotFeetObj.transform.SetParent(mannequinObj.transform, false);
            RectTransform sfRt = slotFeetObj.GetComponent<RectTransform>();
            sfRt.anchoredPosition = new Vector2(0f, -65f);
            sfRt.sizeDelta = new Vector2(55f, 45f);
            Image sfImg = slotFeetObj.GetComponent<Image>();
            sfImg.color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
            sfImg.raycastTarget = false;

            GameObject slotAccObj = new GameObject("Slot_Accessory", typeof(RectTransform), typeof(Image));
            slotAccObj.transform.SetParent(mannequinObj.transform, false);
            RectTransform saRt = slotAccObj.GetComponent<RectTransform>();
            saRt.anchoredPosition = new Vector2(60f, 10f);
            saRt.sizeDelta = new Vector2(45f, 45f);
            Image saImg = slotAccObj.GetComponent<Image>();
            saImg.color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
            saImg.raycastTarget = false;

            // Панель гардероба справа
            GameObject wardrobeObj = new GameObject("WardrobePanel", typeof(RectTransform), typeof(Image));
            wardrobeObj.transform.SetParent(root.transform, false);
            RectTransform wdRt = wardrobeObj.GetComponent<RectTransform>();
            wdRt.anchoredPosition = new Vector2(130f, -5f);
            wdRt.sizeDelta = new Vector2(210f, 220f);
            Image wdImg = wardrobeObj.GetComponent<Image>();
            wdImg.color = new Color(0.12f, 0.15f, 0.2f, 0.95f);

            // Предмет 1: Шлем (Head)
            GameObject itemHeadObj = new GameObject("Item_Head", typeof(RectTransform), typeof(Image), typeof(DressUpItem));
            itemHeadObj.transform.SetParent(root.transform, false);
            RectTransform ihRt = itemHeadObj.GetComponent<RectTransform>();
            ihRt.anchoredPosition = new Vector2(75f, 40f);
            ihRt.sizeDelta = new Vector2(48f, 48f);
            itemHeadObj.GetComponent<Image>().sprite = headSpr;
            DressUpItem itemHead = itemHeadObj.GetComponent<DressUpItem>();
            SerializedObject ihSo = new SerializedObject(itemHead);
            ihSo.FindProperty("_slotType").enumValueIndex = (int)DressSlotType.Head;
            ihSo.ApplyModifiedPropertiesWithoutUndo();

            // Предмет 2: Броня (Body)
            GameObject itemBodyObj = new GameObject("Item_Body", typeof(RectTransform), typeof(Image), typeof(DressUpItem));
            itemBodyObj.transform.SetParent(root.transform, false);
            RectTransform ibRt = itemBodyObj.GetComponent<RectTransform>();
            ibRt.anchoredPosition = new Vector2(185f, 40f);
            ibRt.sizeDelta = new Vector2(48f, 48f);
            itemBodyObj.GetComponent<Image>().sprite = armorSpr;
            DressUpItem itemBody = itemBodyObj.GetComponent<DressUpItem>();
            SerializedObject ibSo = new SerializedObject(itemBody);
            ibSo.FindProperty("_slotType").enumValueIndex = (int)DressSlotType.Body;
            ibSo.ApplyModifiedPropertiesWithoutUndo();

            // Предмет 3: Ботинки (Feet)
            GameObject itemFeetObj = new GameObject("Item_Feet", typeof(RectTransform), typeof(Image), typeof(DressUpItem));
            itemFeetObj.transform.SetParent(root.transform, false);
            RectTransform ifRt = itemFeetObj.GetComponent<RectTransform>();
            ifRt.anchoredPosition = new Vector2(85f, -28f);
            ifRt.sizeDelta = new Vector2(45f, 45f);
            itemFeetObj.GetComponent<Image>().sprite = bootSpr;
            DressUpItem itemFeet = itemFeetObj.GetComponent<DressUpItem>();
            SerializedObject ifSo = new SerializedObject(itemFeet);
            ifSo.FindProperty("_slotType").enumValueIndex = (int)DressSlotType.Feet;
            ifSo.ApplyModifiedPropertiesWithoutUndo();

            // Предмет 4: Реактор/Батарея (Accessory)
            GameObject itemAccObj = new GameObject("Item_Accessory", typeof(RectTransform), typeof(Image), typeof(DressUpItem));
            itemAccObj.transform.SetParent(root.transform, false);
            RectTransform iaRt = itemAccObj.GetComponent<RectTransform>();
            iaRt.anchoredPosition = new Vector2(175f, -28f);
            iaRt.sizeDelta = new Vector2(45f, 45f);
            itemAccObj.GetComponent<Image>().sprite = coreSpr;
            DressUpItem itemAcc = itemAccObj.GetComponent<DressUpItem>();
            SerializedObject iaSo = new SerializedObject(itemAcc);
            iaSo.FindProperty("_slotType").enumValueIndex = (int)DressSlotType.Accessory;
            iaSo.ApplyModifiedPropertiesWithoutUndo();

            // Предмет 5: Брак/Хлам [X] (Junk)
            GameObject itemJunkObj = new GameObject("Item_Junk", typeof(RectTransform), typeof(Image), typeof(DressUpItem));
            itemJunkObj.transform.SetParent(root.transform, false);
            RectTransform ijRt = itemJunkObj.GetComponent<RectTransform>();
            ijRt.anchoredPosition = new Vector2(130f, -80f);
            ijRt.sizeDelta = new Vector2(45f, 45f);
            Image ijImg = itemJunkObj.GetComponent<Image>();
            ijImg.sprite = junkSpr;
            ijImg.color = new Color(0.95f, 0.4f, 0.4f, 1f);
            DressUpItem itemJunk = itemJunkObj.GetComponent<DressUpItem>();
            SerializedObject ijSo = new SerializedObject(itemJunk);
            ijSo.FindProperty("_slotType").enumValueIndex = (int)DressSlotType.Junk;
            ijSo.FindProperty("_isJunk").boolValue = true;
            ijSo.ApplyModifiedPropertiesWithoutUndo();

            // Прогресс
            GameObject progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progObj.transform.SetParent(root.transform, false);
            RectTransform pRt = progObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, 96f);
            pRt.sizeDelta = new Vector2(250f, 26f);
            TextMeshProUGUI pTmp = progObj.GetComponent<TextMeshProUGUI>();
            pTmp.text = "Экипировано: 0 / 4";
            pTmp.fontSize = 18;
            pTmp.alignment = TextAlignmentOptions.Center;
            pTmp.color = Color.white;

            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -85f);
            bRt.sizeDelta = new Vector2(250f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0f, 0.85f, 1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0f;

            M30_CharacterDressUpMechanic mechanic = root.AddComponent<M30_CharacterDressUpMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M30_CharacterDressUp";
            so.FindProperty("_title").stringValue = "Механика #30: Одевание персонажа (Character Dress-Up)";
            so.FindProperty("_instruction").stringValue = "Перетащите элементы экипировки на силуэт робота. Бракованный хлам [X] не подходит!";
            so.FindProperty("_characterSilhouette").objectReferenceValue = mqRt;
            so.FindProperty("_slotHead").objectReferenceValue = shRt;
            so.FindProperty("_slotBody").objectReferenceValue = sbRt;
            so.FindProperty("_slotFeet").objectReferenceValue = sfRt;
            so.FindProperty("_slotAccessory").objectReferenceValue = saRt;
            so.FindProperty("_progressText").objectReferenceValue = pTmp;
            so.FindProperty("_progressFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;

            SerializedProperty itemsProp = so.FindProperty("_items");
            itemsProp.arraySize = 5;
            itemsProp.GetArrayElementAtIndex(0).objectReferenceValue = itemHead;
            itemsProp.GetArrayElementAtIndex(1).objectReferenceValue = itemBody;
            itemsProp.GetArrayElementAtIndex(2).objectReferenceValue = itemFeet;
            itemsProp.GetArrayElementAtIndex(3).objectReferenceValue = itemAcc;
            itemsProp.GetArrayElementAtIndex(4).objectReferenceValue = itemJunk;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M30_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region M31: Liquid Filling
        public static GameObject BuildM31LiquidFillingPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M31_LiquidFilling", typeof(RectTransform), typeof(Image));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Image rootImg = root.GetComponent<Image>();
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;

            Sprite pipeSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Pipe_Source.png");
            Sprite nozzleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Nozzle_Extinguisher.png");

            GameObject instrObj = CreateInstruction(root.transform, "Перемещайте раствороподатчик над лунками по очереди (1 → 2 → 3), чтобы заполнить их раствором!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Контейнер 3 лунок
            GameObject wellsContainerObj = new GameObject("WellsContainer", typeof(RectTransform));
            wellsContainerObj.transform.SetParent(root.transform, false);
            RectTransform wcRt = wellsContainerObj.GetComponent<RectTransform>();
            wcRt.anchoredPosition = new Vector2(0f, -25f);
            wcRt.sizeDelta = new Vector2(450f, 150f);

            // Подложка кассеты лунок
            GameObject trayObj = new GameObject("WellsTray", typeof(RectTransform), typeof(Image));
            trayObj.transform.SetParent(wellsContainerObj.transform, false);
            RectTransform trRt = trayObj.GetComponent<RectTransform>();
            trRt.anchorMin = Vector2.zero;
            trRt.anchorMax = Vector2.one;
            trRt.offsetMin = Vector2.zero;
            trRt.offsetMax = Vector2.zero;
            Image trImg = trayObj.GetComponent<Image>();
            trImg.color = new Color(0.12f, 0.15f, 0.2f, 0.95f);
            trImg.raycastTarget = false;

            float[] wellXs = new float[] { -135f, 0f, 135f };
            RectTransform[] wellRoots = new RectTransform[3];
            Image[] liquidFills = new Image[3];
            Image[] glowRings = new Image[3];
            TMP_Text[] percentTexts = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                // Лунка i + 1
                GameObject wellObj = new GameObject($"Well_{i + 1}", typeof(RectTransform));
                wellObj.transform.SetParent(wellsContainerObj.transform, false);
                RectTransform wRt = wellObj.GetComponent<RectTransform>();
                wRt.anchoredPosition = new Vector2(wellXs[i], 0f);
                wRt.sizeDelta = new Vector2(76f, 96f);
                wellRoots[i] = wRt;

                // Фон лунки (колба/углубление)
                GameObject socketObj = new GameObject("SocketBg", typeof(RectTransform), typeof(Image));
                socketObj.transform.SetParent(wellObj.transform, false);
                RectTransform skRt = socketObj.GetComponent<RectTransform>();
                skRt.anchorMin = Vector2.zero;
                skRt.anchorMax = Vector2.one;
                skRt.offsetMin = Vector2.zero;
                skRt.offsetMax = Vector2.zero;
                Image skImg = socketObj.GetComponent<Image>();
                skImg.color = new Color(0.08f, 0.1f, 0.14f, 1f);
                skImg.raycastTarget = false;

                // Наполнение раствором (Image.Type.Filled, Vertical)
                GameObject liquidObj = new GameObject("LiquidFill", typeof(RectTransform), typeof(Image));
                liquidObj.transform.SetParent(wellObj.transform, false);
                RectTransform lqRt = liquidObj.GetComponent<RectTransform>();
                lqRt.anchorMin = Vector2.zero;
                lqRt.anchorMax = Vector2.one;
                lqRt.offsetMin = new Vector2(4f, 4f);
                lqRt.offsetMax = new Vector2(-4f, -4f);
                Image lqImg = liquidObj.GetComponent<Image>();
                lqImg.type = Image.Type.Filled;
                lqImg.fillMethod = Image.FillMethod.Vertical;
                lqImg.fillAmount = 0f;
                lqImg.color = new Color(0f, 0.8f, 1f, 0.9f);
                lqImg.raycastTarget = false;
                liquidFills[i] = lqImg;

                // Контур / кольцо активности
                GameObject ringObj = new GameObject("GlowRing", typeof(RectTransform), typeof(Image), typeof(Outline));
                ringObj.transform.SetParent(wellObj.transform, false);
                RectTransform rngRt = ringObj.GetComponent<RectTransform>();
                rngRt.anchorMin = Vector2.zero;
                rngRt.anchorMax = Vector2.one;
                rngRt.offsetMin = new Vector2(-2f, -2f);
                rngRt.offsetMax = new Vector2(2f, 2f);
                Image rngImg = ringObj.GetComponent<Image>();
                rngImg.color = (i == 0) ? new Color(1f, 0.88f, 0.2f, 0.9f) : new Color(0.3f, 0.5f, 0.7f, 0.25f);
                rngImg.raycastTarget = false;
                Outline outl = ringObj.GetComponent<Outline>();
                outl.effectColor = new Color(0f, 0f, 0f, 0.6f);
                glowRings[i] = rngImg;

                // Подпись лунки сверху
                GameObject lblObj = new GameObject("WellLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(wellObj.transform, false);
                RectTransform lbRt = lblObj.GetComponent<RectTransform>();
                lbRt.anchoredPosition = new Vector2(0f, 56f);
                lbRt.sizeDelta = new Vector2(100f, 20f);
                TextMeshProUGUI lbTmp = lblObj.GetComponent<TextMeshProUGUI>();
                lbTmp.text = $"Лунка {i + 1}";
                lbTmp.fontSize = 12;
                lbTmp.fontStyle = FontStyles.Bold;
                lbTmp.alignment = TextAlignmentOptions.Center;
                lbTmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
                lbTmp.raycastTarget = false;

                // Процент заполнения по центру
                GameObject pctObj = new GameObject("PercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
                pctObj.transform.SetParent(wellObj.transform, false);
                RectTransform pcRt = pctObj.GetComponent<RectTransform>();
                pcRt.anchoredPosition = new Vector2(0f, -28f);
                pcRt.sizeDelta = new Vector2(80f, 22f);
                TextMeshProUGUI pcTmp = pctObj.GetComponent<TextMeshProUGUI>();
                pcTmp.text = "0%";
                pcTmp.fontSize = 13;
                pcTmp.fontStyle = FontStyles.Bold;
                pcTmp.alignment = TextAlignmentOptions.Center;
                pcTmp.color = Color.white;
                pcTmp.raycastTarget = false;
                percentTexts[i] = pcTmp;
            }

            // Раствороподатчик (перемещаемый инструмент)
            GameObject toolObj = new GameObject("DispenserTool", typeof(RectTransform), typeof(Image));
            toolObj.transform.SetParent(root.transform, false);
            RectTransform tRt = toolObj.GetComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(-135f, 70f);
            tRt.sizeDelta = new Vector2(46f, 70f);
            Image tImg = toolObj.GetComponent<Image>();
            tImg.sprite = nozzleSpr != null ? nozzleSpr : pipeSpr;
            tImg.color = new Color(1f, 0.9f, 0.3f, 1f);
            tImg.raycastTarget = false;

            // Носик дозатора внизу
            GameObject nozzleObj = new GameObject("NozzleTip", typeof(RectTransform));
            nozzleObj.transform.SetParent(toolObj.transform, false);
            RectTransform nRt = nozzleObj.GetComponent<RectTransform>();
            nRt.anchoredPosition = new Vector2(0f, -35f);

            // Визуальная струя раствора из носика
            GameObject streamObj = new GameObject("StreamVisual", typeof(RectTransform), typeof(Image));
            streamObj.transform.SetParent(toolObj.transform, false);
            RectTransform stRt = streamObj.GetComponent<RectTransform>();
            stRt.pivot = new Vector2(0.5f, 1f);
            stRt.anchoredPosition = new Vector2(0f, -35f);
            stRt.sizeDelta = new Vector2(6f, 60f);
            Image stImg = streamObj.GetComponent<Image>();
            stImg.color = new Color(0.2f, 0.9f, 1f, 0.95f);
            stImg.raycastTarget = false;
            streamObj.SetActive(false);

            // Прогресс Текст
            GameObject progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progObj.transform.SetParent(root.transform, false);
            RectTransform pRt = progObj.GetComponent<RectTransform>();
            pRt.anchoredPosition = new Vector2(0f, 92f);
            pRt.sizeDelta = new Vector2(250f, 26f);
            TextMeshProUGUI pTmp = progObj.GetComponent<TextMeshProUGUI>();
            pTmp.text = "Заполнено лунок: 0 / 3";
            pTmp.fontSize = 18;
            pTmp.alignment = TextAlignmentOptions.Center;
            pTmp.color = Color.white;

            // Прогресс Бар снизу
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -112f);
            bRt.sizeDelta = new Vector2(260f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.color = new Color(0f, 0.85f, 1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0f;

            M31_LiquidFillingMechanic mechanic = root.AddComponent<M31_LiquidFillingMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M31_LiquidFilling";
            so.FindProperty("_title").stringValue = "Механика #31: Заполнение лунок раствором (3-Well Solution Dispensing)";
            so.FindProperty("_instruction").stringValue = "Перемещайте раствороподатчик над лунками по очереди (1 → 2 → 3), чтобы заполнить их раствором!";
            so.FindProperty("_dispenserTool").objectReferenceValue = tRt;
            so.FindProperty("_nozzleTip").objectReferenceValue = nRt;
            so.FindProperty("_streamVisual").objectReferenceValue = stImg;
            so.FindProperty("_wellsContainer").objectReferenceValue = wcRt;

            SerializedProperty wrProp = so.FindProperty("_wellRoots");
            SerializedProperty lfProp = so.FindProperty("_liquidFills");
            SerializedProperty grProp = so.FindProperty("_glowRings");
            SerializedProperty ptProp = so.FindProperty("_percentTexts");
            wrProp.arraySize = 3;
            lfProp.arraySize = 3;
            grProp.arraySize = 3;
            ptProp.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                wrProp.GetArrayElementAtIndex(i).objectReferenceValue = wellRoots[i];
                lfProp.GetArrayElementAtIndex(i).objectReferenceValue = liquidFills[i];
                grProp.GetArrayElementAtIndex(i).objectReferenceValue = glowRings[i];
                ptProp.GetArrayElementAtIndex(i).objectReferenceValue = percentTexts[i];
            }

            so.FindProperty("_progressText").objectReferenceValue = pTmp;
            so.FindProperty("_progressFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.FindProperty("_fillSpeed").floatValue = 0.48f;
            so.FindProperty("_wellRadius").floatValue = 55f;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (File.Exists(M31_PREFAB_PATH))
            {
                AssetDatabase.DeleteAsset(M31_PREFAB_PATH);
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M31_PREFAB_PATH);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=#00FF99>Префаб M31_LiquidFilling (3 Лунки) сохранен: {M31_PREFAB_PATH}</color>");
            return prefab;
        }
        #endregion

        #region M32: Airplane Star Glider
        public static GameObject BuildM32AirplaneStarGliderPrefab()
        {
            EnsureDirectories();
            GameObject root = new GameObject("M32_AirplaneStarGlider", typeof(RectTransform), typeof(Image));
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            Image rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.01f); // перехват указателя для маневрирования

            Sprite coinSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Runner_Coin.png");
            Sprite cloudSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Obstacle_Barrier.png");
            Sprite triangleSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Triangle.png");

            GameObject instrObj = CreateInstruction(root.transform, "Ведите курсором вверх/вниз для управления самолетиком. Соберите 5 звезд и избегайте туч [X]!");
            TextMeshProUGUI instrTmp = instrObj.GetComponent<TextMeshProUGUI>();

            // Контейнер неба
            GameObject flightObj = new GameObject("FlightContainer", typeof(RectTransform), typeof(RectMask2D));
            flightObj.transform.SetParent(root.transform, false);
            RectTransform flRt = flightObj.GetComponent<RectTransform>();
            flRt.anchoredPosition = new Vector2(0f, 12f);
            flRt.sizeDelta = new Vector2(620f, 210f);

            // Самолетик
            GameObject planeObj = new GameObject("AirplaneRoot", typeof(RectTransform), typeof(Image));
            planeObj.transform.SetParent(flightObj.transform, false);
            RectTransform plRt = planeObj.GetComponent<RectTransform>();
            plRt.anchoredPosition = new Vector2(-180f, 0f);
            plRt.sizeDelta = new Vector2(50f, 32f);
            Image plImg = planeObj.GetComponent<Image>();
            plImg.sprite = triangleSpr;
            plImg.color = new Color(0.95f, 0.95f, 1f, 1f);
            plImg.raycastTarget = false;
            planeObj.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            // Контейнер звездочек
            GameObject starsObj = new GameObject("StarsContainer", typeof(RectTransform));
            starsObj.transform.SetParent(flightObj.transform, false);
            RectTransform stRt = starsObj.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;

            float[] starXs = { 80f, 220f, 360f, 500f, 640f };
            float[] starYs = { 55f, -45f, 60f, -20f, 40f };
            for (int i = 0; i < starXs.Length; i++)
            {
                GameObject star = new GameObject($"Star_{i}", typeof(RectTransform), typeof(Image));
                star.transform.SetParent(starsObj.transform, false);
                RectTransform sRt = star.GetComponent<RectTransform>();
                sRt.anchoredPosition = new Vector2(starXs[i], starYs[i]);
                sRt.sizeDelta = new Vector2(34f, 34f);
                Image sImg = star.GetComponent<Image>();
                sImg.sprite = coinSpr;
                sImg.color = new Color(1f, 0.85f, 0.1f, 1f);
                sImg.raycastTarget = false;
            }

            // Контейнер облаков/туч [X]
            GameObject cloudsObj = new GameObject("CloudsContainer", typeof(RectTransform));
            cloudsObj.transform.SetParent(flightObj.transform, false);
            RectTransform clRt = cloudsObj.GetComponent<RectTransform>();
            clRt.anchorMin = Vector2.zero;
            clRt.anchorMax = Vector2.one;
            clRt.offsetMin = Vector2.zero;
            clRt.offsetMax = Vector2.zero;

            float[] cloudXs = { 160f, 440f };
            float[] cloudYs = { -30f, 45f };
            for (int i = 0; i < cloudXs.Length; i++)
            {
                GameObject cloud = new GameObject($"StormCloud_{i}", typeof(RectTransform), typeof(Image));
                cloud.transform.SetParent(cloudsObj.transform, false);
                RectTransform cRt = cloud.GetComponent<RectTransform>();
                cRt.anchoredPosition = new Vector2(cloudXs[i], cloudYs[i]);
                cRt.sizeDelta = new Vector2(46f, 30f);
                Image cImg = cloud.GetComponent<Image>();
                cImg.sprite = cloudSpr;
                cImg.color = new Color(0.6f, 0.35f, 0.7f, 0.9f);
                cImg.raycastTarget = false;
            }

            // Счетчик звезд
            GameObject scoreObj = new GameObject("StarsCountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreObj.transform.SetParent(root.transform, false);
            RectTransform scRt = scoreObj.GetComponent<RectTransform>();
            scRt.anchoredPosition = new Vector2(0f, 96f);
            scRt.sizeDelta = new Vector2(250f, 26f);
            TextMeshProUGUI scTmp = scoreObj.GetComponent<TextMeshProUGUI>();
            scTmp.text = "Звезды: 0 / 5";
            scTmp.fontSize = 18;
            scTmp.alignment = TextAlignmentOptions.Center;
            scTmp.color = Color.white;

            // Прогресс бар
            GameObject barBg = new GameObject("ProgressBarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(root.transform, false);
            RectTransform bRt = barBg.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, -85f);
            bRt.sizeDelta = new Vector2(250f, 14f);
            Image bImg = barBg.GetComponent<Image>();
            bImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

            GameObject barFill = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            Image bfImg = barFill.GetComponent<Image>();
            bfImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITES_DIR}/Item_Square.png");
            bfImg.color = new Color(1f, 0.85f, 0.1f, 1f);
            bfImg.type = Image.Type.Filled;
            bfImg.fillMethod = Image.FillMethod.Horizontal;
            bfImg.fillAmount = 0f;

            M32_AirplaneStarGliderMechanic mechanic = root.AddComponent<M32_AirplaneStarGliderMechanic>();
            SerializedObject so = new SerializedObject(mechanic);
            so.FindProperty("_mechanicId").stringValue = "M32_AirplaneStarGlider";
            so.FindProperty("_title").stringValue = "Механика #32: Полет самолетика со сбором звездочек (Airplane Star Glider)";
            so.FindProperty("_instruction").stringValue = "Ведите курсором вверх/вниз для управления самолетиком. Соберите 5 звезд и избегайте туч [X]!";
            so.FindProperty("_airplaneRoot").objectReferenceValue = plRt;
            so.FindProperty("_flightContainer").objectReferenceValue = flRt;
            so.FindProperty("_starsContainer").objectReferenceValue = stRt;
            so.FindProperty("_cloudsContainer").objectReferenceValue = clRt;
            so.FindProperty("_starsCountText").objectReferenceValue = scTmp;
            so.FindProperty("_progressFill").objectReferenceValue = bfImg;
            so.FindProperty("_instructionText").objectReferenceValue = instrTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, M32_PREFAB_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }
        #endregion

        #region Setup 2D Suite In Scene
        private static void Setup2DSuiteInScene(GameObject[] prefabs = null)
        {
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Не удалось открыть сцену: {SCENE_PATH}");
                return;
            }

            if (prefabs == null || prefabs.Length == 0)
            {
                List<GameObject> loadedPrefabs = new List<GameObject>();
                for (int i = 1; i <= 32; i++)
                {
                    string[] found = Directory.GetFiles(PREFABS_DIR, $"M{i:D2}_*.prefab");
                    if (found.Length > 0)
                    {
                        GameObject pGo = AssetDatabase.LoadAssetAtPath<GameObject>(found[0]);
                        if (pGo != null) loadedPrefabs.Add(pGo);
                    }
                }
                prefabs = loadedPrefabs.ToArray();
            }

            GameObject world2DWindow = FindObjectInScene(scene, "World2DSuiteWindow");
            if (world2DWindow == null)
            {
                Debug.LogError("В сцене не найден объект World2DSuiteWindow!");
                return;
            }

            Mechanic2DHost host = world2DWindow.GetComponent<Mechanic2DHost>();
            if (host == null)
            {
                host = world2DWindow.AddComponent<Mechanic2DHost>();
            }

            VerticalLayoutGroup winVlg = world2DWindow.GetComponent<VerticalLayoutGroup>();
            if (winVlg != null)
            {
                winVlg.padding = new RectOffset(8, 8, 4, 4);
                winVlg.spacing = 2f;
                winVlg.childControlHeight = true;
                winVlg.childControlWidth = true;
                winVlg.childForceExpandHeight = true;
                winVlg.childForceExpandWidth = true;
            }

            // 1. CatalogPanel
            Transform catalogPanelTr = world2DWindow.transform.Find("CatalogPanel");
            GameObject catalogPanelObj;
            if (catalogPanelTr == null)
            {
                catalogPanelObj = new GameObject("CatalogPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
                catalogPanelObj.transform.SetParent(world2DWindow.transform, false);
            }
            else
            {
                catalogPanelObj = catalogPanelTr.gameObject;
            }

            RectTransform catRt = catalogPanelObj.GetComponent<RectTransform>();
            catRt.anchorMin = Vector2.zero;
            catRt.anchorMax = Vector2.one;
            catRt.offsetMin = Vector2.zero;
            catRt.offsetMax = Vector2.zero;

            VerticalLayoutGroup catVlg = catalogPanelObj.GetComponent<VerticalLayoutGroup>();
            catVlg.padding = new RectOffset(8, 8, 8, 8);
            catVlg.spacing = 10f;
            catVlg.childControlHeight = true;
            catVlg.childControlWidth = true;
            catVlg.childForceExpandHeight = false;
            catVlg.childForceExpandWidth = true;

            Transform catHeaderTr = catalogPanelObj.transform.Find("CatalogHeader");
            GameObject catHeaderObj;
            if (catHeaderTr == null)
            {
                catHeaderObj = new GameObject("CatalogHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                catHeaderObj.transform.SetParent(catalogPanelObj.transform, false);
            }
            else
            {
                catHeaderObj = catHeaderTr.gameObject;
            }

            LayoutElement chLe = catHeaderObj.GetComponent<LayoutElement>();
            chLe.preferredHeight = 54f;
            chLe.flexibleHeight = 0f;

            HorizontalLayoutGroup chHlg = catHeaderObj.GetComponent<HorizontalLayoutGroup>();
            chHlg.spacing = 14f;
            chHlg.childAlignment = TextAnchor.MiddleCenter;
            chHlg.childControlHeight = true;
            chHlg.childControlWidth = true;
            chHlg.childForceExpandHeight = true;
            chHlg.childForceExpandWidth = false;

            Sprite navSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Btn_Nav_Pill.png");

            Transform oldBackTr = world2DWindow.transform.Find("World2DSuiteWindow_BackButton");
            GameObject btnCatBackObj;
            if (oldBackTr != null)
            {
                btnCatBackObj = oldBackTr.gameObject;
                btnCatBackObj.transform.SetParent(catHeaderObj.transform, false);
            }
            else
            {
                Transform existingInHeader = catHeaderObj.transform.Find("BtnCatalogMainMenu");
                if (existingInHeader != null)
                {
                    btnCatBackObj = existingInHeader.gameObject;
                }
                else
                {
                    btnCatBackObj = CreateButton(catHeaderObj.transform, "BtnCatalogMainMenu", "<-  В главное меню", 200f, 48f, new Color(0.12f, 0.44f, 0.72f, 1f), new Color(0.25f, 0.95f, 1f, 0.95f));
                }
            }
            btnCatBackObj.name = "BtnCatalogMainMenu";
            LayoutElement bcbLe = btnCatBackObj.GetComponent<LayoutElement>();
            if (bcbLe == null) bcbLe = btnCatBackObj.AddComponent<LayoutElement>();
            bcbLe.preferredWidth = 200f;
            bcbLe.preferredHeight = 48f;
            Image bcbImg = btnCatBackObj.GetComponent<Image>();
            if (bcbImg == null) bcbImg = btnCatBackObj.AddComponent<Image>();
            if (navSprite != null) { bcbImg.sprite = navSprite; bcbImg.type = Image.Type.Sliced; bcbImg.color = Color.white; }
            else { bcbImg.color = new Color(0.12f, 0.44f, 0.72f, 1f); }
            bcbImg.raycastTarget = true;
            btnCatBackObj.GetComponent<Button>().targetGraphic = bcbImg;

            Outline bcbOut = btnCatBackObj.GetComponent<Outline>();
            if (bcbOut == null) bcbOut = btnCatBackObj.AddComponent<Outline>();
            bcbOut.effectColor = new Color(0.25f, 0.95f, 1f, 0.95f);
            bcbOut.effectDistance = new Vector2(2f, -2f);
            Shadow bcbShadow = btnCatBackObj.GetComponent<Shadow>();
            if (bcbShadow == null) bcbShadow = btnCatBackObj.AddComponent<Shadow>();
            bcbShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            bcbShadow.effectDistance = new Vector2(1f, -3f);
            TMP_Text bcbTxt = btnCatBackObj.GetComponentInChildren<TMP_Text>();
            if (bcbTxt != null) { bcbTxt.text = "<-  В главное меню"; bcbTxt.raycastTarget = false; bcbTxt.fontSize = 15; bcbTxt.fontStyle = FontStyles.Bold; }

            Transform catTitleTr = catHeaderObj.transform.Find("CatalogTitle");
            GameObject catTitleObj;
            if (catTitleTr == null)
            {
                catTitleObj = new GameObject("CatalogTitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                catTitleObj.transform.SetParent(catHeaderObj.transform, false);
            }
            else
            {
                catTitleObj = catTitleTr.gameObject;
            }
            LayoutElement ctLe = catTitleObj.GetComponent<LayoutElement>();
            ctLe.flexibleWidth = 1f;
            ctLe.preferredHeight = 48f;
            TextMeshProUGUI ctTmp = catTitleObj.GetComponent<TextMeshProUGUI>();
            ctTmp.text = "Каталог 2D-механик (32 теста)";
            ctTmp.fontSize = 20;
            ctTmp.fontStyle = FontStyles.Bold;
            ctTmp.alignment = TextAlignmentOptions.Center;
            ctTmp.color = Color.white;
            ctTmp.raycastTarget = false;

            Transform btnSeqTr = catHeaderObj.transform.Find("BtnRunSequential");
            GameObject btnSeqObj;
            if (btnSeqTr == null)
            {
                btnSeqObj = CreateButton(catHeaderObj.transform, "BtnRunSequential", ">>  Автопрогон всех тестов (1 -> 32)", 320f, 48f, new Color(0.08f, 0.6f, 0.42f, 1f), new Color(0.25f, 1f, 0.65f, 0.95f));
            }
            else
            {
                btnSeqObj = btnSeqTr.gameObject;
            }
            LayoutElement bsLe = btnSeqObj.GetComponent<LayoutElement>();
            if (bsLe == null) bsLe = btnSeqObj.AddComponent<LayoutElement>();
            bsLe.preferredWidth = 320f;
            bsLe.preferredHeight = 48f;
            Image bsImg = btnSeqObj.GetComponent<Image>();
            if (bsImg == null) bsImg = btnSeqObj.AddComponent<Image>();
            if (navSprite != null) { bsImg.sprite = navSprite; bsImg.type = Image.Type.Sliced; bsImg.color = Color.white; }
            else { bsImg.color = new Color(0.08f, 0.6f, 0.42f, 1f); }
            bsImg.raycastTarget = true;
            btnSeqObj.GetComponent<Button>().targetGraphic = bsImg;
            Outline bsOut = btnSeqObj.GetComponent<Outline>();
            if (bsOut == null) bsOut = btnSeqObj.AddComponent<Outline>();
            bsOut.effectColor = new Color(0.25f, 1f, 0.65f, 0.95f);
            bsOut.effectDistance = new Vector2(2f, -2f);
            Shadow bsShadow = btnSeqObj.GetComponent<Shadow>();
            if (bsShadow == null) bsShadow = btnSeqObj.AddComponent<Shadow>();
            bsShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            bsShadow.effectDistance = new Vector2(1f, -3f);
            TMP_Text bsTxt = btnSeqObj.GetComponentInChildren<TMP_Text>();
            if (bsTxt != null) { bsTxt.text = ">>  Автопрогон всех тестов (1 -> 32)"; bsTxt.raycastTarget = false; bsTxt.fontSize = 15; bsTxt.fontStyle = FontStyles.Bold; }

            GraphicRaycaster chGr = catHeaderObj.GetComponent<GraphicRaycaster>();
            if (chGr != null) Object.DestroyImmediate(chGr, true);
            Canvas chCanvas = catHeaderObj.GetComponent<Canvas>();
            if (chCanvas != null) Object.DestroyImmediate(chCanvas, true);

            Transform catScrollTr = catalogPanelObj.transform.Find("CatalogScrollView");
            GameObject catScrollObj;
            if (catScrollTr == null)
            {
                catScrollObj = new GameObject("CatalogScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                catScrollObj.transform.SetParent(catalogPanelObj.transform, false);
            }
            else
            {
                catScrollObj = catScrollTr.gameObject;
            }
            LayoutElement csLe = catScrollObj.GetComponent<LayoutElement>();
            csLe.flexibleHeight = 1f;
            csLe.minHeight = 350f;
            Image csImg = catScrollObj.GetComponent<Image>();
            csImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            ScrollRect csSr = catScrollObj.GetComponent<ScrollRect>();
            csSr.horizontal = false;
            csSr.vertical = true;

            Transform vpTr = catScrollObj.transform.Find("Viewport");
            GameObject vpObj;
            if (vpTr == null)
            {
                vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                vpObj.transform.SetParent(catScrollObj.transform, false);
            }
            else
            {
                vpObj = vpTr.gameObject;
            }
            RectTransform vpRt = vpObj.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(4, 4);
            vpRt.offsetMax = new Vector2(-4, -4);
            csSr.viewport = vpRt;

            Transform cntTr = vpObj.transform.Find("Content");
            GameObject cntObj;
            if (cntTr == null)
            {
                cntObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                cntObj.transform.SetParent(vpObj.transform, false);
            }
            else
            {
                cntObj = cntTr.gameObject;
            }
            RectTransform cntRt = cntObj.GetComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(0f, 1f);
            cntRt.anchorMax = new Vector2(1f, 1f);
            cntRt.pivot = new Vector2(0.5f, 1f);
            cntRt.offsetMin = Vector2.zero;
            cntRt.offsetMax = Vector2.zero;
            csSr.content = cntRt;
            for (int c = cntObj.transform.childCount - 1; c >= 0; c--)
            {
                Object.DestroyImmediate(cntObj.transform.GetChild(c).gameObject);
            }

            VerticalLayoutGroup cntVlg = cntObj.GetComponent<VerticalLayoutGroup>();
            cntVlg.padding = new RectOffset(8, 8, 8, 8);
            cntVlg.spacing = 8f;
            cntVlg.childControlHeight = true;
            cntVlg.childControlWidth = true;
            cntVlg.childForceExpandHeight = false;
            cntVlg.childForceExpandWidth = true;

            ContentSizeFitter cntCsf = cntObj.GetComponent<ContentSizeFitter>();
            cntCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cntCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 2. PlayPanel
            Transform playPanelTr = world2DWindow.transform.Find("PlayPanel");
            GameObject playPanelObj;
            if (playPanelTr == null)
            {
                playPanelObj = new GameObject("PlayPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
                playPanelObj.transform.SetParent(world2DWindow.transform, false);
            }
            else
            {
                playPanelObj = playPanelTr.gameObject;
            }
            RectTransform playRt = playPanelObj.GetComponent<RectTransform>();
            playRt.anchorMin = Vector2.zero;
            playRt.anchorMax = Vector2.one;
            playRt.offsetMin = Vector2.zero;
            playRt.offsetMax = Vector2.zero;

            VerticalLayoutGroup playVlg = playPanelObj.GetComponent<VerticalLayoutGroup>();
            playVlg.padding = new RectOffset(4, 4, 2, 2);
            playVlg.spacing = 2f;
            playVlg.childControlHeight = true;
            playVlg.childControlWidth = true;
            playVlg.childForceExpandHeight = false;
            playVlg.childForceExpandWidth = true;

            Transform playNavTr = playPanelObj.transform.Find("PlayTopNav");
            GameObject playNavObj;
            if (playNavTr == null)
            {
                playNavObj = new GameObject("PlayTopNav", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                playNavObj.transform.SetParent(playPanelObj.transform, false);
            }
            else
            {
                playNavObj = playNavTr.gameObject;
            }
            LayoutElement pnLe = playNavObj.GetComponent<LayoutElement>();
            pnLe.minHeight = 40f;
            pnLe.preferredHeight = 40f;
            pnLe.flexibleHeight = 0f;
            pnLe.layoutPriority = 10;

            HorizontalLayoutGroup pnHlg = playNavObj.GetComponent<HorizontalLayoutGroup>();
            pnHlg.padding = new RectOffset(12, 12, 3, 3);
            pnHlg.spacing = 10f;
            pnHlg.childAlignment = TextAnchor.MiddleCenter;
            pnHlg.childControlHeight = true;
            pnHlg.childControlWidth = true;
            pnHlg.childForceExpandHeight = true;
            pnHlg.childForceExpandWidth = false;

            Transform btnPlayMenuTr = playNavObj.transform.Find("BtnPlayMainMenu");
            GameObject btnPlayMenuObj = btnPlayMenuTr != null ? btnPlayMenuTr.gameObject : CreateButton(playNavObj.transform, "BtnPlayMainMenu", "◄  В главное меню", 175f, 38f, new Color(0.10f, 0.48f, 0.90f, 1f), new Color(0.35f, 0.95f, 1f, 1f));
            LayoutElement bpmLe = btnPlayMenuObj.GetComponent<LayoutElement>();
            if (bpmLe == null) bpmLe = btnPlayMenuObj.AddComponent<LayoutElement>();
            bpmLe.minWidth = 160f;
            bpmLe.preferredWidth = 175f;
            bpmLe.minHeight = 38f;
            bpmLe.preferredHeight = 38f;
            bpmLe.layoutPriority = 10;
            Image bpmImg = btnPlayMenuObj.GetComponent<Image>();
            if (bpmImg == null) bpmImg = btnPlayMenuObj.AddComponent<Image>();
            if (navSprite != null) { bpmImg.sprite = navSprite; bpmImg.type = Image.Type.Sliced; }
            bpmImg.color = new Color(0.10f, 0.48f, 0.90f, 1f);
            bpmImg.raycastTarget = true;
            btnPlayMenuObj.GetComponent<Button>().targetGraphic = bpmImg;

            Outline bpmOut = btnPlayMenuObj.GetComponent<Outline>();
            if (bpmOut == null) bpmOut = btnPlayMenuObj.AddComponent<Outline>();
            bpmOut.effectColor = new Color(0.35f, 0.95f, 1f, 1f);
            bpmOut.effectDistance = new Vector2(2f, -2f);
            Shadow bpmShadow = btnPlayMenuObj.GetComponent<Shadow>();
            if (bpmShadow == null) bpmShadow = btnPlayMenuObj.AddComponent<Shadow>();
            bpmShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            bpmShadow.effectDistance = new Vector2(1f, -3f);
            TMP_Text bpmTxt = btnPlayMenuObj.GetComponentInChildren<TMP_Text>();
            if (bpmTxt != null) { bpmTxt.text = "◄  В главное меню"; bpmTxt.raycastTarget = false; bpmTxt.fontSize = 14; bpmTxt.fontStyle = FontStyles.Bold; }

            Transform btnPlayCatTr = playNavObj.transform.Find("BtnPlayCatalog");
            GameObject btnPlayCatObj = btnPlayCatTr != null ? btnPlayCatTr.gameObject : CreateButton(playNavObj.transform, "BtnPlayCatalog", "≡  Список уровней", 165f, 38f, new Color(0.38f, 0.22f, 0.85f, 1f), new Color(0.78f, 0.58f, 1f, 1f));
            LayoutElement bpcLe = btnPlayCatObj.GetComponent<LayoutElement>();
            if (bpcLe == null) bpcLe = btnPlayCatObj.AddComponent<LayoutElement>();
            bpcLe.minWidth = 150f;
            bpcLe.preferredWidth = 165f;
            bpcLe.minHeight = 38f;
            bpcLe.preferredHeight = 38f;
            bpcLe.layoutPriority = 10;
            Image bpcImg = btnPlayCatObj.GetComponent<Image>();
            if (bpcImg == null) bpcImg = btnPlayCatObj.AddComponent<Image>();
            if (navSprite != null) { bpcImg.sprite = navSprite; bpcImg.type = Image.Type.Sliced; }
            bpcImg.color = new Color(0.38f, 0.22f, 0.85f, 1f);
            bpcImg.raycastTarget = true;
            btnPlayCatObj.GetComponent<Button>().targetGraphic = bpcImg;

            Outline bpcOut = btnPlayCatObj.GetComponent<Outline>();
            if (bpcOut == null) bpcOut = btnPlayCatObj.AddComponent<Outline>();
            bpcOut.effectColor = new Color(0.78f, 0.58f, 1f, 1f);
            bpcOut.effectDistance = new Vector2(2f, -2f);
            Shadow bpcShadow = btnPlayCatObj.GetComponent<Shadow>();
            if (bpcShadow == null) bpcShadow = btnPlayCatObj.AddComponent<Shadow>();
            bpcShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            bpcShadow.effectDistance = new Vector2(1f, -3f);
            TMP_Text bpcTxt = btnPlayCatObj.GetComponentInChildren<TMP_Text>();
            if (bpcTxt != null) { bpcTxt.text = "≡  Список уровней"; bpcTxt.raycastTarget = false; bpcTxt.fontSize = 14; bpcTxt.fontStyle = FontStyles.Bold; }

            GraphicRaycaster pnGr = playNavObj.GetComponent<GraphicRaycaster>();
            if (pnGr != null) Object.DestroyImmediate(pnGr, true);
            Canvas pnCanvas = playNavObj.GetComponent<Canvas>();
            if (pnCanvas != null) Object.DestroyImmediate(pnCanvas, true);

            // Title в центре PlayTopNav
            Transform titleTr = playNavObj.transform.Find("World2DSuiteWindow_Title");
            if (titleTr == null) titleTr = world2DWindow.transform.Find("World2DSuiteWindow_Title");
            GameObject titleObj;
            if (titleTr == null)
            {
                titleObj = new GameObject("World2DSuiteWindow_Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                titleObj.transform.SetParent(playNavObj.transform, false);
            }
            else
            {
                titleObj = titleTr.gameObject;
                titleObj.transform.SetParent(playNavObj.transform, false);
            }
            LayoutElement tLe = titleObj.GetComponent<LayoutElement>();
            if (tLe == null) tLe = titleObj.AddComponent<LayoutElement>();
            tLe.flexibleWidth = 1f;
            tLe.minWidth = 180f;
            tLe.minHeight = 36f;
            tLe.preferredHeight = 38f;
            tLe.layoutPriority = 10;
            TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "[1/32] Snap to Slot";
            titleTmp.fontSize = 17;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = true;
            Button titleBtn = titleObj.GetComponent<Button>();
            if (titleBtn == null) titleBtn = titleObj.AddComponent<Button>();
            titleBtn.targetGraphic = titleTmp;

            // StatusBadge справа в PlayTopNav
            Transform badgeTr = playNavObj.transform.Find("StatusBadge");
            if (badgeTr == null) badgeTr = world2DWindow.transform.Find("StatusBadge");
            GameObject badgeObj;
            if (badgeTr == null)
            {
                badgeObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                badgeObj.transform.SetParent(playNavObj.transform, false);
            }
            else
            {
                badgeObj = badgeTr.gameObject;
                badgeObj.transform.SetParent(playNavObj.transform, false);
            }
            LayoutElement bLe = badgeObj.GetComponent<LayoutElement>();
            if (bLe == null) bLe = badgeObj.AddComponent<LayoutElement>();
            bLe.preferredWidth = 135f;
            bLe.preferredHeight = 32f;
            bLe.flexibleWidth = 0f;
            bLe.flexibleHeight = 0f;
            Image bImg = badgeObj.GetComponent<Image>();
            bImg.color = new Color(0.6f, 0.45f, 0.1f, 0.35f);
            bImg.raycastTarget = true;
            Button badgeBtn = badgeObj.GetComponent<Button>();
            if (badgeBtn == null) badgeBtn = badgeObj.AddComponent<Button>();
            badgeBtn.targetGraphic = bImg;

            Transform badgeTextTr = badgeObj.transform.Find("BadgeText");
            TextMeshProUGUI btTmp;
            if (badgeTextTr == null)
            {
                GameObject btObj = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
                btObj.transform.SetParent(badgeObj.transform, false);
                RectTransform btRt = btObj.GetComponent<RectTransform>();
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero;
                btRt.offsetMax = Vector2.zero;
                btTmp = btObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                btTmp = badgeTextTr.GetComponent<TextMeshProUGUI>();
            }
            btTmp.text = "В ПРОЦЕССЕ";
            btTmp.fontSize = 13;
            btTmp.fontStyle = FontStyles.Bold;
            btTmp.alignment = TextAlignmentOptions.Center;
            btTmp.color = new Color(1f, 0.85f, 0.2f);
            btTmp.raycastTarget = false;

            // ModeBadge в самом правом конце PlayTopNav
            Transform modeBadgeTr = playNavObj.transform.Find("ModeBadge");
            GameObject modeBadgeObj;
            if (modeBadgeTr == null)
            {
                modeBadgeObj = new GameObject("ModeBadge", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                modeBadgeObj.transform.SetParent(playNavObj.transform, false);
            }
            else
            {
                modeBadgeObj = modeBadgeTr.gameObject;
                modeBadgeObj.transform.SetParent(playNavObj.transform, false);
            }
            LayoutElement mbLe = modeBadgeObj.GetComponent<LayoutElement>();
            mbLe.preferredWidth = 140f;
            mbLe.preferredHeight = 32f;
            mbLe.flexibleWidth = 0f;
            TextMeshProUGUI mbTmp = modeBadgeObj.GetComponent<TextMeshProUGUI>();
            mbTmp.text = "ОДИНОЧНЫЙ ТЕСТ";
            mbTmp.fontSize = 12;
            mbTmp.fontStyle = FontStyles.Bold;
            mbTmp.alignment = TextAlignmentOptions.Right;
            mbTmp.color = new Color(0.3f, 0.85f, 1f);
            mbTmp.raycastTarget = false;

            // Удаляем устаревший промежуточный PlayHeaderInfo
            Transform playHeaderTr = playPanelObj.transform.Find("PlayHeaderInfo");
            if (playHeaderTr != null) Object.DestroyImmediate(playHeaderTr.gameObject, true);

            // Порядок детей в PlayTopNav: 0:Menu, 1:Catalog, 2:Title, 3:StatusBadge, 4:ModeBadge
            btnPlayMenuObj.transform.SetSiblingIndex(0);
            btnPlayCatObj.transform.SetSiblingIndex(1);
            titleObj.transform.SetSiblingIndex(2);
            badgeObj.transform.SetSiblingIndex(3);
            modeBadgeObj.transform.SetSiblingIndex(4);

            Transform instrTr = world2DWindow.transform.Find("HeaderInstructionText");
            if (instrTr == null) instrTr = playPanelObj.transform.Find("HeaderInstructionText");
            GameObject headerInstrObj;
            if (instrTr == null)
            {
                headerInstrObj = new GameObject("HeaderInstructionText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                headerInstrObj.transform.SetParent(playPanelObj.transform, false);
            }
            else
            {
                headerInstrObj = instrTr.gameObject;
                headerInstrObj.transform.SetParent(playPanelObj.transform, false);
            }
            LayoutElement hiLe = headerInstrObj.GetComponent<LayoutElement>();
            if (hiLe == null) hiLe = headerInstrObj.AddComponent<LayoutElement>();
            hiLe.minHeight = 20f;
            hiLe.preferredHeight = 20f;
            hiLe.flexibleHeight = 0f;
            TextMeshProUGUI hiTmp = headerInstrObj.GetComponent<TextMeshProUGUI>();
            hiTmp.fontSize = 13;
            hiTmp.alignment = TextAlignmentOptions.Center;
            hiTmp.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
            hiTmp.raycastTarget = false;

            Transform existingPlayArea = world2DWindow.transform.Find("MechanicPlayArea");
            if (existingPlayArea == null) existingPlayArea = playPanelObj.transform.Find("MechanicPlayArea");
            GameObject playAreaObj;
            if (existingPlayArea == null)
            {
                playAreaObj = new GameObject("MechanicPlayArea", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                playAreaObj.transform.SetParent(playPanelObj.transform, false);
            }
            else
            {
                playAreaObj = existingPlayArea.gameObject;
                playAreaObj.transform.SetParent(playPanelObj.transform, false);
            }
            Image paImg = playAreaObj.GetComponent<Image>();
            if (paImg == null) paImg = playAreaObj.AddComponent<Image>();
            paImg.color = new Color(0.06f, 0.08f, 0.11f, 0.85f);
            paImg.raycastTarget = false;

            // УДАЛЯЕМ RectMask2D, чтобы нижние контейнеры/инвентарь (склад мостиков и т.д.) не срезались!
            RectMask2D paMask = playAreaObj.GetComponent<RectMask2D>();
            if (paMask != null) Object.DestroyImmediate(paMask, true);

            LayoutElement paLe = playAreaObj.GetComponent<LayoutElement>();
            if (paLe == null) paLe = playAreaObj.AddComponent<LayoutElement>();
            paLe.flexibleHeight = 1f;
            paLe.minHeight = 240f;
            paLe.preferredHeight = 310f;

            Transform playBottomTr = playPanelObj.transform.Find("PlayBottomControls");
            GameObject playBottomObj;
            if (playBottomTr == null)
            {
                playBottomObj = new GameObject("PlayBottomControls", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                playBottomObj.transform.SetParent(playPanelObj.transform, false);
            }
            else
            {
                playBottomObj = playBottomTr.gameObject;
            }
            LayoutElement pbLe = playBottomObj.GetComponent<LayoutElement>();
            pbLe.minHeight = 36f;
            pbLe.preferredHeight = 36f;
            pbLe.flexibleHeight = 0f;
            pbLe.layoutPriority = 10;

            HorizontalLayoutGroup pbHlg = playBottomObj.GetComponent<HorizontalLayoutGroup>();
            pbHlg.spacing = 10f;
            pbHlg.childAlignment = TextAnchor.MiddleCenter;
            pbHlg.childControlHeight = true;
            pbHlg.childControlWidth = true;
            pbHlg.childForceExpandHeight = true;
            pbHlg.childForceExpandWidth = false;

            Transform btnPrevTr = playBottomObj.transform.Find("BtnPrev");
            GameObject btnPrevObj = btnPrevTr != null ? btnPrevTr.gameObject : CreateButton(playBottomObj.transform, "BtnPrev", "Предыдущий", 140f, 38f);
            LayoutElement bpLe = btnPrevObj.GetComponent<LayoutElement>();
            if (bpLe == null) bpLe = btnPrevObj.AddComponent<LayoutElement>();
            bpLe.preferredWidth = 140f;
            bpLe.preferredHeight = 36f;
            Image bpImg = btnPrevObj.GetComponent<Image>();
            if (bpImg == null) bpImg = btnPrevObj.AddComponent<Image>();
            if (navSprite != null) { bpImg.sprite = navSprite; bpImg.type = Image.Type.Sliced; bpImg.color = Color.white; }
            bpImg.raycastTarget = true;
            btnPrevObj.GetComponent<Button>().targetGraphic = bpImg;
            TMP_Text bpTxt = btnPrevObj.GetComponentInChildren<TMP_Text>();
            if (bpTxt != null) { bpTxt.raycastTarget = false; bpTxt.fontSize = 14; bpTxt.fontStyle = FontStyles.Bold; }

            Transform btnResetTr = world2DWindow.transform.Find("World2DSuiteWindow_SingleButton");
            if (btnResetTr == null) btnResetTr = playBottomObj.transform.Find("BtnReset");
            GameObject btnResetObj;
            if (btnResetTr != null)
            {
                btnResetObj = btnResetTr.gameObject;
                btnResetObj.transform.SetParent(playBottomObj.transform, false);
            }
            else
            {
                btnResetObj = CreateButton(playBottomObj.transform, "BtnReset", "Сброс", 120f, 38f);
            }
            btnResetObj.name = "BtnReset";
            LayoutElement brLe = btnResetObj.GetComponent<LayoutElement>();
            if (brLe == null) brLe = btnResetObj.AddComponent<LayoutElement>();
            brLe.preferredWidth = 120f;
            brLe.preferredHeight = 36f;
            Image brImg = btnResetObj.GetComponent<Image>();
            if (brImg == null) brImg = btnResetObj.AddComponent<Image>();
            if (navSprite != null) { brImg.sprite = navSprite; brImg.type = Image.Type.Sliced; brImg.color = Color.white; }
            brImg.raycastTarget = true;
            btnResetObj.GetComponent<Button>().targetGraphic = brImg;
            TMP_Text brTxt = btnResetObj.GetComponentInChildren<TMP_Text>();
            if (brTxt != null) { brTxt.text = "Сброс"; brTxt.raycastTarget = false; brTxt.fontSize = 14; brTxt.fontStyle = FontStyles.Bold; }

            Transform btnNextTr = world2DWindow.transform.Find("World2DSuiteWindow_FullCycleButton");
            if (btnNextTr == null) btnNextTr = playBottomObj.transform.Find("BtnNext");
            GameObject btnNextObj;
            if (btnNextTr != null)
            {
                btnNextObj = btnNextTr.gameObject;
                btnNextObj.transform.SetParent(playBottomObj.transform, false);
            }
            else
            {
                btnNextObj = CreateButton(playBottomObj.transform, "BtnNext", "Следующий", 140f, 38f);
            }
            btnNextObj.name = "BtnNext";
            LayoutElement bnLe = btnNextObj.GetComponent<LayoutElement>();
            if (bnLe == null) bnLe = btnNextObj.AddComponent<LayoutElement>();
            bnLe.preferredWidth = 140f;
            bnLe.preferredHeight = 36f;
            Image bnImg = btnNextObj.GetComponent<Image>();
            if (bnImg == null) bnImg = btnNextObj.AddComponent<Image>();
            if (navSprite != null) { bnImg.sprite = navSprite; bnImg.type = Image.Type.Sliced; bnImg.color = Color.white; }
            bnImg.raycastTarget = true;
            btnNextObj.GetComponent<Button>().targetGraphic = bnImg;
            TMP_Text bnTxt = btnNextObj.GetComponentInChildren<TMP_Text>();
            if (bnTxt != null) { bnTxt.text = "Следующий"; bnTxt.raycastTarget = false; bnTxt.fontSize = 14; bnTxt.fontStyle = FontStyles.Bold; }

            Transform progTr = playBottomObj.transform.Find("ProgressText");
            GameObject progObj;
            if (progTr == null)
            {
                progObj = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                progObj.transform.SetParent(playBottomObj.transform, false);
            }
            else
            {
                progObj = progTr.gameObject;
            }
            LayoutElement pLe = progObj.GetComponent<LayoutElement>();
            pLe.flexibleWidth = 1f;
            pLe.preferredHeight = 38f;
            TextMeshProUGUI progTmp = progObj.GetComponent<TextMeshProUGUI>();
            progTmp.text = "Прогресс: 0%";
            progTmp.fontSize = 14;
            progTmp.alignment = TextAlignmentOptions.Right;
            progTmp.color = new Color(0.8f, 0.85f, 0.9f);
            progTmp.raycastTarget = false;

            catalogPanelObj.SetActive(true);
            playPanelObj.SetActive(false);

            SerializedObject hostSo = new SerializedObject(host);
            hostSo.FindProperty("_catalogPanel").objectReferenceValue = catalogPanelObj;
            hostSo.FindProperty("_playPanel").objectReferenceValue = playPanelObj;

            hostSo.FindProperty("_btnCatalogBack").objectReferenceValue = btnCatBackObj.GetComponent<Button>();
            hostSo.FindProperty("_btnRunSequential").objectReferenceValue = btnSeqObj.GetComponent<Button>();
            hostSo.FindProperty("_catalogContent").objectReferenceValue = cntRt;

            hostSo.FindProperty("_btnPlayMainMenu").objectReferenceValue = btnPlayMenuObj.GetComponent<Button>();
            hostSo.FindProperty("_btnPlayCatalog").objectReferenceValue = btnPlayCatObj.GetComponent<Button>();
            hostSo.FindProperty("_modeBadgeText").objectReferenceValue = mbTmp;
            var navSpriteProp = hostSo.FindProperty("_navButtonSprite");
            if (navSpriteProp != null) navSpriteProp.objectReferenceValue = navSprite;

            hostSo.FindProperty("_titleText").objectReferenceValue = titleTmp;
            hostSo.FindProperty("_instructionText").objectReferenceValue = hiTmp;
            hostSo.FindProperty("_statusBadgeText").objectReferenceValue = btTmp;
            hostSo.FindProperty("_statusBadgeBg").objectReferenceValue = bImg;
            hostSo.FindProperty("_progressText").objectReferenceValue = progTmp;

            hostSo.FindProperty("_playAreaContainer").objectReferenceValue = playAreaObj.GetComponent<RectTransform>();

            hostSo.FindProperty("_btnPrev").objectReferenceValue = btnPrevObj.GetComponent<Button>();
            hostSo.FindProperty("_btnReset").objectReferenceValue = btnResetObj.GetComponent<Button>();
            hostSo.FindProperty("_btnNext").objectReferenceValue = btnNextObj.GetComponent<Button>();

            SerializedProperty prefabsProp = hostSo.FindProperty("_mechanicPrefabs");
            prefabsProp.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
            {
                BaseMechanic2DModule module = prefabs[i] != null ? prefabs[i].GetComponent<BaseMechanic2DModule>() : null;
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = module;
            }
            hostSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject controllerObj = FindObjectInScene(scene, "UrdtUiTestPoligonController");
            if (controllerObj != null)
            {
                var controllerComp = controllerObj.GetComponent<UrdtUiTestPoligonController>();
                if (controllerComp != null)
                {
                    SerializedObject ctrlSo = new SerializedObject(controllerComp);
                    var w2dBackProp = ctrlSo.FindProperty("_world2DBackButton");
                    if (w2dBackProp != null)
                    {
                        w2dBackProp.objectReferenceValue = btnCatBackObj.GetComponent<Button>();
                        ctrlSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=#00FF99>Сцена URDT_TestPoligon_UI успешно обновлена с каталогом и автопрогоном тестов!</color>");
        }

        private static GameObject CreateInventorySlot(Transform parent, string name, Vector2 pos, string label, Color outlineColor)
        {
            GameObject slot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(parent, false);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(116f, 32f);

            Image img = slot.GetComponent<Image>();
            img.color = new Color(0.04f, 0.08f, 0.14f, 0.7f);
            img.raycastTarget = false;

            Outline outline = slot.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject lbl = new GameObject("SlotLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(slot.transform, false);
            RectTransform lRt = lbl.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = lbl.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 9;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.65f);
            tmp.raycastTarget = false;

            return slot;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, float width, float height, Color bgColor = default, Color outlineColor = default)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(Shadow));
            obj.transform.SetParent(parent, false);

            LayoutElement le = obj.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            Image img = obj.GetComponent<Image>();
            Sprite navSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Btn_Nav_Pill.png");
            if (navSprite != null)
            {
                img.sprite = navSprite;
                img.type = Image.Type.Sliced;
                img.color = bgColor != default ? bgColor : Color.white;
            }
            else
            {
                img.color = bgColor != default ? bgColor : new Color(0.08f, 0.35f, 0.52f, 1f);
            }
            img.raycastTarget = true;

            Outline outline = obj.GetComponent<Outline>();
            outline.effectColor = outlineColor != default ? outlineColor : new Color(0.25f, 0.95f, 1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = obj.GetComponent<Shadow>();
            if (shadow == null) shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -3f);

            GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(obj.transform, false);
            RectTransform lRt = lbl.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = lbl.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // КРИТИЧНО: текст не блокирует клики мышью!

            Button btn = obj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            return obj;
        }

        private static GameObject CreateInstruction(Transform parent, string text)
        {
            GameObject obj = new GameObject("LocalInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 124f);
            rt.sizeDelta = new Vector2(720f, 24f);

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            tmp.raycastTarget = false; // Текст не блокирует события мыши
            return obj;
        }

        private static GameObject CreateTapButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Sprite sprite = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                Sprite navSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/URDT_TestPoligon/2D_Polygon/Sprites/Btn_Nav_Pill.png");
                if (navSprite != null)
                {
                    img.sprite = navSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                else
                {
                    img.color = new Color(0.12f, 0.52f, 0.78f, 1f);
                }
            }
            img.raycastTarget = true;

            Outline outline = obj.GetComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.95f, 1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = obj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -3f);

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(obj.transform, false);
            RectTransform lRt = lblObj.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = lblObj.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // Текст не блокирует клики мыши

            Button btn = obj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            return obj;
        }

        private static GameObject FindObjectInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var match = FindInChildren(root.transform, name);
                if (match != null) return match.gameObject;
            }
            return null;
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

    [InitializeOnLoad]
    public static class Urdt2DSceneAutoSetup
    {
        public const string TRIGGER_FILE = "Temp/Run2DSceneSetup.trigger";
        public const string VALIDATE_TRIGGER_FILE = "Temp/Run2DValidation.trigger";

        static Urdt2DSceneAutoSetup()
        {
            EditorApplication.delayCall += CheckAndRun;
            EditorApplication.update += CheckAndRun;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                CheckAndRun();
            }
        }

        public static void CheckAndRun()
        {
            if (File.Exists(TRIGGER_FILE))
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.LogWarning("[Urdt2DSceneAutoSetup] Unity находится в Play Mode! Выходим из Play Mode для запуска пересборки префабов и сцены...");
                    EditorApplication.isPlaying = false;
                    return;
                }
                try { File.Delete(TRIGGER_FILE); } catch {}
                Debug.Log("<color=#00FF99>[Urdt2DSceneAutoSetup] Триггер обнаружен! Пересобираем префабы и настраиваем сцену...</color>");
                UrdtMechanics2DBuilder.BuildAllAndSetup();
            }

            if (File.Exists(VALIDATE_TRIGGER_FILE))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    return;
                }
                try { File.Delete(VALIDATE_TRIGGER_FILE); } catch {}
                Debug.Log("<color=#00FFFF>[Urdt2DSceneAutoSetup] Триггер валидации обнаружен! Запускаем тестирование всех 32 механик...</color>");
                UrdtMechanics2DValidator.ValidateAllMechanics();
            }
        }
    }
}
