# The Reality Defender - verification record

Updated: 11 November 2026. This record distinguishes configuration and local checks from physical Android observations. Rows marked pending are not passed requirements.

Latest clarified user requirement: the **AR floor placement / anchor point must be 1-5 m from the live phone camera**, inclusive. Both the placement marker and actual tap use this world-distance gate on a real tracked horizontal plane. The accepted base stays at local identity under the world anchor as the camera moves. The projectile range remains 5 m. Practice keeps its nearby virtual-floor placement.

## Build inputs

| Item | Observed input or intended configuration |
| --- | --- |
| Unity | `6000.5.5f1` (`d16e074b49fd`), preserved from the project |
| Renderer | URP `17.5.0`, Mobile Forward baseline |
| Input | Input System `1.19.0` |
| AR packages | AR Foundation `6.5.1` and Google ARCore XR Plugin `6.5.1`, resolved in the package lock |
| XR dependencies | XR Core Utilities `2.6.0`, XR Plug-in Management `4.5.4`, resolved by Package Manager |
| Android tools | Editor-bundled Android SDK, NDK and OpenJDK installed; SDK platforms 34 and 36; Build Tools 36.0.0 |
| Player | `com.assignment.realitydefender`, version 1.0.0, API 26 minimum and API 36 target |
| ABI and backend | IL2CPP, ARM64 only; no 32-bit device coverage claimed |
| Graphics | OpenGL ES 3 baseline; no ES 3.1/3.2 requirement; 30 fps target |
| Orientation | Auto Rotation, Portrait and both Landscape orientations; Android user rotation preference respected |
| Packaging | One non-Development APK, no App Bundle, no ABI splitting, no expansion file |
| Signing | Android Debug signer for local sideloading; APK Signature Scheme v2 verified by bundled apksigner |
| AR capability | AR Optional and Depth Optional; manual XR startup only from the AR flow |

`AndroidBuild.Configure()` persists Android XR settings and the ARCore loader through Unity's asset APIs. `AndroidBuild.ValidateProject()` inspects current Android XR Project Validation issues and writes `Submission/XRValidation.txt`; any blocking rule prevents the APK build. Final aapt2 inspection confirms camera, camera.any, autofocus, AR camera and depth features optional, with `com.google.ar.core=optional`. See `ApkManifest.txt` and `ApkBadging.txt`.

## Local acceptance

| Requirement | Status | Evidence or remaining check |
| --- | --- | --- |
| Read supplied brief, master prompt and visual targets | Reviewed | `Docs/` source files |
| Package resolution and integrated compile | Passed | Unity `6000.5.5f1`; final Play Mode verification exited 0. |
| Grounded combined pedestal/platform | Passed Editor assembly and visual check | Unit root, bottom at Y=0, 0.65 m footprint and 0.13 m height; `SceneAssembly.json` and real Game View screenshots. Physical floor/table check pending. |
| Source-derived railgun and orb | Passed Editor visual check | `Tools/AssetPrep/derivative-audit.json`; retained supplied mesh derivatives, PBR bindings, visible railgun crop and source-derived targets. |
| Real editable target Shader Graph | Compiled and rendered; editor capture pending | All spawned orbs visibly use scrolling veins and cyan Fresnel. Native 29-node Unlit graph and shared `M_EnergyOrb.mat`. Desktop capture helper failed with native pipe unavailable; no substitute graph screenshot was fabricated. |
| Practice Mode tap, drag, three physics hits | Passed Editor Play Mode | Input System pointer press/release places the actual scene; drag rotates the fixed-position camera. Real `OnCollisionEnter` events produce 10, 20, 30 and WAVE CLEARED. |
| Duplicate contacts | Passed real physics | Two same-frame shots at one registered target award exactly ten once. Round IDs and ownership also guard stale callbacks in code; device lifecycle coverage pending. |
| Miss and 5 m collision boundary | Passed real physics | Miss expires without score in about 0.5 s at 11 m/s. Physical contact at 4.6 m scores; a target at 5.3 m remains alive and scores zero. |
| UI input isolation, one base/wave | Passed Editor input and lifecycle tests | Actual pointer click reaches FIRE through EventSystem without rotating or replacing the base. Repeated surface taps retain one base. Replay and menu retire the previous round. |
| Landscape and portrait | Passed rendered inspection | Actual 1280x720 and 720x1280 screenshots; readable score, reticle, status, checks and FIRE. Safe-area anchors implemented; physical notch/rotation test pending. |
| Floor attachment and initial stability | Implemented; device retest pending | Plane-attached anchor, 0.75 s stable floor, full 0.65 m footprint within the observed polygon, 0.5 s continuous anchor tracking before showing the arena. |
| Tracking/pause gating | Implemented; device retest pending | Retire shots and hide the arena while suspended or tracking limited; preserve the wave and recover the same anchor. |
| Android XR Project Validation | No blocking errors | `XRValidation.txt`; one generic XR focus/background warning remains. The app suspends firing and hides placed content on focus/tracking loss. |
| Non-Development APK | Passed | Latest plane-attachment build succeeded with zero errors. `BuildReport.json` and `../Builds/Android/Unity-build-final.log`. |
| APK signature, manifest, package and ABI | Passed | `ApkInspection.json`, `ApkSignature.txt`, `ApkManifest.txt`, `ApkBadging.txt`; ARM64 only, API 26-36, GLES 3.0. |

