# KB Ingestion Pattern

## Use When

The user explicitly requests importing new knowledge into the KBPro knowledge base.

## Process

1. Classify the source into the correct knowledge-base layer.
2. Propose a split when one source belongs to multiple layers.
3. Place content through the approved ingestion pipeline, rebuild the index, and run KB validation.
4. Preserve source attribution and report the imported artifacts.

## Completion

Finish only after ingestion, indexing, and validation succeed or a concrete blocker is reported.
