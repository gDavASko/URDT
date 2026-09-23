using KBP.URDT.Net;
using KBP.URDT.Transport;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Registers the full core command set into a <see cref="CommandRouter"/> (M5.3), each
    /// handler bound to the shared <see cref="UrdtRuntime"/>.
    /// </summary>
    public static class UrdtCommandRegistrar
    {
        public static void RegisterAll(CommandRouter router, UrdtRuntime runtime)
        {
            if (router == null || runtime == null)
            {
                return;
            }

            router.Register(ProtocolConstants.ACTION_INSPECT, new InspectHandler(runtime));
            router.Register(ProtocolConstants.ACTION_QUERY, new QueryHandler(runtime));
            router.Register(ProtocolConstants.ACTION_HIT_TEST, new HitTestHandler(runtime));
            router.Register(ProtocolConstants.ACTION_CLICK, new ClickHandler(runtime));
            router.Register(ProtocolConstants.ACTION_DOUBLE_CLICK, new DoubleClickHandler(runtime));
            router.Register(ProtocolConstants.ACTION_DRAG, new DragHandler(runtime));
            router.Register(ProtocolConstants.ACTION_INPUT_STATUS, new InputStatusHandler(runtime));
            router.Register(ProtocolConstants.ACTION_PRESS_MOVE, new PressMoveHandler(runtime));
            router.Register(ProtocolConstants.ACTION_SWIPE, new SwipeHandler(runtime));
            router.Register(ProtocolConstants.ACTION_SCROLL, new ScrollHandler(runtime));
            router.Register(ProtocolConstants.ACTION_KEY_PRESS, new KeyPressHandler(runtime));
            router.Register(ProtocolConstants.ACTION_TYPE_TEXT, new TypeTextHandler(runtime));
            router.Register(ProtocolConstants.ACTION_MULTI_CLICK, new MultiClickHandler(runtime));
            router.Register(ProtocolConstants.ACTION_MULTI_DRAG, new MultiDragHandler(runtime));
            router.Register(ProtocolConstants.ACTION_PINCH, new PinchHandler(runtime));
            router.Register(ProtocolConstants.ACTION_STEP_FRAME, new StepFrameHandler(runtime));
            router.Register(ProtocolConstants.ACTION_WAIT_FOR, new WaitForHandler(runtime));
            router.Register(ProtocolConstants.ACTION_RESET_STATE, new ResetStateHandler(runtime));
            router.Register(ProtocolConstants.ACTION_CAPTURE, new CaptureHandler(runtime));
            router.Register(ProtocolConstants.ACTION_SUBSCRIBE, new SubscribeHandler(runtime));
            router.Register(ProtocolConstants.ACTION_UNSUBSCRIBE, new UnsubscribeHandler(runtime));
            router.Register(ProtocolConstants.ACTION_SET_TIME_SCALE, new SetTimeScaleHandler(runtime));
            router.Register(ProtocolConstants.ACTION_PIN_FIXED_DELTA, new PinFixedDeltaHandler(runtime));
            router.Register(ProtocolConstants.ACTION_POINTER_DOWN, new PointerDownHandler(runtime));
            router.Register(ProtocolConstants.ACTION_POINTER_UP, new PointerUpHandler(runtime));
        }
    }
}
