/**
 * HTN method library: GDD `playbook` name → executable L2 playbook.
 */

import { PlaybookContext, PlaybookResult } from '../playbook_context.js';
import { matchAndPlace, balanceScale } from './placement.js';
import { tracePath, scrubCoverage, holdInBand, sprayTargets, rotateWheel, wobbleExtract, lensDwell, fillWells } from './precision.js';
import { timingIntercept, rhythmAlternate, mashButton, stackDrop, reactionStrike, popTargets } from './timing.js';
import { pipePuzzle, paintByKey, gridPath } from './puzzles.js';
import { laneRunner, slingshot, carDrive, glider } from './reactive.js';
import { uiScenario } from './ui_suite.js';

export type Playbook = (ctx: PlaybookContext) => Promise<PlaybookResult>;

export const PLAYBOOKS: Record<string, Playbook> = {
  match_and_place: matchAndPlace,
  balance_scale: balanceScale,
  wobble_extract: wobbleExtract,
  trace_path: tracePath,
  scrub_coverage: scrubCoverage,
  hold_in_band: holdInBand,
  spray_targets: sprayTargets,
  rotate_wheel: rotateWheel,
  lens_dwell: lensDwell,
  fill_wells: fillWells,
  timing_intercept: timingIntercept,
  rhythm_alternate: rhythmAlternate,
  mash_button: mashButton,
  stack_drop: stackDrop,
  reaction_strike: reactionStrike,
  pop_targets: popTargets,
  pipe_puzzle: pipePuzzle,
  paint_by_key: paintByKey,
  grid_path: gridPath,
  lane_runner: laneRunner,
  slingshot,
  car_drive: carDrive,
  glider,
  ui_scenario: uiScenario,
};
