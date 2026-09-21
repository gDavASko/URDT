/**
 * Good Example 05: Real-time Runner Lane Switching
 * Demonstrates predictive obstacle scanning and lane repositioning.
 */

const { UrdtClient, WAIT_MS } = require('./urdt_client');

const LANES = [
    { id: 0, x: 440, name: 'Left' },
    { id: 1, x: 640, name: 'Center' },
    { id: 2, x: 840, name: 'Right' }
];

async function runObstacleAvoidanceLoop(client, rounds = 8) {
    console.log('Starting runner lane switching loop...');

    let currentLane = 1; // Start in Center

    for (let i = 0; i < rounds; i++) {
        // Perceive oncoming obstacles
        const query = await client.query({ activeOnly: true });
        const matches = query.data?.matches || [];

        const obstacles = matches.filter(m => m.testId && m.testId.startsWith('Obstacle_'));

        // Find threatening obstacle in current lane within Y distance [300, 600]
        const threat = obstacles.find(obs => {
            const laneMatch = Math.abs(obs.screenPosition.x - LANES[currentLane].x) < 50;
            const inRange = obs.screenPosition.y > 300 && obs.screenPosition.y < 650;
            return laneMatch && inRange;
        });

        if (threat) {
            console.log(`[Alert] Threat detected on ${LANES[currentLane].name} lane at Y=${threat.screenPosition.y}!`);
            
            // Pick safe adjacent lane
            const safeLane = currentLane === 1 ? (Math.random() > 0.5 ? 0 : 2) : 1;
            console.log(`Switching from lane ${currentLane} to ${safeLane}...`);

            // Shift via click or touch on lane area
            await client.click({ x: LANES[safeLane].x, y: 300 });
            currentLane = safeLane;

            // Wait for dodge animation
            await WAIT_MS(250);
        } else {
            await WAIT_MS(100);
        }
    }

    console.log('Runner dodging loop completed safely.');
}

module.exports = { runObstacleAvoidanceLoop };
