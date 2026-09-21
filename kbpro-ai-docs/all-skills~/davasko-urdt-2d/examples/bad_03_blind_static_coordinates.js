/**
 * ANTI-PATTERN: Blind Static Screen Coordinates
 * 
 * WHY THIS FAILS:
 * In Unity Editor Game View, resolution can dynamically scale (e.g. 1920x960 vs 1280x720).
 * Hardcoding static pixel constants causes clicks to miss targets entirely.
 * 
 * Always query live ScreenCenter from the beacon before dispatching input.
 */

// ❌ BAD: Hardcoded static pixel positions
async function hardcodedDrag(client) {
    // FAILS whenever Game View resolution or aspect ratio changes!
    const staticFrom = { x: 490, y: 320 };
    const staticTo = { x: 790, y: 320 };
    await client.drag(staticFrom, staticTo, 20);
}

// ✅ GOOD: Querying dynamic ScreenCenter from live beacon
async function dynamicBeaconDrag(client, itemTestId, slotTestId) {
    const item = await client.inspect(itemTestId);
    const slot = await client.inspect(slotTestId);

    const from = item.data.screenPosition;
    const to = slot.data.screenPosition;

    await client.drag(from, to, 20);
}
