/**
 * ✅ GOOD EXAMPLE 03: Viewport-Aware Scrolling & Dragging
 * 
 * WHY THIS IS CORRECT:
 * 1. Inspects the ScrollRect viewport bounding rect.
 * 2. Compares target element's `ScreenCenter` against viewport bounds.
 * 3. Incrementally scrolls until the element is fully inside the visible viewport.
 */

const rectPattern = /\(x:([-\d.]+),\s*y:([-\d.]+),\s*width:([-\d.]+),\s*height:([-\d.]+)\)/;

function parseRect(screenRectStr) {
    const match = screenRectStr.match(rectPattern);
    if (!match) throw new Error('Invalid ScreenRect: ' + screenRectStr);
    return { x: Number(match[1]), y: Number(match[2]), width: Number(match[3]), height: Number(match[4]) };
}

async function bringIntoViewport(client, scrollId, targetId) {
    const maxAttempts = 30;
    
    for (let i = 0; i < maxAttempts; i++) {
        const scrollSnapshot = await client.call('inspect', { testId: scrollId });
        const scrollComp = scrollSnapshot.data.components.UrdtUiScrollTarget;
        const viewportRect = parseRect(scrollComp.ScreenRect);

        const targetSnapshot = await client.call('inspect', { testId: targetId });
        const targetSlice = Object.values(targetSnapshot.data.components).find(c => c.ScreenCenter != null);
        const center = targetSlice.ScreenCenter;

        const isInside = center.x >= viewportRect.x && center.x <= viewportRect.x + viewportRect.width &&
                         center.y >= viewportRect.y && center.y <= viewportRect.y + viewportRect.height;

        if (isInside) {
            console.log(`✅ [${targetId}] is safely inside viewport!`);
            return targetSlice;
        }

        const deltaY = center.y < viewportRect.y ? -120 : 120;
        await client.call('scroll', { testId: scrollId, deltaX: 0, deltaY });
        await new Promise(r => setTimeout(r, 100));
    }

    throw new Error(`Failed to bring ${targetId} into viewport after ${maxAttempts} attempts`);
}
