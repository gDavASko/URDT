/**
 * ❌ BAD EXAMPLE 06: Unbounded Blind Clicks Without Viewport Check Anti-Pattern
 * 
 * WHY THIS IS WRONG:
 * 1. An element may exist in the hierarchy but be scrolled outside the current ScrollRect viewport.
 * 2. Clicking blindly on an off-screen coordinate or clipped RectTransform fails the raycast
 *    or clicks the background window behind the viewport.
 * 3. ALWAYS check whether the element's `ScreenCenter` is within the viewport, and scroll it into view first!
 */

// ❌ ANTI-PATTERN: Clicking an off-screen element without scrolling
async function badBlindClick(client, targetId) {
    // ❌ WRONG: Element is scrolled out of view, but clicked anyway without checking!
    const res = await client.call('click', { testId: targetId });
    if (!res.data.clicked) {
        console.error('Click failed because target is not visible in viewport!');
    }
}
