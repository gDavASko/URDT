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

import fs from 'node:fs';
import path from 'node:path';

export interface UrdtGraphNode {
  nodeId: string;
  sceneName: string;
  activeModalId: string | null;
  interactiveBeaconIds: string[];
  semanticTokens: string[];
  discoveryTimestamp: number;
  visitCount: number;
  affordancesRemaining: number;
  testedAffordances: Record<string, number>; // beaconId -> probe count (K=3)
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

export class RuntimeGraphifyEngine {
  private readonly nodes: Map<string, UrdtGraphNode> = new Map();
  private readonly edges: UrdtGraphEdge[] = [];
  private entryNodeId: string | null = null;
  private readonly deadlockNodes: Set<string> = new Set();
  private readonly K_PROBES_PER_AFFORDANCE = 3;

  /**
   * MurmurHash3 64-bit implementation in pure TypeScript
   */
  public static murmurHash3_64(str: string, seed = 0): string {
    let h1 = seed ^ 0xdeadbeef;
    let h2 = seed ^ 0x41c64e6d;

    for (let i = 0; i < str.length; i++) {
      const ch = str.charCodeAt(i);
      h1 = Math.imul(h1 ^ ch, 2654435761);
      h2 = Math.imul(h2 ^ ch, 1597334677);
    }

    h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507) ^ Math.imul(h2 ^ (h2 >>> 13), 3266489909);
    h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507) ^ Math.imul(h1 ^ (h1 >>> 13), 3266489909);

