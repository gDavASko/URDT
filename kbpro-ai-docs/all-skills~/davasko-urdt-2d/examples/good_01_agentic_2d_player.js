/**
 * Good Example 01: Agentic Step-by-Step 2D Player Loop
 * Demonstrates live perception, reasoning, honest device action, and delta evaluation.
 */

const { UrdtClient, WAIT_MS } = require('./urdt_client');

async function main() {
    const client = new UrdtClient();
    console.log('Connecting to URDT WebSocket at ws://127.0.0.1:7777/...');
    await client.connect();
    console.log('Handshake successful!\n');

    // 1. Perception: Discover current active module
    const queryRes = await client.query({ activeOnly: true });
    const matches = queryRes.data?.matches || [];
    
    const moduleBeacon = matches.find(m => m.components?.Urdt2DModuleTarget);
    if (!moduleBeacon) {
        console.error('No active Urdt2DModuleTarget found in scene!');
        client.close();
        return;
    }

    const mod = moduleBeacon.components.Urdt2DModuleTarget;
    console.log(`=== Active Mechanic: ${mod.MechanicTitle} (${mod.MechanicId}) ===`);
    console.log(`Instruction: ${mod.Instruction}`);
    console.log(`Initial Progress: ${(mod.ProgressNormalized * 100).toFixed(0)}%\n`);

    // 2. Discover interactive entities
    const draggables = matches.filter(m => m.components?.Urdt2DDraggableTarget && !m.components.Urdt2DDraggableTarget.IsJunk);
    const slots = matches.filter(m => m.components?.Urdt2DSlotTarget && !m.components.Urdt2DSlotTarget.IsOccupied);

    console.log(`Found ${draggables.length} valid draggable items and ${slots.length} open slots.`);

    // 3. Step-by-step solving loop
    for (const item of draggables) {
        const itemComp = item.components.Urdt2DDraggableTarget;
        if (itemComp.IsSnapped) continue;

        // Match item with corresponding slot
        const targetSlot = slots.find(s => s.components.Urdt2DSlotTarget.SlotId === itemComp.ItemId) || slots[0];
        if (!targetSlot) {
            console.warn(`No matching slot found for item ${item.testId}`);
            continue;
        }

        console.log(`\n[Action] Dragging ${item.testId} -> ${targetSlot.testId}...`);
        
        // Execute honest device drag with 20 steps for Unity EventSystem
        await client.drag(item.screenPosition, targetSlot.screenPosition, 20);

        // Allow physics / layout settling
        await WAIT_MS(350);

        // 4. Delta Evaluation
        const updatedMod = await client.inspect(mod.MechanicId);
        const newProgress = updatedMod.data?.components?.Urdt2DModuleTarget?.ProgressNormalized || 0;
        console.log(`[Evaluation] New Progress: ${(newProgress * 100).toFixed(0)}%`);

        if (newProgress >= 1.0) {
            console.log('\n>>> Mechanic Successfully Mastered! Waiting for victory celebration... <<<');
            await WAIT_MS(2800);
            break;
        }
    }

    client.close();
    console.log('Agent session concluded.');
}

main().catch(console.error);
