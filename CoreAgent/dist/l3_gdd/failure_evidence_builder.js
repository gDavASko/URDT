import fs from 'node:fs';
import path from 'node:path';
/**
 * L3 GDD Closed-Loop Failure Evidence Builder.
 * Produces normative JSON Schema Draft-07 packets strictly consumed by AI coding agents
 * and human audit dashboards for autonomous self-healing and post-mortems.
 */
export class FailureEvidenceBuilder {
    static DEFAULT_HARNESS_OUTPUT = 'E:/Projects/URDT/.harness/failure_evidence.json';
    /**
     * Constructs an authenticated, schema-compliant failure evidence packet.
     */
    static build(options) {
        const timestamp = Date.now();
        const incidentId = options.incidentId ||
            `INC_${new Date(timestamp).toISOString().replace(/[-:T.Z]/g, '').slice(0, 14)}_${options.failingPhase.replace(/[^a-zA-Z0-9]/g, '_')}`;
        let nativeDumpInfo = null;
        if (options.nativeCrashDump) {
            nativeDumpInfo = {
                exitCodeHex: options.nativeCrashDump.exitCodeHex,
                signalName: options.nativeCrashDump.signalName,
                faultingModule: options.nativeCrashDump.faultingModule,
                nativeStackTrace: options.nativeCrashDump.nativeStackTrace,
                logTailSnippet: options.nativeCrashDump.logTailSnippet
            };
        }
        const packet = {
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
    static exportToFile(packet, outputPath = FailureEvidenceBuilder.DEFAULT_HARNESS_OUTPUT) {
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
    static validate(packet) {
        const errors = [];
        if (!packet.incidentId)
            errors.push('Missing incidentId');
        if (!packet.timestamp)
            errors.push('Missing timestamp');
        if (!packet.failingPhase)
            errors.push('Missing failingPhase');
        if (!packet.gddInvariantViolated)
            errors.push('Missing gddInvariantViolated');
        if (!packet.stagnationType)
            errors.push('Missing stagnationType');
        if (typeof packet.lastKnownWorldRevision !== 'number')
            errors.push('Missing or invalid lastKnownWorldRevision');
        if (!packet.activeScene)
            errors.push('Missing activeScene');
        if (!Array.isArray(packet.beaconStateSnapshot))
            errors.push('beaconStateSnapshot must be an array');
        if (!Array.isArray(packet.actionHistoryBeforeFailure))
            errors.push('actionHistoryBeforeFailure must be an array');
        if (!packet.diagnostics)
            errors.push('Missing diagnostics object');
        return {
            valid: errors.length === 0,
            errors
        };
    }
}
//# sourceMappingURL=failure_evidence_builder.js.map