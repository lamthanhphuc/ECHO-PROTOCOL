using System.IO;
using EchoProtocol.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.EditorTools
{
    public static class EchoSetup_FAKAndFlashlight
    {
        private const string RunMarker = "Assets/Editor/.run_setup_fak_flashlight";

        [InitializeOnLoadMethod]
        private static void CheckRunMarker()
        {
            EditorApplication.delayCall += () =>
            {
                string fullMarkerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RunMarker));
                if (File.Exists(fullMarkerPath))
                {
                    try
                    {
                        File.Delete(fullMarkerPath);
                    }
                    catch
                    {
                        // Ignore delete errors
                    }

                    ExecuteSetup();
                }
            };
        }

        [MenuItem("Tools/ECHO Protocol/Setup FAK & Flashlight", priority = 10)]
        [MenuItem("ECHO Protocol/Setup FAK & Flashlight", priority = 10)]
        public static void ExecuteSetup()
        {
            Debug.Log("<color=#00E5FF>[ECHO Protocol]</color> Bắt đầu thiết lập First Aid Kit & Flashlight...");

            // 1. Tạo hoặc load ScriptableObject FAK_Definition
            InventoryItemDefinition fakDef = SetupFakDefinition();

            // 2. Cập nhật Player.prefab (Flashlight + Revive Gate + Bleedout)
            SetupPlayerPrefab(fakDef);

            // 3. Cập nhật HUD Prefab và HUD trong scene
            SetupHUD(fakDef);

            // 4. Đặt FAK Pickup trong scene
            SetupScenePickup(fakDef);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00E5FF>[ECHO Protocol]</color> <color=#00FF7F>THÀNH CÔNG!</color> Hoàn tất toàn bộ cài đặt FAK & Flashlight.");
        }

        private static InventoryItemDefinition SetupFakDefinition()
        {
            string assetDir = "Assets/GeeKay3D/First-Aid-Set/Assets";
            if (!Directory.Exists(assetDir))
            {
                Directory.CreateDirectory(assetDir);
            }

            string assetPath = $"{assetDir}/FAK_Definition.asset";
            InventoryItemDefinition fakDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);

            // Tìm WorldPrefab
            GameObject worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_FirstAidPickup_Imported.prefab");
            if (worldPrefab == null)
            {
                worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeeKay3D/First-Aid-Set/Assets/Prefabs/FirstAidKit_Red.prefab");
            }

            if (fakDef == null)
            {
                fakDef = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                AssetDatabase.CreateAsset(fakDef, assetPath);
            }

            SerializedObject fakSo = new SerializedObject(fakDef);
            fakSo.FindProperty("itemId").stringValue = "first_aid_kit";
            fakSo.FindProperty("displayName").stringValue = "First Aid Kit";
            fakSo.FindProperty("itemType").enumValueIndex = (int)InventoryItemType.TeamTool;
            if (worldPrefab != null)
            {
                fakSo.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
            }
            fakSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(fakDef);

            Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã tạo/cập nhật ScriptableObject: {assetPath}");
            return fakDef;
        }

        private static void SetupPlayerPrefab(InventoryItemDefinition fakDef)
        {
            string prefabPath = "Assets/Prefabs/Player.prefab";
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"<color=#FF9800>[ECHO Protocol]</color> Không tìm thấy {prefabPath}");
                return;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;

                // A. PlayerDownState: bleedoutSeconds = 90
                PlayerDownState downState = root.GetComponent<PlayerDownState>();
                if (downState != null)
                {
                    SerializedObject dsSo = new SerializedObject(downState);
                    SerializedProperty bleedProp = dsSo.FindProperty("bleedoutSeconds");
                    if (bleedProp != null) bleedProp.floatValue = 90f;
                    dsSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(downState);
                }

                // B. PlayerReviveInteractable: firstAidKitDefinition + duration 6s + Vietnamese prompt
                PlayerReviveInteractable revive = root.GetComponent<PlayerReviveInteractable>();
                if (revive != null)
                {
                    SerializedObject revSo = new SerializedObject(revive);
                    SerializedProperty fakProp = revSo.FindProperty("firstAidKitDefinition");
                    if (fakProp != null) fakProp.objectReferenceValue = fakDef;

                    SerializedProperty durProp = revSo.FindProperty("reviveDurationSeconds");
                    if (durProp != null) durProp.floatValue = 6f;

                    SerializedProperty promptProp = revSo.FindProperty("revivePrompt");
                    if (promptProp != null) promptProp.stringValue = "Cứu đồng đội";

                    revSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(revive);
                }

                // C. PlayerFlashlight & Light child
                Light flashlightLight = root.GetComponentInChildren<Light>(true);
                if (flashlightLight == null)
                {
                    GameObject lightObj = new GameObject("Flashlight_Light");
                    lightObj.transform.SetParent(root.transform, false);
                    lightObj.transform.localPosition = new Vector3(0f, 1.55f, 0.35f);
                    lightObj.transform.localRotation = Quaternion.identity;

                    flashlightLight = lightObj.AddComponent<Light>();
                    flashlightLight.type = LightType.Spot;
                    flashlightLight.range = 28f;
                    flashlightLight.spotAngle = 65f;
                    flashlightLight.innerSpotAngle = 45f;
                    flashlightLight.color = new Color(1f, 0.96f, 0.88f);
                    flashlightLight.intensity = 2.8f;
                }

                PlayerFlashlight flashlightComp = root.GetComponent<PlayerFlashlight>();
                if (flashlightComp == null)
                {
                    flashlightComp = root.AddComponent<PlayerFlashlight>();
                }

                InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                SerializedObject flashSo = new SerializedObject(flashlightComp);
                SerializedProperty lightProp = flashSo.FindProperty("flashlight");
                if (lightProp != null) lightProp.objectReferenceValue = flashlightLight;

                SerializedProperty actionsProp = flashSo.FindProperty("inputActions");
                if (actionsProp != null) actionsProp.objectReferenceValue = inputActions;

                SerializedProperty startOnProp = flashSo.FindProperty("startOn");
                if (startOnProp != null) startOnProp.boolValue = true;

                SerializedProperty cooldownProp = flashSo.FindProperty("toggleCooldown");
                if (cooldownProp != null) cooldownProp.floatValue = 0.2f;

                flashSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(flashlightComp);
            }

            Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã cập nhật Player.prefab (Flashlight, Revive Gate, Bleedout 90s).");
        }

        private static void SetupHUD(InventoryItemDefinition fakDef)
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 1. Cập nhật HUD Prefab
            string hudPrefabPath = "Assets/Prefabs/UI/PF_GameplayHUD_Canvas.prefab";
            if (File.Exists(hudPrefabPath))
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(hudPrefabPath))
                {
                    GameObject hudRoot = scope.prefabContentsRoot;
                    HUDPlayerVitals vitals = hudRoot.GetComponentInChildren<HUDPlayerVitals>(true);
                    if (vitals != null)
                    {
                        WireVitalsUI(vitals, fakDef, defaultFont);
                    }
                }
                Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã cập nhật HUD trong Prefab {hudPrefabPath}.");
            }

            // 2. Cập nhật HUD trong Scene đang mở (nếu có)
            HUDPlayerVitals sceneVitals = Object.FindAnyObjectByType<HUDPlayerVitals>();
            if (sceneVitals != null)
            {
                WireVitalsUI(sceneVitals, fakDef, defaultFont);
                EditorSceneManager.MarkSceneDirty(sceneVitals.gameObject.scene);
                Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã cập nhật HUD trong active scene.");
            }
        }

        private static void WireVitalsUI(HUDPlayerVitals vitals, InventoryItemDefinition fakDef, Font font)
        {
            Transform panelTrans = vitals.transform;

            // Nới rộng panel nếu cần
            RectTransform panelRt = panelTrans.GetComponent<RectTransform>();
            if (panelRt != null && panelRt.sizeDelta.y < 170f)
            {
                panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, 175f);
            }

            Transform indicatorTrans = panelTrans.Find("FirstAidKit_Indicator");
            GameObject indicatorGo;
            CanvasGroup cg;
            Text labelText;

            if (indicatorTrans == null)
            {
                indicatorGo = new GameObject("FirstAidKit_Indicator");
                indicatorGo.transform.SetParent(panelTrans, false);

                RectTransform rt = indicatorGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(24, 60);
                rt.sizeDelta = new Vector2(-48, 28);

                cg = indicatorGo.AddComponent<CanvasGroup>();
                cg.alpha = 0.35f;

                Image bg = indicatorGo.AddComponent<Image>();
                bg.sprite = HUDTextureUtility.RoundedBox;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.04f, 0.08f, 0.14f, 0.85f);

                // Icon / Tag
                GameObject tagGo = new GameObject("Tag");
                tagGo.transform.SetParent(indicatorGo.transform, false);
                RectTransform tagRt = tagGo.AddComponent<RectTransform>();
                tagRt.anchorMin = new Vector2(0f, 0f);
                tagRt.anchorMax = new Vector2(0f, 1f);
                tagRt.pivot = new Vector2(0f, 0.5f);
                tagRt.anchoredPosition = new Vector2(6, 0);
                tagRt.sizeDelta = new Vector2(24, -8);

                Text tagText = tagGo.AddComponent<Text>();
                tagText.font = font;
                tagText.fontSize = 14;
                tagText.fontStyle = FontStyle.Bold;
                tagText.color = new Color(0f, 0.9f, 0.5f, 1f);
                tagText.alignment = TextAnchor.MiddleCenter;
                tagText.text = "[+]";

                // Label Text
                GameObject textGo = new GameObject("Label");
                textGo.transform.SetParent(indicatorGo.transform, false);
                RectTransform trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.anchoredPosition = new Vector2(34, 0);
                trt.sizeDelta = new Vector2(-40, 0);

                labelText = textGo.AddComponent<Text>();
                labelText.font = font;
                labelText.fontSize = 13;
                labelText.fontStyle = FontStyle.Bold;
                labelText.color = Color.white;
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.text = "<color=#78909C>TÚI CỨU THƯƠNG: KHÔNG CÓ</color>";
            }
            else
            {
                indicatorGo = indicatorTrans.gameObject;
                cg = indicatorGo.GetComponent<CanvasGroup>() ?? indicatorGo.AddComponent<CanvasGroup>();
                labelText = indicatorGo.GetComponentInChildren<Text>();
            }

            SerializedObject vitalsSo = new SerializedObject(vitals);
            SerializedProperty fakDefProp = vitalsSo.FindProperty("firstAidKitDefinition");
            if (fakDefProp != null) fakDefProp.objectReferenceValue = fakDef;

            SerializedProperty groupProp = vitalsSo.FindProperty("firstAidKitGroup");
            if (groupProp != null) groupProp.objectReferenceValue = cg;

            SerializedProperty labelProp = vitalsSo.FindProperty("firstAidKitLabel");
            if (labelProp != null) labelProp.objectReferenceValue = labelText;

            vitalsSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(vitals);
        }

        private static void SetupScenePickup(InventoryItemDefinition fakDef)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            // Kiểm tra xem đã có FAK pickup nào trong scene chưa
            PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
            foreach (var p in pickups)
            {
                var pSo = new SerializedObject(p);
                var itemProp = pSo.FindProperty("item");
                if (itemProp != null && itemProp.objectReferenceValue == fakDef)
                {
                    Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã tồn tại FAK Pickup trong scene: {p.gameObject.name} tại {p.transform.position}");
                    return;
                }
            }

            // Tạo pickup mới
            GameObject worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_FirstAidPickup_Imported.prefab");
            if (worldPrefab == null)
            {
                worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeeKay3D/First-Aid-Set/Assets/Prefabs/FirstAidKit_Red.prefab");
            }

            GameObject fakInstance;
            if (worldPrefab != null)
            {
                fakInstance = (GameObject)PrefabUtility.InstantiatePrefab(worldPrefab);
            }
            else
            {
                fakInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }

            fakInstance.name = "FAK_Pickup";

            // Tìm vị trí player hoặc spawn point để đặt FAK gần đó
            Vector3 spawnPos = new Vector3(-90f, 1f, -31f);
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                spawnPos = player.transform.position + player.transform.forward * 2.5f + Vector3.up * 0.3f;
            }
            fakInstance.transform.position = spawnPos;

            PickupItem pickupComp = fakInstance.GetComponent<PickupItem>();
            if (pickupComp == null)
            {
                pickupComp = fakInstance.AddComponent<PickupItem>();
            }

            SerializedObject pickupSo = new SerializedObject(pickupComp);
            pickupSo.FindProperty("item").objectReferenceValue = fakDef;
            pickupSo.FindProperty("promptOverride").stringValue = "Nhặt Túi Cứu Thương [E]";
            pickupSo.FindProperty("destroyOnPickup").boolValue = true;
            pickupSo.ApplyModifiedProperties();

            Collider col = fakInstance.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider bc = fakInstance.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.6f, 0.4f, 0.4f);
            }

            Undo.RegisterCreatedObjectUndo(fakInstance, "Create FAK Pickup");
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Đã đặt FAK Pickup trong scene ({activeScene.name}) tại {fakInstance.transform.position}");
        }
    }
}
