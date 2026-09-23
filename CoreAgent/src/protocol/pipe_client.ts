import net from 'node:net';

export interface RawCommand {
  cmdType: number;           // 1 byte: 0x01=STEER, 0x02=RESISTANCE, 0x03=TAP, 0x04=DRAG, 0x05=RELEASE
  pointerId: number;         // 1 byte: 1..10 (Unity Touch ID)
  sequenceNumber: number;    // 4 bytes: uint32 LE
  screenX: number;           // 4 bytes: float LE
  screenY: number;           // 4 bytes: float LE
  pressure: number;          // 4 bytes: float LE
  targetTimestampMs: number; // 4 bytes: uint32 LE
  idempotencyKey: number;    // 4 bytes: uint32 LE
}

export const UrdtCommandTypes = {
  STEER: 0x01,
  RESISTANCE: 0x02,
  TAP: 0x03,
  DRAG: 0x04,
  RELEASE: 0x05,
  TOUCH_DOWN: 0x01,
  TOUCH_MOVE: 0x04,
  TOUCH_UP: 0x05,
  HEARTBEAT: 0xFF,
} as const;

export class RawCommandSerializer {
  public static readonly STRUCT_SIZE = 27;

  public static serialize(cmd: RawCommand): Buffer {
    const buf = Buffer.alloc(27);
    buf.writeUInt8(cmd.cmdType & 0xFF, 0);
    buf.writeUInt8(cmd.pointerId & 0xFF, 1);
    buf.writeUInt32LE(cmd.sequenceNumber >>> 0, 2);
    buf.writeFloatLE(cmd.screenX, 6);
    buf.writeFloatLE(cmd.screenY, 10);
    buf.writeFloatLE(cmd.pressure, 14);
    buf.writeUInt32LE(cmd.targetTimestampMs >>> 0, 18);
    buf.writeUInt32LE(cmd.idempotencyKey >>> 0, 22);

    const crc = UrdtPipeClient.computeCrc8(buf, 0, 26);
    buf.writeUInt8(crc, 26);
    return buf;
  }

  public static deserialize(buf: Buffer): RawCommand & { crc8: number } {
    return {
      cmdType: buf.readUInt8(0),
      pointerId: buf.readUInt8(1),
      sequenceNumber: buf.readUInt32LE(2),
      screenX: buf.readFloatLE(6),
      screenY: buf.readFloatLE(10),
      pressure: buf.readFloatLE(14),
      targetTimestampMs: buf.readUInt32LE(18),
      idempotencyKey: buf.readUInt32LE(22),
      crc8: buf.readUInt8(26)
    };
  }
}

export class UrdtPipeClient {
  private pipePath: string;
  private socket: net.Socket | null = null;
  private isConnected = false;
  private frameBuffer = Buffer.alloc(27);
  private reconnectTimer: NodeJS.Timeout | null = null;

  constructor(pipePath: string = '\\\\.\\pipe\\urdt_fast_ticker') {
    this.pipePath = pipePath;
  }

  public connect(): Promise<void> {
    return new Promise((resolve) => {
      this.attemptConnect(() => resolve());
    });
  }

  private attemptConnect(onConnected?: () => void): void {
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }

    this.socket = net.createConnection(this.pipePath, () => {
      this.isConnected = true;
      if (this.reconnectTimer) {
        clearInterval(this.reconnectTimer);
        this.reconnectTimer = null;
      }
      onConnected?.();
    });

    this.socket.on('error', () => {
      this.isConnected = false;
      this.scheduleReconnect();
    });

    this.socket.on('close', () => {
      this.isConnected = false;
      this.scheduleReconnect();
    });
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.attemptConnect();
    }, 500);
  }

  public static computeCrc8(buf: Buffer, offset: number, len: number): number {
    let crc = 0x00;
    const end = offset + len;
    for (let i = offset; i < end; i++) {
      crc ^= buf[i];
      for (let b = 0; b < 8; b++) {
        if ((crc & 0x80) !== 0) {
          crc = ((crc << 1) ^ 0x07) & 0xFF;
        } else {
          crc = (crc << 1) & 0xFF;
        }
      }
    }
    return crc;
  }

  public sendCommand(cmd: RawCommand): boolean {
    if (!this.isConnected || !this.socket) {
      return false;
    }

    const buf = this.frameBuffer;
    buf.writeUInt8(cmd.cmdType & 0xFF, 0);
    buf.writeUInt8(cmd.pointerId & 0xFF, 1);
    buf.writeUInt32LE(cmd.sequenceNumber >>> 0, 2);
    buf.writeFloatLE(cmd.screenX, 6);
    buf.writeFloatLE(cmd.screenY, 10);
    buf.writeFloatLE(cmd.pressure, 14);
    buf.writeUInt32LE(cmd.targetTimestampMs >>> 0, 18);
    buf.writeUInt32LE(cmd.idempotencyKey >>> 0, 22);

    const crc = UrdtPipeClient.computeCrc8(buf, 0, 26);
    buf.writeUInt8(crc, 26);

    return this.socket.write(buf);
  }

  public close(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this.isConnected = false;
  }
}
