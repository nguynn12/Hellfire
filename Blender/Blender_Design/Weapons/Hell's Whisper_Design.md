# HELL'S WHISPER — DESIGN & PRODUCTION SPECIFICATION

**Project:** Hellfire  
**Asset Name:** Hell's Whisper  
**Asset Type:** Weapon / Pistol (Starter Weapon)  
**Art Style:** Dark Fantasy Hybrid  
**Target Engine:** Unity 6  
**Authoring Application:** Blender 5.2.3 LTS  
**Blender Source:** `Blender/Blender_Asset/Weapons/Hells_Whisper.blend`  
**Unity Export:** `Assets/_Project/Models/Weapons/HellsWhisper.fbx` & `HellsWhisper.glb`  
**Status:** Redesign Complete (High-Fidelity Production Approved)

---

## 1. Asset Identity & Gameplay Role

- **In-Game Role:** Starter handgun for the player in Hellfire.
- **Weapon Classification:** Semi-automatic hitscan pistol.
- **Gameplay Specifications:**
  - Damage: 15 HP per hit
  - Fire Interval: 0.25 s
  - Magazine Capacity: 12 rounds
  - Effective Range: 100 m
- **Design Intent:** The weapon reads unmistakably as a combat handgun first (clear slide, barrel, muzzle, trigger, trigger guard, ergonomic grip, and removable magazine) while emanating a distinctive, cursed infernal identity.

---

## 2. Art Direction & Visual Hierarchy

### 2.1 Aesthetic Theme: Dark Fantasy Hybrid
A functional, believable firearm corrupted by demonic dark magic. The silhouette is crisp, readable, and functional, avoiding oversized sci-fi bricks or amorphous fleshy blobs.

### 2.2 Component Breakdown
1. **Slide & Barrel (`SM_Slide`, `SM_Barrel`):**
   - Faceted slide with 45° chamfered top shoulders, combat iron sights (bladed front post with glowing red tritium dot, tactical notched rear sight with dual reference dots).
   - 6 rear and 4 front angled cocking serrations catching specular highlights.
   - Recessed demonic runic sigils carved into both flanks with glowing bloodfire cores.
   - Ejection port with visible chamber block and mechanical extractor claw.
   - Match barrel extending through the slide with crowned muzzle and rifled bore.
   - Infernal compensator at the muzzle with 2 side exhaust ports per side and 2 top vertical gas vents.
   - Recoil guide rod beneath the barrel.
2. **Lower Frame & Receiver (`SM_Frame`):**
   - Tactical Picatinny accessory rail under the barrel with 3 cross-slots.
   - Swept beavertail at the rear protecting the player's hand web.
   - Skeletonized demonic hammer resting cocked against the slide.
   - Slide release lever, ambidextrous thumb safety, takedown pin, and magazine release button.
3. **Trigger & Trigger Guard (`SM_TriggerGuard`, `SM_Trigger`):**
   - Smooth 32-segment combat trigger guard with undercut and square front finger hook.
   - 3-hole skeletonized match curved trigger blade.
4. **Ergonomic Grip & Demon Hide (`SM_Grip`):**
   - Grip angled at 104° rake angle (-14° slant) for optimal FPS viewmodel ergonomics.
   - Frontstrap finger grooves and ribbed backstrap.
   - 3D contoured demon hide leather panels (`M_Demon_Hide`) with convex palm swell and sunken cursed iron hex screws.
5. **Aged Cursed Iron Chains (`SM_Grip_Chains`):**
   - Interconnected 3D cursed iron torus chain links (24 links) wrapping tightly in 3 helical bands across the grip handle and leather panels.
6. **Removable Magazine & Demon Skull Baseplate (`SM_Magazine`):**
   - Standard removable steel magazine casing.
   - Steel collar flush at the magwell base.
   - **Anatomical Demon Skull Floorplate:** Watertight anatomical human/demon cranium facing forward along the barrel axis, featuring a prominent supraorbital brow ridge shelf, deep recessed eye sockets with glowing bloodfire ember orbs (`M_Bloodfire_Rune`), flared zygomatic cheekbones, nasal cavity, dental arch, and **8 predatory teeth including 2 long menacing curved fangs (canines)**.

---

## 3. Measured Technical Specifications

