/**
 * URDT Wire Client — the transport the CoreAgent actually talks to Unity with.
 *
 * Speaks the protocol implemented by the Unity package (Packages/com.davasko.urdt/Runtime/Net):
 *   request  : { api: 1, id, action, payload, timeout_ms? }
 *   response : { api: 1, id, type: "response", status: "ok" | "error" | "ready", data?, error? }
 *   event    : { api: 1, type: "event", event, data }
 *
 * Features: handshake with session token, request/response correlation with per-call timeouts,
 * exponential reconnect (500 → 1000 → 2000 ms) across Play Mode restarts, and a heartbeat.
 */

import { EventEmitter } from 'node:events';
import WebSocket from 'ws';

export interface WireResponse<T = any> {
  api: number;
  id: string;
  type: 'response';
  status: 'ok' | 'error' | 'ready';
  data?: T;
  error?: { code: string; message: string } | any;
  elapsed_ms?: number;
}

export interface WireClientOptions {
  url?: string;
  token?: string;
  projectId?: string;
  defaultTimeoutMs?: number;
  heartbeatMs?: number;
  log?: (msg: string) => void;
}

export class UrdtCallError extends Error {
  constructor(public readonly action: string, public readonly code: string, message: string, public readonly response?: WireResponse) {
    super(`${action}: ${code} ${message}`);
  }
}

const RECONNECT_BACKOFF_MS = [500, 1000, 2000];

export class UrdtWireClient extends EventEmitter {
  public readonly url: string;
  private readonly token: string;
  private readonly projectId: string;
  private readonly defaultTimeoutMs: number;
  private readonly heartbeatMs: number;
  private readonly log: (msg: string) => void;

  private socket: WebSocket | null = null;
  private seq = 0;
  private readonly pending = new Map<string, { resolve: (r: WireResponse) => void; reject: (e: Error) => void; timer: NodeJS.Timeout; action: string }>();
  private ready = false;
  private closing = false;
  private reconnectAttempt = 0;
  private heartbeat: NodeJS.Timeout | null = null;
  public handshakeInfo: any = null;

  constructor(options: WireClientOptions = {}) {
    super();
    this.url = options.url ?? process.env.URDT_URL ?? 'ws://127.0.0.1:7777/';
    this.token = options.token ?? process.env.URDT_TOKEN ?? 'urdt-test-poligon';
    this.projectId = options.projectId ?? process.env.URDT_PROJECT ?? 'urdt-core-agent';
    this.defaultTimeoutMs = options.defaultTimeoutMs ?? 10000;
    this.heartbeatMs = options.heartbeatMs ?? 1000;
    this.log = options.log ?? (() => undefined);
  }

  public get isReady(): boolean {
    return this.ready;
  }

  /** Connects (retrying until `deadlineMs` elapses) and completes the handshake. */
  public async connect(deadlineMs = 60000): Promise<any> {
    this.closing = false;
    const started = Date.now();
    let lastError: unknown = null;
    while (Date.now() - started < deadlineMs) {
      try {
        await this.openSocket();
        const hs = await this.call('handshake', { token: this.token, projectId: this.projectId }, 5000, true);
        this.handshakeInfo = hs;
        this.ready = true;
        this.reconnectAttempt = 0;
        this.startHeartbeat();
        this.emit('ready', hs);
        return hs;
      } catch (err) {
        lastError = err;
        this.dropSocket();
        const delay = RECONNECT_BACKOFF_MS[Math.min(this.reconnectAttempt++, RECONNECT_BACKOFF_MS.length - 1)];
        await new Promise(r => setTimeout(r, delay));
      }
    }
    throw new Error(`URDT server at ${this.url} not reachable: ${String((lastError as Error)?.message ?? lastError)}`);
  }

  /**
   * Waits until the session is connected AND the Unity runtime is live. The server keeps its socket open
   * across Play Mode stop/start and answers E_RUNTIME_UNAVAILABLE meanwhile, so socket state alone is not
   * enough to detect a restart.
   */
  public async waitReady(deadlineMs = 60000): Promise<void> {
    const started = Date.now();
    if (!this.ready) await this.connect(deadlineMs);
    let announced = false;
    while (Date.now() - started < deadlineMs) {
      const h = await this.call('health', {}, 3000).catch(() => null);
      if (h && (h.status === 'ok' || h.status === 'ready') && h.data?.runtime_ready !== false) return;
      if (!announced) { this.log('[wire] Unity runtime not ready (Play Mode stopped?) — waiting'); announced = true; }
      if (!this.ready) await this.connect(Math.max(1000, deadlineMs - (Date.now() - started))).catch(() => undefined);
      await new Promise(r => setTimeout(r, 500));
    }
    throw new Error('Unity runtime did not become ready');
  }

