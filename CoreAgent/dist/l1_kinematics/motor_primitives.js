/**
 * Catalog of 9 Motor Primitives (L1 Kinematics)
 * Factory for creating structured parametric gestures and touch sequences.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-3
 */
export class MotorPrimitivesFactory {
    static sequence = 1;
    static generateKey(primitive) {
        return `cmd_${primitive.toLowerCase()}_${Date.now()}_${MotorPrimitivesFactory.sequence++}`;
    }
    static createTap(pos, pointerId = 0, holdMs = 50, beaconId) {
        return {
            primitive: 'TAP',
            pointerId,
            pos,
            holdDurationMs: holdMs,
            targetBeaconId: beaconId,
            idempotencyKey: MotorPrimitivesFactory.generateKey('tap')
        };
    }
    static createDrag(startPos, endPos, durationMs = 350, pointerId = 0, beaconId) {
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
    static createSwipe(startPos, velocityVector, durationMs = 200, pointerId = 0) {
        return {
            primitive: 'SWIPE',
            pointerId,
            startPos,
            velocityVector,
            durationMs,
            idempotencyKey: MotorPrimitivesFactory.generateKey('swipe')
        };
    }
    static createSlice(waypoints, durationMs = 300, pointerId = 0) {
        return {
            primitive: 'SLICE',
            pointerId,
            waypoints,
            totalDurationMs: durationMs,
            idempotencyKey: MotorPrimitivesFactory.generateKey('slice')
        };
    }
    static createHoldConditioned(pos, targetEvent, maxTimeoutMs = 5000, pointerId = 0) {
        return {
            primitive: 'HOLD_EVENT_CONDITIONED',
            pointerId,
            pos,
            targetEvent,
            maxTimeoutMs,
            idempotencyKey: MotorPrimitivesFactory.generateKey('hold')
        };
    }
    static createChargeAndRelease(pos, chargeTimeMs = 600, releasePos, pointerId = 0) {
        return {
            primitive: 'CHARGE_AND_RELEASE',
            pointerId,
            pos,
            chargeTimeMs,
            releasePos,
            idempotencyKey: MotorPrimitivesFactory.generateKey('charge')
        };
    }
    static createContinuousSteer(stickCenter, steerVector, pointerId = 0) {
        return {
            primitive: 'CONTINUOUS_STEER_STREAM',
            pointerId,
            stickCenter,
            steerVector,
            idempotencyKey: MotorPrimitivesFactory.generateKey('steer')
        };
    }
    static createPinchZoom(centerPos, startDistance, endDistance, durationMs = 400) {
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
    static createQteTimedTap(pos, minMs, maxMs, pointerId = 0) {
        return {
            primitive: 'QTE_TIMED_TAP',
            pointerId,
            pos,
            qteWindow: { minMs, maxMs },
            idempotencyKey: MotorPrimitivesFactory.generateKey('qte')
        };
    }
}
//# sourceMappingURL=motor_primitives.js.map