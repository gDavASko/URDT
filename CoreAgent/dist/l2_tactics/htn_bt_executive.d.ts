/**
 * Hybrid HTN-over-BT Tactical Executive
 * Two-tier execution architecture:
 * Upper Tier: Hierarchical Task Network (HTN) decomposes macro objectives into subgoals.
 * Lower Tier: Reactive Behavior Tree (50 ms ticker) handles priority threat evasion,
 * modal dialog interrupts, and step execution.
 * Enforces the Loop-Break Invariant against deadlocks.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-3
 */
import { MotorCommand } from '../l1_kinematics/motor_primitives.js';
export interface HtnSubGoal {
    id: string;
    description: string;
    targetBeaconId?: string;
    expectedTransitionNodeId?: string;
    primitive: 'TAP' | 'DRAG' | 'SWIPE' | 'WAIT';
    coordinates?: {
        x: number;
        y: number;
    };
}
export interface TacticalContext {
    activeModalId: string | null;
    detectedHazards: Array<{
        id: string;
        threatLevel: 'LETHAL' | 'MODERATE' | 'INFO';
        dangerRadius: number;
    }>;
    currentScreenBeacons: Array<{
        id: string;
        screenPos: {
            x: number;
            y: number;
        };
    }>;
    activeNodeId: string;
}
export declare class HtnBtExecutive {
    private activePlan;
    private currentStepIndex;
    private replanCount;
    private lastExecutedNodeId;
    private identicalNodeCycleCount;
    static readonly MAX_REPLANS = 3;
    static readonly MAX_CYCLES = 2;
    loadMacroPlan(subGoals: HtnSubGoal[]): void;
    getCurrentSubGoal(): HtnSubGoal | null;
    /**
     * 50 ms Behavior Tree Tick:
     * Priority 1: Threat evasion
     * Priority 2: Modal window interrupt
     * Priority 3: HTN step execution
     */
    tickBehaviorTree(ctx: TacticalContext): {
        command: MotorCommand | null;
        priorityTriggered: 1 | 2 | 3 | null;
        status: 'RUNNING' | 'COMPLETED' | 'DEADLOCK_TOPOLOGY';
    };
    advanceStep(): void;
    triggerReplan(): boolean;
}
