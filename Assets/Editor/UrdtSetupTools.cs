using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UrdtSetup
{
    /// <summary>
    /// One-shot project setup helpers invoked headlessly via `unity run -- -executeMethod`.
    /// Kept out of the URDT package itself: this is project-scaffolding, not shipped code.
    /// </summary>
    public static class UrdtSetupTools
    {
        /// <summary>
        /// Imports TextMesh Pro Essential Resources (TMP_Settings + default font) without the
        /// interactive dialog, so the polygon scene's TMP components resolve at runtime.
        /// Invoked via: unity run &lt;proj&gt; -- -executeMethod UrdtSetup.UrdtSetupTools.ImportTmpEssentials
        /// </summary>
        public static void ImportTmpEssentials()
        {
            Type importer = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter"))
                .FirstOrDefault(t => t != null);

            if (importer == null)
            {
                Debug.LogError("URDT setup: TMP_PackageResourceImporter type not found.");
                EditorApplication.Exit(2);
                return;
            }

            MethodInfo import = importer.GetMethod(
                "ImportResources",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(bool), typeof(bool), typeof(bool) },
                null);

            if (import == null)
            {
                Debug.LogError("URDT setup: ImportResources(bool,bool,bool) not found.");
                EditorApplication.Exit(3);
                return;
            }

            // importEssentials: true, importExamples: false, interactive: false
            import.Invoke(null, new object[] { true, false, false });
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("URDT setup: TMP Essential Resources import invoked.");
        }
    }
}
