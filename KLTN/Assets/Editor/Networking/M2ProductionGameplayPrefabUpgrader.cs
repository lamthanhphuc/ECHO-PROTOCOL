using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Editor.Networking
{
    [InitializeOnLoad]
    internal static class M2ProductionGameplayPrefabUpgrader
    {
        private const string PlayerPrefabPath =
            "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab";
        private const string RuntimePlayerPrefabPath =
            "Assets/Prefabs/PlayerNetwork.prefab";
        private const string DefaultCharacterPrefabPath =
            "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab";
        private const string FirstPersonArmsMeshPath =
            "Assets/Prefabs/Player/Generated/P1_Suit_FirstPersonArms.asset";
        private const string FirstPersonArmsMeshName = "P1_Suit_FirstPersonArms";
        private const float FirstPersonArmWeightThreshold = 0.35f;
        private const float FirstPersonForearmWeightThreshold = 0.06f;
        private const float FirstPersonArmSideCutoff = 0.00095f;
        private const string FirstAidDefinitionPath =
            "Assets/ScriptableObjects/Inventory/SO_FirstAid_ItemDefinition.asset";
        private const string FakDefinitionPath =
            "Assets/GeeKay3D/First-Aid-Set/Assets/FAK_Definition.asset";
        private const string NoiseMakerDeployedPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/DistressBeaconDeployed.prefab";
        private const string NoiseMakerClosedPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/DistressBeaconClosed.prefab";
        private const string FirstAidPickupPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/PF_FirstAidPickup_Imported.prefab";
        private const string FieldScannerDefinitionPath =
            "Assets/ScriptableObjects/Inventory/SO_FieldScanner_ItemDefinition.asset";
        private const string NoiseMakerDefinitionPath =
            "Assets/ScriptableObjects/Inventory/SO_NoiseMaker_ItemDefinition.asset";
        private const string DoorJammerDefinitionPath =
            "Assets/ScriptableObjects/Inventory/SO_DoorJammer_ItemDefinition.asset";
        private const string FieldScannerPickupPrefabPath =
            "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab";
        private const string NoiseMakerPickupPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab";
        private const string FirstAidTeamToolPickupPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_FirstAid.prefab";
        private const string PlankPickupPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab";
        private const string PlankDefinitionPath =
            "Assets/ScriptableObjects/Inventory/TeamTools/SO_Plank_ItemDefinition.asset";
        private const string DoorJammerPickupPrefabPath =
            "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_DoorJammer.prefab";
        private const string DoorJammerVisualSourcePath =
            "Assets/Resources/Network/PF_DoorJammer.prefab";
        private const string SectorBoxPrefabPath =
            "Assets/Resources/Network/NetworkSectorBox.prefab";

        static M2ProductionGameplayPrefabUpgrader()
        {
            EditorApplication.delayCall += EnsureTeamToolDropAssetsAndUpgradePlayers;
            EditorApplication.delayCall += CreateSectorBoxPrefabIfNeeded;
        }

        [MenuItem("ECHO Protocol/M2/Upgrade Production Gameplay Prefabs")]
        private static void UpgradeAll()
        {
            EnsureTeamToolDropAssetsAndUpgradePlayers();
            EnsureFirstAidDefinitionsAreTeamTools();
            EnsureNoiseMakerBeaconPrefab();
            CreateSectorBoxPrefabIfNeeded();
        }

        private static void EnsureTeamToolDropAssetsAndUpgradePlayers()
        {
            EnsureNoiseMakerBeaconPrefab();
            EnsureTeamToolPickupPrefab(
                NoiseMakerPickupPrefabPath,
                2,
                "Noise Maker",
                NoiseMakerClosedPrefabPath);
            EnsureTeamToolPickupPrefab(
                FirstAidTeamToolPickupPrefabPath,
                3,
                "First Aid Kit",
                FirstAidPickupPrefabPath);
            EnsureTeamToolPickupPrefab(
                DoorJammerPickupPrefabPath,
                4,
                "Door Jammer",
                DoorJammerVisualSourcePath);
            EnsureTeamToolDefinition(NoiseMakerDefinitionPath, "noise_maker", "Noise Maker", NoiseMakerPickupPrefabPath);
            EnsureTeamToolDefinition(DoorJammerDefinitionPath, "door_jammer", "Door Jammer", DoorJammerPickupPrefabPath);
            UpgradePlayerPrefabIfNeeded();
        }

        private static void UpgradePlayerPrefabIfNeeded()
        {
            UpgradePlayerPrefabAtPath(PlayerPrefabPath);
            UpgradePlayerPrefabAtPath(RuntimePlayerPrefabPath);
        }

        private static void UpgradePlayerPrefabAtPath(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[M2 Prefab Upgrade] Player prefab not found: {prefabPath}");
                return;
            }

            try
            {
                bool changed = false;
                if (root.GetComponent<NetworkPlayerLifeState>() == null)
                {
                    var component = root.AddComponent<NetworkPlayerLifeState>();
                    if (component == null)
                    {
                        Debug.LogError("[M2 Prefab Upgrade] Unity could not create NetworkPlayerLifeState.");
                        return;
                    }

                    changed = true;
                }

                changed |= EnsurePresentation(root);

                if (!changed)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (saved)
                {
                    Debug.Log($"[M2 Prefab Upgrade] Upgraded gameplay presentation on {prefabPath}.");
                }
                else
                {
                    Debug.LogError($"[M2 Prefab Upgrade] Failed to save {prefabPath}; prefab left unchanged.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsurePresentation(GameObject root)
        {
            bool changed = false;
            changed |= RemoveDuplicateComponentsOutsideRoot<PlayerHeldItemView>(root);
            changed |= RemoveDuplicateComponentsOutsideRoot<PlayerHeldItemAnchor>(root);
            changed |= RemoveDuplicateComponentsOutsideRoot<PlayerAnimatorDriver>(root);

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPrefabPath);
                if (characterPrefab != null)
                {
                    GameObject visual = PrefabUtility.InstantiatePrefab(characterPrefab, root.transform) as GameObject;
                    if (visual != null)
                    {
                        visual.name = "CharacterVisual";
                        visual.transform.localPosition = Vector3.zero;
                        visual.transform.localRotation = Quaternion.identity;
                        visual.transform.localScale = Vector3.one;
                        animator = visual.GetComponentInChildren<Animator>(true);
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("[M2 Prefab Upgrade] Missing default character prefab for network player presentation.");
                }
            }

            PlayerHeldItemAnchor anchor = root.GetComponent<PlayerHeldItemAnchor>();
            if (anchor == null)
            {
                anchor = root.AddComponent<PlayerHeldItemAnchor>();
                changed = true;
            }

            PlayerInventory inventory = root.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = root.AddComponent<PlayerInventory>();
                changed = true;
            }

            PlayerInteraction interaction = root.GetComponent<PlayerInteraction>();
            if (interaction == null)
            {
                interaction = root.AddComponent<PlayerInteraction>();
                changed = true;
            }

            NetworkPlayerInteractor networkInteractor = root.GetComponent<NetworkPlayerInteractor>();

            PlayerEnergyCoreCarrier coreCarrier = root.GetComponent<PlayerEnergyCoreCarrier>();
            if (coreCarrier == null)
            {
                coreCarrier = root.AddComponent<PlayerEnergyCoreCarrier>();
                changed = true;
            }

            PlayerInventoryDropInput dropInput = root.GetComponent<PlayerInventoryDropInput>();
            if (dropInput == null)
            {
                dropInput = root.AddComponent<PlayerInventoryDropInput>();
                changed = true;
            }

            PlayerHeldItemView heldItemView = root.GetComponent<PlayerHeldItemView>();
            if (heldItemView == null)
            {
                heldItemView = root.AddComponent<PlayerHeldItemView>();
                changed = true;
            }

            PlayerUpperBodyAim rootAim = root.GetComponent<PlayerUpperBodyAim>();
            GameObject aimOwner = animator != null ? animator.gameObject : root;
            if (rootAim != null && aimOwner != root)
            {
                Object.DestroyImmediate(rootAim, true);
                changed = true;
            }

            PlayerUpperBodyAim aim = aimOwner.GetComponent<PlayerUpperBodyAim>();
            if (aim == null)
            {
                aim = aimOwner.AddComponent<PlayerUpperBodyAim>();
                changed = true;
            }

            NetworkTeamToolHeldView toolView = root.GetComponent<NetworkTeamToolHeldView>();
            if (toolView == null)
            {
                toolView = root.AddComponent<NetworkTeamToolHeldView>();
                changed = true;
            }

            PlayerFirstPersonVisibility visibility = root.GetComponent<PlayerFirstPersonVisibility>();
            if (visibility == null)
            {
                visibility = root.AddComponent<PlayerFirstPersonVisibility>();
                changed = true;
            }
            changed |= ConfigureFirstPersonVisibility(visibility);

            PlayerFirstPersonWristCuffs wristCuffs = root.GetComponent<PlayerFirstPersonWristCuffs>();
            if (wristCuffs == null)
            if (wristCuffs != null)
            {
                wristCuffs = root.AddComponent<PlayerFirstPersonWristCuffs>();
                Object.DestroyImmediate(wristCuffs, true);
                changed = true;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.StartsWith("FirstPersonWristCuff"))
                {
                    Object.DestroyImmediate(t.gameObject, true);
                    changed = true;
                }
            }

            PlayerAnimatorDriver driver = root.GetComponent<PlayerAnimatorDriver>();
            if (driver == null)
            {
                driver = root.AddComponent<PlayerAnimatorDriver>();
                changed = true;
            }

            if (animator != null)
            {
                changed |= EnsureFirstPersonArms(root);

                var anchorSo = new SerializedObject(anchor);
                anchorSo.FindProperty("animator").objectReferenceValue = animator;
                anchorSo.ApplyModifiedPropertiesWithoutUndo();

                var aimSo = new SerializedObject(aim);
                aimSo.FindProperty("animator").objectReferenceValue = animator;
                aimSo.FindProperty("playerRoot").objectReferenceValue = root.transform;
                aimSo.ApplyModifiedPropertiesWithoutUndo();

                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("animator").objectReferenceValue = animator;
                driverSo.FindProperty("inventory").objectReferenceValue = inventory;
                driverSo.FindProperty("coreCarrier").objectReferenceValue = coreCarrier;
                driverSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Object inputActions = ResolveInputActions(root);

            var interactionSo = new SerializedObject(interaction);
            interactionSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            interactionSo.ApplyModifiedPropertiesWithoutUndo();

            if (networkInteractor != null)
            {
                var networkInteractorSo = new SerializedObject(networkInteractor);
                SetObject(networkInteractorSo.FindProperty("_fieldScannerPickupPrefab"), GetNetworkPrefab(FieldScannerPickupPrefabPath));
                SetObject(networkInteractorSo.FindProperty("_noiseMakerPickupPrefab"), GetNetworkPrefab(NoiseMakerPickupPrefabPath));
                SetObject(networkInteractorSo.FindProperty("_firstAidPickupPrefab"), GetNetworkPrefab(FirstAidTeamToolPickupPrefabPath));
                SetObject(networkInteractorSo.FindProperty("_doorJammerPickupPrefab"), GetNetworkPrefab(PlankPickupPrefabPath));
                networkInteractorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var inventorySo = new SerializedObject(inventory);
            SetObject(inventorySo.FindProperty("fieldScannerDefinition"), AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(FieldScannerDefinitionPath));
            SetObject(inventorySo.FindProperty("noiseMakerDefinition"), AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(NoiseMakerDefinitionPath));
            SetObject(inventorySo.FindProperty("firstAidDefinition"), AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(FirstAidDefinitionPath));
            SetObject(inventorySo.FindProperty("doorJammerDefinition"), AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(PlankDefinitionPath));
            inventorySo.ApplyModifiedPropertiesWithoutUndo();

            var carrierSo = new SerializedObject(coreCarrier);
            carrierSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            carrierSo.FindProperty("inventory").objectReferenceValue = inventory;
            carrierSo.ApplyModifiedPropertiesWithoutUndo();

            var dropInputSo = new SerializedObject(dropInput);
            dropInputSo.FindProperty("inventory").objectReferenceValue = inventory;
            dropInputSo.FindProperty("coreCarrier").objectReferenceValue = coreCarrier;
            SetObject(dropInputSo.FindProperty("noiseMakerDeployedPrefab"), AssetDatabase.LoadAssetAtPath<GameObject>(NoiseMakerDeployedPrefabPath));
            dropInputSo.ApplyModifiedPropertiesWithoutUndo();

            var heldItemViewSo = new SerializedObject(heldItemView);
            heldItemViewSo.FindProperty("inventory").objectReferenceValue = inventory;
            heldItemViewSo.FindProperty("coreCarrier").objectReferenceValue = coreCarrier;
            heldItemViewSo.FindProperty("heldItemAnchor").objectReferenceValue = anchor;
            SetVector3(heldItemViewSo.FindProperty("energyCoreLocalPosition"), Vector3.zero);
            SetVector3(heldItemViewSo.FindProperty("energyCoreLocalEulerAngles"), Vector3.zero);
            SetVector3(heldItemViewSo.FindProperty("energyCoreLocalScale"), new Vector3(25f, 25f, 25f));
            SetVector3(heldItemViewSo.FindProperty("energyCoreChildLocalPosition"), new Vector3(0.0012f, -0.2456f, -1.1109f));
            SetVector3(heldItemViewSo.FindProperty("energyCoreChildLocalEulerAngles"), new Vector3(-89.116f, 77.236f, -92.522f));
            SetVector3(heldItemViewSo.FindProperty("energyCoreChildLocalScale"), new Vector3(0.9f, 0.9f, 0.9f));
            SetVector3(heldItemViewSo.FindProperty("firstAidLocalPosition"), new Vector3(0.035f, -0.015f, 0.155f));
            SetVector3(heldItemViewSo.FindProperty("firstAidLocalEulerAngles"), new Vector3(8f, 92f, 170f));
            SetVector3(heldItemViewSo.FindProperty("firstAidLocalScale"), new Vector3(0.15f, 0.15f, 0.3f));
            SetVector3(heldItemViewSo.FindProperty("firstAidChildLocalPosition"), new Vector3(-0.533528f, -3.405526f, -0.3114559f));
            SetVector3(heldItemViewSo.FindProperty("firstAidChildLocalEulerAngles"), new Vector3(0.12f, -0.416f, 5.923f));
            SetVector3(heldItemViewSo.FindProperty("firstAidChildLocalScale"), Vector3.one);
            SetVector3(heldItemViewSo.FindProperty("noiseMakerLocalPosition"), new Vector3(0.018f, 0.132f, -0.065f));
            SetVector3(heldItemViewSo.FindProperty("noiseMakerLocalEulerAngles"), new Vector3(6.176f, 93.2f, 94.562f));
            SetVector3(heldItemViewSo.FindProperty("noiseMakerLocalScale"), new Vector3(0.7f, 0.7f, 0.7f));
            heldItemViewSo.ApplyModifiedPropertiesWithoutUndo();

            var toolViewSo = new SerializedObject(toolView);
            toolViewSo.FindProperty("lobbyState").objectReferenceValue = root.GetComponent<LobbyPlayerState>();
            toolViewSo.FindProperty("heldItemAnchor").objectReferenceValue = anchor;
            SetObject(toolViewSo.FindProperty("toolVisual_2"), AssetDatabase.LoadAssetAtPath<GameObject>(NoiseMakerClosedPrefabPath));
            SetObject(toolViewSo.FindProperty("toolVisual_3"), AssetDatabase.LoadAssetAtPath<GameObject>(FirstAidPickupPrefabPath));
            GameObject plankHeldVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_Plank_HeldVisual.prefab");
            SetObject(toolViewSo.FindProperty("toolVisual_4"), plankHeldVisual != null ? plankHeldVisual : AssetDatabase.LoadAssetAtPath<GameObject>(PlankPickupPrefabPath));
            SetVector3(toolViewSo.FindProperty("noiseMakerLocalPosition"), new Vector3(0.018f, 0.132f, -0.065f));
            SetVector3(toolViewSo.FindProperty("noiseMakerLocalEulerAngles"), new Vector3(6.176f, 93.2f, 94.562f));
            SetVector3(toolViewSo.FindProperty("noiseMakerLocalScale"), new Vector3(0.7f, 0.7f, 0.7f));
            SetVector3(toolViewSo.FindProperty("firstAidLocalPosition"), new Vector3(0.035f, -0.015f, 0.155f));
            SetVector3(toolViewSo.FindProperty("firstAidLocalEulerAngles"), new Vector3(8f, 92f, 170f));
            SetVector3(toolViewSo.FindProperty("firstAidLocalScale"), new Vector3(0.15f, 0.15f, 0.3f));
            SetVector3(toolViewSo.FindProperty("firstAidChildLocalPosition"), new Vector3(-0.533528f, -3.405526f, -0.3114559f));
            SetVector3(toolViewSo.FindProperty("firstAidChildLocalEulerAngles"), new Vector3(0.12f, -0.416f, 5.923f));
            SetVector3(toolViewSo.FindProperty("firstAidChildLocalScale"), Vector3.one);
            toolViewSo.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        private static bool RemoveDuplicateComponentsOutsideRoot<T>(GameObject root) where T : Component
        {
            bool changed = false;
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component == null || component.gameObject == root)
                {
                    continue;
                }

                Object.DestroyImmediate(component, true);
                changed = true;
            }

            return changed;
        }

        private static bool ConfigureFirstPersonVisibility(PlayerFirstPersonVisibility visibility)
        {
            var visibilitySo = new SerializedObject(visibility);
            bool changed = false;
            changed |= SetStringArray(
                visibilitySo.FindProperty("firstPersonOnlyRendererNameTokens"),
                "FirstPersonArms",
                "FirstPersonWristCuff");
            changed |= SetStringArray(
                visibilitySo.FindProperty("firstPersonRendererNameTokens"),
                "hand",
                "arm",
                "upperarm",
                "forearm",
                "glove",
                "sleeve");
            changed |= SetStringArray(
                visibilitySo.FindProperty("alwaysVisibleNameTokens"),
                "Held_",
                "Runtime_CoreCarryAnchor",
                "Runtime_RightHandAnchor");
            if (changed)
            {
                visibilitySo.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static bool EnsureFirstPersonArms(GameObject root)
        {
            SkinnedMeshRenderer suitRenderer = FindSuitRenderer(root);
            if (suitRenderer == null || suitRenderer.sharedMesh == null)
            {
                Debug.LogWarning("[M2 Prefab Upgrade] Could not find suit SkinnedMeshRenderer for first-person arms.");
                return false;
            }

            SkinnedMeshRenderer glovesRenderer = FindGlovesRenderer(root);

            Mesh armsMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FirstPersonArmsMeshPath);
            if (armsMesh == null)
            {
                armsMesh = new Mesh();
                armsMesh.name = FirstPersonArmsMeshName;
                EnsureAssetFolder("Assets/Prefabs/Player/Generated");
                AssetDatabase.CreateAsset(armsMesh, FirstPersonArmsMeshPath);
                PopulateFirstPersonArmsMesh(armsMesh, suitRenderer, glovesRenderer);
                EditorUtility.SetDirty(armsMesh);
                AssetDatabase.SaveAssets();
            }
            else if (armsMesh.vertexCount == 0)
            {
                PopulateFirstPersonArmsMesh(armsMesh, suitRenderer, glovesRenderer);
                EditorUtility.SetDirty(armsMesh);
                AssetDatabase.SaveAssets();
            }

            SkinnedMeshRenderer armsRenderer = FindFirstPersonArmsRenderer(root);
            bool changed = false;
            if (armsRenderer == null)
            {
                GameObject armsObject = new GameObject("FirstPersonArms");
                armsObject.transform.SetParent(suitRenderer.transform.parent, false);
                armsObject.transform.localPosition = suitRenderer.transform.localPosition;
                armsObject.transform.localRotation = suitRenderer.transform.localRotation;
                armsObject.transform.localScale = suitRenderer.transform.localScale;
                armsRenderer = armsObject.AddComponent<SkinnedMeshRenderer>();
                armsRenderer.enabled = false;
                changed = true;
            }

            changed |= CopySkinnedRendererBinding(suitRenderer, armsRenderer, armsMesh);
            if (!armsRenderer.updateWhenOffscreen)
            {
                armsRenderer.updateWhenOffscreen = true;
                changed = true;
            }
            return changed;
        }

        private static SkinnedMeshRenderer FindSuitRenderer(GameObject root)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name == "suit")
                {
                    return renderer;
                }
            }

            return null;
        }

        private static SkinnedMeshRenderer FindFirstPersonArmsRenderer(GameObject root)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name == "FirstPersonArms")
                {
                    return renderer;
                }
            }

            return null;
        }

        private static SkinnedMeshRenderer FindGlovesRenderer(GameObject root)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name == "gloves")
                {
                    return renderer;
                }
            }

            return null;
        }

        private static void PopulateFirstPersonArmsMesh(Mesh targetMesh, SkinnedMeshRenderer suitRenderer, SkinnedMeshRenderer glovesRenderer)
        {
            targetMesh.Clear();
            Mesh suitMesh = suitRenderer.sharedMesh;
            Transform[] bones = suitRenderer.bones;

            var armBones = new HashSet<int>();
            var forearmBones = new HashSet<int>();
            var upperBones = new HashSet<int>();
            var bodyBones = new HashSet<int>();

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                string n = bones[i].name.ToLowerInvariant();
                if (n.Contains("forearm") || n.Contains("hand")) { forearmBones.Add(i); armBones.Add(i); }
                else if (n.Contains("upperarm") || n.Contains("upper_arm")) { upperBones.Add(i); armBones.Add(i); }
                else { bodyBones.Add(i); }
                if (n.Contains("toe"))
                {
                    bodyBones.Add(i);
                }
                else if (n.Contains("forearm") || n.Contains("hand") || n.Contains("elbow") ||
                         n.Contains("thumb") || n.Contains("index") || n.Contains("mid") || 
                         n.Contains("ring") || n.Contains("pinky") || n.Contains("wrist"))
                {
                    forearmBones.Add(i);
                    armBones.Add(i);
                }
                else if (n.Contains("upperarm") || n.Contains("upper_arm"))
                {
                    upperBones.Add(i);
                    armBones.Add(i);
                }
                else
                {
                    bodyBones.Add(i);
                }
            }

            BoneWeight[] suitBW = suitMesh.boneWeights;
            Vector3[] suitVerts = suitMesh.vertices;
            Vector3[] suitNormals = suitMesh.normals;
            Vector4[] suitTangents = suitMesh.tangents;
            Vector2[] suitUV = suitMesh.uv;
            int[] suitTris = suitMesh.triangles;

            bool IsSuitArmVertex(int vi)
            {
                var w = suitBW[vi];
                float foreW = GetWeight(w, forearmBones);
                if (foreW > 0.05f) return true;
                float armW = GetWeight(w, armBones);
                float bodyW = GetWeight(w, bodyBones);
                return armW > 0.25f && armW >= bodyW;
            }

            var keptSuitTris = new List<int>();
            for (int i = 0; i < suitTris.Length; i += 3)
            {
                int a = suitTris[i], b = suitTris[i + 1], c = suitTris[i + 2];
                bool isA = IsSuitArmVertex(a), isB = IsSuitArmVertex(b), isC = IsSuitArmVertex(c);
                if (isA && isB && isC)
                {
                    keptSuitTris.Add(a); keptSuitTris.Add(b); keptSuitTris.Add(c);
                }
                else
                {
                    int armCount = (isA ? 1 : 0) + (isB ? 1 : 0) + (isC ? 1 : 0);
                    if (armCount >= 2)
                    {
                        int nonArm = !isA ? a : (!isB ? b : c);
                        if (GetWeight(suitBW[nonArm], armBones) > 0.1f)
                        {
                            keptSuitTris.Add(a); keptSuitTris.Add(b); keptSuitTris.Add(c);
                        }
                    }
                }
            }

            var newVerts = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newTangents = new List<Vector4>();
            var newUVs = new List<Vector2>();
            var newBW = new List<BoneWeight>();
            var newTris = new List<int>();

            var suitRemap = new Dictionary<int, int>();
            for (int i = 0; i < keptSuitTris.Count; i++)
            {
                int oldIdx = keptSuitTris[i];
                if (!suitRemap.TryGetValue(oldIdx, out int newIdx))
                {
                    newIdx = newVerts.Count;
                    suitRemap[oldIdx] = newIdx;
                    newVerts.Add(suitVerts[oldIdx]);
                    newNormals.Add(suitNormals != null && suitNormals.Length > oldIdx ? suitNormals[oldIdx] : Vector3.up);
                    newTangents.Add(suitTangents != null && suitTangents.Length > oldIdx ? suitTangents[oldIdx] : Vector4.zero);
                    newUVs.Add(suitUV != null && suitUV.Length > oldIdx ? suitUV[oldIdx] : Vector2.zero);
                    newBW.Add(suitBW[oldIdx]);
                }
                newTris.Add(newIdx);
            }

            if (glovesRenderer != null && glovesRenderer.sharedMesh != null)
            {
                Mesh glovesMesh = glovesRenderer.sharedMesh;
                Vector3[] gVerts = glovesMesh.vertices;
                Vector3[] gNormals = glovesMesh.normals;
                Vector4[] gTangents = glovesMesh.tangents;
                Vector2[] gUV = glovesMesh.uv;
                BoneWeight[] gBW = glovesMesh.boneWeights;
                int[] gTris = glovesMesh.triangles;

                int gBase = newVerts.Count;
                for (int i = 0; i < gVerts.Length; i++)
                {
                    newVerts.Add(gVerts[i]);
                    newNormals.Add(gNormals != null && gNormals.Length > i ? gNormals[i] : Vector3.up);
                    newTangents.Add(gTangents != null && gTangents.Length > i ? gTangents[i] : Vector4.zero);
                    newUVs.Add(gUV != null && gUV.Length > i ? gUV[i] : Vector2.zero);
                    newBW.Add(gBW[i]);
                }

                for (int i = 0; i < gTris.Length; i++)
                {
                    newTris.Add(gBase + gTris[i]);
                }
            }

            targetMesh.name = FirstPersonArmsMeshName;
            targetMesh.SetVertices(newVerts);
            targetMesh.SetNormals(newNormals);
            targetMesh.SetTangents(newTangents);
            targetMesh.SetUVs(0, newUVs);
            targetMesh.boneWeights = newBW.ToArray();
            targetMesh.bindposes = suitMesh.bindposes;
            targetMesh.SetTriangles(newTris, 0);
            targetMesh.RecalculateBounds();
        }

        private static float GetWeight(BoneWeight w, HashSet<int> bones)
        {
            float total = 0f;
            if (bones.Contains(w.boneIndex0)) total += w.weight0;
            if (bones.Contains(w.boneIndex1)) total += w.weight1;
            if (bones.Contains(w.boneIndex2)) total += w.weight2;
            if (bones.Contains(w.boneIndex3)) total += w.weight3;
            return total;
        }

        private static void EnsureFirstAidDefinitionsAreTeamTools()
        {
            EnsureItemDefinitionType(FirstAidDefinitionPath, InventoryItemType.TeamTool);
            EnsureItemDefinitionType(FakDefinitionPath, InventoryItemType.TeamTool);
        }

        private static void EnsureItemDefinitionType(string assetPath, InventoryItemType itemType)
        {
            InventoryItemDefinition definition = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);
            if (definition == null)
            {
                return;
            }

            var so = new SerializedObject(definition);
            SerializedProperty itemTypeProperty = so.FindProperty("itemType");
            if (itemTypeProperty != null && itemTypeProperty.enumValueIndex != (int)itemType)
            {
                itemTypeProperty.enumValueIndex = (int)itemType;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
        }

        private static void EnsureNoiseMakerBeaconPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NoiseMakerDeployedPrefabPath);
            if (prefab == null)
            {
                return;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(NoiseMakerDeployedPrefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                bool changed = false;
                if (root.GetComponent<NoiseMakerBeacon>() == null)
                {
                    root.AddComponent<NoiseMakerBeacon>();
                    changed = true;
                }
                if (root.GetComponent<Fusion.NetworkObject>() == null)
                {
                    root.AddComponent<Fusion.NetworkObject>();
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(root);
                }
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NoiseMakerDeployedPrefabPath);
            var labels = AssetDatabase.GetLabels(prefab);
            if (System.Array.IndexOf(labels, "FusionPrefab") < 0)
            {
                ArrayUtility.Add(ref labels, "FusionPrefab");
                AssetDatabase.SetLabels(prefab, labels);
            }
        }

        private static bool CopySkinnedRendererBinding(
            SkinnedMeshRenderer source,
            SkinnedMeshRenderer target,
            Mesh mesh)
        {
            bool changed = false;
            if (target.sharedMesh != mesh)
            {
                target.sharedMesh = mesh;
                changed = true;
            }
            if (target.rootBone != source.rootBone)
            {
                target.rootBone = source.rootBone;
                changed = true;
            }
            if (target.bones != source.bones)
            {
                target.bones = source.bones;
                changed = true;
            }
            if (target.sharedMaterials != source.sharedMaterials)
            {
                target.sharedMaterials = source.sharedMaterials;
                changed = true;
            }
            if (target.updateWhenOffscreen != source.updateWhenOffscreen)
            {
                target.updateWhenOffscreen = source.updateWhenOffscreen;
                changed = true;
            }
            return changed;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static bool SetStringArray(SerializedProperty property, params string[] values)
        {
            if (property == null)
            {
                return false;
            }

            bool changed = property.arraySize != values.Length;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.stringValue != values[i])
                {
                    element.stringValue = values[i];
                    changed = true;
                }
            }

            return changed;
        }

        private static void SetVector3(SerializedProperty property, Vector3 value)
        {
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetObject(SerializedProperty property, Object value)
        {
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static Fusion.NetworkObject GetNetworkPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab != null ? prefab.GetComponent<Fusion.NetworkObject>() : null;
        }

        private static void EnsureTeamToolPickupPrefab(
            string prefabPath,
            int toolId,
            string displayName,
            string visualSourcePath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                return;
            }

            EnsureAssetFolder("Assets/Prefabs/Gameplay/Imported");
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                root.AddComponent<Fusion.NetworkObject>();
                var collider = root.AddComponent<BoxCollider>();
                collider.isTrigger = false;
                collider.size = new Vector3(0.45f, 0.25f, 0.45f);

                var pickup = root.AddComponent<NetworkTeamToolPickup>();
                var pickupSo = new SerializedObject(pickup);
                pickupSo.FindProperty("_toolId").intValue = toolId;
                pickupSo.FindProperty("_toolDisplayName").stringValue = displayName;
                pickupSo.ApplyModifiedPropertiesWithoutUndo();

                var visualSource = AssetDatabase.LoadAssetAtPath<GameObject>(visualSourcePath);
                var sourceRenderer = visualSource != null
                    ? visualSource.GetComponentInChildren<MeshRenderer>(true)
                    : null;
                var sourceFilter = sourceRenderer != null
                    ? sourceRenderer.GetComponent<MeshFilter>()
                    : null;
                if (sourceRenderer != null && sourceFilter != null && sourceFilter.sharedMesh != null)
                {
                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(root.transform, false);
                    visual.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                    visual.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var labels = AssetDatabase.GetLabels(prefab);
                if (System.Array.IndexOf(labels, "FusionPrefab") < 0)
                {
                    ArrayUtility.Add(ref labels, "FusionPrefab");
                    AssetDatabase.SetLabels(prefab, labels);
                }
            }
        }

        private static void EnsureTeamToolDefinition(
            string assetPath,
            string itemId,
            string displayName,
            string worldPrefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath) != null)
            {
                return;
            }

            var definition = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            var definitionSo = new SerializedObject(definition);
            definitionSo.FindProperty("itemId").stringValue = itemId;
            definitionSo.FindProperty("displayName").stringValue = displayName;
            definitionSo.FindProperty("itemType").enumValueIndex = (int)InventoryItemType.TeamTool;
            SetObject(
                definitionSo.FindProperty("worldPrefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(worldPrefabPath));
            definitionSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, assetPath);
        }

        private static Object ResolveInputActions(GameObject root)
        {
            NetworkPlayerMovement movement = root.GetComponent<NetworkPlayerMovement>();
            if (movement == null)
            {
                return null;
            }

            var movementSo = new SerializedObject(movement);
            return movementSo.FindProperty("_inputActions").objectReferenceValue;
        }

        private static void CreateSectorBoxPrefabIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SectorBoxPrefabPath) != null) return;

            const string directory = "Assets/Resources/Network";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Network");
            }

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "NetworkSectorBox";
            root.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var collider = root.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            root.AddComponent<Fusion.NetworkObject>();
            root.AddComponent<NetworkSectorBox>();
            PrefabUtility.SaveAsPrefabAsset(root, SectorBoxPrefabPath, out var saved);
            Object.DestroyImmediate(root);
            if (saved) Debug.Log("[M2 Prefab Upgrade] Created NetworkSectorBox prefab.");
            else Debug.LogError("[M2 Prefab Upgrade] Failed to create NetworkSectorBox prefab.");
        }
    }
}
