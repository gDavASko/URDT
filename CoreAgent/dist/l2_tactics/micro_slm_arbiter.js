/**
 * Micro-SLM Cognitive Arbiter
 * Integrates Qwen GGUF via node-llama-cpp with dynamic GBNF grammar constraints
 * and Zod validation for structured JSON output.
 * Resolves anomalies, unexpected modal dialogs, and gameplay stalls.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-7
 */
import path from 'node:path';
import fs from 'node:fs';
import { z } from 'zod';
export const MicroSlmOutputSchema = z.object({
    diagnosis: z.string(),
    action: z.enum(['TAP', 'DRAG', 'WAIT', 'DISMISS_MODAL']),
    targetBeaconId: z.string(),
    confidence: z.number().optional().default(0.9)
});
export class MicroSlmArbiter {
    modelPath;
    llamaInstance = null;
    model = null;
    context = null;
    isInitialized = false;
    constructor(modelPath = 'E:/Projects/URDT/CoreAgent/models/qwen2.5-0.5b-instruct-q4_k_m.gguf') {
        this.modelPath = path.resolve(modelPath);
    }
    async initialize() {
        if (this.isInitialized)
            return true;
        if (!fs.existsSync(this.modelPath)) {
            console.warn(`[URDT MicroSLM] Model not found at ${this.modelPath}. Running in Deterministic Heuristic mode.`);
            return false;
        }
        try {
            const { getLlama, LlamaChatSession } = await import('node-llama-cpp');
            this.llamaInstance = await getLlama();
            this.model = await this.llamaInstance.loadModel({
                modelPath: this.modelPath,
                gpuLayers: 99
            });
            this.context = await this.model.createContext({
                contextSize: 2048,
                threads: 4
            });
            this.isInitialized = true;
            console.log(`[URDT MicroSLM] Model loaded successfully: ${path.basename(this.modelPath)}`);
            return true;
        }
        catch (err) {
            console.warn(`[URDT MicroSLM] Failed to load node-llama-cpp model (${err.message}). Using deterministic fallback.`);
            return false;
        }
    }
    /**
     * Generates dynamic GBNF grammar string restricting targetBeaconId strictly to visible candidates
     */
    generateDynamicGbnf(validBeaconIds) {
        const idsFormatted = validBeaconIds.length > 0
            ? validBeaconIds.map(id => `"${id}"`).join(' | ')
            : `"none"`;
        return `
root ::= "{" ws "\\"diagnosis\\":" ws string "," ws "\\"action\\":" ws action "," ws "\\"targetBeaconId\\":" ws beacon_id "}"
action ::= "\\"TAP\\"" | "\\"DRAG\\"" | "\\"WAIT\\"" | "\\"DISMISS_MODAL\\""
beacon_id ::= ${idsFormatted}
string ::= "\\"" [^"\\\\]* "\\""
ws ::= [ \\t\\n]*
`.trim();
    }
    /**
     * Evaluates state anomaly through Micro-SLM or deterministic fallback
     */
    async decideAction(ctx) {
        const validIds = ctx.beacons.map(b => b.id);
        // If active model is available, perform constrained generation
        if (this.isInitialized && this.context) {
            try {
                const prompt = this.buildPrompt(ctx);
                const grammar = await this.llamaInstance.createGrammar({
                    grammar: this.generateDynamicGbnf(validIds)
                });
                const { LlamaChatSession } = await import('node-llama-cpp');
                const session = new LlamaChatSession({
                    contextSequence: this.context.getSequence()
                });
                const responseText = await session.prompt(prompt, {
                    grammar,
                    maxTokens: 64,
                    temperature: 0.1
                });
                const parsedJson = JSON.parse(responseText);
                const validated = MicroSlmOutputSchema.parse(parsedJson);
                return validated;
            }
            catch (err) {
                console.warn(`[URDT MicroSLM] Inference error (${err.message}). Falling back to heuristic.`);
            }
        }
        // Deterministic Heuristic Arbiter (Rule-based Fallback)
        return this.decideHeuristic(ctx);
    }
    buildPrompt(ctx) {
        const beaconLines = ctx.beacons
            .map(b => `  * id: "${b.id}", label: "${b.text || ''}", type: "${b.controlType || 'BUTTON'}"`)
            .join('\n');
        return `You are an autonomous tactical game reviewer. Your task is to resolve stalls and progress towards the goal.
GOAL: "${ctx.macroGoal}"
ACTIVE MODAL: "${ctx.activeModalId || 'NONE'}"
VISIBLE BUTTONS:
${beaconLines}

Respond in strict JSON:
{"diagnosis": "reason", "action": "TAP", "targetBeaconId": "button_id"}`;
    }
    decideHeuristic(ctx) {
        // 1. If modal is active, seek close/dismiss/ok buttons
        if (ctx.activeModalId) {
            const dismissCandidates = ctx.beacons.filter(b => {
                const text = `${b.id} ${b.text || ''}`.toLowerCase();
                return text.includes('close') || text.includes('cancel') || text.includes('dismiss') || text.includes('x') || text.includes('ok');
            });
            if (dismissCandidates.length > 0) {
                return {
                    diagnosis: `Modal '${ctx.activeModalId}' detected. Clicking dismiss button.`,
                    action: 'DISMISS_MODAL',
                    targetBeaconId: dismissCandidates[0].id,
                    confidence: 0.95
                };
            }
        }
        // 2. Look for progression buttons (play, start, next, continue, ok)
        const progressCandidates = ctx.beacons.filter(b => {
            const text = `${b.id} ${b.text || ''}`.toLowerCase();
            return text.includes('play') || text.includes('start') || text.includes('next') || text.includes('continue') || text.includes('ok');
        });
        if (progressCandidates.length > 0) {
            return {
                diagnosis: `Found progression action '${progressCandidates[0].id}'.`,
                action: 'TAP',
                targetBeaconId: progressCandidates[0].id,
                confidence: 0.9
            };
        }
        // 3. Fallback to first available interactable beacon
        if (ctx.beacons.length > 0) {
            return {
                diagnosis: `Selecting first available beacon '${ctx.beacons[0].id}' to break stall.`,
                action: 'TAP',
                targetBeaconId: ctx.beacons[0].id,
                confidence: 0.7
            };
        }
        // 4. Inaction / wait
        return {
            diagnosis: 'No interactable elements found. Waiting.',
            action: 'WAIT',
            targetBeaconId: 'none',
            confidence: 0.5
        };
    }
    dispose() {
        if (this.context) {
            this.context.dispose?.();
            this.context = null;
        }
        if (this.model) {
            this.model.dispose?.();
            this.model = null;
        }
        this.isInitialized = false;
    }
}
//# sourceMappingURL=micro_slm_arbiter.js.map