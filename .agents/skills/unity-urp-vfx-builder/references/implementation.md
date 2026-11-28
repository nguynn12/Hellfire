# Implementation decisions

## Native pipeline first
- C# controls effect lifetime and exposed parameters; use AnimationCurve for art-directed changes. Keep persistent assets separate from runtime instances.
- Use ParticleSystem for small to moderate ambient effects, with consistent simulation space, explicit seeds for repeatable previews, bounded particle counts and sensible culling.
- Use URP particle shaders or custom HLSL where needed; check compilation on the actual target graphics API. Use Shader Graph through supported editor workflows when practical.
- Use HDR emission and existing Bloom only when already configured. Evaluate the silhouette with Bloom disabled in the sandbox; avoid changing global volumes.
- Soft particles require transparent materials and an available camera depth texture. Verify configuration; if absent, report it and use a non-depth fallback or request permission for the change.
- Reuse texture assets: tileable noise, flow distortion, soft cloud mask and glint. Generate only what an effect needs with editor-time Texture2D tools; use correct linear/sRGB import settings for data/color maps. Avoid repeated texture generation during updates.
- Reuse materials and mesh buffers; avoid unnecessary per-frame allocations and runtime material duplication. Bound shader math and handle zero-length vectors and small denominators safely.
- Optional VFX Graph 17.5: verify package compatibility, Linear color space, compute support and target platform support. URP support is still listed as preview in the 17.5 documentation. Do not install automatically.
- Custom renderer features are optional; use current Unity Render Graph conventions if approved, and avoid global post-processing changes for local effects.

## Suggested first sandbox: Lava and Frost ambient variants
Create independent reusable environmental prefabs. Lava: warm emissive fissure accents, drifting orange embers and subtle layered haze. Frost: cool crystal shimmer, sparse blue-white motes and low drifting mist. Keep the existing modular geometry unchanged. Design distinct silhouettes and avoid overwhelming the FPS camera. Use placeholder geometry in the sandbox if original assets are unavailable. No external services required.

## Suggested asset layout (adapt to repository conventions)
Assets/_Project/VFX/{Runtime,Editor,Materials,Shaders,Textures,Prefabs,Scenes}
Keep editor-only scripts inside an Editor folder or suitable assembly. Put visual evidence outside imported Assets if repository conventions permit.

## Source notes
Original workflow guidance adapted conceptually from the MIT-licensed Three.js Game Skills by Majid Manzarpour; this is an independent Unity-specific instruction set, not a port of its TypeScript code.
- https://github.com/majidmanzarpour/threejs-game-skills
- https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/System-Requirements.html
- https://docs.unity.com/en-us/engine/6000.5/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/particles-unlit-shader
