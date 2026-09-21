/**
 * ANTI-PATTERN: Single-Frame Zero-Step Teleport Drag
 * 
 * WHY THIS FAILS:
 * Dispatches from origin to destination in 1 step.
 * Unity EventSystem treats 1-step drags as discrete clicks rather than continuous pointer motion deltas.
 * As a result, IBeginDragHandler and IDragHandler are not fired, and OnDrop is never received by the slot.
 * 
 * Always specify at least 15-20 steps.
 */

// ❌ BAD: Single step drag (teleportation)
async function teleportDrag(client, from, to) {
    // Fails in Unity EventSystem!
    await client.drag(from, to, 1);
}

// ✅ GOOD: Multi-step interpolated drag
async function interpolatedDrag(client, from, to) {
    // Correctly triggers IBeginDragHandler, IDragHandler, and OnDrop
    await client.drag(from, to, 20);
}
