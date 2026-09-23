/**
 * Multi-Touch Channel Manager
 * Manages 4 concurrent touch channels (pointerId: 0..3 -> Unity touchId: 1..4).
 * Enforces safety timeout watchdogs and channel arbitration.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-2-1
 */
export class MultiTouchDispatcher {
    static MAX_CHANNELS = 4;
    static MAX_HOLD_DURATION_MS = 10000; // 10s maximum sustained hold without refresh
    channels = [];
    constructor() {
        for (let i = 0; i < MultiTouchDispatcher.MAX_CHANNELS; i++) {
            this.channels.push({
                pointerId: i,
                touchId: i + 1,
                isOccupied: false,
                currentPrimitive: null,
                targetBeaconId: null,
                currentPos: { x: 0, y: 0 },
                acquiredAtTimestampMs: 0,
                lastMovedAtTimestampMs: 0,
                autoReleaseTimer: null
            });
        }
    }
    /**
     * Allocates an available touch channel.
     * If a specific preferred pointerId is requested, attempts to claim it.
     */
    allocateChannel(primitive, targetBeaconId = null, startPos = { x: 0, y: 0 }, preferredPointerId = -1) {
        let channel;
        if (preferredPointerId >= 0 && preferredPointerId < MultiTouchDispatcher.MAX_CHANNELS) {
            if (!this.channels[preferredPointerId].isOccupied) {
                channel = this.channels[preferredPointerId];
            }
        }
        if (!channel) {
            channel = this.channels.find(c => !c.isOccupied);
        }
        if (!channel) {
            return null; // All 4 channels occupied
        }
        channel.isOccupied = true;
        channel.currentPrimitive = primitive;
        channel.targetBeaconId = targetBeaconId;
        channel.currentPos = { ...startPos };
        channel.acquiredAtTimestampMs = Date.now();
        channel.lastMovedAtTimestampMs = channel.acquiredAtTimestampMs;
        // Safety watchdog: Auto-release if held longer than 10s
        if (channel.autoReleaseTimer) {
            clearTimeout(channel.autoReleaseTimer);
        }
        channel.autoReleaseTimer = setTimeout(() => {
            if (channel && channel.isOccupied) {
                console.warn(`[URDT L1] Safety Watchdog: Channel ${channel.pointerId} held > 10s. Auto-releasing.`);
                this.releaseChannel(channel.pointerId);
            }
        }, MultiTouchDispatcher.MAX_HOLD_DURATION_MS);
        return channel;
    }
    updatePosition(pointerId, x, y) {
        if (pointerId < 0 || pointerId >= MultiTouchDispatcher.MAX_CHANNELS)
            return;
        const ch = this.channels[pointerId];
        if (ch.isOccupied) {
            ch.currentPos.x = x;
            ch.currentPos.y = y;
            ch.lastMovedAtTimestampMs = Date.now();
        }
    }
    releaseChannel(pointerId) {
        if (pointerId < 0 || pointerId >= MultiTouchDispatcher.MAX_CHANNELS)
            return;
        const ch = this.channels[pointerId];
        if (ch.autoReleaseTimer) {
            clearTimeout(ch.autoReleaseTimer);
            ch.autoReleaseTimer = null;
        }
        ch.isOccupied = false;
        ch.currentPrimitive = null;
        ch.targetBeaconId = null;
    }
    releaseAll() {
        for (let i = 0; i < MultiTouchDispatcher.MAX_CHANNELS; i++) {
            this.releaseChannel(i);
        }
    }
    getChannel(pointerId) {
        if (pointerId < 0 || pointerId >= MultiTouchDispatcher.MAX_CHANNELS)
            return null;
        return this.channels[pointerId];
    }
    getActiveChannelsCount() {
        return this.channels.filter(c => c.isOccupied).length;
    }
}
//# sourceMappingURL=multi_touch_dispatcher.js.map