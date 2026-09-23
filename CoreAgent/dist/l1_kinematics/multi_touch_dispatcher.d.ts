/**
 * Multi-Touch Channel Manager
 * Manages 4 concurrent touch channels (pointerId: 0..3 -> Unity touchId: 1..4).
 * Enforces safety timeout watchdogs and channel arbitration.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-1
 */
export interface TouchChannelState {
    pointerId: number;
    touchId: number;
    isOccupied: boolean;
    currentPrimitive: string | null;
    targetBeaconId: string | null;
    currentPos: {
        x: number;
        y: number;
    };
    acquiredAtTimestampMs: number;
    lastMovedAtTimestampMs: number;
    autoReleaseTimer: NodeJS.Timeout | null;
}
export declare class MultiTouchDispatcher {
    static readonly MAX_CHANNELS = 4;
    static readonly MAX_HOLD_DURATION_MS = 10000;
    private readonly channels;
    constructor();
    /**
     * Allocates an available touch channel.
     * If a specific preferred pointerId is requested, attempts to claim it.
     */
    allocateChannel(primitive: string, targetBeaconId?: string | null, startPos?: {
        x: number;
        y: number;
    }, preferredPointerId?: number): TouchChannelState | null;
    updatePosition(pointerId: number, x: number, y: number): void;
    releaseChannel(pointerId: number): void;
    releaseAll(): void;
    getChannel(pointerId: number): TouchChannelState | null;
    getActiveChannelsCount(): number;
}
