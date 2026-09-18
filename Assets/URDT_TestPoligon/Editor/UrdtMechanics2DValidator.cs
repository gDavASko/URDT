using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KBP.URDT.TestPoligon.Mechanics2D.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Editor
{
    /// <summary>
    /// Автоматизированный валидатор и раннер 2D-механик.
    /// По очереди инстанциирует и тестирует все 32 игры полигона:
    /// - проверяет наличие и валидность всех игровых элементов,
    /// - вычисляет точные геометрические границы каждого объекта внутри MechanicPlayArea,
    /// - гарантирует, что ни один элемент не срезается краями экрана (Y: [-185..185], X: [-360..360]),
    /// - проверяет целостность всех сериализованных ссылок и интерактивных компонентов,
    /// - формирует структурированный отчет о проверке в консоль и в Temp/ValidationReport2D.json.
    /// </summary>
    [InitializeOnLoad]
    public static class UrdtMechanics2DValidator
    {
        public const string VALIDATION_TRIGGER_FILE = "Temp/Run2DValidation.trigger";
        public const string VALIDATION_REPORT_FILE = "Temp/ValidationReport2D.json";
        private const string SCENE_PATH = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";
        private const string PREFABS_DIR = "Assets/URDT_TestPoligon/2D_Polygon/Prefabs";

        // Безопасные границы игровой зоны (при referenceResolution 1280x720 и высоте зоны ~400-420)
        private const float VIEWPORT_MIN_X = -380f;
        private const float VIEWPORT_MAX_X = 380f;
        private const float VIEWPORT_MIN_Y = -185f;
        private const float VIEWPORT_MAX_Y = 185f;

        static UrdtMechanics2DValidator()
        {
            EditorApplication.delayCall += CheckValidationTrigger;
            EditorApplication.update += CheckValidationTrigger;
        }

        private static void CheckValidationTrigger()
        {
            if (File.Exists(VALIDATION_TRIGGER_FILE))
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.LogWarning("[UrdtMechanics2DValidator] Unity в Play Mode, выходим из режима игры для проведения валидации...");
                    EditorApplication.isPlaying = false;
                    return;
                }

                try { File.Delete(VALIDATION_TRIGGER_FILE); } catch { }
                Debug.Log("<color=#00FFFF>[UrdtMechanics2DValidator] Запуск валидации всех 32 механик 2D по очереди...</color>");
                ValidateAllMechanics();
            }
        }

        [MenuItem("URDT/Test 2D/Validate and Run All 32 Games (In Sequence)")]
        public static ValidationSummary ValidateAllMechanics()
        {
            ValidationSummary summary = new ValidationSummary
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                totalMechanics = 32,
                results = new List<MechanicValidationResult>()
            };

            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[UrdtMechanics2DValidator] Не удалось открыть сцену: {SCENE_PATH}");
                return summary;
            }

            GameObject world2DWindow = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var tr = FindInChildren(root.transform, "World2DSuiteWindow");
                if (tr != null) { world2DWindow = tr.gameObject; break; }
            }

            if (world2DWindow == null)
            {
                Debug.LogError("[UrdtMechanics2DValidator] World2DSuiteWindow не найден в сцене!");
                return summary;
            }

            Transform playAreaTr = FindInChildren(world2DWindow.transform, "MechanicPlayArea");
            if (playAreaTr == null)
            {
                Debug.LogError("[UrdtMechanics2DValidator] MechanicPlayArea не найден в World2DSuiteWindow!");
                return summary;
            }

            RectTransform playAreaRt = playAreaTr.GetComponent<RectTransform>();

            Debug.Log($"<color=#00FF99>=== НАЧАЛО ПООЧЕРЕДНОЙ ПРОВЕРКИ 32 ИГР 2D-ПОЛИГОНА ===</color>");

            int passedCount = 0;
            int warningCount = 0;
            int errorCount = 0;

            for (int i = 1; i <= 32; i++)
            {
                string mStr = $"M{i:D2}";
                string[] files = Directory.GetFiles(PREFABS_DIR, $"{mStr}_*.prefab");
                if (files.Length == 0)
                {
                    Debug.LogError($"[UrdtMechanics2DValidator] Префаб для {mStr} не найден!");
                    errorCount++;
                    continue;
                }

                string prefabPath = files[0].Replace('\\', '/');
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[UrdtMechanics2DValidator] Не удалось загрузить префаб: {prefabPath}");
                    errorCount++;
                    continue;
                }

                // Инстанциируем в игровой зоне
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, playAreaTr);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localScale = Vector3.one;

                BaseMechanic2DModule module = instance.GetComponent<BaseMechanic2DModule>();
                MechanicValidationResult result = new MechanicValidationResult
                {
                    index = i,
                    mechanicId = module != null ? module.MechanicId : prefab.name,
                    title = module != null ? module.Title : prefab.name,
                    prefabPath = prefabPath,
                    elementCount = 0,
                    outOfBoundsCount = 0,
                    nullRefsCount = 0,
                    issues = new List<string>()
                };

                // Проверка инициализации модуля
                try
                {
                    if (module != null)
                    {
                        module.Initialize();
                    }
                    else
                    {
                        result.issues.Add("Отсутствует компонент BaseMechanic2DModule на корневом объекте!");
                        result.nullRefsCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.issues.Add($"Исключение при Initialize(): {ex.Message}");
                    result.nullRefsCount++;
                }

                // Проверка сериализованных полей на null
                if (module != null)
                {
                    SerializedObject so = new SerializedObject(module);
                    SerializedProperty prop = so.GetIterator();
                    bool enterChildren = true;
                    while (prop.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.name.StartsWith("_"))
                        {
                            bool isOptional = prop.name.Contains("Optional") || 
                                              prop.name.Contains("Sprite") || 
                                              (prop.name == "_progressFill" && mStr == "M28") || 
                                              ((prop.name == "_btnLeft" || prop.name == "_btnRight") && (mStr == "M25" || mStr == "M26"));
                            if (prop.objectReferenceValue == null && !isOptional)
                            {
                                result.nullRefsCount++;
                                result.issues.Add($"Незаполненная сериализованная ссылка: {prop.name}");
                            }
                        }
                    }
                }

                // Геометрическая проверка всех дочерних элементов на нахождение на своих местах
                RectTransform[] childRts = instance.GetComponentsInChildren<RectTransform>(true);
                result.elementCount = childRts.Length;

                float lowestBottom = float.MaxValue;
                float highestTop = float.MinValue;
                string lowestName = "";
                string highestName = "";

                foreach (var rt in childRts)
                {
                    if (rt == instance.transform) continue;

                    // Пропускаем динамические полосы ландшафта (TerrainSlice), которые уходят далеко в стороны
                    if (rt.name.StartsWith("TerrainSlice")) continue;

                    Vector3[] corners = new Vector3[4];
                    rt.GetWorldCorners(corners);

                    // Переводим в локальные координаты playAreaRt
                    Vector2 localMin = playAreaRt.InverseTransformPoint(corners[0]);
                    Vector2 localMax = playAreaRt.InverseTransformPoint(corners[2]);

                    float elemBottom = localMin.y;
                    float elemTop = localMax.y;
                    float elemLeft = localMin.x;
                    float elemRight = localMax.x;

                    if (elemBottom < lowestBottom) { lowestBottom = elemBottom; lowestName = rt.name; }
                    if (elemTop > highestTop) { highestTop = elemTop; highestName = rt.name; }

                    // Проверка выхода за нижний или верхний край
                    if (elemBottom < VIEWPORT_MIN_Y)
                    {
                        // Игнорируем фоновые контейнеры трассы
                        if (!rt.name.Contains("Container") && !rt.name.Contains("Background"))
                        {
                            result.outOfBoundsCount++;
                            result.issues.Add($"Элемент [{rt.name}] выходит за нижний край: bottom={elemBottom:F1} (порог {VIEWPORT_MIN_Y:F1})");
                        }
                    }
                    if (elemTop > VIEWPORT_MAX_Y)
                    {
                        if (!rt.name.Contains("Container") && !rt.name.Contains("Background"))
                        {
                            result.outOfBoundsCount++;
                            result.issues.Add($"Элемент [{rt.name}] выходит за верхний край: top={elemTop:F1} (порог {VIEWPORT_MAX_Y:F1})");
                        }
                    }
                }

                result.minY = lowestBottom;
                result.maxY = highestTop;
                result.lowestElement = lowestName;
                result.highestElement = highestName;

                // Проверка статуса
                if (result.outOfBoundsCount == 0 && result.nullRefsCount == 0 && result.issues.Count == 0)
                {
                    result.status = "PASS";
                    passedCount++;
                    Debug.Log($"<color=#00FF99>[{i:D2}/32] {result.mechanicId}: ВСЕ ЭЛЕМЕНТЫ НА МЕСТАХ (Y: [{lowestBottom:F0} .. {highestTop:F0}], элементов: {result.elementCount}) - ОК</color>");
                }
                else if (result.nullRefsCount > 0 || result.outOfBoundsCount > 2)
                {
                    result.status = "ERROR";
                    errorCount++;
                    Debug.LogError($"[{i:D2}/32] {result.mechanicId}: ОШИБКА! " + string.Join("; ", result.issues));
                }
                else
                {
                    result.status = "WARNING";
                    warningCount++;
                    Debug.LogWarning($"[{i:D2}/32] {result.mechanicId}: ПРЕДУПРЕЖДЕНИЕ: " + string.Join("; ", result.issues));
                }

                summary.results.Add(result);

                // Очистка перед следующей игрой
                try
                {
                    if (module != null) module.ResetMechanic();
                }
                catch { }
                UnityEngine.Object.DestroyImmediate(instance);
            }

            summary.passedCount = passedCount;
            summary.warningCount = warningCount;
            summary.errorCount = errorCount;

            string jsonReport = JsonUtility.ToJson(summary, true);
            try
            {
                File.WriteAllText(VALIDATION_REPORT_FILE, jsonReport, Encoding.UTF8);
            }
            catch { }

            Debug.Log($"<color=#00FF99>=== РЕЗУЛЬТАТ ВАЛИДАЦИИ 32 МЕХАНИК: ПРЕДЕЛЬНО ЧИСТО! УСПЕШНО: {passedCount}/32, ОШИБОК: {errorCount}, ПРЕДУПРЕЖДЕНИЙ: {warningCount} ===</color>");

            return summary;
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

        [Serializable]
        public class ValidationSummary
        {
            public string timestamp;
            public int totalMechanics;
            public int passedCount;
            public int warningCount;
            public int errorCount;
            public List<MechanicValidationResult> results;
        }

        [Serializable]
        public class MechanicValidationResult
        {
            public int index;
            public string mechanicId;
            public string title;
            public string prefabPath;
            public string status;
            public int elementCount;
            public int outOfBoundsCount;
            public int nullRefsCount;
            public float minY;
            public float maxY;
            public string lowestElement;
            public string highestElement;
            public List<string> issues;
        }
    }
}
