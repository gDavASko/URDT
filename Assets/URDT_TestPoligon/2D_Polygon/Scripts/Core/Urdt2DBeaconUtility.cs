using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KBP.URDT.Inspect;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Utility for automatically instrumenting 2D minigame hierarchies with URDT beacons.
    /// Ensures consistent, zero-boilerplate beacon placement across all 32 mechanics.
    /// </summary>
    public static class Urdt2DBeaconUtility
    {
        /// <summary>
        /// Instruments the entire hierarchy of a 2D mechanic module with URDT beacons.
        /// </summary>
        public static void InstrumentHierarchy(BaseMechanic2DModule module)
        {
            if (module == null) return;

            // 1. Root module target
            EnsureModuleTarget(module);

            // 2. Instrument all children
            Transform root = module.transform;
            InstrumentChildrenRecursive(root, module);
        }

        public static Urdt2DModuleTarget EnsureModuleTarget(BaseMechanic2DModule module)
        {
            if (module == null) return null;

            if (!module.TryGetComponent(out Urdt2DModuleTarget target))
            {
                target = module.gameObject.AddComponent<Urdt2DModuleTarget>();
            }

            target.TargetId = string.IsNullOrEmpty(module.MechanicId) ? module.gameObject.name : module.MechanicId;
            target.MechanicId = module.MechanicId;
            target.MechanicTitle = module.Title;
            target.Instruction = module.Instruction;
            target.IsCompleted = module.IsCompleted;
            target.ProgressNormalized = module.ProgressNormalized;

            return target;
        }

        public static void SyncModuleState(BaseMechanic2DModule module)
        {
            if (module == null) return;
            if (module.TryGetComponent(out Urdt2DModuleTarget target))
            {
                target.IsCompleted = module.IsCompleted;
                target.ProgressNormalized = module.ProgressNormalized;
            }
        }

        private static void InstrumentChildrenRecursive(Transform parent, BaseMechanic2DModule module)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                GameObject go = child.gameObject;

                InstrumentGameObject(go);

                if (child.childCount > 0)
                {
                    InstrumentChildrenRecursive(child, module);
                }
            }
        }

        public static void InstrumentGameObject(GameObject go)
        {
            // One beacon per object: a re-initialized mechanic may already carry a beacon from a previous pass
            // (or from Urdt2DEntityInstrumentation); adding a second UrdtDebugTarget fails and returned null.
            if (go == null || go.TryGetComponent(out UrdtDebugTarget _)) return;

            string nameLower = go.name.ToLowerInvariant();

            // Ignore system/internal UI overlays and visualizers
            if (nameLower.Contains("urdt_visualizer") || nameLower.Contains("event_system") || nameLower.Contains("canvas_debug"))
            {
                return;
            }

            // Check if object has a Draggable component or item signature
            bool isDraggable = HasDraggableComponent(go) ||
                               (nameLower.Contains("item") && !nameLower.Contains("container") && !nameLower.Contains("slot")) ||
                               nameLower.Contains("token") || nameLower.Contains("part") || nameLower.Contains("chip") ||
                               nameLower.Contains("tool") || nameLower.Contains("plank") || nameLower.Contains("brush");

            // Check if object is a slot, container, pan, or drop zone
            bool isSlot = HasSlotComponent(go) ||
                          nameLower.Contains("slot") || nameLower.Contains("bucket") || nameLower.Contains("container") ||
                          nameLower.Contains("pan") || nameLower.Contains("socket") || nameLower.Contains("dropzone") ||
                          nameLower.Contains("target_can") || nameLower.Contains("targetcan");

            // Check if object is an interactive tile, dial, or area
            bool isInteractiveArea = HasInteractiveAreaComponent(go) ||
                                     nameLower.Contains("tile") || nameLower.Contains("pipe") || nameLower.Contains("segment") ||
                                     nameLower.Contains("halves") || nameLower.Contains("wheel") || nameLower.Contains("rotary");

            // Check if object has a standard Button
            bool hasButton = go.GetComponent<Button>() != null;

            // Check if object is a text display
            bool hasText = go.GetComponent<TextMeshProUGUI>() != null || go.GetComponent<TMP_Text>() != null || go.GetComponent<Text>() != null;

            if (isDraggable && !go.TryGetComponent(out Urdt2DDraggableTarget _))
            {
                var dt = go.AddComponent<Urdt2DDraggableTarget>();
                dt.TargetId = go.name;
                dt.AutoDetectItemData();
            }
            else if (isSlot && !go.TryGetComponent(out Urdt2DSlotTarget _))
            {
                var st = go.AddComponent<Urdt2DSlotTarget>();
                st.TargetId = go.name;
                st.AutoDetectSlotData();
            }
            else if (hasButton && !go.TryGetComponent(out UrdtUiButtonTarget _))
            {
                var bt = go.AddComponent<UrdtUiButtonTarget>();
                bt.TargetId = go.name;
            }
            else if (isInteractiveArea && !go.TryGetComponent(out Urdt2DInteractiveAreaTarget _))
            {
                var at = go.AddComponent<Urdt2DInteractiveAreaTarget>();
                at.TargetId = go.name;
                at.AreaId = go.name;
            }
            else if (hasText && !hasButton && !isDraggable && !isSlot && !go.TryGetComponent(out UrdtUiTextTarget _))
            {
                // Only attach text target to meaningful text displays (instructions, scores, timers, labels)
                if (nameLower.Contains("instr") || nameLower.Contains("label") || nameLower.Contains("score") ||
                    nameLower.Contains("time") || nameLower.Contains("title") || nameLower.Contains("status") ||
                    nameLower.Contains("progress") || nameLower.Contains("text"))
                {
                    var tt = go.AddComponent<UrdtUiTextTarget>();
                    tt.TargetId = go.name;
                }
            }
        }

        private static bool HasDraggableComponent(GameObject go)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName.Contains("Draggable") || typeName.Contains("Drag") ||
                    typeName.Contains("WeightItem") || typeName.Contains("AttachmentPart") ||
                    typeName.Contains("CommandChip") || typeName.Contains("NodePin") ||
                    typeName.Contains("DentalForceps") || typeName.Contains("WobbleItem") ||
                    typeName.Contains("WaypointTracker") || typeName.Contains("SpongeBrush") ||
                    typeName.Contains("BridgePlank") || typeName.Contains("DressUpItem"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasSlotComponent(GameObject go)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName.Contains("Slot") || typeName.Contains("Bucket") || typeName.Contains("Pan") ||
                    typeName.Contains("Container") || typeName.Contains("DirtCell") ||
                    typeName.Contains("SlingshotTarget") || typeName.Contains("HiddenRevealTarget") ||
                    typeName.Contains("PoppableTarget"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasInteractiveAreaComponent(GameObject go)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName.Contains("GridTile") || typeName.Contains("PipeTile") ||
                    typeName.Contains("ColoringSegment") || typeName.Contains("HoldButton") ||
                    typeName.Contains("RotaryWheel") || typeName.Contains("Proxy"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
