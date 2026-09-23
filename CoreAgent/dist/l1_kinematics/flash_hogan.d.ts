/**
 * Flash-Hogan Kinematic Ticker (60 Hz / 16.6 ms)
 * Evaluates Minimum-Jerk trajectory polynomials:
 *   S(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5, tau in [0, 1]
 * Injects physiological micro-tremor (Gaussian Jitter, sigma = 0.8 px).
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-4
 */
import { RawCommand } from '../protocol/pipe_client.js';
export interface GestureTrajectory {
    pointerId: number;
    startPixel: {
        x: number;
        y: number;
    };
    endPixel: {
        x: number;
        y: number;
    };
    durationMs: number;
    startTimestampMs: number;
    idempotencyKey: number;
    onCompleted?: () => void;
}
export declare class FlashHoganTicker {
    static readonly TICK_INTERVAL_NS = 16666667n;
    static readonly TREMOR_SIGMA_PX = 0.8;
    private readonly activeTrajectories;
    private isRunning;
    private timer;
    private sequenceCounter;
    private onFrameCallback;
    setOnFrame(callback: (command: RawCommand) => void): void;
    /**
     * Minimum-Jerk interpolation polynomial: S(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5
     */
    static calculateMinimumJerk(tau: number): number;
    /**
     * Physiological Gaussian micro-tremor using Box-Muller transform
     */
    static generateTremor(sigma?: number): {
        dx: number;
        dy: number;
    };
    startGesture(trajectory: GestureTrajectory): void;
    stopGesture(pointerId: number): void;
    start(): void;
    stop(): void;
    private scheduleNextTick;
    tick(): void;
    getActiveCount(): number;
}
