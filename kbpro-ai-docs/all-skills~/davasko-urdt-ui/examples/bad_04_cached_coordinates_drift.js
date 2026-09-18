/**
 * ❌ BAD EXAMPLE 04: Cached Screen Coordinates Drift Anti-Pattern
 * 
 * WHY THIS IS WRONG:
 * 1. UI layouts in Unity are dynamic: windows animate, lists scroll, resolutions change,
 *    and layout groups reposition elements during layout passes.
 * 2. Caching an (x, y) coordinate from an early query and reusing it later will click
 *    on the wrong element or empty space.
 * 3. ALWAYS query or inspect the target's live `ScreenCenter` immediately before executing an action.
 */

// ❌ ANTI-PATTERN: Caching coordinates at startup and reusing them
async function badCachedCoordinates(client, buttonId) {
    const initial = await client.call('inspect', { testId: buttonId });
    const cachedCenter = initial.data.components.UrdtUiButtonTarget.ScreenCenter;
    
    // ... UI scrolls or modal opens ...
    
    // ❌ WRONG: Clicking old coordinates after UI has moved!
    await client.call('click', { x: cachedCenter.x, y: cachedCenter.y });
}
