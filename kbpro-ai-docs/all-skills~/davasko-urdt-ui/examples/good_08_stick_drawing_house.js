/**
 * ✅ GOOD EXAMPLE 08: Autonomous AI Playing with Virtual Stick & Hold-to-Draw Button
 * 
 * OBJECTIVE:
 * The AI autonomously interacts with a Virtual Stick and a "Hold to Draw" button to draw a house on the canvas:
 * 1. Steers the virtual pen using the Virtual Stick (touch pointerId: 2).
 * 2. Holds down the "Рисовать" button (pointer_down on ui.btn_draw with pointerId: 1) when drawing lines (pen down).
 * 3. Releases the button (pointer_up on ui.btn_draw with pointerId: 1) to reposition the pen freely without leaving ink (pen up).
 * 4. Renders the complete house geometry:
 *    - Bottom floor: Move Right (Stick ➔ RIGHT)
 *    - Right wall: Move Up (Stick ➔ UP)
 *    - Right roof slope: Move Up-Left to peak (Stick ➔ UP-LEFT)
 *    - Left roof slope: Move Down-Left to wall (Stick ➔ DOWN-LEFT)
 *    - Left wall: Move Down back to floor (Stick ➔ DOWN)
 *    - Window: Release button (pen up), move stick towards center, hold button (pen down), draw cross, release button.
 * 5. Verifies live state deltas on the canvas via URDT WebSocket telemetry.
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
            }, 10000);

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

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function runStickDrawingHouse() {
    const client = new UrdtClient();
    await client.connect();
    console.log('🚀 Connected to URDT WebSocket');

    // Nudge to realtime
    await client.call('set_time_scale', { scale: 1 });
    await wait(200);

    // 1. Ensure UI Suite Window is active
    const windowSnap = await client.call('inspect', { testId: 'window_ui_suite' });
    if (!windowSnap.data || !windowSnap.data.activeInHierarchy) {
        console.log('📍 Navigating from Main Menu to UI Suite...');
        await client.call('click', { testId: 'btn_open_ui_suite' });
        await wait(600);
    }

    // 2. Clear canvas for a fresh clean drawing
    console.log('🧹 Resetting canvas to start clean...');
    await client.call('click', { testId: 'ui.btn_clear_canvas' });
    await wait(300);

    const canvasBefore = await client.call('inspect', { testId: 'ui.drawing_canvas' });
    const canvasTarget = canvasBefore.data.components.UrdtUiDrawingTarget;
    console.log(`🎨 Initial Canvas State: Pen=(${canvasTarget.PenX}, ${canvasTarget.PenY}), TotalLength=${canvasTarget.TotalDrawnLength}`);

    // 3. Resolve Stick & Button coordinates
    const stickSnap = await client.call('inspect', { testId: 'ui.virtual_stick' });
    const stickCenter = stickSnap.data.screenPosition;
    console.log(`🕹️ Stick Center resolved at: (${stickCenter.x}, ${stickCenter.y})`);

    const btnSnap = await client.call('inspect', { testId: 'ui.btn_draw' });
    console.log(`🔘 Hold-to-Draw Button resolved at: (${btnSnap.data.screenPosition.x}, ${btnSnap.data.screenPosition.y})`);

    // Helper: Deflect stick in (dx, dy) direction on Channel 2 for durationMs
    const tiltStick = async (dx, dy, durationMs = 700) => {
        const offsetDist = 32; // Deflection within 36px radius
        const targetX = stickCenter.x + dx * offsetDist;
        const targetY = stickCenter.y + dy * offsetDist;

        // Touch 2: Deflect stick towards target direction
        await client.call('pointer_down', {
            testId: 'ui.virtual_stick',
            pointerId: 2,
            x: targetX,
            y: targetY
        });

        // Hold stick deflected for durationMs so pen moves at speed and draws line
        await wait(durationMs);

        // Touch 2: release stick back to center
        await client.call('pointer_up', {
            testId: 'ui.virtual_stick',
            pointerId: 2,
            x: stickCenter.x,
            y: stickCenter.y
        });
        await wait(120);
    };

    // Agentic Turn Executor: Executes a single stroke and evaluates live canvas delta
    const executeAgenticStroke = async (name, dx, dy, durationMs, isPenDown = true) => {
        console.log(`\n--- 🔄 [TURN] ${name} ---`);
        // 1. PERCEPTION: Inspect current state before stroke
        const beforeSnap = await client.call('inspect', { testId: 'ui.drawing_canvas' });
        const before = beforeSnap.data.components.UrdtUiDrawingTarget;
        console.log(`   👁️ [Perception] Pen at (${before.PenX.toFixed(1)}, ${before.PenY.toFixed(1)}), Length=${before.TotalDrawnLength.toFixed(1)}px, PenDown=${isPenDown}`);

        // 2. REASONING: Compute expected direction & steering vector
        console.log(`   🧠 [Reasoning] Plan: Steer stick (${dx}, ${dy}) for ${durationMs}ms on Channel 2 while Channel 1 IsHeld=${isPenDown}`);

        // 3. ACTION: Steer virtual stick
        await tiltStick(dx, dy, durationMs);

        // 4. EVALUATION: Measure delta in game state
        const afterSnap = await client.call('inspect', { testId: 'ui.drawing_canvas' });
        const after = afterSnap.data.components.UrdtUiDrawingTarget;
        const deltaLen = after.TotalDrawnLength - before.TotalDrawnLength;
        const deltaX = after.PenX - before.PenX;
        const deltaY = after.PenY - before.PenY;
        console.log(`   🎯 [Evaluation] Pen moved to (${after.PenX.toFixed(1)}, ${after.PenY.toFixed(1)}) (Δx=${deltaX.toFixed(1)}, Δy=${deltaY.toFixed(1)}), ΔLength=+${deltaLen.toFixed(1)}px`);

        if (isPenDown && deltaLen <= 0.1) {
            console.warn(`   ⚠️ Warning: Expected ink deposition, but ΔLength was ${deltaLen}`);
        } else if (!isPenDown && deltaLen > 0.1) {
            console.warn(`   ⚠️ Warning: Pen should be UP, but ink was deposited! ΔLength=${deltaLen}`);
        }
    };

    console.log('\n🏠 --- AI DRAWING MISSION: DRAW A HOUSE STEP-BY-STEP ---');
    console.log('⏳ Внимание! Запуск через 3 секунды. Переключите взгляд на окно Game в Unity...');
    await wait(1000);
    console.log('⏳ 2...');
    await wait(1000);
    console.log('⏳ 1... Поехали!');
    await wait(1000);

    // PHASE A: HOLD THE "DRAW" BUTTON (Touch 1 = Pen Down)
    console.log('\n👉 1️⃣ Holding "Рисовать" button (pointer_down on ui.btn_draw, pointerId: 1)...');
    await client.call('pointer_down', { testId: 'ui.btn_draw', pointerId: 1 });
    await wait(200);

    // PHASE B: DRAW THE HOUSE CONTOUR (Touch 2 steers stick while Touch 1 holds button)
    await executeAgenticStroke('Bottom Floor (Stick ➔ RIGHT)', 1, 0, 750, true);
    await executeAgenticStroke('Right Wall (Stick ➔ UP)', 0, 1, 700, true);
    await executeAgenticStroke('Right Roof Slope to Peak (Stick ➔ UP-LEFT)', -0.7, 0.7, 650, true);
    await executeAgenticStroke('Left Roof Slope to Wall (Stick ➔ DOWN-LEFT)', -0.7, -0.7, 650, true);
    await executeAgenticStroke('Left Wall back to Floor (Stick ➔ DOWN)', 0, -1, 700, true);

    // PHASE C: RELEASE "DRAW" BUTTON (Touch 1 = Pen Up)
    console.log('\n👉 2️⃣ Releasing "Рисовать" button (pointer_up on ui.btn_draw, pointerId: 1)...');
    await client.call('pointer_up', { testId: 'ui.btn_draw', pointerId: 1 });
    console.log('   🔘 Button released: Pen UP (ink disabled)');
    await wait(300);

    // PHASE D: REPOSITION PEN INTO CENTER (Pen Up - no ink deposited)
    await executeAgenticStroke('Reposition Pen to Center (Free Movement with Pen UP)', 0.7, 0.5, 450, false);

    // PHASE E: DRAW WINDOW (Pen Down)
    console.log('\n👉 3️⃣ Holding "Рисовать" button for Window (pointer_down on ui.btn_draw, pointerId: 1)...');
    await client.call('pointer_down', { testId: 'ui.btn_draw', pointerId: 1 });
    await wait(200);

    await executeAgenticStroke('Window Horizontal Crossbar', 1, 0, 250, true);
    await executeAgenticStroke('Window Diagonal Transition', -0.5, 0.5, 180, true);
    await executeAgenticStroke('Window Vertical Crossbar', 0, -1, 250, true);

    console.log('\n👉 4️⃣ Releasing "Рисовать" button (pointer_up on ui.btn_draw, pointerId: 1)...');
    await client.call('pointer_up', { testId: 'ui.btn_draw', pointerId: 1 });
    await wait(300);

    // 5. EVALUATION: Verify live canvas telemetry
    console.log('\n🔍 --- EVALUATION: VERIFYING DRAWING RESULT ---');
    const canvasAfter = await client.call('inspect', { testId: 'ui.drawing_canvas' });
    const finalTarget = canvasAfter.data.components.UrdtUiDrawingTarget;

    console.log(`   Final Pen Position : (${finalTarget.PenX.toFixed(1)}, ${finalTarget.PenY.toFixed(1)})`);
    console.log(`   Stroke Count       : ${finalTarget.StrokeCount}`);
    console.log(`   Total Drawn Length : ${finalTarget.TotalDrawnLength.toFixed(1)} px`);
    console.log(`   Is Drawing Active  : ${finalTarget.IsDrawing}`);

    if (finalTarget.TotalDrawnLength < 50) {
        throw new Error(`Drawing verification failed: TotalDrawnLength is too short (${finalTarget.TotalDrawnLength})`);
    }

    if (finalTarget.StrokeCount < 4) {
        throw new Error(`Drawing verification failed: Expected at least 4 strokes, got ${finalTarget.StrokeCount}`);
    }

    if (finalTarget.IsDrawing) {
        throw new Error('Pen button was not released properly at end of drawing!');
    }

    console.log('\n🎉 MISSION ACCOMPLISHED! The AI has autonomously drawn the house using the Virtual Stick and Hold-to-Draw button simultaneously!');
    client.close();
}

runStickDrawingHouse().catch(err => {
    console.error('❌ Error executing stick drawing session:', err);
    process.exit(1);
});
