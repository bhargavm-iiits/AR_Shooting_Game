# 08 — UI, UX, and Audio

## 1. UX priorities

The player should always know:
- Whether the app is scanning or ready to play.
- Where a valid placement surface is.
- Whether the base has been placed.
- How to fire.
- How many points they have.
- When the wave is complete.

## 2. Suggested screen layout

### Scanning state
- Top/centre: `MOVE PHONE SLOWLY TO SCAN`
- Optional short subtext: `Find a flat floor or table`
- Minimal reticle or plane visual.
- Hide the FIRE button until the base is placed.

### Playing state
- Top-left: `SCORE  0`
- Bottom-right or bottom-centre: large `FIRE` button.
- Optional small hit confirmation near the crosshair.
- Keep the centre of the screen clear for aiming.

### Wave-cleared state
- Centre: `WAVE CLEARED`
- Smaller line: `FINAL SCORE  30`
- Optional restart button if implemented without risking the baseline.

## 3. Button design

FIRE button:
- Large touch target.
- High contrast against camera passthrough.
- Clear pressed state.
- Avoid placement under the phone's system gesture area.
- Button callback should call `WeaponController.Fire()` once per press.

## 4. UI implementation notes

- Use a Screen Space - Overlay Canvas for the fastest reliable score and button UI.
- Use TextMeshPro for crisp text.
- Apply safe-area padding for notches and rounded corners.
- Use a Canvas Scaler set to Scale With Screen Size.
- Do not allow the UI to cover the main target area.
- Ensure touch input over the button is not interpreted as a placement tap.

## 5. Feedback timing

Recommended feedback:
- Placement: quick pulse or soft ring at the base.
- Fire: short muzzle flash and projectile trail.
- Hit: particle burst and small score pop.
- Wave complete: short scale/fade-in on the message.

Keep effects brief. AR is already visually busy because the real camera feed is moving.

## 6. Audio direction

Optional polish:
- Soft scan/placement confirmation.
- Short laser firing sound.
- Bright impact sound.
- Subtle wave-complete sting.

Audio should be:
- Short and low-latency.
- Comfortable at moderate volume.
- Non-essential to understanding gameplay.
- Muted or disabled gracefully if unavailable.

## 7. Accessibility and readability

- Do not communicate hit state using colour alone; combine colour with particles or animation.
- Use readable text size and strong contrast.
- Keep the FIRE control large enough for touch.
- Avoid rapid flashing.
- Avoid long instructions that block the camera view.
- Test on a smaller phone screen as well as the primary device.

## 8. UI review checklist

- [ ] Scan instruction appears before placement.
- [ ] FIRE is hidden or disabled before placement.
- [ ] FIRE is visible and responsive after placement.
- [ ] Score remains readable over camera backgrounds.
- [ ] UI buttons do not trigger placement.
- [ ] Wave-cleared message is obvious.
- [ ] Safe areas are respected.