## Device coverage

Current delivery APK: **43,820,496 bytes**. SHA-256: `F675A6EAAE02BB5D50E16D3F0B92E71F8999FD26ED9B37DB78C50DCC0939CBA3`. It is a local debug-signed, non-Development build. Unity reported 348 warnings, including warnings from existing Sentis shader resources and obsolete APIs; no build errors. `ApkInspection.json` is regenerated from the actual artifact with `python Tools/verify_apk.py`.

Initial ADB checks returned no device. The user then connected and authorized **CPH2487 / OP5961L1**, running **Android 16 / API 36**, with ARM64 support and Google Play Services for AR **1.57.262640293**. Installation of the signed APK succeeded. The game activity launched and `Device-Initial.png` / `Device-Final-Start.png` confirm the live AR camera and responsive portrait HUD. USB disconnected temporarily and subsequently recovered. Final 1-5 m floor placement and world-anchor stability are tracked separately below.

The latest integrated Editor run after the plane-attachment changes passed all **33 checks**, with **zero runtime Error/Exception/Assert entries**. Evidence: `EditorPhysicsVerification.json`, `Practice-Landscape.png`, `Practice-Portrait.png`, `Practice-Cleared.png`, and `../Builds/Android/Unity-anchor-verification.log`. These are actual Unity Play Mode renders and physics results, not physical AR observations.

Phone health: the complete active capture in `PhoneHealth.json` shows the game's camera ACTIVE, cameraserver PID unchanged, and accelerometer and gyroscope event timestamps advancing with motion samples. No matching crash or managed exception was found in that available game-process log. ARCore emitted camera/IMU synchronization warnings around -36.7 to -48.3 ms; these do not establish sensor failure or prove the sole cause of drift. `PhoneHealth-Interrupted.json` records a later PC-side ADB interruption when Unity shut down, separately from the successful capture. No phone calibration, factory reset, service-data clearing, firmware change, or security-setting change was performed.

The user reported floating/drifting points on the earlier APK. The revised placement attaches the anchor to the actual tracked floor plane instead of creating an independent free anchor at the hit. It also rejects unsteady/undersized plane candidates and hides untracked content. The camera origin remains at unit scale with zero artificial height offset. Physical anchor stability must be observed again on the updated APK before it is marked passed.

