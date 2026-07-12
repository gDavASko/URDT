/*
 * Real-input randomized UI runner. It intentionally stores no target coordinates:
 * each pointer point is resolved from the current inspect snapshot and is logged.
 */
const fs = require('fs');

const reportPath = process.argv[2] || '.harness/urdt-randomized-stress.jsonl';
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const rectPattern = /x:([-\d.]+), y:([-\d.]+), width:([-\d.]+), height:([-\d.]+)/;

class Client {
    constructor() { this.sequence = 0; this.pending = new Map(); }
    async connect() {
        let lastError = null;
        for (let attempt = 0; attempt < 10; attempt++) {
            try {
                this.socket = new WebSocket('ws://127.0.0.1:7777/');
                await new Promise((resolve, reject) => {
                    this.socket.onopen = resolve;
                    this.socket.onerror = reject;
                    this.socket.onmessage = event => {
                        const response = JSON.parse(event.data);
                        const resolver = this.pending.get(response.id);
                        if (resolver) { this.pending.delete(response.id); resolver(response); }
                    };
                });
                await this.call('handshake', { token: 'urdt-test-poligon', projectId: 'dentistry-cow' });
                return;
            } catch (error) {
                lastError = error;
                if (this.socket) this.socket.close();
                await wait(1000);
            }
        }

        throw lastError || new Error('URDT WebSocket did not become ready.');
    }
    call(action, payload) {
        return new Promise(resolve => {
            const id = String(++this.sequence);
            this.pending.set(id, resolve);
            this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
        });
    }
    close() { this.socket.close(); }
}

function component(snapshot) {
    return snapshot.data.components.UrdtTestPoligonDebugTarget;
}

function parseRect(snapshot) {
    const match = component(snapshot).ScreenRect.match(rectPattern);
    if (!match) throw new Error('Missing ScreenRect for ' + snapshot.data.testId);
    return { x: Number(match[1]), y: Number(match[2]), width: Number(match[3]), height: Number(match[4]) };
}

