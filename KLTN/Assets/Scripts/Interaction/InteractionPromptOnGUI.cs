using UnityEngine;

public class InteractionPromptOnGUI : MonoBehaviour
{
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private Vector2 boxSize = new Vector2(360f, 44f);
    [SerializeField] private float bottomOffset = 96f;

    private GUIStyle _style;

    private void Awake()
    {
        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
        }
    }

    private void OnGUI()
    {
        if (interaction == null || string.IsNullOrWhiteSpace(interaction.CurrentPrompt))
        {
            return;
        }

        _style ??= CreateStyle();

        Rect rect = new Rect(
            (Screen.width - boxSize.x) * 0.5f,
            Screen.height - bottomOffset,
            boxSize.x,
            boxSize.y);

        GUI.Box(rect, interaction.CurrentPrompt, _style);
    }

    private static GUIStyle CreateStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            wordWrap = false
        };

        style.normal.textColor = Color.white;
        return style;
    }
}
