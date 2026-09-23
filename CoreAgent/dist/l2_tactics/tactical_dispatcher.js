/**
 * Two-Tier Tactical Dispatcher
 * Coordinates the dual-loop tactical execution:
 * 1. Autopilot Fast Loop (0.01 ms overhead) for nominal HTN/BT execution
 * 2. Cognitive Arbiter Loop (Micro-SLM, 35-50 ms) for anomalies, stalls, and unknown modals
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-2
 */
import { HtnBtExecutive } from './htn_bt_executive.js';
import { MicroSlmArbiter } from './micro_slm_arbiter.js';
import { StagnationPolicy } from './stagnation_policy.js';
import { MultiTouchDispatcher } from '../l1_kinematics/multi_touch_dispatcher.js';
import { FlashHoganTicker } from '../l1_kinematics/flash_hogan.js';
export class TacticalDispatcher {
    htnBt;
    arbiter;
    stagnationPolicy;
    touchManager;
    ticker;
    stuckCount = 0;
    lastStateTimestamp = Date.now();
    lastStateHash = '';
    constructor(modelPath, touchManager, ticker) {
        this.htnBt = new HtnBtExecutive();
        this.arbiter = new MicroSlmArbiter(modelPath);
        this.stagnationPolicy = new StagnationPolicy();
        this.touchManager = touchManager || new MultiTouchDispatcher();
        this.ticker = ticker || new FlashHoganTicker();
    }
    async initialize() {
        await this.arbiter.initialize();
    }
    getHtnExecutive() {
        return this.htnBt;
    }
    /**
     * Process one tactical tick
     */
    async evaluateTacticalStep(ctx, currentStateHash, macroGoal = 'Progress through active scene') {
        const startHr = process.hrtime.bigint();
        const now = Date.now();
        // Check state progression
        if (currentStateHash !== this.lastStateHash) {
            this.lastStateHash = currentStateHash;
            this.lastStateTimestamp = now;
            this.stuckCount = 0;
        }
        else {
            this.stuckCount++;
        }
        // Check stagnation
        const stagResult = this.stagnationPolicy.checkStagnation({
            lastActionTimestampMs: this.lastStateTimestamp,
            lastStateChangeTimestampMs: this.lastStateTimestamp,
            genre: 'UI_MENU',
            stuckCounter: this.stuckCount
        }, now);
        // 1. If heavily stuck -> escalate to L3
        if (stagResult.recommendedAction === 'ESCALATE_L3') {
            const elapsedNs = process.hrtime.bigint() - startHr;
            return {
                mode: 'ESCALATED_L3',
                latencyMs: Number(elapsedNs) / 1e6,
                command: null,
                diagnosis: `Deadlock detected: ${this.stuckCount} consecutive attempts without state delta.`,
                isStagnated: true
            };
        }
        // 2. If anomaly or modal or stall -> Cognitive Arbiter Loop (Micro-SLM)
        if (ctx.activeModalId || stagResult.recommendedAction === 'MICRO_SLM_ARBITER') {
            const arbiterResult = await this.arbiter.decideAction({
                macroGoal,
                activeModalId: ctx.activeModalId,
                beacons: ctx.currentScreenBeacons.map(b => ({ id: b.id })),
                stuckCount: this.stuckCount
            });
            const elapsedNs = process.hrtime.bigint() - startHr;
            const beacon = ctx.currentScreenBeacons.find(b => b.id === arbiterResult.targetBeaconId);
            const pos = beacon ? beacon.screenPos : { x: 960, y: 540 };
            const cmd = {
                primitive: 'TAP',
                pointerId: 0,
                pos,
                holdDurationMs: 50,
                targetBeaconId: arbiterResult.targetBeaconId,
                idempotencyKey: `arbiter_tap_${now}`
            };
            return {
                mode: 'COGNITIVE_ARBITER',
                latencyMs: Number(elapsedNs) / 1e6,
                command: cmd,
                diagnosis: arbiterResult.diagnosis,
                isStagnated: stagResult.isStagnated
            };
        }
        // 3. Autopilot Fast Loop: Evaluate Behavior Tree (0.01 ms)
        const btResult = this.htnBt.tickBehaviorTree(ctx);
        const elapsedNs = process.hrtime.bigint() - startHr;
        return {
            mode: 'AUTOPILOT_FAST',
            latencyMs: Number(elapsedNs) / 1e6,
            command: btResult.command,
            diagnosis: btResult.status,
            isStagnated: false
        };
    }
}
//# sourceMappingURL=tactical_dispatcher.js.map