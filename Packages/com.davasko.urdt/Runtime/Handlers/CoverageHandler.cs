using System;
using System.Collections.Generic;
using System.Reflection;
using KBP.URDT.Inspect;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using URDT.Runtime.Inspectors;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>coverage</c>: lists active, player-interactable objects the agent cannot see — no
    /// <see cref="UrdtDebugTarget"/> on the object or its parents — plus objects that only react to legacy
    /// <c>OnMouse*</c> messages (unreachable for virtual input devices). Payload: <c>scope</c> (optional
    /// beacon TargetId limiting the scan to its hierarchy), <c>limit</c> (default 100).
    /// </summary>
    public sealed class CoverageHandler : ICommandHandler
    {
        private static readonly string[] LegacyMouseMessages = { "OnMouseDown", "OnMouseUp", "OnMouseUpAsButton", "OnMouseDrag" };
        private readonly UrdtRuntime _runtime;

        public CoverageHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject p = PayloadReader.AsObject(command.Payload) ?? new JObject();
            string scope = p.Value<string>("scope");
            int limit = p.Value<int?>("limit") ?? 100;

            Transform root = null;
            if (!string.IsNullOrEmpty(scope))
            {
                foreach (UrdtDebugTarget t in UnityEngine.Object.FindObjectsByType<UrdtDebugTarget>(FindObjectsSortMode.None))
                    if (string.Equals(t.TargetId, scope, StringComparison.Ordinal)) { root = t.transform; break; }
                if (root == null) return Response.Success(command.Id, new JObject { ["error"] = "scope not found", ["scope"] = scope });
            }

            var seen = new HashSet<int>();
            var missing = new JArray();
            int interactables = 0;

            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || !mb.isActiveAndEnabled || mb is UrdtDebugTarget) continue;
                GameObject go = mb.gameObject;
                if (root != null && !go.transform.IsChildOf(root)) continue;
                string ns = mb.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("KBP.URDT", StringComparison.Ordinal) || ns.StartsWith("URDT.", StringComparison.Ordinal)) continue;

                string reason = Classify(mb);
                if (reason == null) continue;
                interactables++;
                if (!seen.Add(go.GetInstanceID())) continue;

                bool legacy = reason == "legacy_OnMouse";
                if (!legacy && go.GetComponentInParent<UrdtDebugTarget>() != null) continue;
                if (missing.Count >= limit) continue;
                missing.Add(new JObject
                {
                    ["path"] = UrdtAutoInstrumentation.ComputeBeaconPath(go),
                    ["component"] = mb.GetType().Name,
                    ["reason"] = legacy ? "legacy_OnMouse" : "no_beacon",
                    ["detail"] = reason
                });
            }

            return Response.Success(command.Id, new JObject
            {
                ["scope"] = scope,
                ["interactables"] = interactables,
                ["missing_count"] = missing.Count,
                ["missing"] = missing
            });
        }

        /// <summary>Why this component makes its object player-interactable, or null.</summary>
        private static string Classify(MonoBehaviour mb)
        {
            if (mb is Selectable s) return s.IsInteractable() ? "ui_" + mb.GetType().Name : null;
            if (mb is Graphic) return null;
            if (mb is EventTrigger) return "event_trigger";
            if (mb is IPointerClickHandler || mb is IPointerDownHandler || mb is IBeginDragHandler || mb is IDragHandler)
                return "pointer_handler";
            Type t = mb.GetType();
            foreach (string m in LegacyMouseMessages)
                if (t.GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                    return "legacy_OnMouse";
            return null;
        }
    }
}
