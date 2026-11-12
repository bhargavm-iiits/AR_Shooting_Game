# Asset credits

## Energy Orb

- **Title:** Energy Orb
- **Creator:** [tamminen](https://sketchfab.com/tamminen)
- **Original:** [Sketchfab model](https://sketchfab.com/3d-models/energy-orb-c37063dad7a046fb84649ad5a621bb55)
- **License:** [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/)
- **Supplied file:** `Assets/Game_Assets/energy_orb.glb`
- **Runtime derivative:** `Assets/RealityDefender/Art/Models/EnergyOrb/EnergyOrbShell.fbx`

The supplied GLB's `asset.extras` explicitly identifies this title, author, source and license. The original file is preserved. The twelve original open shells were reduced to one closed convex hull using the source vertices, with 264 vertices and 524 triangles. The derivative is centered at a 0.14 m diameter and has new spherical UVs. Its original nearly transparent material is replaced by the project's animated cobalt, violet, magenta and cyan energy Shader Graph. These mesh, UV and material changes were made for The Reality Defender assignment. No endorsement by the original creator is implied.

## Supplied pedestal

- **Source:** `Assets/Game_Assets/Base/sci-fi-pedestal/source/pedestal_low.fbx`
- **Supplied maps:** accompanying pedestal textures in `Assets/Game_Assets/Base/sci-fi-pedestal/textures/`
- **Use:** lower support of the combined Game Base, normalized to meter scale; URP material setup and cyan emission treatment.
- **Creator, original download URL and redistribution license:** not supplied in this workspace. No license or readme was present with these files. Attribution and redistribution terms remain unverified.

## Supplied sci-fi platform

- **Source:** `Assets/Game_Assets/Base/sci-fi-platform/source/Sci-Fi Platform.zip`, entry `platform/Platform.fbx`
- **Runtime copy:** `Assets/RealityDefender/Art/Models/Platform/platform/Platform.fbx`
- **Supplied maps:** existing Base Color and Normal maps, plus selected Emission, Metalic and Roughness maps from the archive.
- **Use:** upper deck of the combined Game Base, normalized and seated on the supplied pedestal; URP material conversion and cyan emission treatment.
- **Creator, original download URL and redistribution license:** not supplied in this workspace. No license or readme was present with these files. Attribution and redistribution terms remain unverified.

## Supplied railgun turret

- **Source:** `Assets/Game_Assets/Weapon/railgun-turret/source/Railgun turret.blend`
- **Runtime derivative:** `Assets/RealityDefender/Art/Models/Railgun/RailgunUpper.fbx`
- **Supplied maps:** accompanying railgun Albedo, Normal, Metalness, Roughness, AO and Specular maps.
- **Changes:** baked the source pose to a static mesh, removed the stand and lower swivel support using the source's weighted Base region and support geometry, retained the upper housing, barrel and radiators, centered the mesh and normalized length to 0.36 m. Export contains 9,820 vertices and 16,475 triangles. URP materials and cyan presentation accents are project modifications.
- **Creator, original download URL and redistribution license:** not supplied in this workspace. No license or readme was present with these files. Attribution and redistribution terms remain unverified.

## Downloaded fonts (SIL Open Font License 1.1)

- **Russo One** by Jovanny Lemonad: `Assets/RealityDefender/Resources/Fonts/RussoOne-Regular.ttf`, used for HUD headlines, score, FIRE and level popups.
- **Rajdhani** (Bold, SemiBold) by Indian Type Foundry: `Assets/RealityDefender/Resources/Fonts/Rajdhani-*.ttf`, used for HUD body text.
- Source: the Google Fonts repository (`github.com/google/fonts`, `ofl/russoone`, `ofl/rajdhani`). The licence texts are kept in `Assets/RealityDefender/ThirdParty/Licenses/`. The fonts are unmodified.

## Downloaded sound effects (CC0 1.0)

- **Sci-Fi Sounds** by Kenney (kenney.nl), CC0 1.0 Universal, obtained from the CC0 mirror `github.com/Boyquotes/kenney-sci-fi-sounds-for-godot` (repository LICENSE: CC0 1.0).
- Files in `Assets/RealityDefender/Resources/Audio/`, renamed only:

| Project file | Original file |
| --- | --- |
| `sfx_shot_a.ogg`, `sfx_shot_b.ogg` | `laser_large_001.ogg`, `laser_large_003.ogg` |
| `sfx_burst.ogg`, `sfx_burst_final.ogg` | `explosion_crunch_002.ogg`, `explosion_crunch_004.ogg` |
| `sfx_sub_thump.ogg` | `low_frequency_explosion_000.ogg` |
| `sfx_bubble_pop.ogg` | `slime_000.ogg` |
| `sfx_level_intro.ogg`, `sfx_level_swell.ogg` | `force_field_002.ogg`, `force_field_004.ogg` |
| `sfx_place.ogg` | `impact_metal_002.ogg` |

CC0 does not require attribution; it is given here as a courtesy. The recorded sounds are layered with project-synthesised audio at runtime.

## Project-created presentation

The gameplay code, HUD geometry, scan visualization, supplementary deck rings, animated target material, procedural vein texture, projectile presentation and impact effects were authored for this assignment. Supplied and generated images under `Docs/GameReferences/` are visual references and are not shipped as camera-feed substitutes.

## Distribution status

The files are staged locally for assignment review. No public upload or public sharing has been performed. Confirm the supplied pedestal, platform and railgun redistribution terms before external distribution. Detailed source measurements and derivative operations are retained in `Tools/AssetPrep/source-audit.json`, `Tools/AssetPrep/derivative-audit.json` and the asset-preparation scripts.
