/**
 * URDT Atomic CLI Tool
 * Allows AI agents and developers to play, inspect, and interact with live Unity UI step-by-step
 * through atomic single-action CLI commands or persistent REPL session over WebSocket.
 * 
 * Usage:
 *   Standalone:
 *     node Tools/urdt-cli.js inspect <testId>
 *     node Tools/urdt-cli.js query [--active]
 *     node Tools/urdt-cli.js click <testId>
 *     node Tools/urdt-cli.js pointer_down <testId> [--pointerId <N>] [--x <x>] [--y <y>]
 *     node Tools/urdt-cli.js pointer_up <testId> [--pointerId <N>] [--x <x>] [--y <y>]
 *     node Tools/urdt-cli.js tilt_stick <dx> <dy> [--duration <ms>] [--pointerId <N>]
 *     node Tools/urdt-cli.js wait <ms>
 * 
 *   REPL / Pipeline Mode (Single persistent session):
 *     node Tools/urdt-cli.js --repl
 */

const readline = require('readline');

const URL = process.env.URDT_URL || 'ws://127.0.0.1:7777/';
const TOKEN = process.env.URDT_TOKEN || 'urdt-test-poligon';
const PROJECT_ID = process.env.URDT_PROJECT || 'urdt-project';

const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));

class UrdtClient {
    constructor(url = URL) {
        this.url = url;
        this.seq = 0;
        this.pending = new Map();
    }

    async connect() {
        this.socket = new WebSocket(this.url);
        await new Promise((resolve, reject) => {
            this.socket.onopen = resolve;
            this.socket.onerror = reject;
            this.socket.onmessage = (event) => {
                const msg = JSON.parse(event.data);
                const handler = this.pending.get(msg.id);
                if (handler) {
                    this.pending.delete(msg.id);
                    handler(msg);
                }
            };
        });
        return this.call('handshake', { token: TOKEN, projectId: PROJECT_ID });
    }

    call(action, payload = {}) {
        return new Promise((resolve, reject) => {
            const id = String(++this.seq);
            const timeout = setTimeout(() => {
                this.pending.delete(id);
                reject(new Error(`Timeout awaiting action "${action}"`));
            }, 8000);

            this.pending.set(id, (response) => {
                clearTimeout(timeout);
                resolve(response);
            });

            this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
        });
    }

    close() {
        if (this.socket) this.socket.close();
    }
}

function parseTokens(tokens) {
    const command = tokens[0];
    const positional = [];
    const flags = {};

    for (let i = 1; i < tokens.length; i++) {
        if (tokens[i].startsWith('--')) {
            const key = tokens[i].substring(2);
            if (i + 1 < tokens.length && !tokens[i + 1].startsWith('--')) {
                flags[key] = tokens[++i];
            } else {
                flags[key] = true;
            }
        } else {
            positional.push(tokens[i]);
        }
    }

    return { command, positional, flags };
}

function formatTelemetry(components) {
    if (!components) return '';
    const lines = [];
    for (const [name, data] of Object.entries(components)) {
        if (name === 'UrdtUiStickTarget') {
            lines.push(`  🕹️ Stick: Value=(${data.StickX?.toFixed(2)}, ${data.StickY?.toFixed(2)}), Mag=${data.Magnitude?.toFixed(2)}, Pressed=${data.IsPressed}`);
        } else if (name === 'UrdtUiDrawingTarget') {
            lines.push(`  🎨 Drawing: Pen=(${data.PenX?.toFixed(1)}, ${data.PenY?.toFixed(1)}), Strokes=${data.StrokeCount}, Length=${data.TotalDrawnLength?.toFixed(1)}px, Drawing=${data.IsDrawing}`);
        } else if (name === 'UrdtUiButtonTarget') {
            lines.push(`  🔘 Button: Clicks=${data.InteractionCount}, Result=${data.LastResult || 'none'}`);
        } else if (name === 'UrdtUiToggleTarget') {
            lines.push(`  ☑️ Toggle: Value=${data.ToggleValue}`);
        } else if (name === 'UrdtUiSliderTarget') {
            lines.push(`  🎚️ Slider: Value=${data.SliderValue}`);
        } else if (name === 'UrdtUiInputTarget') {
            lines.push(`  📝 Input: Value="${data.InputValue}"`);
        } else if (name === 'UrdtUiScrollTarget') {
            lines.push(`  📜 Scroll: Pos=(${data.ScrollX?.toFixed(2)}, ${data.ScrollY?.toFixed(2)})`);
        }
    }
    return lines.join('\n');
}

