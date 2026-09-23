import net from 'node:net';
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
};
export class RawCommandSerializer {
    static STRUCT_SIZE = 27;
    static serialize(cmd) {
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
    static deserialize(buf) {
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
    pipePath;
    socket = null;
    isConnected = false;
    frameBuffer = Buffer.alloc(27);
    reconnectTimer = null;
    constructor(pipePath = '\\\\.\\pipe\\urdt_fast_ticker') {
        this.pipePath = pipePath;
    }
    connect() {
        return new Promise((resolve) => {
            this.attemptConnect(() => resolve());
        });
    }
    attemptConnect(onConnected) {
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
    scheduleReconnect() {
        if (this.reconnectTimer)
            return;
        this.reconnectTimer = setTimeout(() => {
            this.reconnectTimer = null;
            this.attemptConnect();
        }, 500);
    }
    static computeCrc8(buf, offset, len) {
        let crc = 0x00;
        const end = offset + len;
        for (let i = offset; i < end; i++) {
            crc ^= buf[i];
            for (let b = 0; b < 8; b++) {
                if ((crc & 0x80) !== 0) {
                    crc = ((crc << 1) ^ 0x07) & 0xFF;
                }
                else {
                    crc = (crc << 1) & 0xFF;
                }
            }
        }
        return crc;
    }
    sendCommand(cmd) {
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
    close() {
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
//# sourceMappingURL=pipe_client.js.map