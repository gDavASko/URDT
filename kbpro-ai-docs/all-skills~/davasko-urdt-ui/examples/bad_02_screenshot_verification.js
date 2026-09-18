/**
 * ❌ BAD EXAMPLE 02: Screenshot & Vision-Based Verification Anti-Pattern
 * 
 * WHY THIS IS WRONG:
 * 1. Screenshots are slow, non-deterministic, resolution-dependent, and wasteful.
 * 2. Visual heuristics frequently hallucinate whether a button was clicked or toggled.
 * 3. URDT protocol provides 100% deterministic, zero-latency state inspection
 *    directly over WebSocket frames.
 * 4. STRICT RULE: Screenshots are strictly forbidden for UI test assertions!
 */

// ❌ ANTI-PATTERN: Taking a screenshot and passing it to vision model
async function badVerifyWithScreenshot(unityCli, client, buttonId) {
    await client.call('click', { testId: buttonId });
    
    // ❌ WRONG: Capturing screen and guessing from pixels
    const screenshot = await unityCli.execute('capture_game_view');
    console.log('Saved screenshot to:', screenshot.path);
    // Passing screenshot to an LLM to guess "is the modal open?" -> Unreliable and slow!
}