  private openSocket(): Promise<void> {
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(this.url, { perMessageDeflate: false });
      this.socket = socket;
      socket.once('open', () => {
        (socket as any)._socket?.setNoDelay?.(true);
        resolve();
      });
      socket.once('error', err => reject(err));
      socket.on('message', data => this.onMessage(data.toString('utf-8')));
      socket.on('close', () => this.onClose(socket));
    });
  }

  private onMessage(text: string): void {
    let msg: any;
    try {
      msg = JSON.parse(text);
    } catch {
      this.emit('raw', text);
      return;
    }
    if (msg.type === 'event') {
      this.emit('event', msg);
      if (msg.event) this.emit(`event:${msg.event}`, msg.data);
      return;
    }
    const entry = msg.id != null ? this.pending.get(String(msg.id)) : undefined;
    if (entry) {
      this.pending.delete(String(msg.id));
      clearTimeout(entry.timer);
      entry.resolve(msg as WireResponse);
    }
  }

  private onClose(socket: WebSocket): void {
    if (this.socket !== socket) return;
    const wasReady = this.ready;
    this.ready = false;
    this.stopHeartbeat();
    for (const [id, entry] of this.pending) {
      clearTimeout(entry.timer);
      entry.reject(new UrdtCallError(entry.action, 'E_DISCONNECTED', 'socket closed'));
      this.pending.delete(id);
    }
    if (wasReady) {
      this.emit('disconnected');
      this.log('[wire] disconnected from Unity; entering reconnect loop');
      if (!this.closing) {
        this.connect(10 * 60 * 1000).catch(err => this.emit('fatal', err));
      }
    }
  }

  private dropSocket(): void {
    if (!this.socket) return;
    const s = this.socket;
    this.socket = null;
    s.removeAllListeners();
    s.on('error', () => undefined);
    try { s.terminate(); } catch { /* ignore */ }
  }

  private startHeartbeat(): void {
    this.stopHeartbeat();
    this.heartbeat = setInterval(() => {
      if (!this.ready) return;
      this.call('ping', {}, 3000).catch(() => {
        this.log('[wire] heartbeat missed; forcing reconnect');
        this.socket?.terminate();
      });
    }, this.heartbeatMs);
    this.heartbeat.unref();
  }

  private stopHeartbeat(): void {
    if (this.heartbeat) clearInterval(this.heartbeat);
    this.heartbeat = null;
  }

  /** Raw call: resolves with the full response envelope (status may be "error"). */
  public call<T = any>(action: string, payload: Record<string, unknown> = {}, timeoutMs?: number, allowBeforeReady = false): Promise<WireResponse<T>> {
    return new Promise((resolve, reject) => {
      if (!this.socket || this.socket.readyState !== WebSocket.OPEN || (!this.ready && !allowBeforeReady)) {
        reject(new UrdtCallError(action, 'E_NOT_CONNECTED', 'no live URDT session'));
        return;
      }
      const id = String(++this.seq);
      const t = timeoutMs ?? this.defaultTimeoutMs;
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new UrdtCallError(action, 'E_TIMEOUT', `no response in ${t} ms`));
      }, t);
      this.pending.set(id, { resolve, reject, timer, action });
      this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
    });
  }

  /** Strict call: returns `data`, throws UrdtCallError on protocol errors. */
  public async request<T = any>(action: string, payload: Record<string, unknown> = {}, timeoutMs?: number): Promise<T> {
    const res = await this.call<T>(action, payload, timeoutMs);
    if (res.status === 'error') {
      const code = res.error?.code ?? 'E_UNKNOWN';
      throw new UrdtCallError(action, code, res.error?.message ?? JSON.stringify(res.error), res);
    }
    return res.data as T;
  }

  public close(): void {
    this.closing = true;
    this.ready = false;
    this.stopHeartbeat();
    this.dropSocket();
  }
}
