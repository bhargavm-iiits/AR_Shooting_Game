# 03 — Unity Project Setup

## 1. Choose a compatible Unity baseline

Use a Unity LTS version that is supported by the AR Foundation, ARCore XR Plugin, and ARKit XR Plugin package versions selected for the project.

Do not mix package versions blindly. In Unity Package Manager, install mutually compatible versions and keep them fixed for the duration of the 48-hour build.

## 2. Create the project

Recommended starting point:
- Unity 3D project.
- Built-in Render Pipeline or URP. URP is a good choice if you want Shader Graph and modern mobile rendering features.
- Portrait or landscape orientation chosen deliberately and kept consistent.
- Version control enabled before major implementation work.

Suggested project name: `RealityDefender`.

## 3. Install packages

In **Window → Package Manager**, add:
- AR Foundation.
- ARCore XR Plugin for Android builds.
- ARKit XR Plugin for iOS builds, if iOS is in scope.
- Shader Graph if it is not already included with the selected render pipeline.
- TextMeshPro essentials when prompted.

Use the package versions marked compatible with the selected Unity editor.

## 4. XR Plug-in Management

Open **Edit → Project Settings → XR Plug-in Management**.

### Android
- Enable the ARCore provider.
- Set the appropriate minimum Android API level required by the ARCore package version.
- Ensure the device supports ARCore.
- Add camera permission through the Unity/ARCore configuration.

### iOS
- Enable the ARKit provider.
- Set a suitable iOS deployment target for the selected AR Foundation/ARKit versions.
- Add a clear camera usage description in Player Settings.
- Test on a physical ARKit-capable device.

The exact platform settings can change between Unity and package versions; follow the package's current compatibility guidance.

## 5. Scene hierarchy

Create a scene named `MainAR`.

Suggested hierarchy:

```text
MainAR
├── AR Session
├── XR Origin (Mobile AR)
│   ├── Camera Offset
│   │   └── Main Camera
│   ├── AR Plane Manager
│   ├── AR Raycast Manager
│   └── AR Anchor Manager
├── Game Systems
│   ├── GameManager
│   ├── TargetSpawner
│   └── ARPlacementController
├── World Content (initially empty)
└── UI Canvas
    ├── ScanInstruction
    ├── ScoreLabel
    ├── WaveClearedLabel
    └── FIREButton
```

The precise XR Origin menu name varies by AR Foundation release. Use the AR Foundation scene setup wizard or package samples if available.

## 6. AR components

Configure the XR Origin:
- `ARPlaneManager`: horizontal plane detection.
- `ARRaycastManager`: screen point to detected plane raycast.
- `ARAnchorManager`: anchor creation/management if using the manager-based anchor workflow.
- AR camera background: enabled and working.

For plane detection, use horizontal-up and horizontal-down classifications if available and appropriate. The assignment specifically needs horizontal surfaces.

## 7. Input and EventSystem

- Add an EventSystem to the scene.
- Add a Canvas with a Graphic Raycaster.
- Ensure the FIRE button receives UI input.
- Placement input must ignore touches that begin over UI.
- If using legacy `Input.GetTouch`, set **Active Input Handling** to a compatible mode in Player Settings. If using the Input System package, implement the equivalent touch path consistently.
- Avoid mixing legacy and new input APIs without a reason.

## 8. Physics setup

Create a `Projectile` layer if useful, but keep collision settings simple initially.

Projectile prefab:
- Sphere or capsule mesh.
- Sphere/Capsule Collider with `Is Trigger` disabled.
- Rigidbody with gravity disabled for a straight arcade shot.
- Collision Detection set to Continuous Dynamic where appropriate.
- A `Projectile` script.
- A bright material/trail for readability.

Target prefab:
- Mesh Renderer using the custom Shader Graph material.
- Sphere or other simple Collider with `Is Trigger` disabled.
- Optional Rigidbody set to Is Kinematic if required by the chosen collision setup; a dynamic projectile Rigidbody is sufficient for ordinary collision callbacks.
- `Target` script.

## 9. Suggested folder structure

```text
Assets/
├── Art/
│   ├── Materials/
│   ├── Models/
│   ├── Shaders/
│   └── Textures/
├── Audio/
├── Prefabs/
│   ├── Arena/
│   ├── Targets/
│   ├── Projectiles/
│   ├── VFX/
│   └── UI/
├── Scenes/
│   └── MainAR.unity
├── Scripts/
│   ├── AR/
│   ├── Gameplay/
│   ├── UI/
│   └── Utilities/
└── Settings/
```

## 10. First device smoke test

Before implementing gameplay:
1. Build a minimal AR scene.
2. Install it on a physical device.
3. Confirm camera permission is requested.
4. Confirm camera passthrough works.
5. Confirm horizontal planes appear.
6. Move around the room and confirm tracking remains stable.

Do this early. An AR setup issue discovered at hour 40 is a serious delivery risk.
