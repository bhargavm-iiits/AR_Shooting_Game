# 10 — 48-Hour Production Plan

The assignment gives a 48-hour time limit. The schedule below prioritises a complete, testable gameplay loop before optional polish.

## Guiding rule

**Build the smallest complete playable version first.** Do not spend half the time polishing the shader before AR placement and collisions work.

## Day 1 — Core AR and gameplay

| Time block | Work | Deliverable |
|---|---|---|
| Hours 0–2 | Create project, install compatible AR packages, configure XR provider | AR project opens |
| Hours 2–5 | Build and test AR camera + horizontal plane detection on device | Working plane detection |
| Hours 5–8 | Implement tap-to-place and surface alignment | Base placement works |
| Hours 8–10 | Add anchor workflow and test stability | Base remains anchored |
| Hours 10–12 | Create target and projectile prefabs | Prefabs ready |
| Hours 12–15 | Implement three-target spawning and FIRE button | Targets spawn; shots fire |
| Hours 15–18 | Implement Rigidbody collision, score, 5 m cleanup, wave completion | Complete basic game loop |

### Day 1 checkpoint

By the end of Day 1, the player should be able to place the base, shoot targets, score hits, and clear the wave even if the visuals are still basic.

## Day 2 — Visual polish, QA, and submission

| Time block | Work | Deliverable |
|---|---|---|
| Hours 18–22 | Build custom target Shader Graph | Energy target material |
| Hours 22–24 | Add hit particle burst and projectile trail | Basic VFX |
| Hours 24–27 | Refine UI, score feedback, and instructions | Readable UX |
| Hours 27–31 | Device QA: tracking, alignment, physics, edge cases | Tested build |
| Hours 31–34 | Fix blockers and performance issues | Stable build |
| Hours 34–37 | Capture screen recording and Shader Graph screenshot | Evidence files |
| Hours 37–40 | Clean project, remove unused assets, check console | Submission-ready project |
| Hours 40–43 | Build final Android/iOS target as required | Final build |
| Hours 43–46 | Upload project and evidence to Google Drive | Shared folder |
| Hours 46–48 | Verify Anyone-with-link Viewer access and test the link | Final submission link |

## Priority tiers

### P0 — Must work
- AR session and plane detection.
- Tap-to-place base.
- Surface-normal alignment.
- Anchoring.
- Three targets.
- FIRE button.
- Rigidbody projectile collision.
- +10 score per hit.
- Projectile cleanup beyond 5 m.
- WAVE CLEARED.
- Required recording and Shader Graph screenshot.

### P1 — Strong polish
- Custom energy Shader Graph.
- Hit particle burst.
- Projectile trail.
- Clear scanning instructions.
- Clean score UI.
- Responsive button feedback.

### P2 — Only if time remains
- Sound effects.
- Restart button.
- Extra animations.
- Additional target patterns.
- Advanced visual effects.

## Time-saving decisions

- Use primitive meshes for the base and targets initially.
- Use three fixed target spawn points.
- Use one wave only.
- Use a Screen Space UI for score and FIRE.
- Use a simple particle burst rather than a complex VFX Graph.
- Capture evidence only after the end-to-end loop is stable.
- Keep package versions fixed once the device build works.

## Final 2-hour buffer

Reserve the final two hours for:
- Build failure recovery.
- Permission/share-link checks.
- Missing evidence.
- A last full play-through.

Do not schedule new features in the final two hours.
