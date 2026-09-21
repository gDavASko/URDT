/**
 * Universal URDT WebSocket Client for 2D Minigames
 * Protocol API v1
 */

const WebSocket = require('ws');

const DEFAULT_PORT = 7777;
const DEFAULT_URL = `ws://127.0.0.1:${DEFAULT_PORT}/`;
const DEFAULT_TOKEN = 'urdt-test-poligon';
const WAIT_MS = (ms) => new Promise(r => setTimeout(r, ms));

class UrdtClient {
    constructor(url = DEFAULT_URL, token = DEFAULT_TOKEN) {
        this.url = url;
        this.token = token;
        this.socket = null;
        this.seq = 0;
        this.pending = new Map();
        this.connected = false;
    }

    async connect(timeoutMs = 15000) {
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => {
                if (!this.connected) {
                    if (this.socket) try { this.socket.terminate(); } catch (e) {}
                    reject(new Error(`WebSocket connection timeout to ${this.url}`));
                }
            }, timeoutMs);

            this.socket = new WebSocket(this.url);

            this.socket.on('open', async () => {
                try {
                    const hs = await this.call('handshake', { token: this.token });
                    this.connected = true;
                    clearTimeout(timer);
                    resolve(hs);
                } catch (err) {
                    clearTimeout(timer);
                    reject(err);
                }
            });

            this.socket.on('message', (raw) => {
                try {
                    const msg = JSON.parse(raw.toString());
                    const resolver = this.pending.get(msg.id);
                    if (resolver) {
                        this.pending.delete(msg.id);
                        resolver(msg);
                    }
                } catch (e) {
                    console.error('Error parsing message:', e);
                }
            });

            this.socket.on('error', (err) => {
                if (!this.connected) {
                    clearTimeout(timer);
                    reject(err);
                }
            });

            this.socket.on('close', () => {
                this.connected = false;
            });
        });
    }

    call(action, payload = {}) {
        return new Promise((resolve, reject) => {
            const id = String(++this.seq);
            const timeout = setTimeout(() => {
                this.pending.delete(id);
                reject(new Error(`Timeout waiting for action: ${action} (id: ${id})`));
            }, 10000);

            this.pending.set(id, (resp) => {
                clearTimeout(timeout);
                resolve(resp);
            });

            this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
        });
    }

    async inspect(testId) {
        return await this.call('inspect', { testId });
    }

    async query(selector = {}) {
        return await this.call('query', { selector });
    }

    async click(testIdOrCoords) {
        if (typeof testIdOrCoords === 'string') {
            return await this.call('click', { testId: testIdOrCoords });
        } else {
            return await this.call('click', testIdOrCoords);
        }
    }

    async drag(from, to, steps = 20) {
        let payload;
        if (from && typeof from === 'object' && from.from && from.to) {
            payload = from;
        } else {
            payload = { from, to, steps };
        }
        return await this.call('drag', payload);
    }

    async pressMove(path, stepsPerSegment = 8) {
        return await this.call('press_move', { path, stepsPerSegment });
    }

    async pointerDown(testIdOrCoords, pointerId = 0) {
        if (typeof testIdOrCoords === 'string') {
            return await this.call('pointer_down', { testId: testIdOrCoords, pointerId });
        } else {
            return await this.call('pointer_down', { ...testIdOrCoords, pointerId });
        }
    }

    async pointerUp(testIdOrCoords, pointerId = 0) {
        if (typeof testIdOrCoords === 'string') {
            return await this.call('pointer_up', { testId: testIdOrCoords, pointerId });
        } else {
            return await this.call('pointer_up', { ...testIdOrCoords, pointerId });
        }
    }

    async setTimeScale(scale = 1.0) {
        return await this.call('set_time_scale', { scale });
    }

    close() {
        if (this.socket) {
            try { this.socket.close(); } catch (e) {}
        }
    }
}

module.exports = { UrdtClient, WAIT_MS };
