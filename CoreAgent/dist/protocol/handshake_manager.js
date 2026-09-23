import { EventEmitter } from 'node:events';
import WebSocket from 'ws';
export class HandshakeManager extends EventEmitter {
    ws = null;
    pingIntervalTimer = null;
    lastPingSentTime = 0;
    lastPongReceivedTime = 0;
    isHandshakeComplete = false;
    sessionToken;
    constructor(sessionToken) {
        super();
        this.sessionToken = sessionToken || `sess_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
    }
    attach(ws) {
        this.ws = ws;
        this.isHandshakeComplete = false;
        this.sendHandshake();
        this.startHeartbeat();
    }
    sendHandshake() {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN)
            return;
        const req = {
            cmd: 'handshake',
            clientVersion: '2.0.0',
            clientType: 'URDT_CORE_AGENT',
            sessionToken: this.sessionToken,
        };
        this.ws.send(JSON.stringify(req));
    }
    handleMessage(data) {
        try {
            const parsed = JSON.parse(data);
            if (parsed.status === 'connected') {
                this.isHandshakeComplete = true;
                this.emit('handshake_ok', parsed);
                return true;
            }
            if (parsed.cmd === 'pong') {
                this.lastPongReceivedTime = Date.now();
                return true;
            }
        }
        catch {
            // Not a handshake or heartbeat JSON
        }
        return false;
    }
    startHeartbeat() {
        this.stopHeartbeat();
        this.lastPongReceivedTime = Date.now();
        this.pingIntervalTimer = setInterval(() => {
            if (!this.ws || this.ws.readyState !== WebSocket.OPEN)
                return;
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
    stopHeartbeat() {
        if (this.pingIntervalTimer) {
            clearInterval(this.pingIntervalTimer);
            this.pingIntervalTimer = null;
        }
    }
    reset() {
        this.stopHeartbeat();
        this.isHandshakeComplete = false;
        this.ws = null;
    }
}
//# sourceMappingURL=handshake_manager.js.map