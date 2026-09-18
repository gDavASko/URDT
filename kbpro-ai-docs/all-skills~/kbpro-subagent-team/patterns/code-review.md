# Code Review Pattern

## Use When

The user requests a review of a change, diff, pull request, or implementation.

## Process

1. Read the task requirements and review only the relevant changed artifacts.
2. Compare behavior, constraints, tests, and repository rules against the change.
3. Assign a judge who did not perform the reviewed substantial work.
4. Report actionable findings with severity, location, evidence, and required correction.

## Completion

Return `PASS`, `CHANGES_REQUIRED`, or `BLOCKED` with evidence.
