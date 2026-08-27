using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Delivers keyboard events to the currently focused uGUI input field.
    /// <para>
    /// URDT drives a virtual Input System keyboard (<c>QueueTextEvent</c>/<c>KeyboardState</c>),
    /// which is the honest device path. However <c>TMP_InputField</c>/<c>InputField</c> read
    /// characters exclusively from the legacy IMGUI <c>Event.PopEvent</c> queue, and on the new
    /// Input System that queue is not fed by synthetic devices. So a device-level keystroke never
    /// reaches the focused field. This bridge closes that gap by handing the focused field the same
    /// <see cref="Event"/> a real keyboard would produce, via the field's public
    /// <c>ProcessEvent(Event)</c> — it does NOT set text or invoke value callbacks directly.
    /// Reflection keeps URDT core free of a hard TextMeshPro dependency.
    /// </para>
    /// </summary>
    internal static class UiKeyboardBridge
    {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type[] EVENT_SIG = { typeof(Event) };

        /// <summary>Sends printable text to the focused field, one KeyDown event per character.</summary>
        public static int SendText(string text)
        {
            if (string.IsNullOrEmpty(text) || !TryGetProcessEvent(out MethodInfo process, out object field))
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < text.Length; i++)
            {
                Event evt = new Event { type = EventType.KeyDown, character = text[i], keyCode = KeyCode.None, modifiers = EventModifiers.None };
                if (TryProcess(process, field, evt))
                {
                    delivered++;
                }
            }

            return delivered;
        }

        /// <summary>Sends a single control/character key (Backspace, arrows, Enter, a printable char) to the focused field.</summary>
        public static bool SendKey(KeyCode keyCode, char character)
        {
            if (!TryGetProcessEvent(out MethodInfo process, out object field))
            {
                return false;
            }

            Event evt = new Event { type = EventType.KeyDown, keyCode = keyCode, character = character, modifiers = EventModifiers.None };
            return TryProcess(process, field, evt);
        }

        private static bool TryProcess(MethodInfo process, object field, Event evt)
        {
            try
            {
                process.Invoke(field, new object[] { evt });
                // Regenerate the field's text mesh so textInfo.characterInfo is valid for the next
                // event (navigation keys index into it). ProcessEvent outside the normal update
                // flow does not schedule this, which otherwise throws IndexOutOfRange on MoveLeft.
                ForceFieldUpdate(field);
                return true;
            }
            catch (TargetInvocationException tie)
            {
                Debug.LogWarning("URDT UiKeyboardBridge ProcessEvent failed (keyCode=" + evt.keyCode +
                    ", char=" + ((int)evt.character) + "): " + (tie.InnerException != null ? tie.InnerException.ToString() : tie.ToString()));
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("URDT UiKeyboardBridge ProcessEvent error: " + exception);
                return false;
            }
        }

        /// <summary>Whether a uGUI input field is currently focused and can receive keyboard events.</summary>
        public static bool HasFocusedField()
        {
            return TryGetProcessEvent(out _, out _);
        }

        private static void ForceFieldUpdate(object field)
        {
            try
            {
                MethodInfo force = field.GetType().GetMethod("ForceLabelUpdate", FLAGS, null, Type.EmptyTypes, null);
                if (force != null)
                {
                    force.Invoke(field, null);
                }
            }
            catch (Exception)
            {
                // Best-effort: a missing/failed label refresh must not fail the keystroke.
            }
        }

        private static bool TryGetProcessEvent(out MethodInfo process, out object field)
        {
            process = null;
            field = null;

            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }

            Component[] components = selected.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                // TMP_InputField and UnityEngine.UI.InputField both expose `void ProcessEvent(Event)`.
                MethodInfo method = component.GetType().GetMethod("ProcessEvent", FLAGS, null, EVENT_SIG, null);
                if (method != null)
                {
                    process = method;
                    field = component;
                    return true;
                }
            }

            return false;
        }
    }
}
