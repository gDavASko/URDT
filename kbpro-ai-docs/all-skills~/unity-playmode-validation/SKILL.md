---
name: unity-playmode-validation
description: "Compile and run Unity tests/console checks. Triggers: run tests, validate in play mode, check console, compile and test."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/unity-wiki/raw/external-skills/unity-ai-skill-coverage-audit.md
  - kbpro-ai-docs/unity-wiki/raw/external-skills/unity-ai-skill-systematization.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
license: project-internal
validated_against:
  - AGENTS.md
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - rg
  - git-diff-read
  - dotnet-build-readback
  - shell-read
forbidden_actions:
  - Do not claim a task is complete without at minimum running dotnet build on changed assemblies.
  - Do not skip Unity test run when the change touches gameplay, UI flow, lifecycle, Addressables, or editor tooling.
  - Do not ignore compile errors in unrelated assemblies — they may indicate broken dependencies.
  - Do not run Unity.exe without user confirmation if Unity is not confirmed to be in PATH.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
known_risks:
  - Dotnet build passing while Unity-specific compile errors exist (e.g., missing UNITY_EDITOR defines).
  - Play Mode tests requiring Unity Editor to be open — cannot run headlessly without license.
  - Console errors introduced by serialized reference breakage after prefab/scene changes.
---

# Unity PlayMode Validation

## Purpose

This skill defines the validation gate that must be run before any code or asset change is reported as complete. It sequences build, test, and readback checks in the correct order.

## When To Use

- After any C# change — always run `dotnet build`.
- After any scene, prefab, ScriptableObject, Addressables, lifecycle, UI, or editor tooling change — also run Unity tests if available.
- When the user asks "does this work?" or "validate the change".
- As the final self-check step described in AGENTS.md.

## Validation Sequence

### Step 1: Build Changed Assemblies
```powershell
dotnet build .\Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
dotnet build .\Assembly-CSharp-Editor.csproj --no-restore /p:BuildProjectReferences=false /m:1 /v:minimal
```
Fix all errors before proceeding. Warnings are not blocking unless they indicate runtime risk.

### Step 2: Check Changed Files
```powershell
git status --short
git diff --name-only
```
Confirm only intended files changed. No unrelated scene/prefab/meta churn.

### Step 3: Verify .meta Files
- Every new `.cs`, `.asset`, `.prefab`, or folder must have a `.meta` file.
- If meta is missing, Unity will regenerate it with a new GUID — breaking references.

### Step 4: Unity Edit Mode Tests (when applicable)
```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -quit
```
Required when the change touches: editor tooling, data parsing, module lifecycle, config loading, ID processors.

### Step 5: Unity Play Mode Tests (when applicable)
```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -quit
```
Required when the change touches: gameplay logic, UI flows, Addressables, scene transitions, input, module start/stop.

### Step 6: Console and Readback Check
- Check Editor console / Editor.log or run tests for new errors.
- Verify serialized references if Inspector fields were modified.
- Run `kbpro-lifecycle-auditor` if new async/tween/subscription code was added.

## Output Format

```markdown
## Validation Report

- dotnet build (runtime): PASS / FAIL [errors]
- dotnet build (editor): PASS / FAIL [errors]
- git status: [list of changed files — expected vs. unexpected]
- .meta files: present / missing [list]
- Unity EditMode tests: PASS / FAIL / SKIPPED [reason]
- Unity PlayMode tests: PASS / FAIL / SKIPPED [reason]
- Console errors: none / [list]
- Skipped checks: [list with reasons]
```

## Test Prompts

1. "Validate these changes before I commit — run through the full check sequence."
2. "The module builds but I'm getting a NullReference in Play Mode — help me triage."
3. "Run the edit mode tests and report results."
4. Negative: "It compiles, so ship it." Skill should run the full sequence and not stop at build pass.