| Physical check | Status | Why it remains pending |
| --- | --- | --- |
| Initial APK installation and launch | Passed | ADB install returned Success; activity launched; actual camera/HUD captured in `Device-Initial.png`. |
| Final updated APK installation and launch | Pending final range update | Includes the clarified 1-5 m placement gate, zero handheld camera height offset and AR services retry fix. |
| Certified phone: floor and tabletop plane detection | Pending | Requires camera, real surfaces and ARCore-certified hardware |
| Deliberate plane tap, alignment and stable anchor while moving | Pending | Requires physical tracking |
| Real-camera three-hit wave and score 30 | Pending | Requires physical Android gameplay |
| Camera denial, revoked permission, service install/cancel | Pending | Requires Android permission and services flows |
| Tracking loss, pause/resume, relocalization and lost-anchor restart | Pending | Requires physical tracking and lifecycle checks |
| Rotation mid-wave with preserved anchor and score | Pending | Requires actual orientation and camera-background testing |
| Unsupported-device Practice Mode without camera permission | Pending | Requires compatible Android device without AR support or permission denied |
| Low-end steady play and three-hit bursts | Pending | No physical measurements available |
| Warm two-minute final APK performance | Pending | No FPS, p95 frame time, memory, GC or thermal values measured |
| Actual AR recording | Pending | `Submission/RealityDefender-AR-demo.mp4` cannot be captured without a physical run |

The performance acceptance target is approximately 30 fps and p95 frame time at or below 40 ms on a tested modest handset. These are targets, not measured results. AR tracking support requires a certified model and suitable sensors, camera conditions, services and permissions; camera resolution alone is not a compatibility test.

## Reproduce the Android build

Close the GUI editor for this project before starting another Unity process. From the project root:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe'
$unityArgs = @('-batchmode', '-quit', '-buildTarget', 'Android', '-projectPath', '"D:\Unity Projects\Assignment"', '-executeMethod', 'RealityDefender.Editor.AndroidBuild.BuildApk', '-logFile', '"D:\Unity Projects\Assignment\Builds\Android\Unity-build.log"')
$run = Start-Process -FilePath $unity -ArgumentList $unityArgs -WindowStyle Hidden -Wait -PassThru
if ($run.ExitCode -ne 0) { throw 'Build failed; inspect Builds/Android/Unity-build.log and Submission/BuildReport.json' }
```

The builder checks `BuildReport.summary.result`, writes `Submission/BuildReport.json`, and exits nonzero on failure. A successful report is followed by checking that the intended APK exists and is nonempty. Signature and manifest inspection are separate gates.

## Capture the remaining assignment evidence

1. Attach and authorize a designated ARCore-certified phone. Record model, Android version, ABI list, AR service version and graphics API. Install with `adb -s <serial> install -r Builds/Android/RealityDefender.apk`. Record installation and first launch separately. Inspect logcat for startup, camera-background, plugin and shader errors.
2. On a well-lit textured floor, test an outside-plane tap and a UI tap, then tap a valid horizontal plane. Confirm one base sits flush, move around it, fire a miss, hit all three targets, and observe 10, 20, 30 and WAVE CLEARED. Start a fresh session and repeat on a tabletop.
3. Test camera denial, temporary tracking loss, background/resume and rotation. Check Practice Mode without granting camera access and on an Android device without AR support.
4. Record a continuous actual AR run starting before scanning: `adb -s <serial> shell screenrecord --time-limit 180 /sdcard/RealityDefender-AR-demo.mp4`, stop after the clear, then `adb -s <serial> pull /sdcard/RealityDefender-AR-demo.mp4 Submission/RealityDefender-AR-demo.mp4`. Replay the file to verify the scan, placement, anchor movement, three hits and final score are visible. Practice or Editor footage must be labeled separately.
5. Open `Assets/RealityDefender/Art/Shaders/SG_EnergyOrb.shadergraph` in Unity. Frame the scroll, texture, color remap, Fresnel and output nodes with readable wires and preview. Save a screenshot to `Submission/RealityDefender-ShaderGraph.png` and inspect it at full size.
6. For performance, profile a Development build with Deep Profiling off, then run the final APK warm for at least two minutes without profiler or recorder overhead. Record device, OS, ABI, service version, conditions, mean FPS, p95 frame time, memory, GC, tracking and thermal stalls.

## Submission and sharing

Keep the APK, actual AR recording, actual Shader Graph screenshot, verification record and asset credits together. Review the supplied models' unresolved redistribution terms in `Submission/AssetCredits.md` before external distribution. No Drive upload, public access change or outbound submission has been performed. After the user authorizes those separate actions, upload the reviewed files, set Anyone with the link to Viewer, and verify the accessible link.
