/**
 * Flash-Hogan Kinematic Ticker (60 Hz / 16.6 ms)
 * Evaluates Minimum-Jerk trajectory polynomials:
 *   S(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5, tau in [0, 1]
 * Injects physiological micro-tremor (Gaussian Jitter, sigma = 0.8 px).
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-4
 */
import { UrdtCommandTypes } from '../protocol/pipe_client.js';
export class FlashHoganTicker {
    static TICK_INTERVAL_NS = 16666667n; // 16.666 ms in nanoseconds
    static TREMOR_SIGMA_PX = 0.8; // Gaussian micro-tremor standard deviation
    activeTrajectories = new Map();
    isRunning = false;
    timer = null;
    sequenceCounter = 1;
    onFrameCallback = null;
    setOnFrame(callback) {
        this.onFrameCallback = callback;
    }
    /**
     * Minimum-Jerk interpolation polynomial: S(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5
     */
    static calculateMinimumJerk(tau) {
        const t = Math.max(0, Math.min(1, tau));
        const t3 = t * t * t;
        const t4 = t3 * t;
        const t5 = t4 * t;
        return 10 * t3 - 15 * t4 + 6 * t5;
    }
    /**
     * Physiological Gaussian micro-tremor using Box-Muller transform
     */
    static generateTremor(sigma = FlashHoganTicker.TREMOR_SIGMA_PX) {
        const u1 = Math.max(1e-6, Math.random());
        const u2 = Math.random();
        const radius = Math.sqrt(-2.0 * Math.log(u1));
        const theta = 2.0 * Math.PI * u2;
        return {
            dx: radius * Math.cos(theta) * sigma,
            dy: radius * Math.sin(theta) * sigma
        };
    }
    startGesture(trajectory) {
        this.activeTrajectories.set(trajectory.pointerId, trajectory);
        if (!this.isRunning) {
            this.start();
        }
    }
    stopGesture(pointerId) {
        this.activeTrajectories.delete(pointerId);
        if (this.activeTrajectories.size === 0) {
            this.stop();
        }
    }
    start() {
        if (this.isRunning)
            return;
        this.isRunning = true;
        this.scheduleNextTick();
    }
    stop() {
        this.isRunning = false;
        if (this.timer) {
            clearTimeout(this.timer);
            this.timer = null;
        }
    }
    scheduleNextTick() {
        if (!this.isRunning)
            return;
        this.timer = setTimeout(() => {
            this.tick();
            this.scheduleNextTick();
        }, 16); // 16 ms nominal setTimeout interval
    }
    tick() {
        const now = Date.now();
        const completedPointerIds = [];
        for (const [pointerId, traj] of this.activeTrajectories.entries()) {
            const elapsed = now - traj.startTimestampMs;
            const tau = traj.durationMs > 0 ? elapsed / traj.durationMs : 1.0;
            const s = FlashHoganTicker.calculateMinimumJerk(tau);
            const tremor = FlashHoganTicker.generateTremor();
            const posX = traj.startPixel.x + (traj.endPixel.x - traj.startPixel.x) * s + tremor.dx;
            const posY = traj.startPixel.y + (traj.endPixel.y - traj.startPixel.y) * s + tremor.dy;
            const frame = {
                cmdType: tau >= 1.0 ? UrdtCommandTypes.STEER : UrdtCommandTypes.STEER,
                pointerId: pointerId + 1, // Unity touchId 1..4
                sequenceNumber: this.sequenceCounter++,
                screenX: posX,
                screenY: posY,
                pressure: 1.0,
                targetTimestampMs: now,
                idempotencyKey: traj.idempotencyKey
            };
            this.onFrameCallback?.(frame);
            if (tau >= 1.0) {
                completedPointerIds.push(pointerId);
                traj.onCompleted?.();
            }
        }
        for (const id of completedPointerIds) {
            this.activeTrajectories.delete(id);
        }
        if (this.activeTrajectories.size === 0) {
            this.stop();
        }
    }
    getActiveCount() {
        return this.activeTrajectories.size;
    }
}
//# sourceMappingURL=flash_hogan.js.map