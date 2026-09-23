using System;
using System.Collections.Generic;
using System.Linq;
using KBP.URDT.TestPoligon.Mechanics2D.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KBP.URDT.TestPoligon.Editor
{
    /// <summary>
    /// Adds the self-building arcade mechanics (M33–M36) to the 2D suite: creates a prefab per mechanic (an empty
    /// full-stretch root with the mechanic component and its metadata) and appends it to the host's list in the
    /// UI scene, keeping the existing 32 mechanics untouched. Idempotent: re-running updates prefabs and skips
    /// entries that are already in the list.
    /// </summary>
    public static class UrdtArcadeMechanicsInstaller
    {
        private const string PREFABS_DIR = "Assets/URDT_TestPoligon/2D_Polygon/Prefabs";
        private const string SCENE_PATH = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";

        private struct Def
        {
            public string Id;
            public string TypeName;
            public string Title;
            public string Instruction;
        }

        private static readonly Def[] Defs =
        {
            new Def { Id = "M33_Arkanoid", TypeName = "KBP.URDT.TestPoligon.Mechanics2D.M33_Arkanoid.M33_ArkanoidMechanic",
                Title = "Механика #33: Арканоид", Instruction = "Отбивайте мяч платформой и разбейте все блоки. Не упустите мяч!" },
            new Def { Id = "M34_Snake", TypeName = "KBP.URDT.TestPoligon.Mechanics2D.M34_Snake.M34_SnakeMechanic",
                Title = "Механика #34: Змейка", Instruction = "Управляйте змейкой и соберите еду. Не врезайтесь в стены и в себя!" },
            new Def { Id = "M35_Merge2", TypeName = "KBP.URDT.TestPoligon.Mechanics2D.M35_Merge2.M35_Merge2Mechanic",
                Title = "Механика #35: Мерж-2", Instruction = "Создавайте предметы и объединяйте одинаковые, чтобы получить предмет нужного уровня." },
            new Def { Id = "M36_Match3", TypeName = "KBP.URDT.TestPoligon.Mechanics2D.M36_Match3.M36_Match3Mechanic",
                Title = "Механика #36: Три в ряд", Instruction = "Меняйте соседние камни местами, собирая линии из трёх и более одного цвета." },
        };

        [MenuItem("Tools/URDT 2D Polygon/Add Arcade Mechanics (M33-M36)")]
        public static void Install()
        {
            var builtPaths = new List<string>();
            foreach (Def def in Defs)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(def.TypeName, false))
                    .FirstOrDefault(t => t != null);
                if (type == null)
                {
                    Debug.LogError($"[Arcade] type not found: {def.TypeName}");
                    continue;
                }

                var root = new GameObject(def.Id, typeof(RectTransform));
                var rt = root.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var mech = (BaseMechanic2DModule)root.AddComponent(type);
                var so = new SerializedObject(mech);
                so.FindProperty("_mechanicId").stringValue = def.Id;
                so.FindProperty("_title").stringValue = def.Title;
                so.FindProperty("_instruction").stringValue = def.Instruction;
                so.ApplyModifiedPropertiesWithoutUndo();

                string path = $"{PREFABS_DIR}/{def.Id}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                builtPaths.Add(path);
                Debug.Log($"[Arcade] prefab saved: {path}");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Arcade] exit Play Mode first");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var built = builtPaths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).Select(g => g != null ? g.GetComponent<BaseMechanic2DModule>() : null).ToList();
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            Mechanic2DHost host = UnityEngine.Object.FindAnyObjectByType<Mechanic2DHost>(FindObjectsInactive.Include);
            if (host == null)
            {
                Debug.LogError("[Arcade] Mechanic2DHost not found in the UI scene");
                return;
            }

            var hostSo = new SerializedObject(host);
            SerializedProperty list = hostSo.FindProperty("_mechanicPrefabs");
            var existing = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var m = list.GetArrayElementAtIndex(i).objectReferenceValue as BaseMechanic2DModule;
                if (m != null) existing.Add(m.MechanicId);
            }

            int added = 0;
            foreach (BaseMechanic2DModule m in built)
            {
                if (m == null) { Debug.LogWarning("[Arcade] built prefab has no mechanic component"); continue; }
                if (existing.Contains(m.MechanicId)) { Debug.Log($"[Arcade] {m.MechanicId} already in the list"); continue; }
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = m;
                added++;
            }

            hostSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FF99>[Arcade] {built.Count} prefabs built, {added} added to the 2D suite (total {list.arraySize}).</color>");
        }
    }
}
