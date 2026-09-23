// Alternating rhythm: press left/right alternately with an interval in the middle of the stage's window
// (MinInterval..MaxInterval exposed by the game), timed by the wall clock; never touch the junk lever.
export default async function (api: any) {
  let side = 0;                      // 0 = left, 1 = right
  let lastPress = 0, lastFails = -1, lastStage = -1, startSide = 0;
  while (!api.expired()) {
    const s = await api.state();
    if (s.IsCompleted) return 'COMPLETED';
    if (s.IsInTransition) { await api.sleep(80); lastPress = 0; continue; }
    if (s.FailCount !== lastFails || s.CurrentStage !== lastStage) {
      // New series: after an alternation fail start from the other side next time.
      if (lastFails >= 0 && s.FailCount > lastFails && /чередован|alternat/i.test(String(s.LastFailReason))) startSide = 1 - startSide;
      lastFails = s.FailCount; lastStage = s.CurrentStage; side = startSide; lastPress = 0;
    }
    const lo = Number(s.MinInterval ?? 0.3), hi = Number(s.MaxInterval ?? 1.0);
    const intervalMs = (lo + (hi - lo) * 0.45) * 1000;
    const now = Date.now();
    if (lastPress === 0 || now - lastPress >= intervalMs) {
      const parts = await api.parts();
      const btn = parts.find((b: any) => b.kind === 'button' && (side === 0 ? /left/i : /right/i).test(b.testId) && !/junk/i.test(b.testId));
      if (!btn) { await api.sleep(100); continue; }
      await api.motor.tap({ testId: btn.testId }, 16);
      lastPress = Date.now();
      side = 1 - side;
    }
    await api.sleep(10);
  }
  return 'TIMEOUT';
}
