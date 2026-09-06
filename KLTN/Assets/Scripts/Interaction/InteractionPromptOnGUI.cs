using UnityEngine;

public class InteractionPromptOnGUI : MonoBehaviour
{
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private EchoProtocol.Networking.NetworkPlayerInteractor networkInteractor;
    [SerializeField] private Vector2 boxSize = new Vector2(360f, 44f);
    [SerializeField] private float bottomOffset = 96f;

    private GUIStyle _style;

    private void Awake()
    {
        if (FindAnyObjectByType<EchoProtocol.UI.HUD.HUDInteractionPrompt>() != null)
        {
            enabled = false;
            return;
        }

        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
        }

        if (networkInteractor == null)
        {
            networkInteractor = GetComponent<EchoProtocol.Networking.NetworkPlayerInteractor>();
        }
    }

    private void OnGUI()
    {
        if (FindAnyObjectByType<EchoProtocol.UI.HUD.HUDInteractionPrompt>() != null)
        {
            return;
        }

        string prompt = null;
        if (interaction != null && !string.IsNullOrWhiteSpace(interaction.CurrentPrompt))
        {
            prompt = interaction.CurrentPrompt;
        }
        else if (networkInteractor != null && networkInteractor.CurrentCandidate != null)
        {
            prompt = networkInteractor.CurrentCandidate.InteractionPrompt;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _style ??= CreateStyle();

        Rect rect = new Rect(
            (Screen.width - boxSize.x) * 0.5f,
            Screen.height - bottomOffset,
            boxSize.x,
            boxSize.y);

        GUI.Box(rect, prompt, _style);
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
