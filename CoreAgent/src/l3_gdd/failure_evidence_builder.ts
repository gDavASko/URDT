import fs from 'node:fs';
import path from 'node:path';
import { NativeCrashDump } from './crash_watchdog.js';

export type StagnationType =
  | 'MICRO_STUCK'
  | 'MACRO_STUCK'
  | 'CRASH_EXCEPTION'
  | 'DEADLOCK_TOPOLOGY'
  | 'LAYOUT_DEFECT'
  | 'ASSET_STREAMING_STALL'
  | 'NATIVE_ENGINE_CRASH';

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
export class FailureEvidenceBuilder {
  public static readonly DEFAULT_HARNESS_OUTPUT = 'E:/Projects/URDT/.harness/failure_evidence.json';

  /**
   * Constructs an authenticated, schema-compliant failure evidence packet.
   */
  public static build(options: FailureEvidenceOptions): UrdtFailureEvidencePacket {
    const timestamp = Date.now();
    const incidentId =
      options.incidentId ||
      `INC_${new Date(timestamp).toISOString().replace(/[-:T.Z]/g, '').slice(0, 14)}_${options.failingPhase.replace(/[^a-zA-Z0-9]/g, '_')}`;

    let nativeDumpInfo: NativeCrashDumpInfo | null = null;
    if (options.nativeCrashDump) {
      nativeDumpInfo = {
        exitCodeHex: options.nativeCrashDump.exitCodeHex,
        signalName: options.nativeCrashDump.signalName,
        faultingModule: options.nativeCrashDump.faultingModule,
        nativeStackTrace: options.nativeCrashDump.nativeStackTrace,
        logTailSnippet: options.nativeCrashDump.logTailSnippet
      };
    }

    const packet: UrdtFailureEvidencePacket = {
      $schema: 'http://json-schema.org/draft-07/schema#',
      incidentId,
      timestamp,
      failingPhase: options.failingPhase,
      gddInvariantViolated: options.gddInvariantViolated,
      stagnationType: options.stagnationType,
      lastKnownWorldRevision: options.lastKnownWorldRevision,
      activeScene: options.activeScene,
      beaconStateSnapshot: options.beaconStateSnapshot || [],
      actionHistoryBeforeFailure: options.actionHistoryBeforeFailure || [],
      diagnostics: {
        engineConsoleErrors: options.engineConsoleErrors || [],
        activeModalDialog: options.activeModalDialog ?? null,
        recommendedAiFix: options.recommendedAiFix || 'Investigate stack trace and state snapshot.',
        crashDashcamBase64: options.crashDashcamBase64 || '',
        assetStreamingInfo: options.assetStreamingInfo ?? null,
        nativeCrashDump: nativeDumpInfo
      }
    };

    return packet;
  }

  /**
   * Writes the packet to .harness/failure_evidence.json strictly on Drive E:.
   */
  public static exportToFile(
    packet: UrdtFailureEvidencePacket,
    outputPath: string = FailureEvidenceBuilder.DEFAULT_HARNESS_OUTPUT
  ): string {
    const resolvedPath = path.resolve(outputPath);
    const dir = path.dirname(resolvedPath);

    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }

    const payload = JSON.stringify(packet, null, 2);
    fs.writeFileSync(resolvedPath, payload, 'utf-8');

    return resolvedPath;
  }

  /**
   * Validates structural compliance against JSON Schema Draft-07 requirements.
   */
  public static validate(packet: UrdtFailureEvidencePacket): { valid: boolean; errors: string[] } {
    const errors: string[] = [];

    if (!packet.incidentId) errors.push('Missing incidentId');
    if (!packet.timestamp) errors.push('Missing timestamp');
    if (!packet.failingPhase) errors.push('Missing failingPhase');
    if (!packet.gddInvariantViolated) errors.push('Missing gddInvariantViolated');
    if (!packet.stagnationType) errors.push('Missing stagnationType');
    if (typeof packet.lastKnownWorldRevision !== 'number') errors.push('Missing or invalid lastKnownWorldRevision');
    if (!packet.activeScene) errors.push('Missing activeScene');
    if (!Array.isArray(packet.beaconStateSnapshot)) errors.push('beaconStateSnapshot must be an array');
    if (!Array.isArray(packet.actionHistoryBeforeFailure)) errors.push('actionHistoryBeforeFailure must be an array');
    if (!packet.diagnostics) errors.push('Missing diagnostics object');

    return {
      valid: errors.length === 0,
      errors
    };
  }
}
