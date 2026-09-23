import { EventEmitter } from 'node:events';
import WebSocket from 'ws';

export interface HandshakeRequest {
  cmd: 'handshake';
  clientVersion: string;
  clientType: string;
  sessionToken: string;
}

export interface HandshakeResponse {
  status: 'connected';
  protocolVersion: string;
  engine: string;
  engineVersion: string;
  viewport: { width: number; height: number; dpi: number };
  activeScene: string;
  worldRevision: number;
}

export class HandshakeManager extends EventEmitter {
  private ws: WebSocket | null = null;
  private pingIntervalTimer: NodeJS.Timeout | null = null;
  private lastPingSentTime = 0;
  private lastPongReceivedTime = 0;
  private isHandshakeComplete = false;
  private sessionToken: string;

  constructor(sessionToken?: string) {
    super();
    this.sessionToken = sessionToken || `sess_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
  }

  public attach(ws: WebSocket): void {
    this.ws = ws;
    this.isHandshakeComplete = false;
    this.sendHandshake();
    this.startHeartbeat();
  }

  private sendHandshake(): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return;

    const req: HandshakeRequest = {
      cmd: 'handshake',
      clientVersion: '2.0.0',
      clientType: 'URDT_CORE_AGENT',
      sessionToken: this.sessionToken,
    };

    this.ws.send(JSON.stringify(req));
  }

  public handleMessage(data: string): boolean {
    try {
      const parsed = JSON.parse(data);

      if (parsed.status === 'connected') {
        this.isHandshakeComplete = true;
        this.emit('handshake_ok', parsed as HandshakeResponse);
        return true;
      }

      if (parsed.cmd === 'pong') {
        this.lastPongReceivedTime = Date.now();
        return true;
      }
    } catch {
      // Not a handshake or heartbeat JSON
    }
    return false;
  }

  private startHeartbeat(): void {
    this.stopHeartbeat();
    this.lastPongReceivedTime = Date.now();

    this.pingIntervalTimer = setInterval(() => {
      if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return;

      const now = Date.now();
      // Check if server is unacknowledged for > 3000 ms
      if (this.isHandshakeComplete && now - this.lastPongReceivedTime > 3000) {
        this.emit('heartbeat_timeout');
        return;
      }

      this.lastPingSentTime = now;
      this.ws.send(JSON.stringify({ cmd: 'ping', t: now }));
    }, 1000);
  }

  public stopHeartbeat(): void {
    if (this.pingIntervalTimer) {
      clearInterval(this.pingIntervalTimer);
      this.pingIntervalTimer = null;
    }
  }

  public reset(): void {
    this.stopHeartbeat();
    this.isHandshakeComplete = false;
    this.ws = null;
  }
}
