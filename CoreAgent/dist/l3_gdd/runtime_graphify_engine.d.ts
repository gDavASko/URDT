/**
 * Runtime Graphify Engine
 * Synthesizes a Multi-Layer Directed Multigraph G = (V, E) of reachable application states.
 * Enforces State Explosion Protection:
 * 1. Digit masking: /\b\d+([.,]\d+)?\b/g -> <NUM>
 * 2. Alphabetical beacon sorting
 * 3. MurmurHash3 64-bit vertex hashing
 * 4. Coordinate decoupling
 *
 * Normative Spec: Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md#section-5
 */
export interface UrdtGraphNode {
    nodeId: string;
    sceneName: string;
    activeModalId: string | null;
    interactiveBeaconIds: string[];
    semanticTokens: string[];
    discoveryTimestamp: number;
    visitCount: number;
    affordancesRemaining: number;
    testedAffordances: Record<string, number>;
}
export interface UrdtGraphEdge {
    edgeId: string;
    fromNodeId: string;
    toNodeId: string;
    actionPrimitive: 'TAP' | 'DRAG' | 'SWIPE' | 'CONTINUOUS_STEER' | 'TIMEOUT_WAIT';
    targetBeaconId: string;
    kinematicParams: {
        screenNormalizedStart: [number, number];
        screenNormalizedEnd?: [number, number];
        durationMs: number;
    };
    transitionLatencyMs: number;
    traversalCount: number;
    successRate: number;
}
export interface ApplicationMap {
    $schema: string;
    version: string;
    generatedAt: string;
    entryNodeId: string;
    nodes: Record<string, UrdtGraphNode>;
    edges: UrdtGraphEdge[];
    controlBindings: Record<string, any>;
    coverageGaps: string[];
    deadlockNodes: string[];
}
export declare class RuntimeGraphifyEngine {
    private readonly nodes;
    private readonly edges;
    private entryNodeId;
    private readonly deadlockNodes;
    private readonly K_PROBES_PER_AFFORDANCE;
    /**
     * MurmurHash3 64-bit implementation in pure TypeScript
     */
    static murmurHash3_64(str: string, seed?: number): string;
    /**
     * State normalization and hashing
     */
    computeNodeHash(sceneName: string, activeModalId: string | null, beacons: Array<{
        id: string;
        semanticLabel?: string;
    }>): {
        hash: string;
        sortedIds: string[];
        maskedTokens: string[];
    };
    /**
     * Discovers or updates a node in the graph
     */
    registerState(sceneName: string, activeModalId: string | null, beacons: Array<{
        id: string;
        semanticLabel?: string;
    }>): UrdtGraphNode;
    /**
     * Records a transition between two nodes
     */
    recordTransition(fromNodeId: string, toNodeId: string, action: 'TAP' | 'DRAG' | 'SWIPE' | 'CONTINUOUS_STEER' | 'TIMEOUT_WAIT', targetBeaconId: string, latencyMs: number, startPos: [number, number], endPos?: [number, number], isSuccess?: boolean): UrdtGraphEdge;
    /**
     * Retrieves the next untested affordance for Frontier Queue exploration
     */
    getNextUntestedAffordance(nodeId: string): {
        beaconId: string;
        probeIndex: number;
    } | null;
    /**
     * Exports application_map.json
     */
    exportApplicationMap(outputPath: string, controlBindings?: Record<string, any>, coverageGaps?: string[]): ApplicationMap;
    getNodesCount(): number;
    getEdgesCount(): number;
}
