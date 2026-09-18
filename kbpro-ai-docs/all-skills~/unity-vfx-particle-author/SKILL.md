---
name: unity-vfx-particle-author
description: "ParticleSystem, VFX materials. Triggers: particle system, VFX, emission, burst, trail, sub-emitter, particle material, visual effect."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/unity-wiki/raw/external-skills/unity-ai-skill-coverage-audit.md
  - kbpro-ai-docs/unity-wiki/raw/external-skills/unity-ai-skill-systematization.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
license: project-internal
validated_against:
  - AGENTS.md
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
forbidden_actions:
  - Do not use Instantiate/Destroy for frequent VFX — use object pooling.
  - Do not enable Collision on particles unless the effect genuinely requires physics collision.
  - Do not set Max Particles higher than needed — profile on mobile.
  - Do not use heavy texture sampling in particle shaders on mobile without LOD consideration.
  - Do not leave particle systems playing after the owning object is disabled — call Stop() in Dispose.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
known_risks:
  - Particle system leak when Play() is called without Stop() on module/object disable.
  - High mobile GPU load from overdraw and large particle quads.
  - Trail renderer memory growth if trails are not cleared on Stop.
---

# Unity VFX & Particle Author

## Purpose

Author ParticleSystem configurations and supporting materials/shaders for visual effects. Ensures effects are mobile-performant, use pooling, and are cleanly stopped in KBPro lifecycle.

## When To Use

- Designing emission rate, burst, shape, lifetime, size over lifetime, or color over lifetime.
- Configuring trails, sub-emitters, or collision on particles.
- Creating or selecting a material/shader for a particle system.
- Reviewing particle systems for mobile performance (overdraw, max particles, CPU cost).

## Lifecycle Rules

```csharp
[SerializeField] private ParticleSystem _effect;

public void PlayEffect() => _effect.Play();

public void Dispose()
{
    if (_effect != null && _effect.isPlaying)
        _effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
}
```
- Pool the parent GameObject — do not Instantiate/Destroy per hit.
- On pool return: `Stop(true, StopEmittingAndClear)` to clear all particles.
- On pool get: `Clear()` then `Play()`.

## Performance Checklist

| Setting | Mobile Guideline |
|---|---|
| Max Particles | ≤ 50 for burst, ≤ 20 continuous on-screen per system |
| Texture | Packed atlas, power-of-2, compressed |
| Render Mode | Billboard or Stretched Billboard (no Mesh unless required) |
| Collision | Disabled unless physically required |
| Lights | Disabled — use emissive material instead |
| Trails | Short lifetime, low vertex count |

## Material Guidance

- Use URP Particles/Unlit or URP Particles/Lit base shader.
- Additive blending for fire, glow, sparks.
- Alpha blending for smoke, dust.
- Premultiply for soft edges.
- Avoid stacking transparent particles in the same screen area (overdraw).

## Output Format

- Describe ParticleSystem module settings (emission, shape, size, color, lifetime) as a table or checklist.
- State material/shader choice and blend mode.
- Show `Play()`, `Stop()`, and pool integration code.

## Test Prompts

1. "Design a hit-spark particle system for a train collision — mobile-safe settings."
2. "Create a coin-collect burst effect with pooling and Dispose lifecycle."
3. "Review this particle system — is it mobile-safe? What should change?"
4. Negative: "Instantiate this effect on every hit." Skill should enforce pooling.
