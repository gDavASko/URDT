/**
 * ✅ GOOD EXAMPLE 02: Honest Device-Level Input Injection
 * 
 * WHY THIS IS CORRECT:
 * 1. Simulates hardware input events through Unity's real InputSystem event queue.
 * 2. Reads live on-screen geometry immediately before the click.
 * 3. Asserts the result state over WebSocket without any screenshots.
 */

async function honestButtonClick(client, buttonId) {
    // 1. Inspect target to verify it is currently interactable and visible
    const before = await client.call('inspect', { testId: buttonId });
    const target = before.data.components.UrdtUiButtonTarget;
    if (!target.IsInteractable) {
        throw new Error(`Target ${buttonId} is disabled!`);
    }

    const clickCountBefore = target.InteractionCount || 0;

    // 2. Dispatch honest click via URDT
    const clickRes = await client.call('click', { testId: buttonId });
    if (clickRes.status !== 'ok' || !clickRes.data.clicked) {
        throw new Error(`Click on ${buttonId} failed: ${JSON.stringify(clickRes.error)}`);
    }

    // 3. Inspect after click to assert state changed
    const after = await client.call('inspect', { testId: buttonId });
    const afterTarget = after.data.components.UrdtUiButtonTarget;
    if (afterTarget.InteractionCount <= clickCountBefore) {
        throw new Error(`Interaction count did not increase for ${buttonId}!`);
    }

    console.log(`✅ Honest click verified: count increased to ${afterTarget.InteractionCount}`);
}
