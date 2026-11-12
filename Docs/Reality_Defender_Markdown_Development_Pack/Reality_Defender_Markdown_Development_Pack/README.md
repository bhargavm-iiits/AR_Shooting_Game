# The Reality Defender — Development Pack

A practical Markdown documentation pack for building the **The Reality Defender** smartphone AR mini-game described in the supplied assignment.

## What this pack contains

| File | Purpose |
|---|---|
| `01_game_design.md` | Game concept, player journey, mechanics, and acceptance criteria |
| `02_requirements_traceability.md` | Assignment requirements mapped to implementation and evidence |
| `03_unity_project_setup.md` | Unity, AR Foundation, scene, input, and build setup |
| `04_technical_architecture.md` | Scene structure, components, data flow, and project conventions |
| `05_implementation_guide.md` | Step-by-step build order and integration notes |
| `06_csharp_reference.md` | Starter C# implementation examples for the core game loop |
| `07_shader_graph_and_vfx.md` | Energy-target Shader Graph and hit-explosion VFX workflow |
| `08_ui_ux_audio.md` | UI layout, interaction feedback, accessibility, and audio direction |
| `09_testing_and_qa.md` | Device test plan, edge cases, performance checks, and bug triage |
| `10_48_hour_plan.md` | A time-boxed production plan for the assignment |
| `11_build_submission.md` | Android/iOS build checklist and required submission evidence |
| `12_risks_and_decisions.md` | Technical risks, mitigations, and decisions to record |

## Project brief at a glance

- **Game:** The Reality Defender
- **Platform:** ARCore/ARKit-compatible smartphone; no headset required
- **Engine:** Unity with AR Foundation
- **Time limit in the brief:** 48 hours
- **Core loop:** detect a horizontal surface → tap to place an anchored game base → spawn three targets → fire camera-forward projectiles → score hits → clear the wave

## Important scope note

This archive contains **Markdown documentation and code examples**, not a ready-to-open Unity project, compiled app, art asset bundle, or completed Shader Graph asset. Use the code examples as implementation starting points, then create the Unity scenes, prefabs, materials, UI, and platform settings described here.

The supplied assignment is the source for mandatory requirements. Recommendations added to make the game feel more polished or production-ready are labelled as **recommended implementation** rather than assignment requirements.

## Suggested reading order

1. `01_game_design.md`
2. `02_requirements_traceability.md`
3. `03_unity_project_setup.md`
4. `04_technical_architecture.md`
5. `05_implementation_guide.md`
6. `06_csharp_reference.md`
7. `07_shader_graph_and_vfx.md`
8. `08_ui_ux_audio.md`
9. `09_testing_and_qa.md`
10. `10_48_hour_plan.md`
11. `11_build_submission.md`
12. `12_risks_and_decisions.md`

## Definition of done

The assignment's minimum playable result is complete when a tester can:

1. Open the app on a supported phone and see horizontal-plane detection.
2. Tap a detected floor/table to place the game base.
3. See the base remain anchored and aligned to the surface.
4. See three floating targets appear above the base.
5. Tap **FIRE** and launch a projectile from the camera forward direction.
6. Physically hit a target, see it disappear with hit feedback, and gain 10 points.
7. Fire a projectile that misses and confirm it is destroyed after travelling more than 5 metres.
8. Clear all three targets and see **WAVE CLEARED**.

The required evidence is a screen recording, a Shader Graph screenshot, and a Google Drive share link with “Anyone with the link can view” access.
