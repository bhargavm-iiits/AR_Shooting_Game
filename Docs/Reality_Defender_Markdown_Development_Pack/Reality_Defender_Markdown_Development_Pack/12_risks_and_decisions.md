# 12 — Risks and Technical Decisions

## 1. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| AR package/editor incompatibility | Project may not build or run | Select compatible versions first; do not upgrade mid-project |
| Device does not support ARCore/ARKit | No real-device AR test | Confirm device support before development |
| Plane detection is unreliable | Placement cannot be demonstrated | Test early on a textured, well-lit floor/table |
| Anchor workflow differs by package version | Base may drift or API may fail to compile | Verify the installed AR Foundation anchor API and test on device |
| Touch over FIRE also places base | Incorrect interaction | Ignore UI touches and lock placement after success |
| Projectile passes through target | Hit loop fails | Rigidbody, non-trigger colliders, continuous collision detection |
| Multiple score awards | Incorrect score | One-time target destruction guard |
| Shader is too expensive or unreadable | Poor mobile performance/visual clarity | Keep graph simple; test on device and tune emission |
| VFX objects accumulate | Performance degradation | Set particle lifetimes and cleanup |
| Final build fails near deadline | No submission | Build to device at the beginning and keep a time buffer |
| Evidence is incomplete | Deliverable rejected | Capture recording and graph screenshot before final hours |

## 2. Technical decisions to record

Keep a short decision log in this format:

```text
Decision:
Date:
Context:
Options considered:
Chosen option:
Reason:
Trade-offs:
Follow-up test:
```

Suggested decisions:
- Unity editor and package versions.
- URP or Built-in Render Pipeline.
- Android, iOS, or both.
- Legacy Input or Input System.
- Anchor creation API for the selected AR Foundation version.
- Target spawn layout and scale.
- Shader Graph Lit or Unlit.
- Projectile speed and collision detection mode.

## 3. Non-negotiable implementation rules

- Do not allow placement on an arbitrary screen point; require a plane raycast hit.
- Do not allow repeated placement unless a restart/reposition feature is deliberately designed.
- Do not award score from a projectile trigger or collision more than once.
- Do not let a missed projectile live forever.
- Do not use a default standard material for the target; use the custom Shader Graph material.
- Do not rely only on Editor testing for AR tracking.
- Do not leave final evidence capture until the last few minutes.

## 4. Scope control

If time becomes short, preserve this order:

1. AR session and plane detection.
2. Tap-to-place and anchored base.
3. Three targets.
4. FIRE and Rigidbody projectile.
5. Collision, score, 5 m cleanup.
6. WAVE CLEARED.
7. Shader Graph.
8. Hit VFX and other polish.
9. Evidence and Drive handoff.

The game is not complete if the visual polish is excellent but the required gameplay loop cannot be demonstrated.
