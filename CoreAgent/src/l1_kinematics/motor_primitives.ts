/**
 * Catalog of 9 Motor Primitives (L1 Kinematics)
 * Factory for creating structured parametric gestures and touch sequences.
 * 
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-3
 */

export type MotorPrimitiveType =
  | 'TAP'
  | 'DRAG'
  | 'SWIPE'
  | 'SLICE'
  | 'HOLD_EVENT_CONDITIONED'
  | 'CHARGE_AND_RELEASE'
  | 'CONTINUOUS_STEER_STREAM'
  | 'PINCH_ZOOM'
  | 'QTE_TIMED_TAP';

export interface BaseMotorCommand {
  primitive: MotorPrimitiveType;
  pointerId: number; // 0..3
  targetBeaconId?: string;
  idempotencyKey: string;
}

export interface TapCommand extends BaseMotorCommand {
  primitive: 'TAP';
  pos: { x: number; y: number };
  holdDurationMs: number;
}

export interface DragCommand extends BaseMotorCommand {
  primitive: 'DRAG';
  startPos: { x: number; y: number };
  endPos: { x: number; y: number };
  durationMs: number;
  speedProfile: 'minimum_jerk' | 'linear';
}

export interface SwipeCommand extends BaseMotorCommand {
  primitive: 'SWIPE';
  startPos: { x: number; y: number };
  velocityVector: { vx: number; vy: number };
  durationMs: number;
}

export interface SliceCommand extends BaseMotorCommand {
  primitive: 'SLICE';
  waypoints: Array<{ x: number; y: number }>;
  totalDurationMs: number;
}

export interface HoldEventCommand extends BaseMotorCommand {
  primitive: 'HOLD_EVENT_CONDITIONED';
  pos: { x: number; y: number };
  targetEvent: string;
  maxTimeoutMs: number;
}

export interface ChargeReleaseCommand extends BaseMotorCommand {
  primitive: 'CHARGE_AND_RELEASE';
  pos: { x: number; y: number };
  chargeTimeMs: number;
  releasePos?: { x: number; y: number };
}

export interface ContinuousSteerCommand extends BaseMotorCommand {
  primitive: 'CONTINUOUS_STEER_STREAM';
  stickCenter: { x: number; y: number };
  steerVector: { x: number; y: number }; // normalized -1.0 .. +1.0
}

export interface PinchZoomCommand extends BaseMotorCommand {
  primitive: 'PINCH_ZOOM';
  centerPos: { x: number; y: number };
  startDistance: number;
  endDistance: number;
  durationMs: number;
}

export interface QteTimedTapCommand extends BaseMotorCommand {
  primitive: 'QTE_TIMED_TAP';
  pos: { x: number; y: number };
  qteWindow: { minMs: number; maxMs: number };
}

export type MotorCommand =
  | TapCommand
  | DragCommand
  | SwipeCommand
  | SliceCommand
  | HoldEventCommand
  | ChargeReleaseCommand
  | ContinuousSteerCommand
  | PinchZoomCommand
  | QteTimedTapCommand;

export class MotorPrimitivesFactory {
  private static sequence = 1;

  private static generateKey(primitive: string): string {
    return `cmd_${primitive.toLowerCase()}_${Date.now()}_${MotorPrimitivesFactory.sequence++}`;
  }

  public static createTap(pos: { x: number; y: number }, pointerId = 0, holdMs = 50, beaconId?: string): TapCommand {
    return {
      primitive: 'TAP',
      pointerId,
      pos,
      holdDurationMs: holdMs,
      targetBeaconId: beaconId,
      idempotencyKey: MotorPrimitivesFactory.generateKey('tap')
    };
  }

  public static createDrag(
    startPos: { x: number; y: number },
    endPos: { x: number; y: number },
    durationMs = 350,
    pointerId = 0,
    beaconId?: string
  ): DragCommand {
    return {
      primitive: 'DRAG',
      pointerId,
      startPos,
      endPos,
      durationMs,
      speedProfile: 'minimum_jerk',
      targetBeaconId: beaconId,
      idempotencyKey: MotorPrimitivesFactory.generateKey('drag')
    };
  }

  public static createSwipe(
    startPos: { x: number; y: number },
    velocityVector: { vx: number; vy: number },
    durationMs = 200,
    pointerId = 0
  ): SwipeCommand {
    return {
      primitive: 'SWIPE',
      pointerId,
      startPos,
      velocityVector,
      durationMs,
      idempotencyKey: MotorPrimitivesFactory.generateKey('swipe')
    };
  }

  public static createSlice(
    waypoints: Array<{ x: number; y: number }>,
    durationMs = 300,
    pointerId = 0
  ): SliceCommand {
    return {
      primitive: 'SLICE',
      pointerId,
      waypoints,
      totalDurationMs: durationMs,
      idempotencyKey: MotorPrimitivesFactory.generateKey('slice')
    };
  }

  public static createHoldConditioned(
    pos: { x: number; y: number },
    targetEvent: string,
    maxTimeoutMs = 5000,
    pointerId = 0
  ): HoldEventCommand {
    return {
      primitive: 'HOLD_EVENT_CONDITIONED',
      pointerId,
      pos,
      targetEvent,
      maxTimeoutMs,
      idempotencyKey: MotorPrimitivesFactory.generateKey('hold')
    };
  }

  public static createChargeAndRelease(
    pos: { x: number; y: number },
    chargeTimeMs = 600,
    releasePos?: { x: number; y: number },
    pointerId = 0
  ): ChargeReleaseCommand {
    return {
      primitive: 'CHARGE_AND_RELEASE',
      pointerId,
      pos,
      chargeTimeMs,
      releasePos,
      idempotencyKey: MotorPrimitivesFactory.generateKey('charge')
    };
  }

  public static createContinuousSteer(
    stickCenter: { x: number; y: number },
    steerVector: { x: number; y: number },
    pointerId = 0
  ): ContinuousSteerCommand {
    return {
      primitive: 'CONTINUOUS_STEER_STREAM',
      pointerId,
      stickCenter,
      steerVector,
      idempotencyKey: MotorPrimitivesFactory.generateKey('steer')
    };
  }

  public static createPinchZoom(
    centerPos: { x: number; y: number },
    startDistance: number,
    endDistance: number,
    durationMs = 400
  ): PinchZoomCommand {
    return {
      primitive: 'PINCH_ZOOM',
      pointerId: 0,
      centerPos,
      startDistance,
      endDistance,
      durationMs,
      idempotencyKey: MotorPrimitivesFactory.generateKey('pinch')
    };
  }

  public static createQteTimedTap(
    pos: { x: number; y: number },
    minMs: number,
    maxMs: number,
    pointerId = 0
  ): QteTimedTapCommand {
    return {
      primitive: 'QTE_TIMED_TAP',
      pointerId,
      pos,
      qteWindow: { minMs, maxMs },
      idempotencyKey: MotorPrimitivesFactory.generateKey('qte')
    };
  }
}
