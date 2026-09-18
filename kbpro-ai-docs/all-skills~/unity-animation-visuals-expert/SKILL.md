---
name: unity-animation-visuals-expert
description: "Spine, DOTween, Unity Animator. Triggers: animate, DOTween, Spine controller, Animator, visual transition, optimize animation."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Skills/AnimationVisualsExpert.md
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
  - Do not use UnityEngine.UI.Text — always TextMeshPro.
  - Do not create new animation services when existing KBPro audio/spine services cover the need.
  - Do not create custom tutor attention/idle tween components when an existing `AbstractIdleEffect` such as `JumpingIdleEffect`, `PulsingIdleEffect`, `MovingIdleEfect`, or `FloatingIdleEffect` can be configured.
  - Do not modify `Assets/KBPro/kbpro-tutorsystem/**` for module-specific animation behavior; compose existing tutor APIs from the module.
  - Do not skip SafeKill before starting a new tween on an object.
  - Do not use string-based Animator parameter access — always hash via Animator.StringToHash().
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
known_risks:
  - Creating tween leaks by not calling SafeKill() before reassigning.
  - Reimplementing an existing KBPro idle effect with local DOTween code because the agent did not inspect `kbpro-tutorsystem/Runtime/IdleEffects`.
  - Mixing tutor guided-drag object selection with attention-pulse object selection, causing the ghost hand to drag random tools.
  - Forgetting to unregister Spine event callbacks in Dispose.
  - Using string Animator parameters instead of cached hashes (allocation per call).
---

# Unity Animation & Visuals Expert

## Purpose

Apply this skill for all animation and visual transition work in the project: Spine skeletal animations, DOTween sequences, and Unity Animator.

## When To Use

- Task mentions Spine, `SkeletonAnimation`, `SkeletonGraphic`, Spine Events, Mix Settings.
- Task mentions DOTween, `SafeKill`, `MakeSequence`, tweens, `Sequence`, easing.
- Task mentions `Animator`, state machine, animation parameters, `StateMachineBehaviour`.
- User says "animate", "transition", "visual polish", or "optimize animator".

## Do Not Use

- Do not use this skill to add audio logic — use `kbpro-async-reactive-expert` for async animation chains.
- Do not create new animation managers if the existing `kbpro-spine` and DOTween utilities cover the need.
- Do not create new tutor idle/attention animation components until existing `AbstractIdleEffect`
  implementations in `Assets/KBPro/kbpro-tutorsystem/Runtime/IdleEffects/` have been inspected.

## Workflow

### Spine Animation
1. Use `SkeletonAnimation` for world-space objects; `SkeletonGraphic` for UI (Canvas).
2. Drive state via `AnimationState.SetAnimation` and `AddAnimation`.
3. Use Spine Events (`spineListener`) for hit points, VFX triggers, audio cues.
4. Configure Mix Settings for smooth transitions between states.
5. Register Spine event listeners in `Initialize`; unregister in `Dispose`.

### DOTween (KBPro Way)
1. Before starting any tween on a field, call `tween.SafeKill()` (or `SafeKill(true)` to complete).
2. For repeated animations, cache `Tween`/`Sequence` — do not allocate in `Update`.
3. Use professional easing: `Ease.OutQuad` for movement, `Ease.OutBack` for UI pop.
4. Chain with `.OnComplete()` or `.AppendCallback()` for logic sync.
5. Always kill tweens in `Dispose` / `OnDisable`.

### Tutor Idle / Attention Effects
1. Before writing a new tutor attention animation, inspect existing `AbstractIdleEffect`
   implementations: `JumpingIdleEffect`, `PulsingIdleEffect`, `MovingIdleEfect`,
   `FloatingIdleEffect`, `RotationIdleEffect`, and related effects.
2. If the desired behavior is a jump, pulse, move, float, rotation, activation, or existing Spine
   cue, configure that effect on the tutor object instead of creating a module-local clone.
3. Keep animation implementation separate from tutor orchestration. A module may choose which
   existing `IdleEffect` to play, but the animation itself should remain the shared effect.
4. For tutorials that combine random attention pulses with a guided drag of one valid object, do
   not reuse the drag object pool for attention selection. Keep the drag tutor object specific and
   drive attention by calling existing `IdleEffect.PlayIdleEffect(...)` on the selected object.
5. Treat edits under `Assets/KBPro/kbpro-tutorsystem/**` as platform-level changes requiring
   explicit approval; module tasks should only configure and compose those APIs.

### Unity Animator
1. Cache parameter hashes once: `private static readonly int _hashRunning = Animator.StringToHash("Running");`
2. Prefer `StateMachineBehaviour` for complex in-state logic over checking state in `Update`.
3. Avoid `CrossFade` by string — use the hash overload.

## Output Format

When generating animation code:
- Show full `Initialize` and `Dispose` lifecycle with SafeKill/unsubscribe.
- State any Spine Events that need corresponding C# bindings.
- List fields that need to be serialized (`[SerializeField] private`) vs. cached in `Awake`.

## Test Prompts

1. "Write a DOTween window-open sequence with scale and alpha, using SafeKill."
2. "Create a Spine controller wrapper with event callbacks, Initialize and Dispose."
3. "Optimize this Animator code: it uses string parameters in Update."
4. Negative: "Animate this in Update without caching." Skill should refuse and produce a cached solution.
5. Negative: "Create a custom jumping tutor attention component." Skill should first require using
   `JumpingIdleEffect` unless a documented gap proves it is insufficient.
