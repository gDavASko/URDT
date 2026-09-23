import { EventEmitter } from 'node:events';
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
    screenPixel: {
        x: number;
        y: number;
    };
    worldPos: {
        x: number;
        y: number;
        z: number;
    };
    sizeScreen: {
        x: number;
        y: number;
    };
    flags: {
        isInteractable: boolean;
        isSnapped: boolean;
    };
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
export declare class UrdtWsClient extends EventEmitter {
    private url;
    private ws;
    private handshake;
    private isConnected;
    private latestState;
    private reconnectTimeout;
    constructor(url?: string);
    connect(): Promise<void>;
    private attemptConnect;
    private scheduleReconnect;
    reconnect(): void;
    sendCommand(cmd: CommandEnvelope): boolean;
    getLatestState(): StateEnvelope | null;
    close(): void;
}
