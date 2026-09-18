/**
 * ANTI-PATTERN: The "Dead Script" / Monolithic Batch Runner
 *
 * DO NOT DO THIS!
 *
 * Why this is bad:
 * 1. The AI delegates the entire testing loop to a static, hardcoded script.
 * 2. If the UI layout changes, or a button is moved, or an unexpected modal dialog
 *    appears, the script blindly crashes because it has zero runtime reasoning.
 * 3. The AI is detached from the game, acting merely as a passive spectator.
 *
 * CORRECT APPROACH:
 * The AI itself must drive the session turn-by-turn:
 * - Query what is on screen.
 * - Reason about the current UI layout and modal states.
 * - Dispatch one targeted action.
 * - Verify the delta immediately before proceeding.
 */

// ANTI-PATTERN: Hardcoding all 14 steps into a monolithic runner
async function badMonolithicRunner(client) {
    // Rigid step 1: blind click
    await client.call('click', { testId: 'btn_open_ui_suite' });
    await sleep(1000);

    // Rigid step 2: assuming modal button is visible without checking viewport
    await client.call('click', { testId: 'ui.modal_button' });
    await sleep(1000);

    // Rigid step 3: assuming modal close button is hittable
    await client.call('click', { testId: 'ui.modal_close_button' });
    await sleep(1000);

    // Rigid step 4: clicking an input field that might be scrolled off-screen!
    // If the scroll position is different, this click fails or hits the wrong element!
    await client.call('click', { testId: 'ui.text_input' });
    await client.call('type_text', { text: 'Hello' });

    // CRASH! If anything changed, this script cannot adapt, reason, or recover!
}
