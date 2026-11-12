---
name: unity-ar-assets
description: Import supplied FBX, ZIP, Blender, or glTF models and assemble grounded, lightweight URP prefabs for a Unity handheld AR game. Use for model intake, material remapping, prefab geometry, or mobile asset verification.
---

# Unity AR asset preparation

## Inspect and import

1. Inventory each mesh source and its texture maps, author and license metadata, dimensions, material slots, and visible geometry. Keep original files and Unity GUIDs intact. Put exports, generated maps, materials, and prefabs in a project-owned directory. Treat concept images as composition and palette references.
2. Import FBX with a verified axis and scale conversion. For ZIP sources, list entries first and extract only the intended model and companion maps to a project-owned location. Preserve relative texture paths where the format needs them. Reuse equivalent existing textures after verifying their contents and intended channel usage.
3. For a `.blend` source, export the required visible meshes to FBX using the installed Blender version. Use selected meshes and required transforms only, including evaluated geometry when modifiers affect the silhouette. Rebuild material bindings in URP and check facing, normals, UVs, and bounds after export. Use the derived FBX as the runtime prefab's dependency so reopening or building the project can use the same export.
4. For `.glb`, use a Unity-compatible glTFast editor importer or export a derived FBX with Blender if an extra Unity dependency is unnecessary. Inspect the imported children and use the visible shell geometry needed for the silhouette. Preserve source metadata and record any mesh simplification. Prefer an editor-imported asset that loads with the scene. [Unity glTFast editor import](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.14/manual/ImportEditor.html)

## Assemble and shade

- Make the placement prefab root a unit-scale transform at the bottom center of the combined footprint with local +Y up. Normalize mesh rotation, scale, and pivot offsets under the visual children. Measure the combined bounds in prefab-local space with Unity's meter scale and verify the lowest visible point contacts local `Y=0` on a test plane.
- Seat the upper platform on the lower pedestal with each source visibly contributing to the silhouette. Check seam overlap, coplanar flicker, and clearance from low camera angles. Keep the placement system responsible for anchoring the assembled root.
- When the brief calls for a first-person viewmodel, derive the visible parts from the supplied source and keep the prefab camera-relative and render-only. Fit it beyond the near plane, keep its silhouette clear of the reticle and controls, and let the active camera determine shot pose and range.
- Map albedo and colored emission as color textures. Import normal maps with the Normal Map type. Read roughness, metallic, occlusion, and masks as linear data. Inspect the real channels before remapping. When packing for URP Lit, use metallic in red, occlusion in green, and smoothness in alpha. Convert authored roughness to smoothness as `1 - roughness` when needed. [Unity URP channel packing](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shaders-in-universalrp-channel-packed-texture.html)
- Use opaque URP materials for solid base and turret surfaces and reserve transparency for geometry that needs it. Assign the game's custom Shader Graph material to the visible orb geometry and keep the orb collision shape independent of visual animation. If overlapping shells add overdraw or obscure the target, reduce the derived orb to a representative shell and verify its silhouette.
- Start with mobile texture overrides and mipmaps for world assets. Use ETC2 for the broad OpenGL ES 3 tier and select the appropriate alpha support for each map. Reduce texture dimensions, submeshes, and transparent layers as profiling warrants. Use simple gameplay colliders on the owned prefabs and keep render meshes independent. [Unity GPU texture formats by platform](https://docs.unity3d.com/6000.5/Documentation/Manual/texture-choose-format-by-platform.html)

## Validate the handoff

- Open the prefabs in the active URP setup and inspect from realistic phone viewpoints. Confirm contact with the placement plane, a readable deck, and a clearly visible target with its actual Shader Graph material. Check the viewmodel crop across supported aspect ratios. Verify serialized references, material slots, and Android rendering when a device is available.
- Report the derived prefab paths, measured footprint and target bounds, the transform contract for placement, and any remaining import or mobile rendering checks. Keep creator, title, original source, license, and modifications in the distribution credits. Resolve unknown redistribution terms before external sharing. [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/)

## Reality Defender intake

- Under `Assets/Game_Assets`, the pedestal is `Base/sci-fi-pedestal/source/pedestal_low.fbx`. Its source maps are 4096 px square, which makes Android import overrides important.
- The platform archive at `Base/sci-fi-platform/source/Sci-Fi Platform.zip` contains `platform/Platform.fbx` and texture maps. The existing platform `Base Color.png` and `Normal.png` match their archived counterparts pixel for pixel. The archived `platforms_occ.png` is solid white, so a default neutral occlusion value is sufficient. Inspect the remaining optional maps against the model before assigning them.
- The turret source is `Weapon/railgun-turret/source/Railgun turret.blend`. Export the visible barrel and housing as FBX using the installed Blender and use the supplied texture maps. Fit the result as `RailgunViewmodel.prefab` low on the active camera, with a cyan grille and an open aiming area. Keep the arena deck unobstructed and match the uploaded mockups' blue ring, cyan vents, and small amber inserts.
- `energy_orb.glb` contains twelve sphere meshes with about 11,520 indexed triangles and a nearly transparent source material. Verify the derived target is visible against a live camera feed with the custom material. Credit the original and describe any visual or mesh changes. [Energy Orb by tamminen](https://sketchfab.com/3d-models/energy-orb-c37063dad7a046fb84649ad5a621bb55), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)