| Metric | Measured Value |
|---|---|
| **Overall Length (Y)** | 0.240 m (24.0 cm) |
| **Overall Height (Z)** | 0.150 m (15.0 cm) |
| **Overall Width (X)** | 0.036 m (3.6 cm) |
| **Base Vertices** | 2,958 |
| **Total Evaluated Triangles** | 19,508 |
| **Total Evaluated Vertices** | 10,148 |
| **Pivot / Origin** | (0, 0, 0) placed at the Hand Grip Center (Palm & Web) |
| **Forward Axis** | +Y (Standard Unity forward when exported with bake_space_transform) |
| **Up Axis** | +Z |

### Detailed Object Breakdown

| Object Name | Base Verts | Eval Tris | Materials Assigned | Modifiers |
|---|---|---|---|---|
| `SM_Slide` | 328 | 4,428 | `M_Infernal_Black_Iron`, `M_Bloodfire_Rune`, `M_Cursed_Iron` | Bevel, WeightedNormal |
| `SM_Barrel` | 160 | 1,428 | `M_Infernal_Black_Iron`, `M_Cursed_Iron` | Bevel, WeightedNormal |
| `SM_Frame` | 96 | 1,296 | `M_Infernal_Black_Iron`, `M_Cursed_Iron` | Bevel, WeightedNormal |
| `SM_TriggerGuard` | 36 | 256 | `M_Infernal_Black_Iron` | Bevel, WeightedNormal |
| `SM_Trigger` | 24 | 324 | `M_Cursed_Iron` | Bevel, WeightedNormal |
| `SM_Grip` | 194 | 864 | `M_Infernal_Black_Iron`, `M_Demon_Hide`, `M_Cursed_Iron` | Bevel, WeightedNormal |
| `SM_Grip_Chains` | 1,920 | 3,840 | `M_Cursed_Iron` | WeightedNormal |
| `SM_Magazine` | 210 | 7,072 | `M_Cursed_Iron`, `M_Bone_Skull`, `M_Bloodfire_Rune` | Bevel, WeightedNormal, Subsurf |
| **Total** | **2,958** | **19,508** | **5 PBR Materials** | Fully Evaluated |

---

## 4. Material & PBR Specifications

Strict adherence to physically based rendering rules and viewport clarity:

1. **`M_Infernal_Black_Iron`:**
   - Metallic: 0.95 | Roughness: 0.38
   - Base Color: Linear (0.025, 0.025, 0.028)
   - Viewport Solid Display Color: (0.10, 0.10, 0.12) — Charcoal Iron.
2. **`M_Demon_Hide`:**
   - Metallic: 0.00 (Dielectric) | Roughness: 0.70
   - Base Color: Linear (0.055, 0.025, 0.015) — Burnt dark-brown organic hide.
   - Viewport Solid Display Color: (0.28, 0.14, 0.08) — Warm Leather.
3. **`M_Cursed_Iron`:**
   - Metallic: 0.88 | Roughness: 0.48
   - Base Color: Linear (0.050, 0.048, 0.045) — Aged gunmetal.
   - Viewport Solid Display Color: (0.18, 0.18, 0.20) — Cursed steel.
4. **`M_Bone_Skull`:**
   - Metallic: 0.00 (Dielectric) | Roughness: 0.55
   - Base Color: Linear (0.42, 0.38, 0.30) — Weathered ivory bone.
   - Viewport Solid Display Color: (0.75, 0.70, 0.58) — Bone Ivory.
5. **`M_Bloodfire_Rune`:**
   - Metallic: 0.00 | Roughness: 0.50
   - Base Color: Linear (0.35, 0.02, 0.01)
   - Emission Color: Linear (1.00, 0.06, 0.01) | Emission Strength: 8.0
   - Viewport Solid Display Color: (1.00, 0.15, 0.03) — Molten Bloodfire.

---

## 5. Unity 6 Export Verification

- **Exported Formats:**
  - `Assets/_Project/Models/Weapons/HellsWhisper.fbx`
  - `Assets/_Project/Models/Weapons/HellsWhisper.glb`
  - `Assets/_Project/Models/Weapons/HellsWhisper_materials.json`
- **Transforms & Pivot:**
  - Root `HellsWhisper` at `(0, 0, 0)`, rotation `(0, 0, 0)`, scale `(1, 1, 1)`.
  - Exported with `bake_space_transform=True`, `apply_unit_scale=True`.
- **Archive Safety:**
  - The previous model backup is safely preserved in `Blender/Blender_Asset/Weapons/Archive/Hells_Whisper_Old_Backup.blend`.
