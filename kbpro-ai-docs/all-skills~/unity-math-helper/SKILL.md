---
name: unity-math-helper
description: "Vector math, Quaternions, coordinate space, shader math. Triggers: calculate direction, rotation math, coordinate space, FOV, gimbal, shader math."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Skills/UnityMathHelper.md
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
  - Do not use Euler angles for smooth rotation — use Quaternion.RotateTowards or Slerp.
  - Do not compute expensive math (sqrt, trig) every Update without caching or approximating.
  - Do not use conditional branches (if/else) in HLSL fragment shaders in hot paths — use step/lerp.
  - Do not add unnecessary texture samples in shader — keep mobile overdraw in mind.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
known_risks:
  - Gimbal Lock from direct Euler manipulation.
  - Expensive per-frame trigonometry without approximation.
  - Shader branching that prevents GPU batching.
---

# Unity Math Helper

## Purpose

Assist with Vector math, Quaternion operations, coordinate space conversions, and HLSL/Shader Graph math logic. Provides correct, optimized implementations that avoid common Unity math pitfalls.

## When To Use

- Direction, distance, alignment (dot product), perpendicular (cross product), FOV checks.
- Smooth rotation: `Quaternion.RotateTowards`, `Quaternion.Slerp`, avoiding Gimbal Lock.
- Converting between World, Local, Screen, and Viewport spaces.
- Writing or explaining HLSL vertex/fragment shaders for URP effects.
- Shader Graph node logic for effects like dissolve, outline, glow, rim light.

## Math Patterns

### Direction and Distance
```csharp
Vector3 dir = (target.position - origin.position).normalized;
float dist = Vector3.Distance(origin.position, target.position);
```

### FOV Check (dot product)
```csharp
float dot = Vector3.Dot(transform.forward, dir);
bool inFOV = dot > Mathf.Cos(halfAngle * Mathf.Deg2Rad);
```

### Smooth Rotation (no Gimbal Lock)
```csharp
Quaternion targetRot = Quaternion.LookRotation(dir);
transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, speed * Time.deltaTime);
```

### Coordinate Spaces
- World → Local: `transform.InverseTransformPoint(worldPos)`
- Local → World: `transform.TransformPoint(localPos)`
- World → Screen: `Camera.main.WorldToScreenPoint(worldPos)`
- Screen → World (at depth): `Camera.main.ScreenToWorldPoint(new Vector3(sx, sy, depth))`

## Shader / HLSL Guidelines

- Use `step(edge, x)` and `lerp(a, b, t)` instead of `if/else` in fragment shaders.
- Minimize texture samples — pack channels (R, G, B, A) to avoid extra samplers.
- For dissolve: `clip(tex.r - _Threshold)`.
- For outline: sample adjacent UVs and check against threshold.
- For glow/rim: `pow(1 - dot(viewDir, normalDir), _RimPower)`.

## Output Format

- Provide the math as a static helper method or inline code with comments explaining the geometry.
- For shaders, show the full relevant HLSL block with input/output struct.

## Test Prompts

1. "Calculate the direction from the train to the nearest passenger and rotate toward it."
2. "Write a smooth quaternion rotation that stops at the target without overshoot."
3. "Explain this HLSL dissolve effect and optimize it for mobile."
4. Negative: "Just use euler angles to rotate smoothly." Skill should explain Gimbal Lock and use Quaternion instead.
