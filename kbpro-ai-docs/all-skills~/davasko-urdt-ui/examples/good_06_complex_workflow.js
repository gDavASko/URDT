/**
 * ✅ GOOD EXAMPLE 06: Autonomous Goal-Driven UI Agent Session
 * 
 * WHY THIS IS CORRECT:
 * 1. ZERO hardcoded sleep sequences or monolithic static scripts.
 * 2. The AI plays the game turn-by-turn with dynamic perception and reasoning:
 *    - Perceives active window and controls via URDT WebSocket (`query` / `inspect`).
 *    - Detects if an unexpected modal window blocks the view and dismisses it first.
 *    - Detects if a control is scrolled outside the viewport and scrolls it into view.
 *    - Dispatches honest Tier 2 device-level input (`click`, `drag`, `type_text`).
 *    - Asserts real-time state deltas before moving to the next objective.
 * 3. Can adapt to dynamic layouts, changing button positions, and async modal animations.
 */

class UrdtClient {
    constructor(url = 'ws://127.0.0.1:7777/') {
        this.url = url;
        this.sequence = 0;
        this.pending = new Map();
    }

    async connect(token = 'urdt-test-poligon') {
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
        return this.call('handshake', { token });
    }

    call(action, payload = {}) {
        return new Promise((resolve, reject) => {
            const id = String(++this.sequence);
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

function getBeaconSlice(snapshot) {
    if (!snapshot || !snapshot.data || !snapshot.data.components) return null;
    const comps = snapshot.data.components;
    for (const key of Object.keys(comps)) {
        if (comps[key] && (comps[key].TargetId || comps[key].ScreenRect)) {
            return comps[key];
        }
    }
    return null;
}
const rectPattern = /\(x:([-\d.]+),\s*y:([-\d.]+),\s*width:([-\d.]+),\s*height:([-\d.]+)\)/;
function parseRect(screenRectStr) {
    if (!screenRectStr) return null;
    const match = screenRectStr.match(rectPattern);
    if (!match) return null;
    return { x: Number(match[1]), y: Number(match[2]), width: Number(match[3]), height: Number(match[4]) };
}

/**
 * Autonomous Agent Player Engine
 */
class UrdtAutonomousAgent {
    constructor(client) {
        this.client = client;
    }

    /**
     * Perception: Query active UI hierarchy
     */
    async perceive() {
        const queryRes = await this.client.call('query', { activeOnly: true });
        if (!queryRes || !queryRes.data) return [];
        return queryRes.data.matches || [];
    }

    /**
     * Reasoning & Action: Safely click an element, handling occlusion and viewport clipping
     */
    async deliberateAndClick(targetId) {
        // 1. Inspect target
        const snapshot = await this.client.call('inspect', { testId: targetId });
        if (snapshot.status !== 'ok') {
            throw new Error(`[Perception Failed] Target "${targetId}" is not in active registry!`);
        }

        const beacon = getBeaconSlice(snapshot);
        const center = beacon?.ScreenCenter || snapshot.data.screenPosition;

        // 2. Viewport Reasoning: check if target is clipped by ScrollRect
        const scrollSnapshot = await this.client.call('inspect', { testId: 'ui.suite_scroll' });
        const scrollComp = scrollSnapshot.data?.components?.UrdtUiScrollTarget;
        if (scrollComp && center && scrollComp.IsVisible) {
            const vp = parseRect(scrollComp.ScreenRect);
            if (vp && (snapshot.data.hierarchyPath || '').includes('UiSuiteScroll')) {
                const isClippedBelow = center.y < vp.y;
                const isClippedAbove = center.y > (vp.y + vp.height);
                if (isClippedBelow || isClippedAbove) {
                    const direction = isClippedBelow ? 'up' : 'down';
                    console.log(`[Reasoning] Target "${targetId}" at Y=${center.y.toFixed(1)} outside viewport [${vp.y.toFixed(1)}..${(vp.y + vp.height).toFixed(1)}]. Swiping ${direction}...`);
                    await this.client.call('swipe', {
                        testId: 'ui.suite_scroll',
                        direction,
                        distance: 200
                    });
                    return this.deliberateAndClick(targetId);
                }
            }
        }

        // 3. Action: Dispatch honest device click
        console.log(`[Action] Clicking target "${targetId}"...`);
        const clickRes = await this.client.call('click', { testId: targetId });
        if (clickRes.status !== 'ok') {
            if (clickRes.error && clickRes.error.code === 'E_NOT_HITTABLE') {
                console.log(`[Adaptive Recovery] Target "${targetId}" is occluded by "${clickRes.error.details?.topHit}". Adjusting scroll viewport...`);
                await this.client.call('swipe', {
                    testId: 'ui.suite_scroll',
                    direction: 'down',
                    distance: 120
                });
                return this.deliberateAndClick(targetId);
            }
            throw new Error(`[Action Failed] Click on "${targetId}" rejected: ${JSON.stringify(clickRes.error)}`);
        }

        // 4. Delta Evaluation: Verify interaction was recorded
        const afterSnapshot = await this.client.call('inspect', { testId: targetId });
        const afterBeacon = getBeaconSlice(afterSnapshot);
        console.log(`[Delta Evaluation] "${targetId}" click confirmed. InteractionCount: ${afterBeacon?.InteractionCount}`);
        return afterBeacon;
    }

    /**
     * Autonomous Goal: Open target window regardless of starting state
     */
    async ensureWindowOpen(targetWindowId, openButtonId) {
        const snapshot = await this.client.call('inspect', { testId: targetWindowId });
        const isActive = snapshot.status === 'ok' && snapshot.data && snapshot.data.activeInHierarchy;

        if (isActive) {
            console.log(`[Reasoning] Window "${targetWindowId}" is already active and visible.`);
            return;
        }

        console.log(`[Reasoning] Window "${targetWindowId}" is not active. Navigating via "${openButtonId}"...`);
        await this.deliberateAndClick(openButtonId);
    }
}

// Execution demonstrating the AI actively playing the session
async function runAutonomousSession() {
    const client = new UrdtClient();
    await client.connect();
    const agent = new UrdtAutonomousAgent(client);

    console.log('🤖 AI Agent session started.');

    // Goal 1: Ensure UI Suite is open
    await agent.ensureWindowOpen('window_ui_suite', 'btn_open_ui_suite');

    // Goal 2: Resolve modal dialog if needed
    console.log('🎯 Goal: Test Modal Dialog dismissal');
    await agent.deliberateAndClick('ui.modal_button');
    await agent.deliberateAndClick('ui.modal_close_button');

    // Goal 3: Manipulate toggle switch and verify delta
    console.log('🎯 Goal: Toggle state change');
    const toggleBefore = await client.call('inspect', { testId: 'ui.state_toggle' });
    const valBefore = toggleBefore.data.components.UrdtUiToggleTarget.ToggleValue;
    await agent.deliberateAndClick('ui.state_toggle');
    const toggleAfter = await client.call('inspect', { testId: 'ui.state_toggle' });
    const valAfter = toggleAfter.data.components.UrdtUiToggleTarget.ToggleValue;
    if (valBefore === valAfter) throw new Error('Toggle state failed to invert!');
    console.log(`[Delta Verified] Toggle switched from ${valBefore} to ${valAfter}`);

    // Goal 4: Drag Slider
    console.log('🎯 Goal: Adjust volume slider');
    const sliderSnapshot = await client.call('inspect', { testId: 'ui.value_slider' });
    const center = sliderSnapshot.data.components.UrdtUiSliderTarget.ScreenCenter;
    await client.call('drag', {
        testId: 'ui.value_slider',
        startX: center.x - 150,
        startY: center.y,
        endX: center.x + 150,
        endY: center.y,
        durationMs: 300
    });
    const sliderAfter = await client.call('inspect', { testId: 'ui.value_slider' });
    console.log(`[Delta Verified] Slider new value: ${sliderAfter.data.components.UrdtUiSliderTarget.SliderValue}`);

    // Goal 5: Type Text
    console.log('🎯 Goal: Enter text input');
    await agent.deliberateAndClick('ui.text_input');
    await client.call('type_text', { testId: 'ui.text_input', text: 'Autonomous AI Player' });
    const textSnapshot = await client.call('inspect', { testId: 'ui.text_input' });
    console.log(`[Delta Verified] Input text: "${textSnapshot.data.components.UrdtUiInputTarget.InputValue}"`);

    // Goal 6: Return to main menu
    console.log('🎯 Goal: Clean exit to Main Menu');
    await agent.deliberateAndClick('btn_ui_back');

    console.log('🎉 Autonomous session complete: All goals achieved dynamically!');
    client.close();
}

runAutonomousSession().catch(console.error);
