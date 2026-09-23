/**
 * Micro-SLM Cognitive Arbiter
 * Integrates Qwen GGUF via node-llama-cpp with dynamic GBNF grammar constraints
 * and Zod validation for structured JSON output.
 * Resolves anomalies, unexpected modal dialogs, and gameplay stalls.
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md#section-3-7
 */
import { z } from 'zod';
export declare const MicroSlmOutputSchema: z.ZodObject<{
    diagnosis: z.ZodString;
    action: z.ZodEnum<["TAP", "DRAG", "WAIT", "DISMISS_MODAL"]>;
    targetBeaconId: z.ZodString;
    confidence: z.ZodDefault<z.ZodOptional<z.ZodNumber>>;
}, "strip", z.ZodTypeAny, {
    action: "TAP" | "DRAG" | "WAIT" | "DISMISS_MODAL";
    targetBeaconId: string;
    diagnosis: string;
    confidence: number;
}, {
    action: "TAP" | "DRAG" | "WAIT" | "DISMISS_MODAL";
    targetBeaconId: string;
    diagnosis: string;
    confidence?: number | undefined;
}>;
export type MicroSlmOutput = z.infer<typeof MicroSlmOutputSchema>;
export interface ArbiterPromptContext {
    macroGoal: string;
    activeModalId: string | null;
    beacons: Array<{
        id: string;
        text?: string;
        controlType?: string;
    }>;
    stuckCount: number;
}
export declare class MicroSlmArbiter {
    private modelPath;
    private llamaInstance;
    private model;
    private context;
    private isInitialized;
    constructor(modelPath?: string);
    initialize(): Promise<boolean>;
    /**
     * Generates dynamic GBNF grammar string restricting targetBeaconId strictly to visible candidates
     */
    generateDynamicGbnf(validBeaconIds: string[]): string;
    /**
     * Evaluates state anomaly through Micro-SLM or deterministic fallback
     */
    decideAction(ctx: ArbiterPromptContext): Promise<MicroSlmOutput>;
    private buildPrompt;
    decideHeuristic(ctx: ArbiterPromptContext): MicroSlmOutput;
    dispose(): void;
}
