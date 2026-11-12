# 02 — Assignment Requirements Traceability

This document separates the requirements explicitly stated in the supplied assignment from recommended implementation details.

## 1. Functional requirements

| ID | Assignment requirement | Implementation location | Verification |
|---|---|---|---|
| AR-01 | Use Unity with AR Foundation | Project setup and packages | Project opens with AR session |
| AR-02 | Detect horizontal planes such as floor/table | `ARPlaneManager` | Plane visual appears on a real surface |
| AR-03 | User taps screen to spawn Game Base on a detected plane | `ARPlacementController` | Tap valid plane; base appears at hit pose |
| AR-04 | Base aligns with surface normal | Placement rotation calculation | Inspect from side; base sits level on plane |
| AR-05 | Base becomes anchored and immovable | `ARAnchor` / anchor workflow | Move phone around; base remains in place |
| VFX-01 | Do not use standard Unity materials for targets | Custom Shader Graph material | Target prefab uses custom graph material |
| VFX-02 | Target looks volatile/energy-filled | Scrolling noise, fresnel rim, and/or vertex wobble | Capture Shader Graph and in-game target |
| VFX-03 | Particle explosion on target destruction is a bonus | Hit VFX prefab | Destroy target; particle effect plays |
| GAME-01 | Main camera acts as cannon | `WeaponController` | Shot starts near camera |
| GAME-02 | UI button labelled FIRE | Canvas button | Button is visible and fires |
| GAME-03 | Projectile moves forward from camera position | `WeaponController` + Rigidbody | Shot direction follows camera forward |
| GAME-04 | Game Base spawns three floating targets | `TargetSpawner` | Exactly three targets appear |
| GAME-05 | Projectile uses Rigidbody and physically collides with targets | Projectile prefab + colliders | Collision removes target |
| GAME-06 | Hit removes target and awards +10 | `Target` + `GameManager` | Score increases by exactly 10 per hit |
| GAME-07 | Projectile is destroyed after travelling more than 5 metres | `Projectile` | Fire into open space; projectile cleans itself up |
| UI-01 | Show score in world space or screen space | `ScoreUI` | Score visible during play |
| UI-02 | Show WAVE CLEARED when all three targets are cleared | `GameManager` + UI | Destroy all targets; message appears |
| DEL-01 | Screen recording demonstrates plane detection, placement, and hitting at least three targets | Evidence capture | Recording reviewed |
| DEL-02 | Screenshot of Shader Graph setup | Shader Graph editor | Screenshot included |
| DEL-03 | Submit files through Google Drive with Anyone-with-link View permission | Submission folder | Link opens in a signed-out/private browser |

## 2. Acceptance checklist

### Arena and AR
- [ ] AR session starts without a fatal error.
- [ ] Horizontal planes are detected.
- [ ] Placement only occurs when a valid plane is hit.
- [ ] A tap on UI does not accidentally place the base.
- [ ] Base local up axis aligns with the hit surface normal.
- [ ] Base is anchored and does not follow later screen taps.
- [ ] Plane scanning/visualisation is hidden or reduced after placement.

### Targets and visuals
- [ ] Exactly three targets spawn after placement.
- [ ] Each target uses the custom Shader Graph material.
- [ ] Targets are easy to distinguish from the camera background.
- [ ] Optional hit particle effect plays at the target location.

### Shooting and scoring
- [ ] FIRE button is functional.
- [ ] Projectile spawns in front of the AR camera.
- [ ] Projectile has a Rigidbody and a non-trigger collider.
- [ ] Target collision is detected reliably.
- [ ] Each target can score only once.
- [ ] Each valid hit adds exactly 10 points.
- [ ] Projectile is cleaned up after exceeding 5 metres.
- [ ] WAVE CLEARED appears after all three targets are removed.

### Deliverables
- [ ] Screen recording saved.
- [ ] Shader Graph screenshot saved.
- [ ] Unity project and required supporting files placed in submission folder.
- [ ] Google Drive permission set to Anyone with the link — Viewer.
- [ ] Link tested in a private/incognito browser window.

## 3. Recommended quality gates

These are production-quality checks beyond the literal assignment:
- No console errors during the complete play loop.
- No duplicate score awards from a single collision.
- No projectiles remain alive indefinitely.
- No UI controls are hidden by device safe areas.
- App can be restarted and a new session can be played.
- A first-time tester can complete the loop without verbal help.
