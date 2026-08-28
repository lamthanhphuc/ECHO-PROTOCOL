using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class PlayerSceneSetup
{
    private const string PlayerName = "Player";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("KLTN/Setup FPS Player")]
    public static void SetupFpsPlayer()
    {
        EnsurePlayerTag();

        GameObject player = GameObject.Find(PlayerName);
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = PlayerName;
            player.transform.position = new Vector3(0f, 1f, 0f);

            Object.DestroyImmediate(player.GetComponent<Collider>());
        }

        player.tag = "Player";

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = player.AddComponent<CharacterController>();
        }

        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            movement = player.AddComponent<PlayerMovement>();
        }

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
        {
            Debug.LogError("[KLTN] Input Actions not found at " + InputActionsPath);
        }
        else
        {
            SerializedObject movementSo = new SerializedObject(movement);
            movementSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            movementSo.FindProperty("walkSpeed").floatValue = 4f;
            movementSo.FindProperty("sprintSpeed").floatValue = 7f;
            movementSo.FindProperty("crouchSpeed").floatValue = 2f;
            movementSo.FindProperty("standingHeight").floatValue = 2f;
            movementSo.FindProperty("crouchHeight").floatValue = 1.2f;
            movementSo.FindProperty("maxStamina").floatValue = 100f;
            movementSo.FindProperty("sprintStaminaDrainPerSecond").floatValue = 25f;
            movementSo.FindProperty("staminaRegenPerSecond").floatValue = 18f;
            movementSo.FindProperty("minStaminaToSprint").floatValue = 5f;
            movementSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            Debug.Log("[KLTN] Main Camera was missing, created a new FPS Main Camera.");
        }

        PlayerCamera playerCamera = mainCamera.GetComponent<PlayerCamera>();
        if (playerCamera == null)
        {
            playerCamera = mainCamera.gameObject.AddComponent<PlayerCamera>();
        }

        SerializedObject cameraSo = new SerializedObject(playerCamera);
        cameraSo.FindProperty("target").objectReferenceValue = player.transform;
        cameraSo.FindProperty("inputActions").objectReferenceValue = inputActions;
        cameraSo.FindProperty("mouseSensitivity").floatValue = 0.12f;
        cameraSo.FindProperty("eyeHeight").floatValue = 1.65f;
        cameraSo.FindProperty("crouchEyeHeight").floatValue = 1.05f;
        cameraSo.FindProperty("eyeHeightTransitionSpeed").floatValue = 10f;
        cameraSo.FindProperty("lockCursorOnEnable").boolValue = true;
        cameraSo.ApplyModifiedPropertiesWithoutUndo();

        mainCamera.transform.position = player.transform.position + Vector3.up * 1.65f;
        mainCamera.transform.rotation = player.transform.rotation;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = player.AddComponent<PlayerInventory>();
        }

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction == null)
        {
            interaction = player.AddComponent<PlayerInteraction>();
        }

        SerializedObject interactionSo = new SerializedObject(interaction);
        interactionSo.FindProperty("inputActions").objectReferenceValue = inputActions;
        interactionSo.FindProperty("raycastCamera").objectReferenceValue = mainCamera;
        interactionSo.FindProperty("interactDistance").floatValue = 3f;
        interactionSo.ApplyModifiedPropertiesWithoutUndo();

        InteractionPromptOnGUI prompt = player.GetComponent<InteractionPromptOnGUI>();
        if (prompt == null)
        {
            prompt = player.AddComponent<InteractionPromptOnGUI>();
        }

        SerializedObject promptSo = new SerializedObject(prompt);
        promptSo.FindProperty("interaction").objectReferenceValue = interaction;
        promptSo.ApplyModifiedPropertiesWithoutUndo();

        PlayerInventoryDropInput dropInput = player.GetComponent<PlayerInventoryDropInput>();
        if (dropInput == null)
        {
            dropInput = player.AddComponent<PlayerInventoryDropInput>();
        }

        PlayerEnergyCoreCarrier energyCoreCarrier = player.GetComponent<PlayerEnergyCoreCarrier>();
        if (energyCoreCarrier == null)
        {
            energyCoreCarrier = player.AddComponent<PlayerEnergyCoreCarrier>();
        }

        SerializedObject dropInputSo = new SerializedObject(dropInput);
        dropInputSo.FindProperty("inventory").objectReferenceValue = inventory;
        dropInputSo.FindProperty("dropOrigin").objectReferenceValue = mainCamera.transform;
        dropInputSo.FindProperty("dropForwardDistance").floatValue = 1.25f;
        dropInputSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject energyCoreCarrierSo = new SerializedObject(energyCoreCarrier);
        energyCoreCarrierSo.FindProperty("inputActions").objectReferenceValue = inputActions;
        energyCoreCarrierSo.FindProperty("inventory").objectReferenceValue = inventory;
        energyCoreCarrierSo.FindProperty("movement").objectReferenceValue = movement;
        energyCoreCarrierSo.FindProperty("dropOrigin").objectReferenceValue = mainCamera.transform;
        energyCoreCarrierSo.FindProperty("carrySpeedMultiplier").floatValue = 0.72f;
        energyCoreCarrierSo.FindProperty("blockSprintWhileCarrying").boolValue = true;
        energyCoreCarrierSo.FindProperty("lockTeamToolWhileCarrying").boolValue = true;
        energyCoreCarrierSo.ApplyModifiedPropertiesWithoutUndo();

        PlayerHidingController hidingController = player.GetComponent<PlayerHidingController>();
        if (hidingController == null)
        {
            hidingController = player.AddComponent<PlayerHidingController>();
        }

        SerializedObject hidingSo = new SerializedObject(hidingController);
        hidingSo.FindProperty("inputActions").objectReferenceValue = inputActions;
        hidingSo.FindProperty("playerCamera").objectReferenceValue = mainCamera;
        hidingSo.FindProperty("movement").objectReferenceValue = movement;
        hidingSo.FindProperty("interaction").objectReferenceValue = interaction;
        hidingSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;

        Debug.Log("[KLTN] FPS Player setup complete. WASD move, mouse look, Shift sprint, C crouch, hold E interact, 1/2 select slot, G drop, T drop tool. Hiding controller is ready for HidingSpot lockers.");
    }

    private static void EnsurePlayerTag()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

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
}
