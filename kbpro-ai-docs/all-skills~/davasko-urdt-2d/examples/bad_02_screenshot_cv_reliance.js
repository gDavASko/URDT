/**
 * ANTI-PATTERN: Screenshot / Computer Vision Reliance
 * 
 * WHY THIS IS FORBIDDEN:
 * Screenshots, OCR, and OpenCV are strictly prohibited in the URDT protocol.
 * They introduce high latency, non-deterministic color thresholding failures,
 * and fail to capture hidden component metadata (like IsJunk, IsSnapped, AcceptedType).
 * 
 * Always use URDT structured beacons via query() and inspect().
 */

// ❌ FORBIDDEN: Relying on visual screenshots
async function visualPerceptionLoop(client) {
    /*
    // ILLEGAL: Grabbing frame buffers and parsing pixels
    const frame = await client.call('capture_screenshot');
    const detectedBoxes = myOpenCvDetector.detect(frame);
    */
    throw new Error('Screenshot perception is strictly prohibited. Perceive state via URDT beacons.');
}
