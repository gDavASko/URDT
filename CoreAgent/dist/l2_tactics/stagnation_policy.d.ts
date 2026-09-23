/**
 * Stagnation Policy & 5-Layer Modal Filter
 * Evaluates screen state across 5 filters to distinguish legitimate waiting/countdowns
 * from soft-locks, deadlocks, and interaction stalls.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-4, section-3-5
 */
export interface ModalEvaluationContext {
    modalId: string | null;
    modalSpawnedAtMs: number;
    screenText: string[];
    isAnimationPlaying: boolean;
    isVideoPlaying: boolean;
    gddMandatoryDurationMs?: number;
    isCloseButtonPresent: boolean;
}
export interface StagnationContext {
    lastActionTimestampMs: number;
    lastStateChangeTimestampMs: number;
    genre: 'RUNNER' | 'PUZZLE' | 'ACTION' | 'UI_MENU';
    linearVelocity?: number;
    stuckCounter: number;
}
export declare class StagnationPolicy {
    static readonly GRACE_PERIOD_MS = 4000;
    static readonly COUNTDOWN_REGEX: RegExp;
    /**
     * Evaluates whether a modal window is in a legitimate delay vs a soft-lock defect.
     */
    evaluateModalState(ctx: ModalEvaluationContext, nowMs?: number): {
        isSuppressed: boolean;
        reason: string;
        isSoftLock: boolean;
    };
    /**
     * Checks whether gameplay or UI interaction has stagnated.
     */
    checkStagnation(ctx: StagnationContext, nowMs?: number): {
        isStagnated: boolean;
        stuckLevel: number;
        recommendedAction: 'CONTINUE' | 'MICRO_SLM_ARBITER' | 'ESCALATE_L3';
    };
}
