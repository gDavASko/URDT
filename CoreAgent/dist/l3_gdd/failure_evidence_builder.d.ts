import { NativeCrashDump } from './crash_watchdog.js';
export type StagnationType = 'MICRO_STUCK' | 'MACRO_STUCK' | 'CRASH_EXCEPTION' | 'DEADLOCK_TOPOLOGY' | 'LAYOUT_DEFECT' | 'ASSET_STREAMING_STALL' | 'NATIVE_ENGINE_CRASH';
export interface AssetStreamingInfo {
    stalledUrl: string;
    lastReportedProgress: number;
    stalledDurationSec: number;
    httpStatusCode: number | null;
}
export interface NativeCrashDumpInfo {
    exitCodeHex: string;
    signalName: string | null;
    faultingModule: string;
    nativeStackTrace: string[];
    logTailSnippet: string;
}
export interface FailureDiagnostics {
    engineConsoleErrors: string[];
    activeModalDialog: string | null;
    recommendedAiFix: string;
    crashDashcamBase64: string;
    assetStreamingInfo: AssetStreamingInfo | null;
    nativeCrashDump: NativeCrashDumpInfo | null;
}
export interface UrdtFailureEvidencePacket {
    $schema?: string;
    incidentId: string;
    timestamp: number;
    failingPhase: string;
    gddInvariantViolated: string;
    stagnationType: StagnationType;
    lastKnownWorldRevision: number;
    activeScene: string;
    beaconStateSnapshot: Array<Record<string, unknown>>;
    actionHistoryBeforeFailure: Array<Record<string, unknown>>;
    diagnostics: FailureDiagnostics;
}
export interface FailureEvidenceOptions {
    incidentId?: string;
    failingPhase: string;
    gddInvariantViolated: string;
    stagnationType: StagnationType;
    lastKnownWorldRevision: number;
    activeScene: string;
    beaconStateSnapshot?: Array<Record<string, unknown>>;
    actionHistoryBeforeFailure?: Array<Record<string, unknown>>;
    engineConsoleErrors?: string[];
    activeModalDialog?: string | null;
    recommendedAiFix?: string;
    crashDashcamBase64?: string;
    assetStreamingInfo?: AssetStreamingInfo | null;
    nativeCrashDump?: NativeCrashDump | null;
}
/**
 * L3 GDD Closed-Loop Failure Evidence Builder.
 * Produces normative JSON Schema Draft-07 packets strictly consumed by AI coding agents
 * and human audit dashboards for autonomous self-healing and post-mortems.
 */
export declare class FailureEvidenceBuilder {
    static readonly DEFAULT_HARNESS_OUTPUT = "E:/Projects/URDT/.harness/failure_evidence.json";
    /**
     * Constructs an authenticated, schema-compliant failure evidence packet.
     */
    static build(options: FailureEvidenceOptions): UrdtFailureEvidencePacket;
    /**
     * Writes the packet to .harness/failure_evidence.json strictly on Drive E:.
     */
    static exportToFile(packet: UrdtFailureEvidencePacket, outputPath?: string): string;
    /**
     * Validates structural compliance against JSON Schema Draft-07 requirements.
     */
    static validate(packet: UrdtFailureEvidencePacket): {
        valid: boolean;
        errors: string[];
    };
}
