---
name: unity-target-gameplay
description: Implement or verify camera-aimed Unity target practice with Rigidbody projectiles, one award per target, bounded shot lifetime, a wave controller, and touch HUD. Use for gameplay code shared by tracked AR and virtual practice.
---

# Unity target gameplay

Use the project brief for wave size, points, and shot range. The Reality Defender requires three floating targets per placed base, ten points per hit, projectile destruction after five metres, and the exact completion text `WAVE CLEARED`. Coordinate prefab references and the placement-ready signal with the scene owner.

## Wave and input

1. Start one wave after the base is ready. Instantiate the target prefab once at each of three base-local sockets. Give each orb one non-trigger `SphereCollider` fitted to its visible body. Keep the hit volume aligned with the rendered orb while the shader animates its surface. Fit and aim at all three from a typical floor or tabletop viewing distance.
2. Give one controller authority over the current wave, its living targets, and score. Enter play with score 0. A hit consumes one live target and adds ten points. When the last target is consumed, publish score 30 and the completion state once. Bind the HUD to that state and keep it visible until the player leaves or explicitly restarts.
3. Route the `FIRE` button through a single UI action and a short, tunable cooldown. Gate shots on a live wave, an active camera, and reliable tracking in AR. Consume UI touches before placement or practice aiming. If tracking pauses, suspend new shots and expire in-flight shots while retaining the wave. Reset input listeners and retire existing shots, targets, and effects before a deliberate replay. Use a round ID or equivalent ownership guard if callbacks can arrive after reset.

## Rigidbody shot

1. Sample the active camera pose when the button fires. Spawn the physics body at that world-space position and set its forward launch velocity once. Use a non-kinematic `Rigidbody`, a non-trigger `SphereCollider`, zero gravity and damping, and `Rigidbody.linearVelocity = cameraForward * speed` on Unity 6. Keep the projectile outside the moving camera and anchor hierarchies. Use the first visible segment beyond the near clip for the projectile effect. For Reality Defender, attach the railgun viewmodel to the active camera and drive any recoil from accepted fire input. Keep its render transform and animation independent of shot origin, direction, range, and collision. [Unity Rigidbody linearVelocity](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Rigidbody-linearVelocity.html)
2. In the Physics collision matrix, enable shot-to-target contacts and exclude the camera, arena props, other shots, and scan visuals. Start with sweep-based `Continuous` for linear shots against static target colliders. Use `ContinuousDynamic` with a compatible target collision mode if target bodies move during play. Verify at the configured speed and timestep on the smallest hit volume. Route scoring through `OnCollisionEnter` from the projectile's actual contact. [Unity collision detection modes](https://docs.unity3d.com/6000.5/Documentation/Manual/choose-collision-detection-mode.html) · [Unity OnCollisionEnter](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Collider.OnCollisionEnter.html)
3. Measure straight-line world displacement from the saved launch point using the physics body's position. For a five-metre range, destroy the missed shot once squared displacement exceeds `25f`, and enforce the same range guard in the collision callback because the hit can occur between range checks. Claim the shot as spent before invoking the wave controller. Accept only a target in the active wave that can be removed from the living set. Disable the hit target's collider and renderer promptly, emit one independent short particle burst at the captured contact, and retire the target and projectile. Ignore callbacks against a consumed target.

## HUD and practice

Use a scaled screen-space canvas. Apply `Screen.safeArea` to the score and controls and refresh it when dimensions change. Keep the score in a top corner and `FIRE` comfortably reachable above the system gesture area. Align the reticle to the active camera's projected forward axis, including when the AR camera has an asymmetric projection. Turn off raycast targeting on decorative graphics. In Practice, drag to rotate a fixed-position virtual camera and use the same firing, collision, score, and wave code. [Unity Screen.safeArea](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Screen-safeArea.html)

For Reality Defender, use the uploaded landscape composition and adapt it to portrait with the same HUD state. Reflow the title, score, wave/target strip, and circular `FIRE` around the live play area. Project one amber `+10` from the accepted hit's position when visible. On completion, show `WAVE CLEARED`, `SCORE 30`, and three checks, dim the disabled `FIRE` button, and lower or hide the viewmodel.

## Verify

- A valid placement creates exactly three distinct targets. UI taps and rapid repeat input preserve the single base and wave.
- Aim and hit using the normal `FIRE` button. Three physics hits remove the three targets, produce one burst apiece, and advance `0 → 10 → 20 → 30` before showing `WAVE CLEARED`. Test two projectiles reaching one target in the same physics step.
- Fire past the targets. The projectile disappears just beyond five metres and the miss awards zero points. Place test colliders on either side of the limit to exercise both the range check and collision callback.
- Repeat the same score and completion sequence in Practice. Check wide and narrow layouts, an inset screen, and tracking loss or pause with a shot in flight. On a supported device, rotate mid-wave and verify that the reticle, viewmodel, and controls reflow with the same anchor and score. Confirm the live projectile count returns to zero after repeated misses.
