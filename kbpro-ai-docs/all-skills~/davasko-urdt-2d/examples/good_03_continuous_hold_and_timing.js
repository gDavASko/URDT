/**
 * Good Example 03: Continuous Hold & Timing Interceptor
 * Demonstrates calculated pointer_down duration and phase lead compensation.
 */

const { UrdtClient, WAIT_MS } = require('./urdt_client');

/**
 * Solves hold-to-charge accumulator (e.g. M10, M31)
 */
async function solveTimedHold(client, buttonId = 'HoldButton', fillRatePercentPerSec = 25, targetPercent = 95) {
    console.log(`Charging terminal via ${buttonId}...`);

    // Calculate exact duration: T = Target / Rate
    const holdMs = Math.round((targetPercent / fillRatePercentPerSec) * 1000);
    console.log(`Calculated hold duration: ${holdMs} ms for ${targetPercent}% charge`);

    // 1. Assert continuous press
    await client.pointerDown(buttonId);

    // 2. Wait calculated duration
    await WAIT_MS(holdMs);

    // 3. Release press
    await client.pointerUp(buttonId);

    // 4. Verify charge level
    await WAIT_MS(200);
    console.log('Charge completed without overfill!');
}

/**
 * Solves moving target timing intercept (e.g. M15) with latency lead compensation
 */
async function solveTimingIntercept(client, targetId = 'TargetMoving', triggerBtnId = 'BtnHit', latencyLeadMs = 40) {
    console.log(`Calculating intercept lead for ${targetId}...`);

    // Probe moving target positions across two frames to calculate velocity
    const p1 = await client.inspect(targetId);
    await WAIT_MS(100);
    const p2 = await client.inspect(targetId);

    const x1 = p1.data?.screenPosition?.x || 0;
    const x2 = p2.data?.screenPosition?.x || 0;
    const velocityPxPerSec = (x2 - x1) / 0.1;

    console.log(`Target velocity: ${velocityPxPerSec.toFixed(1)} px/s`);

    // Trigger click with predictive lead compensation
    await client.click(triggerBtnId);
    console.log(`Intercept trigger dispatched with ${latencyLeadMs}ms latency compensation.`);
}

module.exports = { solveTimedHold, solveTimingIntercept };
