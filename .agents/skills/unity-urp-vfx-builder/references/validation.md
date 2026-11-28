# Validation and approval gates

## Technical
- Confirm Unity version and actual render pipeline package version.
- Check compiler and shader errors; inspect new warnings separately from baseline warnings.
- Validate saved prefab references and missing scripts/materials.
- Enter Play Mode in the sandbox; inspect start, sustained playback, reset and stop. For loops, inspect several cycles and transitions.
- Check repeatability with fixed seed and camera; capture at matching settings for before/after comparisons.
- Use Profiler / Frame Debugger when available to measure baseline versus active effects: CPU/GPU frame time, GC allocations, draw calls, particle count and transparency overdraw where observable. Do not invent values or use browser budgets as Unity targets.
- Record hardware, resolution, graphics API, and test mode. Editor measurements are not standalone build benchmarks.

## Visual
Score each criterion as pass / revise / not tested, with evidence: shape, value contrast, palette, motion, depth blending, FPS readability, material quality, consistency with the dark fantasy setting. Capture with and without Bloom using a sandbox-only configuration. Record a short sequence if possible, not just screenshots. Avoid excessive flash or camera disruption.

## Change safety
Do not overwrite user changes. Keep original assets and scenes intact. Report actual git diff and new files. No commit, push, or production scene integration without approval. If editor access is missing, deliver the implementation as unverified and list precise manual checks.
