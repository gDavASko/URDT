/**
 * Catalog of 9 Motor Primitives (L1 Kinematics)
 * Factory for creating structured parametric gestures and touch sequences.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-3
 */
export type MotorPrimitiveType = 'TAP' | 'DRAG' | 'SWIPE' | 'SLICE' | 'HOLD_EVENT_CONDITIONED' | 'CHARGE_AND_RELEASE' | 'CONTINUOUS_STEER_STREAM' | 'PINCH_ZOOM' | 'QTE_TIMED_TAP';
export interface BaseMotorCommand {
    primitive: MotorPrimitiveType;
    pointerId: number;
    targetBeaconId?: string;
    idempotencyKey: string;
}
export interface TapCommand extends BaseMotorCommand {
    primitive: 'TAP';
    pos: {
        x: number;
        y: number;
    };
    holdDurationMs: number;
}
export interface DragCommand extends BaseMotorCommand {
    primitive: 'DRAG';
    startPos: {
        x: number;
        y: number;
    };
    endPos: {
        x: number;
        y: number;
    };
    durationMs: number;
    speedProfile: 'minimum_jerk' | 'linear';
}
export interface SwipeCommand extends BaseMotorCommand {
    primitive: 'SWIPE';
    startPos: {
        x: number;
        y: number;
    };
    velocityVector: {
        vx: number;
        vy: number;
    };
    durationMs: number;
}
export interface SliceCommand extends BaseMotorCommand {
    primitive: 'SLICE';
    waypoints: Array<{
        x: number;
        y: number;
    }>;
    totalDurationMs: number;
}
export interface HoldEventCommand extends BaseMotorCommand {
    primitive: 'HOLD_EVENT_CONDITIONED';
    pos: {
        x: number;
        y: number;
    };
    targetEvent: string;
    maxTimeoutMs: number;
}
export interface ChargeReleaseCommand extends BaseMotorCommand {
    primitive: 'CHARGE_AND_RELEASE';
    pos: {
        x: number;
        y: number;
    };
    chargeTimeMs: number;
    releasePos?: {
        x: number;
        y: number;
    };
}
export interface ContinuousSteerCommand extends BaseMotorCommand {
    primitive: 'CONTINUOUS_STEER_STREAM';
    stickCenter: {
        x: number;
        y: number;
    };
    steerVector: {
        x: number;
        y: number;
    };
}
export interface PinchZoomCommand extends BaseMotorCommand {
    primitive: 'PINCH_ZOOM';
    centerPos: {
        x: number;
        y: number;
    };
    startDistance: number;
    endDistance: number;
    durationMs: number;
}
export interface QteTimedTapCommand extends BaseMotorCommand {
    primitive: 'QTE_TIMED_TAP';
    pos: {
        x: number;
        y: number;
    };
    qteWindow: {
        minMs: number;
        maxMs: number;
    };
}
export type MotorCommand = TapCommand | DragCommand | SwipeCommand | SliceCommand | HoldEventCommand | ChargeReleaseCommand | ContinuousSteerCommand | PinchZoomCommand | QteTimedTapCommand;
export declare class MotorPrimitivesFactory {
    private static sequence;
    private static generateKey;
    static createTap(pos: {
        x: number;
        y: number;
    }, pointerId?: number, holdMs?: number, beaconId?: string): TapCommand;
    static createDrag(startPos: {
        x: number;
        y: number;
    }, endPos: {
        x: number;
        y: number;
    }, durationMs?: number, pointerId?: number, beaconId?: string): DragCommand;
    static createSwipe(startPos: {
        x: number;
        y: number;
    }, velocityVector: {
        vx: number;
        vy: number;
    }, durationMs?: number, pointerId?: number): SwipeCommand;
    static createSlice(waypoints: Array<{
        x: number;
        y: number;
    }>, durationMs?: number, pointerId?: number): SliceCommand;
    static createHoldConditioned(pos: {
        x: number;
        y: number;
    }, targetEvent: string, maxTimeoutMs?: number, pointerId?: number): HoldEventCommand;
    static createChargeAndRelease(pos: {
        x: number;
        y: number;
    }, chargeTimeMs?: number, releasePos?: {
        x: number;
        y: number;
    }, pointerId?: number): ChargeReleaseCommand;
    static createContinuousSteer(stickCenter: {
        x: number;
        y: number;
    }, steerVector: {
        x: number;
        y: number;
    }, pointerId?: number): ContinuousSteerCommand;
    static createPinchZoom(centerPos: {
        x: number;
        y: number;
    }, startDistance: number, endDistance: number, durationMs?: number): PinchZoomCommand;
    static createQteTimedTap(pos: {
        x: number;
        y: number;
    }, minMs: number, maxMs: number, pointerId?: number): QteTimedTapCommand;
}
