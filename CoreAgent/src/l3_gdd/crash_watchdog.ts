import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
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
export class CrashWatchdog extends EventEmitter {
  private tailLineLimit: number;
  private explicitLogPath?: string;

  constructor(options: WatchdogOptions = {}) {
    super();
    this.tailLineLimit = options.tailLineLimit || 200;
    this.explicitLogPath = options.logPath;
  }

  /**
   * Attaches watchdog listeners to a spawned Unity child process.
   */
  public watchProcess(childProcess: ChildProcess): void {
    childProcess.on('exit', async (code, signal) => {
      // Exit code 0 is normal success.
      // Exit code 42 is normal clean process recycling requested by URDT.
      if (code === 0 || code === 42) {
        this.emit('clean_exit', { code, signal });
        return;
      }

      const effectiveCode = code ?? -1;
      const dump = await this.inspectCrash(effectiveCode);
      this.emit('crash_detected', dump);
    });

    childProcess.on('error', async (err) => {
      this.emit('process_error', err);
    });
  }

  /**
   * Analyzes the crash log following an abnormal process exit.
   */
  public async inspectCrash(exitCode: number, logOverride?: string): Promise<NativeCrashDump> {
    const targetLogPath = logOverride || this.explicitLogPath || this.resolvePlayerLogPath();
    const tailSnippet = this.extractTrailingLines(targetLogPath, this.tailLineLimit);
    const codeHex = `0x${(exitCode >>> 0).toString(16).toUpperCase().padStart(8, '0')}`;

    const signalName = this.detectSignal(exitCode, tailSnippet);
    const faultingModule = this.extractFaultingModule(tailSnippet);
    const nativeStackTrace = this.extractStackTrace(tailSnippet);

    const dump: NativeCrashDump = {
      exitCode,
      exitCodeHex: codeHex,
      signalName,
      faultingModule,
      nativeStackTrace,
      logTailSnippet: tailSnippet,
      detectedAt: new Date().toISOString(),
      logFilePath: targetLogPath || 'UNKNOWN'
    };

    return dump;
  }

  /**
   * Resolves the most likely location of Player.log.
   */
  public resolvePlayerLogPath(): string | null {
    const candidatePaths: string[] = [
      // Direct project test logs
      path.resolve('E:/Projects/URDT/player_live.log'),
      path.resolve('E:/Projects/URDT/player_test.log'),
      path.resolve('E:/Projects/URDT/Logs/Player.log'),
      path.resolve(process.cwd(), 'player_live.log'),
      path.resolve(process.cwd(), 'player_test.log')
    ];

    if (process.platform === 'win32') {
      const userProfile = process.env.USERPROFILE || 'C:/Users/DavASko';
      candidatePaths.push(
        path.join(userProfile, 'AppData/LocalLow/DavASko/URDT/Player.log'),
        path.join(userProfile, 'AppData/LocalLow/DefaultCompany/URDT/Player.log'),
        path.join(userProfile, 'AppData/LocalLow/DavASko/URDT_gen/Player.log')
      );
    } else {
      const home = os.homedir();
      candidatePaths.push(
        path.join(home, '.config/unity3d/DavASko/URDT/Player.log'),
        path.join(home, '.config/unity3d/DefaultCompany/URDT/Player.log')
      );
    }

    for (const p of candidatePaths) {
      if (fs.existsSync(p)) {
        return p;
      }
    }

    return null;
  }

  /**
   * Extracts the trailing N lines from a log file.
   */
  public extractTrailingLines(filePath: string | null, limit: number): string {
    if (!filePath || !fs.existsSync(filePath)) {
      return `[Watchdog] Log file not found at: ${filePath || 'null'}`;
    }

    try {
      const content = fs.readFileSync(filePath, 'utf-8');
      const lines = content.split(/\r?\n/);
      const trailing = lines.slice(Math.max(0, lines.length - limit));
      return trailing.join('\n');
    } catch (ex) {
      return `[Watchdog] Error reading log file: ${String(ex)}`;
    }
  }

  /**
   * Identifies signal name or crash exception.
   */
  private detectSignal(exitCode: number, logSnippet: string): string | null {
    const hex = (exitCode >>> 0).toString(16).toUpperCase();

    if (hex === 'C0000005' || logSnippet.includes('0xC0000005') || /access violation/i.test(logSnippet)) {
      return 'SIGSEGV / EXCEPTION_ACCESS_VIOLATION (0xC0000005)';
    }
    if (hex === 'C00000FD' || logSnippet.includes('0xC00000FD') || /stack overflow/i.test(logSnippet)) {
      return 'SIGSEGV / EXCEPTION_STACK_OVERFLOW (0xC00000FD)';
    }
    if (hex === '80000003' || /breakpoint/i.test(logSnippet)) {
      return 'STATUS_BREAKPOINT (0x80000003)';
    }
    if (/sigsegv/i.test(logSnippet)) {
      return 'SIGSEGV';
    }
    if (/sigbus/i.test(logSnippet)) {
      return 'SIGBUS';
    }
    if (/sigabrt|abort/i.test(logSnippet)) {
      return 'SIGABRT';
    }
    if (/nullreferenceexception/i.test(logSnippet)) {
      return 'NullReferenceException';
    }
    if (/fatal error in unity/i.test(logSnippet)) {
      return 'FATAL_ENGINE_ERROR';
    }

    return null;
  }

  /**
   * Isolates the faulting native library or subsystem.
   */
  private extractFaultingModule(logSnippet: string): string {
    const moduleMatches = logSnippet.match(/\b([A-Za-z0-9_\-\.]+\.(?:dll|so|dylib))\b/gi);
    if (moduleMatches && moduleMatches.length > 0) {
      for (let i = moduleMatches.length - 1; i >= 0; i--) {
        const mod = moduleMatches[i];
        if (/UnityPlayer|libil2cpp|mono|d3d12|PhysX|nvwgf2umx/i.test(mod)) {
          return mod;
        }
      }
      return moduleMatches[moduleMatches.length - 1];
    }
    return 'UnityEngine.CoreModule';
  }

  /**
   * Extracts call stack lines from log text.
   */
  private extractStackTrace(logSnippet: string): string[] {
    const lines = logSnippet.split(/\r?\n/);
    const trace: string[] = [];
    let capturing = false;

    for (const line of lines) {
      if (/stack\s*trace:|crash!+|\(Filename:|at\s+.*in\s+/i.test(line)) {
        capturing = true;
      }

      if (capturing) {
        const trimmed = line.trim();
        if (trimmed.length > 0) {
          trace.push(trimmed);
        }
        if (trace.length >= 30) break;
      }
    }

    if (trace.length === 0) {
      // Return last 10 non-empty lines as fallback stack context
      const nonEmpty = lines.filter(l => l.trim().length > 0);
      return nonEmpty.slice(-10);
    }

    return trace;
  }
}