    const high = (h1 >>> 0).toString(16).padStart(8, '0');
    const low = (h2 >>> 0).toString(16).padStart(8, '0');
    return `${high}${low}`;
  }

  /**
   * State normalization and hashing
   */
  public computeNodeHash(
    sceneName: string,
    activeModalId: string | null,
    beacons: Array<{ id: string; semanticLabel?: string }>
  ): { hash: string; sortedIds: string[]; maskedTokens: string[] } {
    // 1. Sort beacon IDs alphabetically
    const sortedIds = beacons.map(b => b.id).sort();

    // 2. Normalize and mask numerical tokens in labels
    const numberRegex = /\b\d+([.,]\d+)?\b/g;
    const maskedTokens = beacons
      .map(b => (b.semanticLabel || b.id).replace(numberRegex, '<NUM>'))
      .sort();

    // 3. Assemble invariant state descriptor
    const descriptor = [
      sceneName,
      activeModalId || 'NO_MODAL',
      sortedIds.join(';'),
      maskedTokens.join(';')
    ].join('|');

    const hash = RuntimeGraphifyEngine.murmurHash3_64(descriptor);
    return { hash, sortedIds, maskedTokens };
  }

  /**
   * Discovers or updates a node in the graph
   */
  public registerState(
    sceneName: string,
    activeModalId: string | null,
    beacons: Array<{ id: string; semanticLabel?: string }>
  ): UrdtGraphNode {
    const { hash, sortedIds, maskedTokens } = this.computeNodeHash(sceneName, activeModalId, beacons);

    let node = this.nodes.get(hash);
    if (!node) {
      const tested: Record<string, number> = {};
      for (const id of sortedIds) {
        tested[id] = 0;
      }

      node = {
        nodeId: hash,
        sceneName,
        activeModalId,
        interactiveBeaconIds: sortedIds,
        semanticTokens: maskedTokens,
        discoveryTimestamp: Date.now(),
        visitCount: 1,
        affordancesRemaining: sortedIds.length * this.K_PROBES_PER_AFFORDANCE,
        testedAffordances: tested
      };

      this.nodes.set(hash, node);

      if (!this.entryNodeId) {
        this.entryNodeId = hash;
      }
    } else {
      node.visitCount++;
    }

    return node;
  }

  /**
   * Records a transition between two nodes
   */
  public recordTransition(
    fromNodeId: string,
    toNodeId: string,
    action: 'TAP' | 'DRAG' | 'SWIPE' | 'CONTINUOUS_STEER' | 'TIMEOUT_WAIT',
    targetBeaconId: string,
    latencyMs: number,
    startPos: [number, number],
    endPos?: [number, number],
    isSuccess = true
  ): UrdtGraphEdge {
    const edgeId = `edge_${fromNodeId.substring(0, 6)}_${toNodeId.substring(0, 6)}_${targetBeaconId}_${action}`;
    
    let edge = this.edges.find(e => e.edgeId === edgeId);
    if (edge) {
      edge.traversalCount++;
      edge.transitionLatencyMs = (edge.transitionLatencyMs * (edge.traversalCount - 1) + latencyMs) / edge.traversalCount;
      const successCount = (edge.successRate * (edge.traversalCount - 1)) + (isSuccess ? 1 : 0);
      edge.successRate = successCount / edge.traversalCount;
    } else {
      edge = {
        edgeId,
        fromNodeId,
        toNodeId,
        actionPrimitive: action,
        targetBeaconId,
        kinematicParams: {
          screenNormalizedStart: startPos,
          screenNormalizedEnd: endPos,
          durationMs: latencyMs
        },
        transitionLatencyMs: latencyMs,
        traversalCount: 1,
        successRate: isSuccess ? 1.0 : 0.0
      };
      this.edges.push(edge);
    }

    // Update tested affordance count for the fromNode
    const fromNode = this.nodes.get(fromNodeId);
    if (fromNode) {
      const current = fromNode.testedAffordances[targetBeaconId] || 0;
      fromNode.testedAffordances[targetBeaconId] = current + 1;
      
      let totalTested = 0;
      for (const id of fromNode.interactiveBeaconIds) {
        totalTested += Math.min(this.K_PROBES_PER_AFFORDANCE, fromNode.testedAffordances[id] || 0);
      }
      fromNode.affordancesRemaining = Math.max(0, (fromNode.interactiveBeaconIds.length * this.K_PROBES_PER_AFFORDANCE) - totalTested);

      // Check deadlock condition: all affordances probed K times, but no outgoing edge to a different node
      if (fromNode.affordancesRemaining === 0) {
        const hasExternalExit = this.edges.some(e => e.fromNodeId === fromNodeId && e.toNodeId !== fromNodeId && e.successRate > 0.5);
        if (!hasExternalExit) {
          this.deadlockNodes.add(fromNodeId);
        }
      }
    }

    return edge;
  }

  /**
   * Retrieves the next untested affordance for Frontier Queue exploration
   */
  public getNextUntestedAffordance(nodeId: string): { beaconId: string; probeIndex: number } | null {
    const node = this.nodes.get(nodeId);
    if (!node) return null;

    for (const beaconId of node.interactiveBeaconIds) {
      const count = node.testedAffordances[beaconId] || 0;
      if (count < this.K_PROBES_PER_AFFORDANCE) {
        return { beaconId, probeIndex: count };
      }
    }

    return null;
  }

  /**
   * Exports application_map.json
   */
  public exportApplicationMap(
    outputPath: string,
    controlBindings: Record<string, any> = {},
    coverageGaps: string[] = []
  ): ApplicationMap {
    const nodeObj: Record<string, UrdtGraphNode> = {};
    for (const [id, node] of this.nodes.entries()) {
      nodeObj[id] = node;
    }

    const map: ApplicationMap = {
      $schema: 'urdt/application_map_v1.json',
      version: '1.0.0',
      generatedAt: new Date().toISOString(),
      entryNodeId: this.entryNodeId || '',
      nodes: nodeObj,
      edges: this.edges,
      controlBindings,
      coverageGaps,
      deadlockNodes: Array.from(this.deadlockNodes)
    };

    const dir = path.dirname(outputPath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }

    fs.writeFileSync(outputPath, JSON.stringify(map, null, 2), 'utf-8');
    return map;
  }

  public getNodesCount(): number {
    return this.nodes.size;
  }

  public getEdgesCount(): number {
    return this.edges.length;
  }
}
