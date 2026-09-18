/**
 * PRODUCTION PATTERN: Step-by-Step Autonomous AI Player Loop
 *
 * This pattern illustrates how an AI Agent drives live gameplay/UI interaction:
 * 1. Perception: Query what is currently visible on screen.
 * 2. Reasoning: Check coordinates, viewport bounds, and modal occlusion.
 * 3. Action: Execute an atomic Tier 2 device-level input.
 * 4. Delta Evaluation: Immediately inspect game state delta before deciding next move.
 */

async function agentTurn(client, targetId, expectedDeltaCheck) {
    // 1. PERCEPTION: Inspect target before acting
    const before = await client.call('inspect', { testId: targetId });
    if (before.status !== 'ok') {
        throw new Error(`Target ${targetId} not found in active registry!`);
    }

    const comp = extractBeaconComponent(before.data);
    const screenPos = before.data.screenPosition;

    // 2. REASONING: Check if element is on screen or clipped by a scroll rect
    if (screenPos && (screenPos.y < 0 || screenPos.y > 1080)) {
        console.log(`Target ${targetId} is clipped at Y=${screenPos.y}. Scrolling into view first...`);
        await client.call('swipe', {
            testId: 'ui.suite_scroll',
            direction: screenPos.y < 0 ? 'up' : 'down',
            distance: 200
        });
        // Re-inspect after scrolling
        return agentTurn(client, targetId, expectedDeltaCheck);
    }

    // 3. ACTION: Dispatch honest device input
    console.log(`Target ${targetId} is visible and hittable. Dispatching click...`);
    const actionResult = await client.call('click', { testId: targetId });
    if (actionResult.status !== 'ok') {
        throw new Error(`Action failed: ${JSON.stringify(actionResult.error)}`);
    }

    // 4. EVALUATION: Inspect live state delta
    const after = await client.call('inspect', { testId: targetId });
    const deltaObserved = expectedDeltaCheck(comp, extractBeaconComponent(after.data));

    if (!deltaObserved) {
        throw new Error(`State delta not observed after interacting with ${targetId}!`);
    }

    console.log(`Delta verified successfully for ${targetId}. AI proceeds to next objective.`);
    return after.data;
}

function extractBeaconComponent(data) {
    if (!data || !data.components) return null;
    for (const key of Object.keys(data.components)) {
        const c = data.components[key];
        if (c && (c.TargetId || c.ScreenRect)) return c;
    }
    return null;
}
