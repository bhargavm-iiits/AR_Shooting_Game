# 11 — Build and Submission

## 1. Required deliverables from the assignment

1. **Screen recording:** show plane detection, tap-to-place, and shooting mechanics, hitting at least three targets.
2. **Shader Graph screenshot:** show the custom target Shader Graph setup.
3. **Google Drive link:** share the files with **Anyone with the link can view** permission.

## 2. Recommended submission folder

```text
The_Reality_Defender_Submission/
├── Unity_Project/
│   ├── Assets/
│   ├── Packages/
│   ├── ProjectSettings/
│   └── README.md
├── Builds/
│   ├── Android/
│   └── iOS/                 (if required and buildable in your environment)
├── Evidence/
│   ├── Gameplay_Recording.mp4
│   └── ShaderGraph_Setup.png
└── Documentation/
    └── Development_Notes.md
```

Only include platform folders that are relevant. For a Unity project handoff, include the project source folders needed to reopen the project; generated cache folders such as `Library/` are normally unnecessary.

## 3. Screen recording shot list

Capture one continuous, easy-to-follow recording where possible:

1. Launch the app.
2. Show the camera view and horizontal plane detection.
3. Move the phone slowly until a floor/table plane is detected.
4. Tap the surface and show the Game Base placement.
5. Move around the base briefly to demonstrate anchoring.
6. Show all three floating targets.
7. Aim and press FIRE to hit the first target.
8. Show score changing to 10.
9. Hit the second target and show score 20.
10. Hit the third target and show score 30.
11. Show `WAVE CLEARED`.

The assignment requires hitting at least three targets; ensure the recording clearly shows all three successful hits.

## 4. Shader Graph screenshot

The screenshot should:
- Show the graph title.
- Show the relevant nodes and connections.
- Make the scrolling noise and/or fresnel/displacement logic visible.
- Include the material/target context if possible.
- Be legible at normal zoom.

Suggested filename: `ShaderGraph_Setup.png`.

## 5. Build checklist

### Android
- [ ] ARCore provider enabled.
- [ ] Package and player settings are compatible.
- [ ] Camera permission works.
- [ ] App installs on the test phone.
- [ ] App launches and completes the game loop.
- [ ] Final APK/AAB generated as required by the recipient.

### iOS
- [ ] ARKit provider enabled.
- [ ] Camera usage description set.
- [ ] Xcode export/build tested on a compatible device.
- [ ] App launches and completes the game loop.

## 6. Google Drive sharing

1. Upload the project, build (if requested), recording, screenshot, and documentation.
2. Select the submission folder or required files.
3. Open Share / General access.
4. Change access to **Anyone with the link**.
5. Set role to **Viewer**.
6. Copy the link.
7. Test the link in a private/incognito browser window or another account that is not signed in.
8. Confirm the folder contents can be opened.

Do not assume the link works merely because it opens while signed in as the owner.

## 7. Final handoff checklist

- [ ] Unity project source included.
- [ ] Final device build included if requested.
- [ ] Gameplay recording included.
- [ ] Shader Graph screenshot included.
- [ ] README or development notes included.
- [ ] Google Drive general access is Anyone with the link — Viewer.
- [ ] Link tested while signed out.
- [ ] No private credentials, API keys, or unrelated personal files included.
