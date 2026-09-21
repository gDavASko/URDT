/**
 * Good Example 02: Dynamic Drag & Drop with Dwell Settling
 * Demonstrates filter by !IsJunk, multi-step drag, and settling pause for elastic mechanics.
 */

const { UrdtClient, WAIT_MS } = require('./urdt_client');

async function solveSnapToSlot(client, mechanicId = 'M01_SnapToSlot') {
    console.log(`Solving ${mechanicId} dynamically...`);

    // Fetch live scene items
    const query = await client.query({ activeOnly: true });
    const items = query.data?.matches || [];

    // Filter valid items: exclude junk and already snapped objects
    const validItems = items.filter(x => {
        const d = x.components?.Urdt2DDraggableTarget;
        return d && !d.IsJunk && !d.IsSnapped;
    });

    const slots = items.filter(x => x.components?.Urdt2DSlotTarget && !x.components.Urdt2DSlotTarget.IsOccupied);

    console.log(`Valid items: ${validItems.length}, Available slots: ${slots.length}`);

    for (const item of validItems) {
        const itemTarget = item.components.Urdt2DDraggableTarget;
        // Match slot by ID or accepted type
        const matchingSlot = slots.find(s => {
            const slotTarget = s.components.Urdt2DSlotTarget;
            return slotTarget.SlotId === itemTarget.ItemId || slotTarget.AcceptedType === itemTarget.Category;
        });

        if (!matchingSlot) continue;

        console.log(`Dragging ${item.testId} from (${item.screenPosition.x}, ${item.screenPosition.y}) to (${matchingSlot.screenPosition.x}, ${matchingSlot.screenPosition.y})...`);

        // Execute interpolated multi-step drag
        await client.drag(item.screenPosition, matchingSlot.screenPosition, 20);

        // Dwell settling pause: essential for spring dampers and snapping animation
        await WAIT_MS(350);

        // Delta check
        const inspection = await client.inspect(item.testId);
        const snapped = inspection.data?.components?.Urdt2DDraggableTarget?.IsSnapped;
        console.log(`Item ${item.testId} snap status: ${snapped}`);
    }
}

module.exports = { solveSnapToSlot };
