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
    viewport: {
        width: number;
        height: number;
        dpi: number;
    };
    activeScene: string;
    worldRevision: number;
}
export declare class HandshakeManager extends EventEmitter {
    private ws;
    private pingIntervalTimer;
    private lastPingSentTime;
    private lastPongReceivedTime;
    private isHandshakeComplete;
    private sessionToken;
    constructor(sessionToken?: string);
    attach(ws: WebSocket): void;
    private sendHandshake;
    handleMessage(data: string): boolean;
    private startHeartbeat;
    stopHeartbeat(): void;
    reset(): void;
}