async function handleCommand(client, command, positional, flags) {
    switch (command) {
        case 'inspect': {
            const testId = positional[0];
            if (!testId) throw new Error('Missing testId');
            const res = await client.call('inspect', { testId });
            if (res.status !== 'ok') {
                console.log(`❌ Inspect failed: ${res.error || res.status}`);
            } else {
                const d = res.data;
                console.log(`🔎 [INSPECT] ${testId}:`);
                console.log(`  Window: ${d.activeWindow || 'none'} | Active: ${d.activeInHierarchy} | Pos: (${d.screenPosition?.x}, ${d.screenPosition?.y})`);
                const telem = formatTelemetry(d.components);
                if (telem) console.log(telem);
            }
            break;
        }

            case 'query': {
                const activeOnly = Boolean(flags.active || flags.activeOnly);
                const res = await client.call('query', { activeOnly });
                if (res.status !== 'ok') {
                    console.log(`❌ Query failed: ${res.error || res.status}`);
                } else {
                    const targets = res.data?.matches || res.data?.targets || [];
                    console.log(`📋 [QUERY] Found ${targets.length} targets (activeOnly=${activeOnly}):`);
                    for (const t of targets) {
                        console.log(`  - ${(t.testId || t.targetId || 'unnamed').padEnd(25)} win=${t.activeWindow || 'none'} pos=(${t.screenPosition?.x}, ${t.screenPosition?.y})`);
                    }
                }
                break;
            }

        case 'click': {
            const testId = positional[0];
            if (!testId) throw new Error('Missing testId');
            const res = await client.call('click', { testId });
            console.log(`👆 [CLICK] ${testId}: status=${res.status}`);
            break;
        }

        case 'pointer_down': {
            const testId = positional[0];
            if (!testId) throw new Error('Missing testId');
            const payload = { testId };
            if (flags.pointerId !== undefined) payload.pointerId = Number(flags.pointerId);
            if (flags.x !== undefined) payload.x = Number(flags.x);
            if (flags.y !== undefined) payload.y = Number(flags.y);
            const res = await client.call('pointer_down', payload);
            console.log(`👇 [POINTER_DOWN] ${testId} (pointerId=${payload.pointerId ?? 0}): status=${res.status}`);
            break;
        }

        case 'pointer_up': {
            const testId = positional[0];
            if (!testId) throw new Error('Missing testId');
            const payload = { testId };
            if (flags.pointerId !== undefined) payload.pointerId = Number(flags.pointerId);
            if (flags.x !== undefined) payload.x = Number(flags.x);
            if (flags.y !== undefined) payload.y = Number(flags.y);
            const res = await client.call('pointer_up', payload);
            console.log(`☝️ [POINTER_UP] ${testId} (pointerId=${payload.pointerId ?? 0}): status=${res.status}`);
            break;
        }

        case 'tilt_stick': {
            const dx = parseFloat(positional[0] || '0');
            const dy = parseFloat(positional[1] || '0');
            const durationMs = flags.duration ? Number(flags.duration) : 600;
            const pointerId = flags.pointerId !== undefined ? Number(flags.pointerId) : 2;

            const stickSnap = await client.call('inspect', { testId: 'ui.virtual_stick' });
            if (!stickSnap.data || !stickSnap.data.screenPosition) {
                throw new Error('Could not resolve screen position for ui.virtual_stick');
            }
            const center = stickSnap.data.screenPosition;
            const offsetDist = 32;
            const targetX = center.x + dx * offsetDist;
            const targetY = center.y + dy * offsetDist;

            console.log(`🕹️ [TILT_STICK] dir=(${dx}, ${dy}) target=(${targetX.toFixed(1)}, ${targetY.toFixed(1)}) duration=${durationMs}ms...`);

            // Deflect stick by pressing at target offset
            await client.call('pointer_down', { testId: 'ui.virtual_stick', pointerId, x: targetX, y: targetY });

            // Hold stick deflected for durationMs so pen moves at speed and draws line
            await wait(durationMs);

            // Release stick back to center
            await client.call('pointer_up', { testId: 'ui.virtual_stick', pointerId, x: targetX, y: targetY });
            await wait(100);

            console.log(`🕹️ [TILT_STICK] Complete. Stick auto-centered.`);
            break;
        }

        case 'wait': {
            const ms = Number(positional[0] || '500');
            await wait(ms);
            console.log(`⏳ [WAIT] ${ms}ms passed`);
            break;
        }

        case 'type_text': {
            const testId = positional[0];
            const text = positional.slice(1).join(' ');
            if (!testId || text === undefined) throw new Error('Usage: type_text <testId> <text>');
            const res = await client.call('type_text', { testId, text });
            console.log(`⌨️ [TYPE_TEXT] ${testId} -> "${text}": status=${res.status}`);
            break;
        }

        case 'set_time_scale': {
            const scale = parseFloat(positional[0] || '1');
            const res = await client.call('set_time_scale', { scale });
            console.log(`⏱️ [TIME_SCALE] scale=${scale}: status=${res.status}`);
            break;
        }

        case 'hit_test': {
            const testId = positional[0];
            const payload = testId ? (testId.includes(',') ? { x: Number(testId.split(',')[0]), y: Number(testId.split(',')[1]) } : { testId }) : {};
            const res = await client.call('hit_test', payload);
            console.log(`🎯 [HIT_TEST] Result:`, JSON.stringify(res.data, null, 2));
            break;
        }

        case 'call': {
            const action = positional[0];
            if (!action) throw new Error('Usage: call <action> [jsonPayload]');
            const raw = positional.slice(1).join(' ') || '{}';
            const payload = JSON.parse(raw);
            const res = await client.call(action, payload);
            console.log(JSON.stringify(res, null, 2));
            break;
        }

        case 'drag': {
            const fromRaw = positional[0];
            const toRaw = positional[1];
            const steps = flags.steps ? Number(flags.steps) : 10;
            const pointerId = flags.pointerId !== undefined ? Number(flags.pointerId) : 0;
            const parsePoint = (pt) => {
                if (!pt) throw new Error('Invalid point: ' + pt);
                if (pt.includes(',')) {
                    const [x, y] = pt.split(',').map(Number);
                    return { x, y };
                }
                return { testId: pt };
            };
            const payload = {
                from: parsePoint(fromRaw),
                to: parsePoint(toRaw),
                steps,
                pointerId
            };
            const res = await client.call('drag', payload);
            console.log(`↔️ [DRAG] status=${res.status}`, res.data || res.error);
            break;
        }

        case 'press_move': {
            const steps = flags.steps ? Number(flags.steps) : 10;
            const pointerId = flags.pointerId !== undefined ? Number(flags.pointerId) : 0;
            const pathJson = positional.join(' ');
            const path = JSON.parse(pathJson);
            const payload = { path, steps, pointerId };
            const res = await client.call('press_move', payload);
            console.log(`〰️ [PRESS_MOVE] status=${res.status}`, res.data || res.error);
            break;
        }

        default:
            throw new Error(`Unknown command: "${command}"`);
    }
}

