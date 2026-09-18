# Validation and Debugging Pattern

## Use When

The user requests diagnosis, error investigation, runtime validation, or build/test analysis.

## Process

1. Capture the exact failure, environment, reproduction path, and available evidence.
2. Inspect fresh logs, state, and source near the failing boundary.
3. Form a minimal hypothesis and verify it with a machine check or an independent judge.
4. Implement a fix only when the user authorizes it, then re-run the relevant validation.

## Completion

Return the cause, evidence, validation result, and remaining risks.
