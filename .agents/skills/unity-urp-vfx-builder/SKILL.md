---
name: unity-urp-vfx-builder
description: Design, implement and visually verify stylized environmental and magical visual effects in existing Unity URP projects using C#, particles, procedural textures and shaders. Use for portals, ambient fog, embers, frost, crystals and magical light.
---
# Unity URP VFX Builder

## Scope
Work with the existing project and its render pipeline. In HELLFIRE the expected baseline is Unity 6000.5.6f1 and URP 17.5.0; verify ProjectSettings/ProjectVersion.txt, Packages/manifest.json and packages-lock.json before assuming versions. Respect AGENTS.md, art direction, existing assets and current scene state. Do not install external packages, upgrade Unity, modify shared renderer settings or commit/push without explicit approval. Do not use Three.js or browser dependencies.

## Workflow
1. Inspect the existing render settings, target camera, folder conventions, project changes and available editor/test tools. Capture a baseline if possible. Report unavailable capabilities without pretending to have run Unity.
2. Define one visual brief: silhouette, palette, duration, scale, layering, readability and expected performance. For ambient loops, use a seamless cycle; for transient magical effects, build a distinct buildup, reveal, peak, settle and fade. Avoid generic glowing rings as a default.
3. Read references/implementation.md for implementation decisions. Prefer native ParticleSystem and URP-compatible materials first. Use procedural textures and authored mesh silhouettes as needed. Reserve VFX Graph and custom renderer features for demonstrated needs and explicit approval.
4. Work in an isolated VFX sandbox scene and dedicated asset subtree following existing conventions. Create reusable prefabs, shared materials and deterministic test controls. Use Unity Editor APIs for generating persistent assets and references; do not fabricate .meta GUIDs or hand-write unsupported serialized graph data. Do not edit production scenes or gameplay.
5. Verify actual compilation, prefab integrity, Play Mode, camera-view captures and measured performance when tools permit. Read references/validation.md. Never mark unperformed checks as passed. Do not claim AAA quality from source code or a single still.
6. Deliver a concise report with changed files, asset paths, effect settings, screenshots or clip, measured checks, remaining issues and a proposed next step. Stop before production integration unless authorized.
