# 04 — Technical Architecture

## 1. Architecture goals

- Keep AR placement separate from combat logic.
- Keep score and wave state in one authoritative game-state component.
- Make targets responsible for their own one-time destruction response.
- Make projectiles responsible for their own distance-based cleanup.
- Keep visual effects replaceable through prefab references.
- Avoid unnecessary singletons and hidden dependencies.

## 2. Runtime flow

```text
AR Session starts
      |
      v
Horizontal planes detected
      |
      v
Player taps a detected plane
      |
      v
ARPlacementController raycasts screen point
      |
      v
Instantiate Game Base at hit pose + anchor
      |
      v
TargetSpawner creates 3 targets
      |
      v
GameManager starts wave and score UI
      |
      v
Player presses FIRE
      |
      v
WeaponController creates Rigidbody projectile
      |
      +------------------------------+
      |                              |
      v                              v
Projectile hits target          Projectile travels > 5 m
      |                              |
      v                              v
Target awards +10, VFX           Projectile destroys itself
and destroys itself
      |
      v
GameManager decrements remaining targets
      |
      v
Remaining targets == 0
      |
      v
Show WAVE CLEARED
```

## 3. Main components

| Component | Responsibility | Depends on |
|---|---|---|
| `ARPlacementController` | Detect placement tap, raycast to plane, create base and anchor | ARRaycastManager, ARPlaneManager, base prefab |
| `TargetSpawner` | Spawn exactly three targets in base-local positions | Target prefab, GameManager |
| `GameManager` | Score, remaining-target count, wave-complete event/state | UI or events |
| `WeaponController` | Spawn projectile from camera and launch it forward | Camera, projectile prefab |
| `Projectile` | Store origin and destroy itself after max travel distance | Rigidbody |
| `Target` | Accept one hit, spawn VFX, award score, remove target | GameManager, VFX prefab |
| `ScoreUI` | Display score and wave-complete state | TMP labels or equivalent |

## 4. Suggested prefab contracts

### Game Base prefab
- Root pivot at the bottom contact plane.
- Local Y axis points out of the top surface.
- Optional visual ring/footprint.
- Child transform named `TargetSpawnRoot` if using fixed spawn points.
- Avoid placing the pivot in the centre of the model if that causes it to intersect the real surface.

### Target prefab
- Collider sized to match visible target.
- Custom Shader Graph material.
- `Target` script.
- Optional child VFX anchor.
- Optional subtle idle animation.

### Projectile prefab
- Collider with `Is Trigger` off.
- Rigidbody.
- `Projectile` script.
- Optional trail renderer or particle trail.
- Keep scale and collider size proportionate.

### Hit VFX prefab
- Particle System.
- Short lifetime.
- Play On Awake enabled if instantiated on hit.
- Auto-destroy or explicit cleanup after particle completion.

## 5. Coordinate-space convention

Use the Game Base transform as the parent coordinate frame for targets.

Example local positions:
- Target A: `(-0.35, 0.55, 0.00)`
- Target B: `(0.00, 0.80, 0.12)`
- Target C: `(0.35, 0.60, 0.00)`

These are starting values, not fixed requirements. Adjust based on base scale, camera field of view, and target size.

## 6. State model

Recommended states:

| State | Meaning | Allowed actions |
|---|---|---|
| `Scanning` | No base placed yet | Scan and place |
| `Playing` | Base placed and targets remain | Fire |
| `WaveCleared` | All targets destroyed | Show completion; optional restart |

Keep state transitions explicit. Do not allow a second placement tap to create another base unless a restart/reposition feature is intentionally implemented.

## 7. Events and ownership

Recommended ownership:
- `GameManager` owns score and remaining target count.
- `Target` calls the game manager exactly once when destroyed by a projectile.
- `Projectile` owns max-distance cleanup.
- `ARPlacementController` owns the one-time transition from scanning to placed.
- `ScoreUI` listens to game-state changes or is updated through public methods.

Avoid having the UI independently calculate score or remaining targets.

## 8. Performance principles

- Use simple meshes and colliders.
- Keep particle bursts short and bounded.
- Avoid expensive transparent overdraw on large screen areas.
- Use a small number of materials.
- Disable plane visualisation after placement if it is no longer needed.
- Destroy or pool projectiles; for this small assignment, `Destroy` is acceptable.
- Avoid per-frame allocations in frequently called gameplay methods.
