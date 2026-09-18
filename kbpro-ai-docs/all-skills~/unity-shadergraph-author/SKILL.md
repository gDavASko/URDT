---
name: unity-shadergraph-author
description: "Unity Shader Graph URP. Triggers: Shader Graph, shadergraph, create graph, graph node, URP graph, visual shader."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Skills/UnityMathHelper.md
  - kbpro-ai-docs/unity-wiki/raw/external-skills/unity-ai-skill-coverage-audit.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
license: project-internal
validated_against:
  - AGENTS.md
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
forbidden_actions:
  - Do not create Shader Graph for URP with Built-in RP nodes (they will silently break).
  - Do not add SpriteRenderer-specific passes unless the target is a SpriteRenderer.
  - Do not add SRP Batcher or extra passes "just in case" — validate against target renderer.
  - Do not add unnecessary texture samples — pack RGBA channels to stay mobile-friendly.
  - Do not add conditional HLSL branches in Fragment — use step/lerp instead.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
known_risks:
  - Unity 6000.0.67f1 URP 17 Shader Graph API differences from earlier versions.
  - Graph nodes that compile but produce unexpected results on mobile GPU drivers.
  - Overdraw from unintentional transparency or full-screen passes.
---

# Unity Shader Graph Author

## Purpose

Create and modify Shader Graph assets correctly for URP 17 / Unity 6000.0.67f1. Ensures graphs compile, respect project render pipeline, and stay mobile-performant.

## When To Use

- Creating a new Shader Graph for a material effect.
- Adding or modifying properties, keywords, nodes, or subgraphs.
- Reimporting or validating an existing graph after Unity version change.
- Explaining what a graph does.

## Do Not Use

- For HLSL `.shader` file authoring — use `unity-shader-developer`.
- For material property changes only — use `unity-material-author`.

## Workflow

1. Read `kbpro-ai-docs/unity-wiki/wiki/runbooks/unity-shader-ai-guidelines.md` if available, or `unity-wiki/raw/shader-ai-guidelines.md` for mandatory shader rules.
2. Confirm URP target: `Universal Render Pipeline/Lit` or `Universal Render Pipeline/Unlit` base.
3. Define properties in the Blackboard with meaningful names and default values.
4. Build the node graph for the effect — describe nodes, connections, and math.
5. Validate:
   - No Built-in RP nodes.
   - No unnecessary texture samples.
   - Correct blend mode (Opaque/Transparent/Cutout) for the use case.
   - Keywords for variants only if needed.
6. Describe reimport or validation step.
7. Note mobile performance impact (overdraw, ALU cost).

## Common Effect Patterns

| Effect | Key Nodes |
|---|---|
| Dissolve | Sample Texture 2D → One Minus → Step(threshold) → Alpha Clip |
| Outline | Sample adjacent UVs → subtract → step → mix with base color |
| Rim Light | View Direction → Normal → Dot → One Minus → Power → multiply |
| Distortion | Sample noise → remap → offset UV → Sample main texture |
| Hologram | Time → Sin → multiply opacity → Fresnel for edge glow |

## Output Format

- Describe the graph as a node list with connections (not JSON — readable description).
- List all Blackboard properties with type and default value.
- State expected material settings (blend mode, render queue, surface type).
- State mobile performance notes.

## Test Prompts

1. "Create a URP Shader Graph for a dissolve effect with noise texture and threshold slider."
2. "Add a rim light property to an existing Lit Shader Graph."
3. "Explain what this Shader Graph node chain does and how to optimize it."
4. Negative: "Use Built-in RP base for this URP project." Skill should refuse and specify URP base.
