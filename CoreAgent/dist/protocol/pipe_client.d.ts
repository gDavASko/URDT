export interface RawCommand {
    cmdType: number;
    pointerId: number;
    sequenceNumber: number;
    screenX: number;
    screenY: number;
    pressure: number;
    targetTimestampMs: number;
    idempotencyKey: number;
}
export declare const UrdtCommandTypes: {
    readonly STEER: 1;
    readonly RESISTANCE: 2;
    readonly TAP: 3;
    readonly DRAG: 4;
    readonly RELEASE: 5;
    readonly TOUCH_DOWN: 1;
    readonly TOUCH_MOVE: 4;
    readonly TOUCH_UP: 5;
    readonly HEARTBEAT: 255;
};
export declare class RawCommandSerializer {
    static readonly STRUCT_SIZE = 27;
    static serialize(cmd: RawCommand): Buffer;
    static deserialize(buf: Buffer): RawCommand & {
        crc8: number;
    };
}
export declare class UrdtPipeClient {
    private pipePath;
    private socket;
    private isConnected;
    private frameBuffer;
    private reconnectTimer;
    constructor(pipePath?: string);
    connect(): Promise<void>;
    private attemptConnect;
    private scheduleReconnect;
    static computeCrc8(buf: Buffer, offset: number, len: number): number;
    sendCommand(cmd: RawCommand): boolean;
    close(): void;
}
