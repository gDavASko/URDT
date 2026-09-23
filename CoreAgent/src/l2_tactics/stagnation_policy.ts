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

export class StagnationPolicy {
  public static readonly GRACE_PERIOD_MS = 4000;           // Layer 5: 4.0s grace window
  public static readonly COUNTDOWN_REGEX = /\b(0?[0-9]|1[0-5])\s*(s|sec)?\b/i; // Layer 4

  /**
   * Evaluates whether a modal window is in a legitimate delay vs a soft-lock defect.
   */
  public evaluateModalState(ctx: ModalEvaluationContext, nowMs = Date.now()): {
    isSuppressed: boolean;
    reason: string;
    isSoftLock: boolean;
  } {
    if (!ctx.modalId) {
      return { isSuppressed: false, reason: 'NO_MODAL', isSoftLock: false };
    }

    const elapsedMs = nowMs - ctx.modalSpawnedAtMs;

    // Layer 1: GDD Contract
    if (ctx.gddMandatoryDurationMs && elapsedMs < ctx.gddMandatoryDurationMs) {
      return {
        isSuppressed: true,
        reason: `GDD_MANDATORY_DURATION: ${elapsedMs}ms / ${ctx.gddMandatoryDurationMs}ms`,
        isSoftLock: false
      };
    }

    // Layer 3: Active Animation / Video
    if (ctx.isVideoPlaying || ctx.isAnimationPlaying) {
      return {
        isSuppressed: true,
        reason: 'ACTIVE_ENGINE_MEDIA_PLAYING',
        isSoftLock: false
      };
    }

    // Layer 4: Countdown Regex Scanner
    for (const text of ctx.screenText) {
      if (StagnationPolicy.COUNTDOWN_REGEX.test(text)) {
        return {
          isSuppressed: true,
          reason: `LEGITIMATE_COUNTDOWN_DETECTED: "${text}"`,
          isSoftLock: false
        };
      }
    }

    // Layer 5: 4.0s Grace Period
    if (elapsedMs < StagnationPolicy.GRACE_PERIOD_MS) {
      return {
        isSuppressed: true,
        reason: `GRACE_PERIOD_ACTIVE: ${elapsedMs}ms < 4000ms`,
        isSoftLock: false
      };
    }

    // If grace period elapsed and no close button exists -> Soft-Lock Defect!
    if (!ctx.isCloseButtonPresent) {
      return {
        isSuppressed: false,
        reason: `CRITICAL_SOFT_LOCK: Modal '${ctx.modalId}' lacks close button after ${elapsedMs}ms.`,
        isSoftLock: true
      };
    }

    return {
      isSuppressed: false,
      reason: 'MODAL_READY_FOR_INTERACTION',
      isSoftLock: false
    };
  }

  /**
   * Checks whether gameplay or UI interaction has stagnated.
   */
  public checkStagnation(ctx: StagnationContext, nowMs = Date.now()): {
    isStagnated: boolean;
    stuckLevel: number;
    recommendedAction: 'CONTINUE' | 'MICRO_SLM_ARBITER' | 'ESCALATE_L3';
  } {
    const timeSinceLastChange = nowMs - ctx.lastStateChangeTimestampMs;

    let thresholdMs = 5000;
    switch (ctx.genre) {
      case 'RUNNER':
        thresholdMs = (ctx.linearVelocity !== undefined && ctx.linearVelocity < 0.1) ? 500 : 3000;
        break;
      case 'PUZZLE':
        thresholdMs = 15000; // Puzzles allow 15s deliberation
        break;
      case 'UI_MENU':
        thresholdMs = 2000;
        break;
      case 'ACTION':
      default:
        thresholdMs = 5000;
        break;
    }

    if (timeSinceLastChange >= thresholdMs || ctx.stuckCounter > 0) {
      if (ctx.stuckCounter >= 3) {
        return {
          isStagnated: true,
          stuckLevel: ctx.stuckCounter,
          recommendedAction: 'ESCALATE_L3'
        };
      }

      return {
        isStagnated: true,
        stuckLevel: ctx.stuckCounter,
        recommendedAction: 'MICRO_SLM_ARBITER'
      };
    }

    return {
      isStagnated: false,
      stuckLevel: 0,
      recommendedAction: 'CONTINUE'
    };
  }
}
