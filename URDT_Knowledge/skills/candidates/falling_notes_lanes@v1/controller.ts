// Falling-notes lanes: for each lane take the note nearest to the hit line and press that lane's button when the
// note enters the centre of the hit window (compensating for the loop latency); each note is pressed once and a
// lane is never pressed without a note in the window (wrong presses count as errors).
export default async function (api: any) {
  const pressed = new Set<string>();
  let loopMs = 20;
  while (!api.expired()) {
    const t0 = Date.now();
    const s = await api.state();
    if (s.IsCompleted) return 'COMPLETED';
    if (s.IsInTransition) { pressed.clear(); await api.sleep(80); continue; }
    const speed = Number(s.NoteSpeed ?? 300), win = Number(s.HitWindow ?? 40);
    const parts = await api.parts();
    const notes = parts.filter((b: any) => /^Note_\d+$/.test(b.testId) && typeof b.game?.DistanceToHitLine === 'number');
    const buttons = parts.filter((b: any) => /^BtnLane_\d+$/.test(b.testId));
    // Distance the note travels before our press lands: loop latency + ~1 frame of input delivery.
    const lead = speed * (loopMs + 12) / 1000;
    for (let lane = 0; lane < buttons.length; lane++) {
      const cand = notes.filter((n: any) => n.game.Lane === lane && !pressed.has(n.testId))
        .sort((a: any, b: any) => a.game.DistanceToHitLine - b.game.DistanceToHitLine);
      const n = cand.find((x: any) => x.game.DistanceToHitLine > -win);
      if (!n) continue;
      const d = n.game.DistanceToHitLine - lead;
      if (d <= win * 0.35 && d >= -win * 0.6) {
        await api.motor.tap({ testId: `BtnLane_${lane}` }, 8);
        pressed.add(n.testId);
      }
    }
    loopMs = loopMs * 0.8 + (Date.now() - t0) * 0.2;
    await api.sleep(4);
  }
  return 'TIMEOUT';
}
