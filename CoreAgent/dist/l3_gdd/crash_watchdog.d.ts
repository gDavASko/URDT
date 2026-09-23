import { ChildProcess } from 'node:child_process';
import { EventEmitter } from 'node:events';
export interface NativeCrashDump {
    exitCode: number;
    exitCodeHex: string;
    signalName: string | null;
    faultingModule: string;
    nativeStackTrace: string[];
    logTailSnippet: string;
    detectedAt: string;
    logFilePath: string;
}
export interface WatchdogOptions {
    logPath?: string;
    tailLineLimit?: number;
}
/**
 * Post-Mortem Native Crash Watchdog.
 * Intercepts engine abnormal terminations (exitCode != 0), extracts the trailing 200 lines
 * of Player.log, and isolates OS fault signals, faulting native libraries, and crash stack traces.
 */
export declare class CrashWatchdog extends EventEmitter {
    private tailLineLimit;
    private explicitLogPath?;
    constructor(options?: WatchdogOptions);
    /**
     * Attaches watchdog listeners to a spawned Unity child process.
     */
    watchProcess(childProcess: ChildProcess): void;
    /**
     * Analyzes the crash log following an abnormal process exit.
     */
    inspectCrash(exitCode: number, logOverride?: string): Promise<NativeCrashDump>;
    /**
     * Resolves the most likely location of Player.log.
     */
    resolvePlayerLogPath(): string | null;
    /**
     * Extracts the trailing N lines from a log file.
     */
    extractTrailingLines(filePath: string | null, limit: number): string;
    /**
     * Identifies signal name or crash exception.
     */
    private detectSignal;
    /**
     * Isolates the faulting native library or subsystem.
     */
    private extractFaultingModule;
    /**
     * Extracts call stack lines from log text.
     */
    private extractStackTrace;
}
