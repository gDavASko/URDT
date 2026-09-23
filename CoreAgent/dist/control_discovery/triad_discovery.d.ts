/**
 * Triad Control Scheme Discovery Module
 * Autonomous discovery of control schemes via Triad Source Model:
 * 1. GDD Prior (Spec-driven hypotheses)
 * 2. Semantic Correlation (On-screen text, labels, beacon topology)
 * 3. Empirical Micro-Probing (100 ms physical pulse -> physics delta assertion)
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-4
 */
export type ControlIntent = 'STEER' | 'THROTTLE' | 'BRAKE' | 'JUMP' | 'FIRE' | 'INTERACT' | 'PAUSE';
export interface GddControlPrior {
    intent: ControlIntent;
    keywords: string[];
    preferredRegion: 'LEFT' | 'RIGHT' | 'BOTTOM' | 'ANY';
    probeType: 'MICRO_TAP' | 'STICK_DEFLECT' | 'HOLD_100MS';
}
export interface BeaconCandidate {
    id: string;
    semanticLabel: string;
    controlType: string;
    screenPixel: {
        x: number;
        y: number;
    };
}
export interface TelemetrySnapshot {
    timestampMs: number;
    linearVelocityMagnitude: number;
    angularVelocity: number;
    playerState: string;
}
export interface ControlBinding {
    intent: ControlIntent;
    pointerId: number;
    targetBeaconId: string;
    screenPos: [number, number];
    confidenceScore: number;
    verifiedTimestampMs: number;
}
export declare class TriadDiscoveryEngine {
    private readonly defaultPriors;
    private readonly confirmedBindings;
    private readonly unverifiedDefects;
    /**
     * Step 2: Correlate active on-screen beacons with GDD priors by keyword matching and screen positioning.
     */
    correlateSemantics(beacons: BeaconCandidate[], screenWidth?: number, screenHeight?: number): Map<ControlIntent, BeaconCandidate[]>;
    private isRegionValid;
    /**
     * Step 3: Evaluate physical delta under micro-probing pulse.
     * If velocity delta > 0.05 or player state changed, mapping is confirmed.
     */
    evaluateMicroProbeDelta(intent: ControlIntent, candidate: BeaconCandidate, pre: TelemetrySnapshot, post: TelemetrySnapshot, pointerId: number): boolean;
    getBindings(): Record<string, ControlBinding>;
    getDefects(): {
        intent: ControlIntent;
        candidateId: string;
        reason: string;
    }[];
}
