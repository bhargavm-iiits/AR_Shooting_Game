# 05 — Implementation Guide

Follow this order so each stage can be tested before the next one is added.

## Phase 1 — Prove AR works

1. Create the `MainAR` scene.
2. Add AR Session and XR Origin.
3. Configure horizontal plane detection.
4. Add an AR plane visualiser or simple debug visual.
5. Build to a physical phone.
6. Confirm camera passthrough and plane detection.

**Exit condition:** a real horizontal surface is detected reliably.

## Phase 2 — Tap-to-place Game Base

1. Create a Game Base prefab with its pivot at the surface contact point.
2. Add `ARRaycastManager` and `ARPlaneManager` references to the placement controller.
3. On a screen tap, raycast using `TrackableType.PlaneWithinPolygon`.
4. Ignore touches over UI.
5. Use the returned hit position.
6. Align the prefab's local up axis to the hit pose's up axis.
7. Add or create an AR anchor using the API supported by your installed AR Foundation version.
8. Lock placement after the first successful tap.
9. Hide or reduce plane visuals once placement is complete.

**Exit condition:** the base sits on the plane and remains stable while the player moves around it.

### Alignment note

A safe orientation approach is:
- `surfaceUp = hit.pose.up`
- Project camera forward onto the plane to obtain a stable heading.
- If the projected vector is too small, use a fallback forward vector.
- Construct rotation with `Quaternion.LookRotation(projectedForward, surfaceUp)`.

This keeps the base's up axis aligned to the detected surface normal while giving it a predictable facing direction.

## Phase 3 — Spawn the three targets

1. Create the target prefab.
2. Add a collider and `Target` script.
3. Create three spawn points as children of the Game Base, or calculate three base-local positions.
4. Instantiate exactly three targets after successful placement.
5. Call `GameManager.BeginWave(3)`.

**Exit condition:** all three targets appear in the intended positions after placement.

## Phase 4 — Build the weapon and projectile

1. Create a projectile prefab with a non-trigger collider and Rigidbody.
2. Disable gravity for a straight arcade projectile unless a ballistic arc is desired.
3. Create a FIRE UI button.
4. Connect the button's OnClick event to `WeaponController.Fire`.
5. Spawn the projectile slightly in front of the camera.
6. Set its rotation to the camera rotation.
7. Set Rigidbody velocity to camera forward multiplied by projectile speed.
8. Record the projectile's spawn position.
9. Destroy it when distance from the spawn position exceeds 5 metres.

**Exit condition:** every FIRE press produces one visible projectile moving in the camera's forward direction.

## Phase 5 — Collision and scoring

1. Ensure the projectile collider and target collider are not triggers.
2. Ensure the projectile has a Rigidbody.
3. Add the `Projectile` component to the projectile prefab.
4. In the target collision handler, detect a projectile component.
5. Guard target destruction with a boolean so it can only happen once.
6. Spawn hit VFX at the target position.
7. Add 10 points.
8. Decrement remaining targets.
9. Destroy the target and projectile.

**Exit condition:** one target hit produces one VFX event and exactly +10 score.

## Phase 6 — Wave completion and UI

1. Add a score label to the Canvas.
2. Add a hidden `WAVE CLEARED` label.
3. Update score after every valid target hit.
4. When remaining targets reaches zero, show the wave-cleared label.
5. Ensure the FIRE button does not block the placement raycast before the base is placed.

**Exit condition:** after the third target is destroyed, the completion message appears.

## Phase 7 — Visual polish

1. Create a custom Shader Graph target material.
2. Add emissive energy colour and fresnel rim.
3. Add scrolling noise or animated UVs.
4. Add a small projectile trail.
5. Add a particle burst on target destruction.
6. Add restrained UI and audio feedback.
7. Test against both bright and dark real-world backgrounds.

**Exit condition:** the targets are visually distinct and each hit feels intentional.

## Phase 8 — Full end-to-end test

Perform this exact sequence on a physical device:
1. Launch app.
2. Scan a floor or table.
3. Place the base.
4. Confirm three targets spawn.
5. Aim and hit target one.
6. Confirm score is 10.
7. Hit target two; confirm score is 20.
8. Hit target three; confirm score is 30 and WAVE CLEARED appears.
9. Fire a deliberate miss and confirm the projectile is cleaned up after 5 metres.
10. Restart the app and repeat.

## Integration checklist

- [ ] All script references assigned in Inspector.
- [ ] Button OnClick event wired.
- [ ] Projectile prefab has Rigidbody and collider.
- [ ] Target prefab has collider and Target script.
- [ ] Target material uses Shader Graph.
- [ ] Base placement is one-time.
- [ ] Anchor behaviour verified on a physical phone.
- [ ] Score and wave-complete UI tested.
- [ ] No duplicate collision scoring.
