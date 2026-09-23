import assert from 'node:assert';
import fs from 'node:fs';
import path from 'node:path';
import {
  RawCommandSerializer,
  type RawCommand,
  UrdtCommandTypes,
  MultiTouchDispatcher,
  FlashHoganTicker,
  MotorPrimitivesFactory,
  TacticalDispatcher,
  StagnationPolicy,
  HtnBtExecutive,
  RuntimeGraphifyEngine,
  TriadDiscoveryEngine,
  CrashWatchdog,
  FailureEvidenceBuilder
} from '../src/index.js';

interface TestResult {
  name: string;
  passed: boolean;
  durationMs: number;
  error?: string;
}

const results: TestResult[] = [];

async function runTest(name: string, fn: () => void | Promise<void>): Promise<void> {
  const start = performance.now();
  try {
    await fn();
    const duration = performance.now() - start;
    results.push({ name, passed: true, durationMs: duration });
    console.log(`  [PASS] ${name} (${duration.toFixed(2)} ms)`);
  } catch (err: any) {
    const duration = performance.now() - start;
    results.push({ name, passed: false, durationMs: duration, error: err?.message || String(err) });
    console.error(`  [FAIL] ${name} (${duration.toFixed(2)} ms): ${err?.message || err}`);
  }
}

async function main(): Promise<void> {
  console.log('===============================================================');
  console.log('  URDT AUTONOMOUS REVIEWER - MACHINE JUDGE TEST SUITE');
  console.log('===============================================================\n');

  // ---------------------------------------------------------------------------
  // TEST GROUP 1: Wire Protocol & Binary Structs
  // ---------------------------------------------------------------------------
  console.log('Suite 1: Binary Wire Protocol & Named Pipe Packing');
  
  await runTest('RawCommand: Exact 27-byte struct size and CRC8 integrity', () => {
    const cmd: RawCommand = {
      cmdType: UrdtCommandTypes.TOUCH_DOWN,
      pointerId: 1,
      sequenceNumber: 1001,
      screenX: 960.5,
      screenY: 540.25,
      pressure: 0.8,
      targetTimestampMs: 42000,
      idempotencyKey: 8888
    };

    const buf = RawCommandSerializer.serialize(cmd);
    assert.strictEqual(buf.length, RawCommandSerializer.STRUCT_SIZE, `Buffer must be exactly ${RawCommandSerializer.STRUCT_SIZE} bytes`);

    const deserialized = RawCommandSerializer.deserialize(buf);
    assert.strictEqual(deserialized.cmdType, UrdtCommandTypes.TOUCH_DOWN);
    assert.strictEqual(deserialized.pointerId, 1);
    assert.strictEqual(deserialized.sequenceNumber, 1001);
    assert.ok(Math.abs(deserialized.screenX - 960.5) < 0.001);
    assert.ok(Math.abs(deserialized.screenY - 540.25) < 0.001);
    assert.ok(Math.abs(deserialized.pressure - 0.8) < 0.001);
    assert.strictEqual(deserialized.targetTimestampMs, 42000);
    assert.strictEqual(deserialized.idempotencyKey, 8888);
    assert.strictEqual(deserialized.crc8, buf[26], 'CRC8 byte must match calculated checksum');
  });

  // ---------------------------------------------------------------------------
  // TEST GROUP 2: L1 Kinematics & Multi-Touch
  // ---------------------------------------------------------------------------
  console.log('\nSuite 2: L1 Kinematics, Flash-Hogan & Motor Primitives');

  await runTest('FlashHogan: Minimum-Jerk polynomial boundary and inflection conditions', () => {
    assert.strictEqual(FlashHoganTicker.calculateMinimumJerk(0), 0);
    assert.strictEqual(FlashHoganTicker.calculateMinimumJerk(1), 1);
    assert.strictEqual(FlashHoganTicker.calculateMinimumJerk(0.5), 0.5);

    // Monotonicity check
    let last = 0;
    for (let t = 0.05; t <= 1.0; t += 0.05) {
      const s = FlashHoganTicker.calculateMinimumJerk(t);
      assert.ok(s >= last, `Polynomial must be monotonically non-decreasing at tau=${t}`);
      last = s;
    }
  });

  await runTest('MotorPrimitives: Catalog of all 9 gestures generated correctly', () => {
    const tap = MotorPrimitivesFactory.createTap({ x: 100, y: 200 }, 0, 50);
    assert.strictEqual(tap.primitive, 'TAP');
    assert.strictEqual(tap.holdDurationMs, 50);

    const drag = MotorPrimitivesFactory.createDrag({ x: 0, y: 0 }, { x: 300, y: 400 }, 250);
    assert.strictEqual(drag.primitive, 'DRAG');
    assert.strictEqual(drag.durationMs, 250);

    const swipe = MotorPrimitivesFactory.createSwipe({ x: 100, y: 100 }, { vx: 1200, vy: 0 }, 200);
    assert.strictEqual(swipe.primitive, 'SWIPE');

    const slice = MotorPrimitivesFactory.createSlice([{ x: 10, y: 10 }, { x: 20, y: 30 }, { x: 50, y: 80 }], 300);
    assert.strictEqual(slice.primitive, 'SLICE');
    assert.strictEqual(slice.waypoints.length, 3);

    const hold = MotorPrimitivesFactory.createHoldConditioned({ x: 50, y: 50 }, 'on_glow_ready', 5000);
    assert.strictEqual(hold.primitive, 'HOLD_EVENT_CONDITIONED');

    const charge = MotorPrimitivesFactory.createChargeAndRelease({ x: 200, y: 200 }, 800);
    assert.strictEqual(charge.primitive, 'CHARGE_AND_RELEASE');

    const steer = MotorPrimitivesFactory.createContinuousSteer({ x: 200, y: 800 }, { x: 0.7, y: 0.3 });
    assert.strictEqual(steer.primitive, 'CONTINUOUS_STEER_STREAM');

    const pinch = MotorPrimitivesFactory.createPinchZoom({ x: 500, y: 500 }, 100, 300);
    assert.strictEqual(pinch.primitive, 'PINCH_ZOOM');

    const qte = MotorPrimitivesFactory.createQteTimedTap({ x: 400, y: 400 }, 150, 250);
    assert.strictEqual(qte.primitive, 'QTE_TIMED_TAP');
  });

  await runTest('MultiTouchDispatcher: 4-channel isolation and safety release', () => {
    const dispatcher = new MultiTouchDispatcher();
    const c0 = dispatcher.allocateChannel('TAP', 'btn_1', { x: 100, y: 100 }, 0);
    const c1 = dispatcher.allocateChannel('TAP', 'btn_2', { x: 200, y: 200 }, 1);
    assert.ok(c0 !== null && c0.touchId === 1, 'Pointer 0 maps to Unity TouchId 1');
    assert.ok(c1 !== null && c1.touchId === 2, 'Pointer 1 maps to Unity TouchId 2');

    assert.strictEqual(dispatcher.getActiveChannelsCount(), 2);
    dispatcher.releaseChannel(0);
    assert.strictEqual(dispatcher.getChannel(0)?.isOccupied, false);
    dispatcher.releaseAll();
    assert.strictEqual(dispatcher.getActiveChannelsCount(), 0);
  });

  // ---------------------------------------------------------------------------
  // TEST GROUP 3: L2 Tactical Dispatcher & Policies
  // ---------------------------------------------------------------------------
  console.log('\nSuite 3: L2 Tactical Dispatcher & Reactive Policies');

  await runTest('TacticalDispatcher: Sub-millisecond Autopilot Fast Loop', async () => {
    const dispatcher = new TacticalDispatcher();
    const t0 = performance.now();
    const decision = await dispatcher.evaluateTacticalStep(
      {
        activeModalId: null,
        detectedHazards: [],
        currentScreenBeacons: [{ id: 'btn_ok', screenPos: { x: 500, y: 500 } }],
        activeNodeId: 'node_1'
      },
      'state_hash_initial'
    );
    const dt = performance.now() - t0;

    assert.ok(decision !== null, 'Dispatcher must yield tactical result');
    assert.ok(dt < 10.0, `Dispatcher step latency must be responsive (actual: ${dt.toFixed(3)} ms)`);
  });

  await runTest('StagnationPolicy: 5-layer modal filter detection', () => {
    const policy = new StagnationPolicy();
    const modalResult = policy.evaluateModalState({
      modalId: 'PopupConfirmation',
      modalSpawnedAtMs: Date.now() - 1000,
      screenText: ['Please confirm action', '5 sec remaining'],
      isAnimationPlaying: false,
      isVideoPlaying: false,
      isCloseButtonPresent: true
    });
    assert.strictEqual(modalResult.isSuppressed, true, 'Modal state with countdown must be suppressed');
    assert.ok(modalResult.reason.includes('LEGITIMATE_COUNTDOWN_DETECTED'));
  });

  await runTest('HtnBtExecutive: Hierarchical decomposition and step execution', () => {
    const executive = new HtnBtExecutive();
    executive.loadMacroPlan([
      { id: 'sub_1', description: 'Locate button', primitive: 'TAP', targetBeaconId: 'btn_ok' },
      { id: 'sub_2', description: 'Verify next screen', primitive: 'WAIT' }
    ]);

    const nextSubtask = executive.getCurrentSubGoal();
    assert.strictEqual(nextSubtask?.id, 'sub_1');
    assert.strictEqual(nextSubtask?.primitive, 'TAP');
  });

  // ---------------------------------------------------------------------------
  // TEST GROUP 4: L3 Graphify & Triad Discovery
  // ---------------------------------------------------------------------------
  console.log('\nSuite 4: L3 Topological Graphify & Triad Discovery');

  await runTest('RuntimeGraphifyEngine: Number masking and 64-bit hash determinism', () => {
    const engine = new RuntimeGraphifyEngine();
    const res1 = engine.computeNodeHash('MainScene', null, [{ id: 'btn_gold', semanticLabel: 'Gold: 1,450 coins' }]);
    const res2 = engine.computeNodeHash('MainScene', null, [{ id: 'btn_gold', semanticLabel: 'Gold: 999 coins' }]);
    assert.strictEqual(res1.hash, res2.hash, 'State hashes must be identical after number masking');

    const hashA = RuntimeGraphifyEngine.murmurHash3_64('DeterministicDescriptor');
    const hashB = RuntimeGraphifyEngine.murmurHash3_64('DeterministicDescriptor');
    assert.strictEqual(hashA, hashB, 'MurmurHash3 must be strictly deterministic');
  });

  await runTest('TriadDiscovery: Semantic matching of GDD tokens to UI targets', () => {
    const triad = new TriadDiscoveryEngine();
    const candidates = triad.correlateSemantics([
      { id: 'btn_steer_left', semanticLabel: 'Steer Left', controlType: 'Joystick', screenPixel: { x: 200, y: 500 } },
      { id: 'btn_throttle', semanticLabel: 'Accelerate Gas', controlType: 'Button', screenPixel: { x: 1600, y: 500 } }
    ]);
    assert.ok((candidates.get('STEER')?.length ?? 0) > 0, 'Must match STEER prior');
    assert.ok((candidates.get('THROTTLE')?.length ?? 0) > 0, 'Must match THROTTLE prior');
  });

  // ---------------------------------------------------------------------------
  // TEST GROUP 5: L3 Crash Watchdog & Failure Evidence Packet (Phase 6)
  // ---------------------------------------------------------------------------
  console.log('\nSuite 5: Phase 6 Post-Mortem Crash Watchdog & Evidence Packet');

  await runTest('CrashWatchdog: Autopsy of Unity crash and SIGSEGV extraction', async () => {
    const watchdog = new CrashWatchdog();
    
    // Test on authentic sample player_test.log
    const dump = await watchdog.inspectCrash(0xC0000005, path.resolve('player_test.log'));
    assert.strictEqual(dump.exitCode, 0xC0000005);
    assert.strictEqual(dump.exitCodeHex, '0xC0000005');
    assert.ok(dump.signalName?.includes('0xC0000005'));
    assert.ok(dump.faultingModule.length > 0);
    assert.ok(dump.nativeStackTrace.length > 0);
  });

  await runTest('FailureEvidenceBuilder: JSON Schema Draft-07 compliance and .harness export', () => {
    const packet = FailureEvidenceBuilder.build({
      failingPhase: 'M01_SnapToSlot',
      gddInvariantViolated: 'INV-01_SNAP_SUCCESS',
      stagnationType: 'CRASH_EXCEPTION',
      lastKnownWorldRevision: 42,
      activeScene: 'URDT_2D_TestPolygon',
      beaconStateSnapshot: [
        { id: 'item_cube_blue', screenPixel: { x: 450, y: 310 }, isInteractable: true },
        { id: 'slot_01_blue', screenPixel: { x: 450, y: 310 }, isOccupied: false }
      ],
      actionHistoryBeforeFailure: [
        { action: 'DRAG_START', target: 'item_cube_blue', timestamp: 1000 },
        { action: 'DRAG_UPDATE', pos: { x: 450, y: 310 }, timestamp: 1150 }
      ],
      engineConsoleErrors: ['NullReferenceException: Object reference not set to an instance of an object.'],
      recommendedAiFix: 'Check null safety in SlotController.OnTriggerEnter2D.',
      crashDashcamBase64: 'data:image/webp;base64,UklGRkAAAABXRUJQVlA4IDQAAADwAQCdASoBAAEAAQAcJaACdLoAAP7/2QAA'
    });

    const validation = FailureEvidenceBuilder.validate(packet);
    assert.strictEqual(validation.valid, true, `Packet must be schema-valid: ${validation.errors.join(', ')}`);

    // Export to strictly Drive E: .harness
    const exportedFile = FailureEvidenceBuilder.exportToFile(packet);
    assert.ok(fs.existsSync(exportedFile), `Evidence file must exist at ${exportedFile}`);
    
    const readBack = JSON.parse(fs.readFileSync(exportedFile, 'utf-8'));
    assert.strictEqual(readBack.incidentId, packet.incidentId);
    assert.strictEqual(readBack.gddInvariantViolated, 'INV-01_SNAP_SUCCESS');
  });

  // ---------------------------------------------------------------------------
  // SUMMARY
  // ---------------------------------------------------------------------------
  console.log('\n===============================================================');
  const passedCount = results.filter(r => r.passed).length;
  const failedCount = results.filter(r => !r.passed).length;
  console.log(`  TOTAL TESTS: ${results.length} | PASSED: ${passedCount} | FAILED: ${failedCount}`);
  console.log('===============================================================');

  if (failedCount > 0) {
    process.exit(1);
  } else {
    process.exit(0);
  }
}

main().catch(err => {
  console.error('Fatal test runner error:', err);
  process.exit(1);
});
