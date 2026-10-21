# Main Menu typography

The menu is authored by `KLTN/Assets/Editor/MainMenuTypographyAuthoring.cs`.
Open `Assets/Scenes/MainMenu.unity` in Edit mode, wait for compilation, then run
**ECHO PROTOCOL > Main Menu > Apply Cinematic Typography** to regenerate and save it.
The command refuses Play mode and the local request runner leaves unsaved scene changes alone.

## Reference composition

At the CanvasScaler reference resolution of 1920 x 1080:

- The visible title starts around (144, 178), spans 570 pixels horizontally, and uses two 88-pixel capital rows with a 118-pixel baseline advance.
- The red subtitle starts around (150, 410).
- Button rectangles start at (104, 522), are 420 x 76, and repeat every 100 pixels. Text, indicator, and number are children of the original Button, so visuals and click areas align.

The user approved changing the menu's typography dimensions and row positions to match the supplied reference. The authoring pass snapshots RectTransforms outside MainMenuPanel and checks that they remain unchanged.

## Assets and effects

`MainMenuIndustrialTitleFont.Ensure` creates `Resources/UI/MainMenuTypography/IndustrialTitleTMP.asset`, a static SDF font with original angular block glyphs for ECHO PROTOCOL. Its atlas and material are persistent subassets. The local Roboto Black font bootstraps TMP's asset metadata; its license and provenance are in `Assets/Fonts/Roboto-LICENSE.txt`. Menu labels retain the existing Oswald TMP asset with Vietnamese glyphs.

`TitleMetal.mat` uses the full **TextMeshPro/Distance Field** shader. `TitleDistressedFace.png` combines a mostly opaque silver surface with generated scratches, erosion, and restrained dried-red stains. `TitleBlood.png` and `TitleScratches.png` retain transparent source layers. `ButtonWornFace.png` adds lighter wear to menu labels. Paragraph texture mapping keeps damage positioned across the full label rather than repeating it on every character.

`MainMenuTitleAccentGraphic` draws the red registration streaks. `MainMenuTitleSignalEffect` creates a faint red echo behind the title, a brief introduction, and occasional interference. `MainMenuButtonTextFeedback` reuses each authored indicator and creates one hover material per button; it does not allocate materials per frame.

The scene keeps the original HeaderText, SystemLabel, HostGameButton, JoinGameButton, OptionsButton, and LogoutButton objects. Button listeners and navigation remain on those objects. RowSurface, rules, indicators, numbers, and TitleSignalAccents provide the decorative layers.

## Verification

The authoring command checks font/material/atlas assignments, texture imports, glyph coverage, and protected RectTransforms. It also captures the actual Canvas in an isolated Unity preview scene at 1920 x 1080 to `KLTN/Temp/MainMenuTypography-preview.png`; the real camera and source background are not changed by this capture. The reviewed final capture is retained at `docs/previews/MainMenuTypography.png`.

The final pass compiled in Unity 6000.5.8f1 and rendered the industrial font at 570 x 206 visible pixels. Repeating the authoring command preserved the font GUID, atlas/material subasset IDs, and title material atlas reference. A semantic comparison with the original scene found no changes to 33 protected Button, camera, CanvasScaler, Profile, Matchmaking, and Leaderboard components. No new compiler, shader, or authoring error occurred during the successful final pass.

For runtime verification, enter Play mode, reach MainMenu, and check Vietnamese labels, a short opening echo, subtle interference after 8-13 seconds, button hover brightness and indicators, and the Console. The Edit mode preview verifies the static appearance; it does not prove pointer handling or runtime animations.

## Previous mismatch

The earlier title used narrow Oswald at 48 points and rendered only about 212 pixels wide. The reference title is approximately 570 pixels wide. The subtitle and menu rows were similarly too small or too tightly spaced. Texture changes alone could not fix those proportions. The original mobile TMP shader also lacked a face texture slot. Finally, this installed TMP version can return false from TryAddCharacters when all glyphs already exist; coverage is now checked with HasCharacters instead.
