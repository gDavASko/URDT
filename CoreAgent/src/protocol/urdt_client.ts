import { EventEmitter } from 'node:events';
import WebSocket from 'ws';
import { HandshakeManager, HandshakeResponse } from './handshake_manager.js';

export interface CommandEnvelope {
  $schema?: 'urdt/command_v2.json';
  idempotencyKey: string;
  worldRevision: number;
  action: string;
  pointerId: number;
  params: Record<string, unknown>;
  timeoutMs?: number;
}

export interface BeaconState {
  id: string;
  layer: 'PHYSICS_2D' | 'PHYSICS_3D' | 'UI_CANVAS';
  screenPixel: { x: number; y: number };
  worldPos: { x: number; y: number; z: number };
  sizeScreen: { x: number; y: number };
  flags: { isInteractable: boolean; isSnapped: boolean };
}

export interface StateEnvelope {
  $schema?: 'urdt/state_v2.json';
  timestamp: number;
  worldRevision: number;
  sceneName: string;
  activeModalId: string | null;
  beacons: BeaconState[];
  hazards: Array<Record<string, unknown>>;
  eventsQueue: string[];
}

export class UrdtWsClient extends EventEmitter {
  private url: string;
  private ws: WebSocket | null = null;
  private handshake: HandshakeManager;
  private isConnected = false;
  private latestState: StateEnvelope | null = null;
  private reconnectTimeout: NodeJS.Timeout | null = null;

  constructor(url: string = 'ws://127.0.0.1:9002') {
    super();
    this.url = url;
    this.handshake = new HandshakeManager();

    this.handshake.on('handshake_ok', (response: HandshakeResponse) => {
      this.isConnected = true;
      this.emit('connected', response);
    });

    this.handshake.on('heartbeat_timeout', () => {
      console.warn('[URDT] Heartbeat timeout (>3000ms). Triggering soft reconnect...');
      this.reconnect();
    });
  }

  public connect(): Promise<void> {
    return new Promise((resolve) => {
      this.attemptConnect(() => resolve());
    });
  }

  private attemptConnect(onConnected?: () => void): void {
    if (this.ws) {
      this.ws.removeAllListeners();
      this.ws.close();
      this.ws = null;
    }

    // Connect with perMessageDeflate disabled
    this.ws = new WebSocket(this.url, {
      perMessageDeflate: false,
    });

    this.ws.on('open', () => {
      // Set TCP_NODELAY on underlying socket
      const socket = (this.ws as any)?._socket;
      if (socket && typeof socket.setNoDelay === 'function') {
        socket.setNoDelay(true);
      }
      this.handshake.attach(this.ws!);
      onConnected?.();
    });

    this.ws.on('message', (data: WebSocket.Data) => {
      const text = data.toString('utf-8');
      if (this.handshake.handleMessage(text)) {
        return;
      }

      try {
        const parsed = JSON.parse(text);
        if (parsed.beacons || parsed.worldRevision !== undefined) {
          this.latestState = parsed as StateEnvelope;
          this.emit('state', this.latestState);
        } else {
          this.emit('message', parsed);
        }
      } catch (err) {
        this.emit('raw_message', text);
      }
    });

    this.ws.on('error', (err) => {
      this.emit('error', err);
      this.scheduleReconnect();
    });

    this.ws.on('close', () => {
      this.isConnected = false;
      this.handshake.reset();
      this.emit('disconnected');
      this.scheduleReconnect();
    });
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimeout) return;
    this.reconnectTimeout = setTimeout(() => {
      this.reconnectTimeout = null;
      this.attemptConnect();
    }, 1000);
  }

  public reconnect(): void {
    if (this.ws) {
      this.ws.terminate();
    }
  }

  public sendCommand(cmd: CommandEnvelope): boolean {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      return false;
    }
    this.ws.send(JSON.stringify(cmd));
    return true;
  }

  public getLatestState(): StateEnvelope | null {
    return this.latestState;
  }

  public close(): void {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }
    this.handshake.reset();
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
    this.isConnected = false;
  }
}
