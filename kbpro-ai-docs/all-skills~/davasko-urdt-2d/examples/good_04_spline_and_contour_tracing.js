/**
 * Good Example 04: Spline & Contour Tracing via press_move
 * Demonstrates channel navigation and closed-loop perimeter cutting.
 */

const { UrdtClient, WAIT_MS } = require('./urdt_client');

/**
 * Traces a narrow channel without triggering out-of-bounds resets (M08)
 */
async function solveWaypointChannel(client, waypointPrefix = 'Waypoint_') {
    console.log('Discovering sequential waypoints...');

    const query = await client.query({ activeOnly: true });
    const matches = query.data?.matches || [];

    // Filter and sort waypoints by name
    const waypoints = matches
        .filter(m => m.testId && m.testId.startsWith(waypointPrefix))
        .sort((a, b) => a.testId.localeCompare(b.testId));

    if (waypoints.length === 0) {
        console.error('No waypoints found!');
        return;
    }

    console.log(`Found ${waypoints.length} waypoints in channel.`);

    // Build polyline coordinates
    const polyline = waypoints.map(w => ({
        x: w.screenPosition.x,
        y: w.screenPosition.y
    }));

    console.log('Dispatching press_move along polyline...');
    await client.pressMove(polyline, 10);
    await WAIT_MS(300);
    console.log('Channel navigation completed!');
}

/**
 * Traces a closed polygon contour ensuring loop closure (M29)
 */
async function solveClosedContour(client, pointsArray) {
    console.log(`Cutting closed contour with ${pointsArray.length} vertices...`);

    // Ensure loop closure: last point equals first point
    const closedLoop = [...pointsArray];
    if (closedLoop.length > 0) {
        closedLoop.push({ ...closedLoop[0] });
    }

    await client.pressMove(closedLoop, 8);
    await WAIT_MS(300);
    console.log('Perimeter successfully cut and closed.');
}

module.exports = { solveWaypointChannel, solveClosedContour };
