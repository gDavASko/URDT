/**
 * Triad Control Scheme Discovery Module
 * Autonomous discovery of control schemes via Triad Source Model:
 * 1. GDD Prior (Spec-driven hypotheses)
 * 2. Semantic Correlation (On-screen text, labels, beacon topology)
 * 3. Empirical Micro-Probing (100 ms physical pulse -> physics delta assertion)
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-4
 */
export class TriadDiscoveryEngine {
    defaultPriors = [
        {
            intent: 'STEER',
            keywords: ['stick', 'steer', 'joystick', 'wheel', 'left', 'right', 'dpad'],
            preferredRegion: 'LEFT',
            probeType: 'STICK_DEFLECT'
        },
        {
            intent: 'THROTTLE',
            keywords: ['gas', 'throttle', 'accelerate', 'pedal', 'drive', 'forward'],
            preferredRegion: 'RIGHT',
            probeType: 'HOLD_100MS'
        },
        {
            intent: 'BRAKE',
            keywords: ['brake', 'stop', 'reverse', 'handbrake'],
            preferredRegion: 'RIGHT',
            probeType: 'HOLD_100MS'
        },
        {
            intent: 'JUMP',
            keywords: ['jump', 'leap', 'hop', 'up'],
            preferredRegion: 'RIGHT',
            probeType: 'MICRO_TAP'
        },
        {
            intent: 'FIRE',
            keywords: ['fire', 'shoot', 'attack', 'hit', 'action'],
            preferredRegion: 'RIGHT',
            probeType: 'MICRO_TAP'
        },
        {
            intent: 'INTERACT',
            keywords: ['use', 'open', 'take', 'interact', 'e'],
            preferredRegion: 'ANY',
            probeType: 'MICRO_TAP'
        },
        {
            intent: 'PAUSE',
            keywords: ['pause', 'menu', 'settings', 'esc'],
            preferredRegion: 'ANY',
            probeType: 'MICRO_TAP'
        }
    ];
    confirmedBindings = new Map();
    unverifiedDefects = [];
    /**
     * Step 2: Correlate active on-screen beacons with GDD priors by keyword matching and screen positioning.
     */
    correlateSemantics(beacons, screenWidth = 1920, screenHeight = 1080) {
        const candidatesMap = new Map();
        for (const prior of this.defaultPriors) {
            const matched = [];
            for (const beacon of beacons) {
                const textToMatch = `${beacon.id} ${beacon.semanticLabel} ${beacon.controlType}`.toLowerCase();
                const matchesKeyword = prior.keywords.some(kw => textToMatch.includes(kw));
                if (matchesKeyword) {
                    if (this.isRegionValid(beacon.screenPixel, prior.preferredRegion, screenWidth, screenHeight)) {
                        matched.push(beacon);
                    }
                }
            }
            candidatesMap.set(prior.intent, matched);
        }
        return candidatesMap;
    }
    isRegionValid(pos, region, width, height) {
        if (region === 'ANY')
            return true;
        if (region === 'LEFT')
            return pos.x <= width * 0.5;
        if (region === 'RIGHT')
            return pos.x >= width * 0.5;
        if (region === 'BOTTOM')
            return pos.y <= height * 0.5;
        return true;
    }
    /**
     * Step 3: Evaluate physical delta under micro-probing pulse.
     * If velocity delta > 0.05 or player state changed, mapping is confirmed.
     */
    evaluateMicroProbeDelta(intent, candidate, pre, post, pointerId) {
        const deltaVelocity = Math.abs(post.linearVelocityMagnitude - pre.linearVelocityMagnitude);
        const deltaAngular = Math.abs(post.angularVelocity - pre.angularVelocity);
        const stateChanged = post.playerState !== pre.playerState;
        const isConfirmed = deltaVelocity > 0.05 || deltaAngular > 0.05 || stateChanged;
        if (isConfirmed) {
            const binding = {
                intent,
                pointerId,
                targetBeaconId: candidate.id,
                screenPos: [candidate.screenPixel.x, candidate.screenPixel.y],
                confidenceScore: 0.95,
                verifiedTimestampMs: Date.now()
            };
            this.confirmedBindings.set(intent, binding);
            return true;
        }
        else {
            this.unverifiedDefects.push({
                intent,
                candidateId: candidate.id,
                reason: `Control candidate '${candidate.id}' unresponsive: physics delta (v=${deltaVelocity.toFixed(3)}, w=${deltaAngular.toFixed(3)}) under 100ms micro-probe.`
            });
            return false;
        }
    }
    getBindings() {
        const result = {};
        for (const [intent, binding] of this.confirmedBindings.entries()) {
            result[intent] = binding;
        }
        return result;
    }
    getDefects() {
        return [...this.unverifiedDefects];
    }
}
//# sourceMappingURL=triad_discovery.js.map