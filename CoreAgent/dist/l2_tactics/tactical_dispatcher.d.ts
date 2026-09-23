/**
 * Two-Tier Tactical Dispatcher
 * Coordinates the dual-loop tactical execution:
 * 1. Autopilot Fast Loop (0.01 ms overhead) for nominal HTN/BT execution
 * 2. Cognitive Arbiter Loop (Micro-SLM, 35-50 ms) for anomalies, stalls, and unknown modals
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-2
 */
import { HtnBtExecutive, TacticalContext } from './htn_bt_executive.js';
import { MultiTouchDispatcher } from '../l1_kinematics/multi_touch_dispatcher.js';
import { FlashHoganTicker } from '../l1_kinematics/flash_hogan.js';
import { MotorCommand } from '../l1_kinematics/motor_primitives.js';
export interface TacticalEvaluationResult {
    mode: 'AUTOPILOT_FAST' | 'COGNITIVE_ARBITER' | 'ESCALATED_L3';
    latencyMs: number;
    command: MotorCommand | null;
    diagnosis?: string;
    isStagnated: boolean;
}
export declare class TacticalDispatcher {
    private readonly htnBt;
    private readonly arbiter;
    private readonly stagnationPolicy;
    private readonly touchManager;
    private readonly ticker;
    private stuckCount;
    private lastStateTimestamp;
    private lastStateHash;
    constructor(modelPath?: string, touchManager?: MultiTouchDispatcher, ticker?: FlashHoganTicker);
    initialize(): Promise<void>;
    getHtnExecutive(): HtnBtExecutive;
    /**
     * Process one tactical tick
     */
    evaluateTacticalStep(ctx: TacticalContext, currentStateHash: string, macroGoal?: string): Promise<TacticalEvaluationResult>;
}
