# Store Terminal artwork and layout

`TeamToolShopPanel` builds the four equipment cards inside `ShopPopup/Window`.
The content has a 72 px bottom inset. `CloseButton` occupies 20–56 px from the
bottom, leaving a 16 px gap; decorative graphics do not intercept clicks.

The images are rendered from the meshes and materials of these imported prefabs:

| Card | Prefab under `Assets/Prefabs/Gameplay/Imported` |
| --- | --- |
| First Kit | `PF_TeamToolPickup_FirstAid.prefab` |
| Noise Maker | `PF_TeamToolPickup_NoiseMaker.prefab` |
| Scanner | `PF_Scanner_Imported.prefab` |
| Distress Beacon | `DistressBeaconDeployed.prefab` |

Distress Beacon uses its deployed appearance for the thumbnail. This does not
change its catalog identity or gameplay behaviour.

The generated 512×512 PNG files are stored under
`Assets/Resources/Shop/Thumbnails`, so the player build loads the same images as
the Editor without relying on `AssetDatabase` or an API response.

To regenerate after changing a prefab, exit Play mode and use
**ECHO Protocol > Shop > Bake Prefab Thumbnails**. The baker copies only mesh
geometry and materials into isolated preview scenes; pickup and networking
components are not instantiated.

**ECHO Protocol > Shop > Validate Store Preview** renders the MainMenu in an
isolated scene at 1920×1080 and 1280×720, checks the four image references and
checks that content and buy buttons do not overlap Close. Screenshots are saved
as `Temp/StoreTerminal-1920x1080.png` and `Temp/StoreTerminal-1280x720.png`.
No account requests or purchases are made by this preview.

Manual interaction check: open Shop, switch Equipment/Cosmetics, and press Close.
Buying still requires the corresponding items in the connected backend catalog.