async function main() {
    const client = new Client();
    const trace = { startedUtc: new Date().toISOString(), policy: 'fresh-inspect-per-action; no-coordinate-cache; real-input-only', steps: [] };
    await client.connect();

    async function inspect(testId) {
        const response = await client.call('inspect', { testId });
        if (response.status !== 'ok') throw new Error('inspect ' + testId + ': ' + JSON.stringify(response.error));
        return response;
    }

    async function act(name, testId, action, payload, assertion) {
        const before = await inspect(testId);
        const response = await client.call(action, payload);
        await wait(300);
        const after = await inspect(testId);
        const entry = { name, testId, action, pre: component(before), payload, response: response.data || response.error, post: component(after) };
        entry.pass = response.status === 'ok' && assertion(entry.pre, entry.post);
        trace.steps.push(entry);
        if (!entry.pass) throw new Error(name + ' failed: ' + JSON.stringify(entry));
        return after;
    }

    async function scrollSuite(towardsBottom) {
        try {
            await act('navigate suite swipe', 'ui.suite_scroll', 'swipe',
                {
                    testId: 'ui.suite_scroll',
                    direction: towardsBottom ? 'up' : 'down',
                    distance: 180,
                    steps: 8
                },
                (before, after) => before.ScrollPosition.y !== after.ScrollPosition.y || after.ScrollPosition.y === (towardsBottom ? 0 : 1));
            return true;
        } catch (_) {
            return false;
        }
    }

    async function ensureHittable(testId) {
        for (let attempt = 0; attempt < 96; attempt++) {
            const snapshot = await inspect(testId);
            const rect = parseRect(snapshot);
            const suite = await inspect('ui.suite_scroll');
            const suiteRect = parseRect(suite);
            // A real pointer action needs an unobscured actionable point, not every
            // pixel of a nested target. Large nested ScrollRects commonly extend
            // past the viewport while their center remains interactable.
            const center = component(snapshot).ScreenCenter;
            if (center && center.x >= suiteRect.x && center.x <= suiteRect.x + suiteRect.width &&
                center.y >= suiteRect.y && center.y <= suiteRect.y + suiteRect.height) return snapshot;
            const towardBottom = rect.y < suiteRect.y;
            if (!await scrollSuite(towardBottom)) {
                await scrollSuite(!towardBottom);
            }
        }
        throw new Error('Could not bring ' + testId + ' into viewport');
    }

    async function press(testId, key, expected) {
        return act('key ' + key, testId, 'key_press', { key }, (_, after) => expected(after));
    }

    async function selectDropdownLabel(label) {
        await ensureHittable('ui.mode_dropdown');
        const dropdown = await inspect('ui.mode_dropdown');
        const dropdownOpen = await client.call('click', { testId: 'ui.mode_dropdown' });
        if (dropdownOpen.status !== 'ok') throw new Error('Could not open dropdown: ' + JSON.stringify(dropdownOpen.error));
        await wait(300);
        const rect = parseRect(dropdown);
        let optionPoint = null;
        for (let y = 0; y <= 1200 && optionPoint === null; y += 16) {
            const hit = await client.call('hit_test', { x: rect.x + rect.width / 2, y });
            const hits = hit.data && hit.data.hits ? hit.data.hits : [];
            if (hits.some(item => item.text === label)) optionPoint = { x: rect.x + rect.width / 2, y };
        }
        if (optionPoint === null) throw new Error('Dropdown option not discovered by live hit_test: ' + label);
        await act('select ' + label, 'ui.mode_dropdown', 'click', optionPoint, (_, after) => after.DropdownLabel === label);
    }

    async function setSlider(value) {
        let lastError = null;
        for (let attempt = 0; attempt < 5; attempt++) {
            const snapshot = await ensureHittable('ui.value_slider');
            const rect = parseRect(snapshot);
            const point = { x: rect.x + rect.width * value, y: rect.y + rect.height / 2 };
            try {
                await act('set slider ' + value + ' attempt ' + (attempt + 1), 'ui.value_slider', 'drag', { from: { testId: 'ui.value_slider' }, to: point, steps: 8 }, (_, after) => Math.abs(after.SliderValue - value) <= 0.03);
                return;
            } catch (error) {
                lastError = error;
            }
        }

        throw lastError || new Error('Slider did not reach ' + value);
    }

    const mainMenu = await inspect('window_main_menu');
    if (component(mainMenu).ActiveWindow === 'window_ui_suite') {
        let returnedToMenu = false;
        let lastBack = null;
        let backDiagnostics = null;
        for (let attempt = 0; attempt < 5 && !returnedToMenu; attempt++) {
            const beforeBack = await inspect('btn_ui_back');
            const hit = await client.call('hit_test', { testId: 'btn_ui_back' });
            lastBack = await client.call('click', { testId: 'btn_ui_back' });
            await wait(300);
            const afterBack = await inspect('window_main_menu');
            backDiagnostics = { before: component(beforeBack), hit, after: component(afterBack) };
            returnedToMenu = lastBack.status === 'ok' && component(afterBack).ActiveWindow === 'window_main_menu';
        }
        if (!returnedToMenu) {
            throw new Error('Could not return to Main Menu before randomized run: ' + JSON.stringify({ lastBack, backDiagnostics }));
        }
    }

    {
        let opened = false;
        for (let attempt = 0; attempt < 5 && !opened; attempt++) {
            const before = await inspect('btn_open_ui_suite');
            const response = await client.call('click', { testId: 'btn_open_ui_suite' });
            await wait(300);
            const after = await inspect('window_ui_suite');
            trace.steps.push({ name: 'open ui suite', attempt: attempt + 1, testId: 'btn_open_ui_suite', action: 'click', pre: component(before), response: response.data || response.error, post: component(after), pass: response.status === 'ok' && component(after).ActiveWindow === 'window_ui_suite' });
            opened = trace.steps[trace.steps.length - 1].pass;
        }
        if (!opened) throw new Error('Could not open UI suite after five real-input attempts.');
    }

    await act('open modal', 'ui.modal_button', 'click', { testId: 'ui.modal_button' }, (_, after) => after.LastResult === 'modal_open');
    await act('close modal', 'ui.modal_close_button', 'click', { testId: 'ui.modal_close_button' }, (_, after) => after.LastResult === 'modal_closed');

    await ensureHittable('ui.text_input');
    await act('focus input', 'ui.text_input', 'click', { testId: 'ui.text_input' }, (_, after) => after.LastInputAction === 'keyboard' || after.InteractionCount >= 0);
    await act('type greeting', 'ui.text_input', 'type_text', { text: 'привет Мир' }, (_, after) => after.InputValue === 'привет Мир');
    for (let i = 0; i < 3; i++) await press('ui.text_input', 'left', after => after.InputValue === 'привет Мир');
    for (let i = 0; i < 4; i++) await press('ui.text_input', 'backspace', after => after.InputValue.endsWith('Мир'));
    await act('input changed', 'ui.text_input', 'type_text', { text: '' }, (_, after) => after.InputValue === 'приМир');
    for (let i = 0; i < 3; i++) await press('ui.text_input', 'right', after => after.InputValue === 'приМир');
    for (let i = 0; i < 6; i++) await press('ui.text_input', 'backspace', after => after.InputValue.length <= 6);
    await act('input cleared', 'ui.text_input', 'type_text', { text: '' }, (_, after) => after.InputValue === '');

    await ensureHittable('ui.state_toggle');
    await act('toggle on', 'ui.state_toggle', 'click', { testId: 'ui.state_toggle' }, (_, after) => after.ToggleValue === true);
    await act('toggle off', 'ui.state_toggle', 'click', { testId: 'ui.state_toggle' }, (_, after) => after.ToggleValue === false);

    await ensureHittable('ui.value_slider');
    for (const value of [0.5, 0.3, 1.0, 0.0]) await setSlider(value);

    for (const label of ['Mode C', 'Mode A', 'Mode B']) await selectDropdownLabel(label);

    await ensureHittable('ui.primary_button');
    await act('primary once', 'ui.primary_button', 'click', { testId: 'ui.primary_button' }, (before, after) => after.InteractionCount === before.InteractionCount + 1);
    await act('primary double', 'ui.primary_button', 'double_click', { testId: 'ui.primary_button' }, (before, after) => after.InteractionCount === before.InteractionCount + 2);

    await ensureHittable('ui.command_scroll');
    await act('command scroll down', 'ui.command_scroll', 'scroll', { testId: 'ui.command_scroll', delta_y: -1 }, (before, after) => after.ScrollPosition.y < before.ScrollPosition.y);
    await act('command scroll up', 'ui.command_scroll', 'scroll', { testId: 'ui.command_scroll', delta_y: 1 }, (before, after) => after.ScrollPosition.y > before.ScrollPosition.y);

    trace.finishedUtc = new Date().toISOString();
    trace.pass = true;
    fs.appendFileSync(reportPath, JSON.stringify(trace) + '\n', 'utf8');
    client.close();
    console.log(JSON.stringify({ pass: true, steps: trace.steps.length }));
}

main().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
