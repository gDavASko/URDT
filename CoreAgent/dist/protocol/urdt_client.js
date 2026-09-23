import { EventEmitter } from 'node:events';
import WebSocket from 'ws';
import { HandshakeManager } from './handshake_manager.js';
export class UrdtWsClient extends EventEmitter {
    url;
    ws = null;
    handshake;
    isConnected = false;
    latestState = null;
    reconnectTimeout = null;
    constructor(url = 'ws://127.0.0.1:9002') {
        super();
        this.url = url;
        this.handshake = new HandshakeManager();
        this.handshake.on('handshake_ok', (response) => {
            this.isConnected = true;
            this.emit('connected', response);
        });
        this.handshake.on('heartbeat_timeout', () => {
            console.warn('[URDT] Heartbeat timeout (>3000ms). Triggering soft reconnect...');
            this.reconnect();
        });
    }
    connect() {
        return new Promise((resolve) => {
            this.attemptConnect(() => resolve());
        });
    }
    attemptConnect(onConnected) {
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
            const socket = this.ws?._socket;
            if (socket && typeof socket.setNoDelay === 'function') {
                socket.setNoDelay(true);
            }
            this.handshake.attach(this.ws);
            onConnected?.();
        });
        this.ws.on('message', (data) => {
            const text = data.toString('utf-8');
            if (this.handshake.handleMessage(text)) {
                return;
            }
            try {
                const parsed = JSON.parse(text);
                if (parsed.beacons || parsed.worldRevision !== undefined) {
                    this.latestState = parsed;
                    this.emit('state', this.latestState);
                }
                else {
                    this.emit('message', parsed);
                }
            }
            catch (err) {
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
    scheduleReconnect() {
        if (this.reconnectTimeout)
            return;
        this.reconnectTimeout = setTimeout(() => {
            this.reconnectTimeout = null;
            this.attemptConnect();
        }, 1000);
    }
    reconnect() {
        if (this.ws) {
            this.ws.terminate();
        }
    }
    sendCommand(cmd) {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            return false;
        }
        this.ws.send(JSON.stringify(cmd));
        return true;
    }
    getLatestState() {
        return this.latestState;
    }
    close() {
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
//# sourceMappingURL=urdt_client.js.map