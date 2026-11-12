# 09 — Testing and QA

## 1. Device-first testing

AR behaviour must be verified on a physical ARCore/ARKit-capable device. Editor simulation can help with iteration, but it does not replace real-world tracking tests.

Record:
- Device model and OS version.
- Unity editor version.
- AR Foundation version.
- ARCore/ARKit plugin version.
- Build configuration.
- Known limitations.

## 2. Test matrix

| Test ID | Scenario | Expected result |
|---|---|---|
| T-01 | Launch with camera permission granted | Camera passthrough and AR session start |
| T-02 | Launch with permission denied | App handles denial clearly; no crash |
| T-03 | Scan a flat table | Horizontal plane is detected |
| T-04 | Scan a floor | Horizontal plane is detected |
| T-05 | Tap a detected plane | One base is placed |
| T-06 | Tap empty/non-detected area | No base is placed |
| T-07 | Tap FIRE area before placement | No accidental base placement |
| T-08 | Move around placed base | Base remains anchored |
| T-09 | Inspect base from side | Base aligns to surface normal |
| T-10 | Place base successfully | Exactly three targets spawn |
| T-11 | Press FIRE | Projectile starts in front of camera and travels forward |
| T-12 | Hit a target | Target disappears and score increases by 10 |
| T-13 | Hit same target repeatedly/overlap collisions | Score is awarded once only |
| T-14 | Fire into open space | Projectile disappears after travelling more than 5 m |
| T-15 | Destroy all three targets | Score is 30 and WAVE CLEARED appears |
| T-16 | Complete one play session | No unhandled exception or persistent error |
| T-17 | Test in bright room | Target remains readable |
| T-18 | Test in dim room | Target remains readable without excessive bloom |

## 3. Physics debugging

If projectiles pass through targets:
- Confirm both objects have colliders.
- Confirm `Is Trigger` is disabled if using `OnCollisionEnter`.
- Confirm the projectile has a Rigidbody.
- Confirm layer collision matrix allows the pair.
- Try Continuous Dynamic collision detection.
- Reduce projectile speed or increase collider size.
- Confirm target collider is not disabled by an animation or prefab setting.

If targets score twice:
- Keep the one-time destruction guard.
- Destroy the projectile after a valid hit.
- Confirm there is not a second hit handler awarding points.

## 4. AR debugging

If no plane appears:
- Confirm the phone supports the platform AR service.
- Confirm the correct XR provider is enabled.
- Confirm camera permission.
- Confirm AR Session and XR Origin are in the scene.
- Confirm the plane manager is enabled and configured for horizontal planes.
- Move the phone slowly across a textured, well-lit surface.

If the base appears tilted:
- Check the Game Base prefab's local axes.
- Check that the root pivot is aligned with the intended contact plane.
- Use the hit pose's surface normal as the base up direction.
- Check for an unintended rotation on a parent object.

If the base drifts:
- Verify anchor creation and tracking state.
- Do not disable required AR managers before the anchor is established.
- Test the anchor workflow for the installed AR Foundation version.

## 5. Performance checks

Recommended goals:
- Stable frame pacing on the target phone.
- No excessive particle bursts.
- No runaway projectile count.
- No repeated material instantiation.
- No large transparent overlays.
- No avoidable allocations in Update loops.

Use Unity Profiler on device if performance is poor.

## 6. Regression checklist

After changing placement, physics, or shader settings, rerun:
- Place base.
- Spawn three targets.
- Hit one target.
- Verify +10.
- Miss once and verify cleanup.
- Hit remaining targets.
- Verify wave completion.

## 7. Bug report template

```text
Title:
Device / OS:
Unity version:
AR Foundation version:
Build (development/release):
Steps to reproduce:
Expected result:
Actual result:
Frequency:
Logs / screenshot / recording:
Severity:
```

## 8. Release gate

Do not submit until:
- The full required play loop works on a physical device.
- No blocker bug prevents placement or firing.
- Score and completion state are correct.
- Required evidence has been captured.
