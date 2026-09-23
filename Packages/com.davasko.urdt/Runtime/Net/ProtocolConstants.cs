namespace KBP.URDT.Net
{
    /// <summary>
    /// Protocol-level constants (engine-agnostic, no UnityEngine types).
    /// </summary>
    public static class ProtocolConstants
    {
        /// <summary>Wire API major version. Incompatible clients get E_API_VERSION.</summary>
        public const int API_VERSION = 1;

        public const string SERVER_VERSION = "0.1.0";

        /// <summary>Input coordinate origin reported in handshake (Input System / EventSystem convention).</summary>
        public const string ORIGIN_BOTTOM_LEFT = "bottom-left";

        public const string TYPE_RESPONSE = "response";
        public const string TYPE_EVENT = "event";

        public const string STATUS_OK = "ok";
        public const string STATUS_ERROR = "error";
        public const string STATUS_READY = "ready";

        public const string ACTION_HANDSHAKE = "handshake";
        public const string ACTION_PING = "ping";
        public const string ACTION_HEALTH = "health";
        public const string ACTION_INSPECT = "inspect";
        public const string ACTION_QUERY = "query";
        public const string ACTION_HIT_TEST = "hit_test";
        public const string ACTION_CLICK = "click";
        public const string ACTION_DOUBLE_CLICK = "double_click";
        public const string ACTION_DRAG = "drag";
        public const string ACTION_PRESS_MOVE = "press_move";
        public const string ACTION_SWIPE = "swipe";
        public const string ACTION_SCROLL = "scroll";
        public const string ACTION_KEY_PRESS = "key_press";
        public const string ACTION_TYPE_TEXT = "type_text";
        public const string ACTION_MULTI_CLICK = "multi_click";
        public const string ACTION_MULTI_DRAG = "multi_drag";
        public const string ACTION_PINCH = "pinch";
        public const string ACTION_STEP_FRAME = "step_frame";
        public const string ACTION_WAIT_FOR = "wait_for";
        public const string ACTION_RESET_STATE = "reset_state";
        public const string ACTION_CAPTURE = "capture";
        public const string ACTION_SUBSCRIBE = "subscribe";
        public const string ACTION_UNSUBSCRIBE = "unsubscribe";
        public const string ACTION_SET_TIME_SCALE = "set_time_scale";
        public const string ACTION_PIN_FIXED_DELTA = "pin_fixed_delta";
        public const string ACTION_POINTER_DOWN = "pointer_down";
        public const string ACTION_POINTER_UP = "pointer_up";
        public const string ACTION_INPUT_STATUS = "input_status";

        /// <summary>Payload value of <c>pacing</c> that plays a gesture one pointer state per game frame.</summary>
        public const string PACING_REALTIME = "realtime";

        /// <summary>Namespace prefix for game-provided arrange-only test seams (protocol §4.14).</summary>
        public const string CUSTOM_PREFIX = "custom:";

        public const string EVENT_OBJECT_REGISTERED = "object_registered";
        public const string EVENT_OBJECT_UNREGISTERED = "object_unregistered";
        public const string EVENT_SCENE_LOADED = "scene_loaded";
        public const string EVENT_LOG_ERROR = "log_error";
        public const string EVENT_SESSION_WARNING = "session_warning";
        public const string EVENT_SHUTTING_DOWN = "shutting_down";

        private static readonly string[] KNOWN_EVENTS =
        {
            EVENT_SCENE_LOADED,
            EVENT_OBJECT_REGISTERED,
            EVENT_OBJECT_UNREGISTERED,
            EVENT_LOG_ERROR,
            EVENT_SESSION_WARNING,
            EVENT_SHUTTING_DOWN
        };

        public static bool IsKnownEvent(string eventName)
        {
            for (int i = 0; i < KNOWN_EVENTS.Length; i++)
            {
                if (string.Equals(KNOWN_EVENTS[i], eventName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