async function main() {
    const rawArgs = process.argv.slice(2);
    const isRepl = rawArgs.includes('--repl') || rawArgs.includes('-i');

    const client = new UrdtClient();
    await client.connect();

    if (isRepl) {
        console.log(`🚀 URDT REPL session started. Type commands line-by-line.`);
        console.log(`[URDT_READY]`);

        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout,
            terminal: false
        });

        for await (const line of rl) {
            const trimmed = line.trim();
            if (!trimmed || trimmed.startsWith('#') || trimmed.startsWith('//')) {
                continue;
            }
            if (trimmed === 'exit' || trimmed === 'quit') {
                break;
            }

            const tokens = trimmed.match(/(?:[^\s"]+|"[^"]*")+/g).map(t => t.replace(/^"|"$/g, ''));
            const { command, positional, flags } = parseTokens(tokens);

            try {
                await handleCommand(client, command, positional, flags);
            } catch (err) {
                console.error(`❌ Error executing "${trimmed}":`, err.message);
            }
            console.log(`[URDT_READY]`);
        }

        client.close();
        process.exit(0);
    } else {
        const { command, positional, flags } = parseTokens(rawArgs);
        if (!command) {
            console.log(`URDT Atomic CLI\nUsage: node Tools/urdt-cli.js <command> [args] [--flags] | node Tools/urdt-cli.js --repl`);
            client.close();
            process.exit(0);
        }

        try {
            await handleCommand(client, command, positional, flags);
        } finally {
            client.close();
        }
    }
}

main().catch(err => {
    console.error('❌ Fatal:', err.message);
    process.exit(1);
});
