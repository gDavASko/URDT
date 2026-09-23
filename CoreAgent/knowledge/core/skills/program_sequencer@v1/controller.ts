// Program sequencer: fill the timeline slots in order with the required command types, clearing wrong chips,
// never using junk chips, then press the run button. Repeats for every stage.
export default async function (api: any) {
  const ORDER = ['Forward', 'Turn', 'Forward'];
  while (!api.expired()) {
    const s = await api.state();
    if (s.IsCompleted) return 'COMPLETED';
    if (s.IsInTransition || s.IsRunning) { await api.sleep(150); continue; }
    const parts = await api.parts();
    const slots = parts.filter((b: any) => /^Slot_\d+$/.test(b.testId)).sort((a: any, b: any) => a.game.StepIndex - b.game.StepIndex);
    const chips = parts.filter((b: any) => b.kind === 'draggable' && b.game && b.game.CommandType && !b.game.IsJunk);
    const tray = parts.find((b: any) => b.testId === 'ChipsTray');
    const near = (a: any, b: any) => Math.hypot(a.center.x - b.center.x, a.center.y - b.center.y) < 30;
    let acted = false;
    const used = new Set<string>();
    for (let i = 0; i < slots.length && i < ORDER.length; i++) {
      const slot = slots[i];
      const inSlot = parts.find((c: any) => c.kind === 'draggable' && c.game && c.game.CommandType && near(c, slot));
      if (inSlot && inSlot.game.CommandType === ORDER[i] && !inSlot.game.IsJunk) { used.add(inSlot.testId); continue; }
      if (inSlot && tray) { await api.motor.drag(inSlot.center, { x: tray.center.x, y: tray.center.y - 90 }); acted = true; break; }
      const chip = chips.find((c: any) => c.game.CommandType === ORDER[i] && !used.has(c.testId) && !slots.some((sl: any) => near(c, sl)));
      if (!chip) { api.say(`no free ${ORDER[i]} chip`); break; }
      await api.motor.drag(chip.center, slot.center);
      used.add(chip.testId);
      acted = true;
      break;
    }
    if (acted) { await api.sleep(250); continue; }
    const run = parts.find((b: any) => b.kind === 'button' && /execute|run|start/i.test(b.testId));
    if (run) { await api.motor.tap({ testId: run.testId }); await api.sleep(2500); }
    else await api.sleep(300);
  }
  return 'TIMEOUT';
}
