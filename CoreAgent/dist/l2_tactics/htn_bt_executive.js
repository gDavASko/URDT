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
import { MotorPrimitivesFactory } from '../l1_kinematics/motor_primitives.js';
export class HtnBtExecutive {
    activePlan = [];
    currentStepIndex = 0;
    replanCount = 0;
    lastExecutedNodeId = null;
    identicalNodeCycleCount = 0;
    static MAX_REPLANS = 3;
    static MAX_CYCLES = 2;
    loadMacroPlan(subGoals) {
        this.activePlan = [...subGoals];
        this.currentStepIndex = 0;
        this.replanCount = 0;
        this.identicalNodeCycleCount = 0;
    }
    getCurrentSubGoal() {
        if (this.currentStepIndex < this.activePlan.length) {
            return this.activePlan[this.currentStepIndex];
        }
        return null;
    }
    /**
     * 50 ms Behavior Tree Tick:
     * Priority 1: Threat evasion
     * Priority 2: Modal window interrupt
     * Priority 3: HTN step execution
     */
    tickBehaviorTree(ctx) {
        // Loop-Break Invariant check
        if (this.lastExecutedNodeId === ctx.activeNodeId) {
            this.identicalNodeCycleCount++;
            if (this.identicalNodeCycleCount >= HtnBtExecutive.MAX_CYCLES && this.replanCount >= HtnBtExecutive.MAX_REPLANS) {
                return {
                    command: null,
                    priorityTriggered: null,
                    status: 'DEADLOCK_TOPOLOGY'
                };
            }
        }
        else {
            this.identicalNodeCycleCount = 0;
            this.lastExecutedNodeId = ctx.activeNodeId;
        }
        // Priority 1: Lethal threat evasion
        const lethalHazard = ctx.detectedHazards.find(h => h.threatLevel === 'LETHAL');
        if (lethalHazard) {
            // Evasive swipe away from hazard
            const evasion = MotorPrimitivesFactory.createSwipe({ x: 960, y: 540 }, { vx: 0, vy: -500 }, 150);
            return {
                command: evasion,
                priorityTriggered: 1,
                status: 'RUNNING'
            };
        }
        // Priority 2: Modal dialog interrupt
        if (ctx.activeModalId) {
            const closeBeacon = ctx.currentScreenBeacons.find(b => {
                const id = b.id.toLowerCase();
                return id.includes('close') || id.includes('cancel') || id.includes('dismiss') || id.includes('ok');
            });
            if (closeBeacon) {
                const tap = MotorPrimitivesFactory.createTap(closeBeacon.screenPos, 0, 50, closeBeacon.id);
                return {
                    command: tap,
                    priorityTriggered: 2,
                    status: 'RUNNING'
                };
            }
        }
        // Priority 3: HTN Step Execution
        const currentGoal = this.getCurrentSubGoal();
        if (!currentGoal) {
            return { command: null, priorityTriggered: null, status: 'COMPLETED' };
        }
        let targetPos = currentGoal.coordinates || { x: 960, y: 540 };
        if (currentGoal.targetBeaconId) {
            const beacon = ctx.currentScreenBeacons.find(b => b.id === currentGoal.targetBeaconId);
            if (beacon) {
                targetPos = beacon.screenPos;
            }
        }
        let cmd;
        if (currentGoal.primitive === 'DRAG') {
            cmd = MotorPrimitivesFactory.createDrag(targetPos, { x: targetPos.x + 200, y: targetPos.y }, 350);
        }
        else {
            cmd = MotorPrimitivesFactory.createTap(targetPos, 0, 50, currentGoal.targetBeaconId);
        }
        return {
            command: cmd,
            priorityTriggered: 3,
            status: 'RUNNING'
        };
    }
    advanceStep() {
        this.currentStepIndex++;
    }
    triggerReplan() {
        this.replanCount++;
        if (this.replanCount > HtnBtExecutive.MAX_REPLANS) {
            return false; // Replan budget exhausted
        }
        return true;
    }
}
//# sourceMappingURL=htn_bt_executive.js.map