# 01 — Game Design: The Reality Defender

## 1. Product summary

**The Reality Defender** is a short, smartphone-based augmented-reality target-practice game. The player scans a real horizontal surface, places a small virtual arena, and destroys three floating energy targets with camera-forward shots.

The experience should feel stable, readable, responsive, and satisfying rather than like a technical AR demo.

## 2. Design pillars

1. **Stable AR:** The arena should sit convincingly on a detected real-world surface and stay anchored.
2. **Immediate feedback:** Every placement, shot, hit, miss, and wave completion should have a clear response.
3. **Simple interaction:** The player should understand scanning, placing, and firing without a tutorial wall.
4. **Readable targets:** Targets must stand out against varied real-world backgrounds.
5. **Reliable performance:** The game should run on an ARCore/ARKit-compatible phone without requiring a headset.

## 3. Core player journey

1. **Start:** Show a concise instruction: “Move your phone slowly to find a floor or table.”
2. **Scan:** AR Foundation detects horizontal planes. A subtle plane visual can communicate where placement is possible.
3. **Place:** The player taps a detected plane. The Game Base is created at the hit pose, aligned to the surface normal, and anchored.
4. **Wave begins:** Three energy targets appear at fixed positions above the base. The placement instruction disappears.
5. **Shoot:** The player presses the on-screen **FIRE** button. A projectile travels from the camera in its forward direction.
6. **Hit:** A projectile physically collides with a target. The target is removed, a hit effect plays, and score increases by 10.
7. **Miss:** A projectile is removed after travelling more than 5 metres from its spawn point.
8. **Complete:** When all three targets are destroyed, display **WAVE CLEARED**.

## 4. Game rules

| Rule | Required behaviour |
|---|---|
| Placement | One tap on a detected horizontal plane places the base |
| Surface alignment | Base local up axis follows the detected plane normal |
| Anchoring | Base remains fixed after placement |
| Target count | Exactly three targets for the first wave |
| Fire input | UI button labelled `FIRE` |
| Projectile origin | Main AR camera, with a small forward offset to avoid spawning inside the camera |
| Projectile direction | Camera forward |
| Hit | Target disappears and score increases by 10 |
| Miss cleanup | Projectile is destroyed after travelling more than 5 metres |
| Completion | Show `WAVE CLEARED` after all three targets are cleared |

## 5. Recommended presentation

These are polish recommendations, not extra assignment requirements:

- Use a compact sci-fi arena base with a clear placement footprint.
- Give targets a bright emissive core, animated noise, and a fresnel rim.
- Use a brief muzzle flash or projectile trail.
- Add a short impact burst and a restrained hit sound.
- Animate score changes with a small scale pulse.
- Keep the FIRE button large enough for one-handed use.
- Avoid excessive screen shake; real-world camera movement already adds motion.

## 6. Difficulty and pacing

The assignment only requires one wave of three targets. Keep the first build to that scope.

Recommended first-wave layout:
- One target near the centre.
- Two targets offset left and right.
- Vary target height slightly while keeping all three clearly above the base.
- Keep target spacing wide enough that one projectile does not accidentally overlap multiple targets.

Do not add extra waves, lives, timers, upgrades, or monetisation until the required loop is stable.

## 7. Success criteria

A successful play session:
- Makes it obvious when the environment is being scanned.
- Makes the placement point and surface alignment understandable.
- Does not allow the base to drift after placement.
- Produces predictable camera-forward firing.
- Gives clear hit confirmation and score feedback.
- Ends cleanly with a visible wave-complete state.

## 8. Out of scope for the assignment baseline

- Multiplayer or cloud anchors.
- Persistent saves or leaderboards.
- Procedural levels.
- User accounts.
- In-app purchases or ads.
- Headset support.
- Multiple weapon types.
