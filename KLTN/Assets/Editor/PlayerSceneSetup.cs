using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class PlayerSceneSetup
{
    private const string PlayerName = "Player";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("KLTN/Setup WASD Player")]
    public static void SetupWasdPlayer()
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
        controller.center = new Vector3(0f, 0f, 0f);

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            movement = player.AddComponent<PlayerMovement>();
        }

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
        {
            Debug.LogError($"[KLTN] Không tìm thấy Input Actions tại {InputActionsPath}");
        }
        else
        {
            SerializedObject movementSo = new SerializedObject(movement);
            movementSo.FindProperty("inputActions").objectReferenceValue = inputActions;
            movementSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[KLTN] Không tìm thấy Main Camera trong scene.");
            return;
        }

        PlayerCamera playerCamera = mainCamera.GetComponent<PlayerCamera>();
        if (playerCamera == null)
        {
            playerCamera = mainCamera.gameObject.AddComponent<PlayerCamera>();
        }

        SerializedObject cameraSo = new SerializedObject(playerCamera);
        cameraSo.FindProperty("target").objectReferenceValue = player.transform;
        cameraSo.FindProperty("offset").vector3Value = new Vector3(0f, 2f, -5f);
        cameraSo.FindProperty("smoothSpeed").floatValue = 5f;
        cameraSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;

        Debug.Log("[KLTN] WASD Player setup hoàn tất. Nhấn Play và dùng WASD để di chuyển.");
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
