# Agent Creation and Registration Rules

Create a new agent only for a confirmed organizational workflow need. Do not add
an agent from a generic domain description, an external article, or an assumption
that it might be useful.

## Required Process

1. Confirm the agent purpose, workflow, authority boundaries, done criteria, and local knowledge sources with the user.
2. Create one agent context inside `agents/` or one of its topical subdirectories.
3. Add an entry to `agents/registry.yaml` with `id`, relative `context`, concise `description`, capability tier, and `allowed_model_families`.
4. Verify that every agent documentation link targets an existing local file inside `kbpro-subagent-team`.
5. Perform independent validation of the context structure, registry entry, and alignment with the confirmed workflow.

## Required Agent Context Structure

Every agent context must contain these sections:

1. Agent Name.
2. Agent Role.
3. Agent Working Rules.
4. Agent Constraints.
5. Agent Documentation — only relative links to local documents inside this skill.
6. Additional Information.

## Prohibitions

- Do not link an agent context to websites, external skill repositories, or unverified general guidance.
- Do not add an agent without a registry entry, and do not leave a registry entry without an existing agent context.
- Do not assign authority outside the confirmed workflow or user authorization.
