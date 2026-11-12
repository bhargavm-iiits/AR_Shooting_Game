# The Reality Defender — Submission

Unity AR target-practice game for the *Mixed Reality Game Dev* assignment.
Unity 6000.5.5f1 · AR Foundation 6.5.1 · Google ARCore XR Plugin 6.5.1 · URP 17.5 · Android (IL2CPP, ARM64).

## Files

| File | What it is |
| --- | --- |
| `RealityDefender_Submission_Demo.mp4` | **Deliverable 1: screen recording.** An edited 90-second 1080p demo built around one continuous real-device AR take: plane detection → tap to place → three levels of shooting (9 targets hit) → WAVE CLEARED. Chapter titles, a live assignment checklist and a target counter are overlaid. A labelled 2× Practice Mode segment follows. |
| `ShaderGraph_Screenshot.png` | **Deliverable 2: Shader Graph screenshot. Still to add; see below.** |
| `RealityDefender.apk` | Release (non-development) Android build, 44.4 MB. SHA-256 `23be20a0058ef02ca203d5aafe67b79baf678f7c7d4ac4280b0c9e6d623b750f` |
| `Raw_Recordings/AR_Run_RealDevice.mp4` | The unedited AR recording (OnePlus 11R, ARCore), 66 s |
| `Raw_Recordings/Practice_Mode.mp4` | The unedited camera-free Practice Mode recording, 48 s |
| `Screenshots/` | Rendered Game View captures from the automated Play Mode run (level-complete popup, level 3 free flight, practice portrait/landscape, cleared) |
| `Evidence/EditorPhysicsVerification.json` | Automated Play Mode run: 52/52 checks passed with real Rigidbody collisions across all 3 levels, 0 runtime errors |
| `Evidence/BuildReport.json` | Unity build report for the APK: succeeded, 0 errors |
| `Evidence/AssetCredits.md` | Attribution for the supplied models and the downloaded fonts (OFL) and sounds (CC0) |

## Assignment coverage

| Requirement | Where to see it in the demo |
| --- | --- |
| AR Foundation, horizontal plane detection | Chapter 02 (≈ 6–12 s): scanned plane outline and placement ring on a real floor |
| Tap the screen to spawn the Game Base on the plane | Chapter 03 (≈ 12 s) |
| Aligned with the surface normal, anchored and immovable | Chapter 03 onward: the base stays put while the phone moves |
| Custom Shader Graph targets (no standard materials) | `Assets/RealityDefender/Art/Shaders/SG_EnergyOrb.shadergraph`: scrolling noise and Fresnel rim, inside a custom soap-film shell shader |
| Particle System explosion on destruction (bonus) | Every hit: layered particle blast; a different signature death per level |
| FIRE button spawns a camera-forward projectile | Throughout: a Rigidbody projectile, drawn as a plasma lance |
| The Game Base spawns floating targets | Levels 1–3: 2 static, 3 orbiting and 4 free-flying targets (the brief's minimum is 3) |
| Rigidbody physics collisions; hit → target disappears with VFX → +10 | Counter rises 0 → 9; the score reaches 90 |
| Miss: projectile destroyed after 5 m | Verified in `Evidence/EditorPhysicsVerification.json` (miss expiry and 4.6 m / 5.3 m boundary checks) |
| Score counter and **WAVE CLEARED** | Top-right score; WAVE CLEARED after each level and at the end |

Note: the device recordings captured no audio. The demo's sound is the game's own effects, synced to the on-screen events in post-production, over an ambient bed (stated on screen).

## Still to do before sending

1. **Shader Graph screenshot.** Open the project in Unity and choose **Reality Defender ▸ Open Shader Graph for Evidence**, or double-click `Assets/RealityDefender/Art/Shaders/SG_EnergyOrb.shadergraph`. Maximise the graph window, frame the time → UV scroll → texture → colour → Fresnel → output nodes, and save the screenshot here as `ShaderGraph_Screenshot.png`.
2. **Google Drive.** Upload this folder and set sharing to *Anyone with the link → Viewer*.
