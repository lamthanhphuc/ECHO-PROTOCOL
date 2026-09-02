using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class SciFiPlayerCharacterSceneSetup
{
    private const string RunMarker = "Assets/Editor/.run_scifi_player_character_scene_setup";
    private const string SciFiScenePath = "Assets/Scenes/SciFi.unity";
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab";
    private const string AnimatorControllerPath = "Assets/Animations/Player/AC_PlayerCharacter.controller";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string LocalPlayerVisualLayer = "LocalPlayerVisual";
    private const string PlayerVisualName = "Player_Visual";

    [InitializeOnLoadMethod]
    private static void RunRequestedSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker)))
            {
                return;
            }

            File.Delete(ToAbsolutePath(RunMarker));
            SetupActiveSciFiPlayer();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Setup SciFi Player Character")]
    public static void SetupActiveSciFiPlayer()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != SciFiScenePath)
        {
            Debug.LogError("[SciFiPlayerCharacterSceneSetup] Open the SciFi scene first: " + SciFiScenePath);
            return;
        }

        EnsurePlayerTag();
        int localVisualLayer = EnsureLayer(LocalPlayerVisualLayer);

        PlayerMovement movement = FindScenePlayerMovement();
        if (movement == null)
        {
            Debug.LogError("[SciFiPlayerCharacterSceneSetup] No PlayerMovement found in SciFi scene.");
            return;
        }

        GameObject player = movement.gameObject;
        player.tag = "Player";

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        Camera mainCamera = EnsureMainCamera(player.transform);
        PlayerCamera playerCamera = ConfigurePlayerCamera(mainCamera, player.transform, inputActions);
        CharacterController characterController = ConfigureCharacterController(player);

        ConfigureMovement(movement, inputActions);
        PlayerInteraction interaction = ConfigureInteraction(player, inputActions, mainCamera);
        PlayerInventory inventory = EnsureComponent<PlayerInventory>(player);
        ConfigureInventoryDrop(player, inventory, mainCamera.transform);
        ConfigureEnergyCoreCarrier(player, inputActions, inventory, movement, mainCamera.transform);
        PlayerHidingController hidingController = ConfigureHiding(player, inputActions, mainCamera, movement, interaction);

        Transform visualRoot = AttachLocalPlayerVisual(player.transform, characterController, localVisualLayer);
        RemoveEmptyRootAnimationComponents(player);

        PlayerDownState downState = ConfigureDownState(player, movement, interaction, hidingController, playerCamera, visualRoot);
        ConfigureAnimatorDriver(player, visualRoot, movement, characterController, player.GetComponent<PlayerEnergyCoreCarrier>(), downState);
        ConfigureRevive(player, downState);
        ConfigureSpectate(player, downState, playerCamera);

        if (localVisualLayer >= 0)
        {
            mainCamera.cullingMask &= ~(1 << localVisualLayer);
        }

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(mainCamera);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Selection.activeGameObject = player;

        Debug.Log("[SciFiPlayerCharacterSceneSetup] SciFi player character setup complete. Local visual attached and gameplay references configured.");
    }

    private static PlayerMovement FindScenePlayerMovement()
    {
        PlayerMovement[] movements = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include);
        foreach (PlayerMovement movement in movements)
        {
            if (movement.gameObject.scene == SceneManager.GetActiveScene() &&
                (movement.name == "FirstPersonPlayer" || movement.name == "Player"))
            {
                return movement;
            }
        }

        foreach (PlayerMovement movement in movements)
        {
            if (movement.gameObject.scene == SceneManager.GetActiveScene())
            {
                return movement;
            }
        }

        return null;
    }

    private static Camera EnsureMainCamera(Transform player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        mainCamera.transform.position = player.position + Vector3.up * 1.65f;
        mainCamera.transform.rotation = player.rotation;
        return mainCamera;
    }

    private static PlayerCamera ConfigurePlayerCamera(Camera mainCamera, Transform target, InputActionAsset inputActions)
    {
        PlayerCamera playerCamera = EnsureComponent<PlayerCamera>(mainCamera.gameObject);
        SerializedObject cameraSo = new SerializedObject(playerCamera);
        SetObject(cameraSo, "target", target);
        SetObject(cameraSo, "inputActions", inputActions);
        SetFloat(cameraSo, "mouseSensitivity", 0.12f);
        SetFloat(cameraSo, "eyeHeight", 1.65f);
        SetFloat(cameraSo, "crouchEyeHeight", 1.05f);
        SetFloat(cameraSo, "eyeHeightTransitionSpeed", 10f);
        SetBool(cameraSo, "lockCursorOnEnable", true);
        cameraSo.ApplyModifiedPropertiesWithoutUndo();
        return playerCamera;
    }

    private static CharacterController ConfigureCharacterController(GameObject player)
    {
        CharacterController controller = EnsureComponent<CharacterController>(player);
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;
        return controller;
    }

    private static void ConfigureMovement(PlayerMovement movement, InputActionAsset inputActions)
    {
        SerializedObject movementSo = new SerializedObject(movement);
        SetObject(movementSo, "inputActions", inputActions);
        SetFloat(movementSo, "walkSpeed", 4f);
        SetFloat(movementSo, "sprintSpeed", 7f);
        SetFloat(movementSo, "crouchSpeed", 2f);
        SetFloat(movementSo, "sprintForwardInputThreshold", 0.75f);
        SetFloat(movementSo, "sprintSideInputTolerance", 0.2f);
        SetFloat(movementSo, "standingHeight", 2f);
        SetFloat(movementSo, "crouchHeight", 1.2f);
        SetFloat(movementSo, "maxStamina", 100f);
        SetFloat(movementSo, "sprintStaminaDrainPerSecond", 25f);
        SetFloat(movementSo, "staminaRegenPerSecond", 18f);
        SetFloat(movementSo, "minStaminaToSprint", 5f);
        movementSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerInteraction ConfigureInteraction(GameObject player, InputActionAsset inputActions, Camera mainCamera)
    {
        PlayerInteraction interaction = EnsureComponent<PlayerInteraction>(player);
        SerializedObject interactionSo = new SerializedObject(interaction);
        SetObject(interactionSo, "inputActions", inputActions);
        SetObject(interactionSo, "raycastCamera", mainCamera);
        SetFloat(interactionSo, "interactDistance", 3f);
        interactionSo.ApplyModifiedPropertiesWithoutUndo();

        InteractionPromptOnGUI prompt = EnsureComponent<InteractionPromptOnGUI>(player);
        SerializedObject promptSo = new SerializedObject(prompt);
        SetObject(promptSo, "interaction", interaction);
        promptSo.ApplyModifiedPropertiesWithoutUndo();
        return interaction;
    }

    private static void ConfigureInventoryDrop(GameObject player, PlayerInventory inventory, Transform dropOrigin)
    {
        PlayerInventoryDropInput dropInput = EnsureComponent<PlayerInventoryDropInput>(player);
        SerializedObject dropInputSo = new SerializedObject(dropInput);
        SetObject(dropInputSo, "inventory", inventory);
        SetObject(dropInputSo, "dropOrigin", dropOrigin);
        SetFloat(dropInputSo, "dropForwardDistance", 1.25f);
        dropInputSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEnergyCoreCarrier(
        GameObject player,
        InputActionAsset inputActions,
        PlayerInventory inventory,
        PlayerMovement movement,
        Transform dropOrigin)
    {
        PlayerEnergyCoreCarrier carrier = EnsureComponent<PlayerEnergyCoreCarrier>(player);
        SerializedObject carrierSo = new SerializedObject(carrier);
        SetObject(carrierSo, "inputActions", inputActions);
        SetObject(carrierSo, "inventory", inventory);
        SetObject(carrierSo, "movement", movement);
        SetObject(carrierSo, "dropOrigin", dropOrigin);
        SetFloat(carrierSo, "carrySpeedMultiplier", 0.72f);
        SetBool(carrierSo, "blockSprintWhileCarrying", true);
        SetBool(carrierSo, "lockTeamToolWhileCarrying", true);
        carrierSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerHidingController ConfigureHiding(
        GameObject player,
        InputActionAsset inputActions,
        Camera mainCamera,
        PlayerMovement movement,
        PlayerInteraction interaction)
    {
        PlayerHidingController hidingController = EnsureComponent<PlayerHidingController>(player);
        SerializedObject hidingSo = new SerializedObject(hidingController);
        SetObject(hidingSo, "inputActions", inputActions);
        SetObject(hidingSo, "playerCamera", mainCamera);
        SetObject(hidingSo, "movement", movement);
        SetObject(hidingSo, "interaction", interaction);
        hidingSo.ApplyModifiedPropertiesWithoutUndo();
        return hidingController;
    }

    private static Transform AttachLocalPlayerVisual(Transform player, CharacterController characterController, int layer)
    {
        Transform existing = player.Find(PlayerVisualName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[SciFiPlayerCharacterSceneSetup] Missing player visual prefab: " + PlayerVisualPrefabPath);
            return null;
        }

        GameObject visual = PrefabUtility.InstantiatePrefab(prefab, player) as GameObject;
        if (visual == null)
        {
            Debug.LogError("[SciFiPlayerCharacterSceneSetup] Could not instantiate player visual prefab.");
            return null;
        }

        visual.name = PlayerVisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        if (characterController != null)
        {
            AlignVisualBottomToController(visual.transform, characterController);
        }

        if (layer >= 0)
        {
            SetLayerRecursively(visual, layer);
        }

        return visual.transform;
    }

    private static void RemoveEmptyRootAnimationComponents(GameObject player)
    {
        Animator rootAnimator = player.GetComponent<Animator>();
        if (rootAnimator != null && rootAnimator.avatar == null && rootAnimator.runtimeAnimatorController == null)
        {
            Object.DestroyImmediate(rootAnimator);
        }

        Animation legacyAnimation = player.GetComponent<Animation>();
        if (legacyAnimation != null)
        {
            Object.DestroyImmediate(legacyAnimation);
        }
    }

    private static void AlignVisualBottomToController(Transform visual, CharacterController characterController)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float controllerBottom = characterController.transform.position.y + characterController.center.y - characterController.height * 0.5f;
        visual.position += Vector3.up * (controllerBottom - bounds.min.y);
    }

    private static void ConfigureAnimatorDriver(
        GameObject player,
        Transform visualRoot,
        PlayerMovement movement,
        CharacterController characterController,
        PlayerEnergyCoreCarrier coreCarrier,
        PlayerDownState downState)
    {
        if (visualRoot == null)
        {
            return;
        }

        Animator visualAnimator = visualRoot.GetComponentInChildren<Animator>(true);
        if (visualAnimator == null)
        {
            Debug.LogError("[SciFiPlayerCharacterSceneSetup] Player visual has no Animator.");
            return;
        }

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
        if (controller != null)
        {
            visualAnimator.runtimeAnimatorController = controller;
        }

        foreach (PlayerAnimatorDriver childDriver in visualRoot.GetComponentsInChildren<PlayerAnimatorDriver>(true))
        {
            Object.DestroyImmediate(childDriver);
        }

        PlayerAnimatorDriver animatorDriver = EnsureComponent<PlayerAnimatorDriver>(player);
        if (animatorDriver == null)
        {
            return;
        }

        SerializedObject driverSo = new SerializedObject(animatorDriver);
        SetObject(driverSo, "animator", visualAnimator);
        SetObject(driverSo, "movement", movement);
        SetObject(driverSo, "characterController", characterController);
        SetObject(driverSo, "coreCarrier", coreCarrier);
        SetObject(driverSo, "downState", downState);
        SetFloat(driverSo, "walkSpeedReference", 4f);
        SetFloat(driverSo, "runSpeedReference", 7f);
        SetFloat(driverSo, "speedDampTime", 0.16f);
        SetFloat(driverSo, "directionDampTime", 0.12f);
        driverSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerDownState ConfigureDownState(
        GameObject player,
        PlayerMovement movement,
        PlayerInteraction interaction,
        PlayerHidingController hidingController,
        PlayerCamera playerCamera,
        Transform visualRoot)
    {
        PlayerDownState downState = EnsureComponent<PlayerDownState>(player);
        SerializedObject downStateSo = new SerializedObject(downState);
        SetObject(downStateSo, "movement", movement);
        SetObject(downStateSo, "interaction", interaction);
        SetObject(downStateSo, "hidingController", hidingController);
        SetObject(downStateSo, "playerCamera", playerCamera);
        SetObject(downStateSo, "visualRoot", visualRoot);
        SetFloat(downStateSo, "maxHealth", 100f);
        SetFloat(downStateSo, "bleedoutSeconds", 45f);
        SetFloat(downStateSo, "crawlSpeedMultiplier", 0.32f);
        SetFloat(downStateSo, "downedCameraEyeHeight", 0.55f);
        SetFloat(downStateSo, "revivedHealth", 35f);
        SetFloat(downStateSo, "reviveProtectionSeconds", 3f);
        downStateSo.ApplyModifiedPropertiesWithoutUndo();
        return downState;
    }

    private static void ConfigureRevive(GameObject player, PlayerDownState downState)
    {
        PlayerReviveInteractable revive = EnsureComponent<PlayerReviveInteractable>(player);
        SerializedObject reviveSo = new SerializedObject(revive);
        SetObject(reviveSo, "downState", downState);
        SetFloat(reviveSo, "reviveDurationSeconds", 2.5f);
        SetString(reviveSo, "revivePrompt", "Revive teammate");
        reviveSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSpectate(GameObject player, PlayerDownState downState, PlayerCamera playerCamera)
    {
        PlayerSpectateController spectate = EnsureComponent<PlayerSpectateController>(player);
        SerializedObject spectateSo = new SerializedObject(spectate);
        SetObject(spectateSo, "downState", downState);
        SetObject(spectateSo, "playerCamera", playerCamera);
        SetBool(spectateSo, "autoSpectateWhenEliminated", true);
        spectateSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void EnsurePlayerTag()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == "Player")
            {
                return;
            }
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Player";
        tagManager.ApplyModifiedProperties();
    }

    private static int EnsureLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
            {
                return i;
            }
        }

        for (int layerIndex = 10; layerIndex < layers.arraySize; layerIndex++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return layerIndex;
            }
        }

        Debug.LogWarning("[SciFiPlayerCharacterSceneSetup] No free user layer slot available for " + layerName);
        return -1;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }
}
