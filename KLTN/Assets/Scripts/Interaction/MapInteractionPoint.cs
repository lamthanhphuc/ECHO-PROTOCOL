using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class MapInteractionPoint : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionId;
    [SerializeField] private string prompt = "Interact";
    [SerializeField] private bool requireEnabled = true;
    [SerializeField] private UnityEvent interacted;

    public string InteractionId => interactionId;
    public string InteractionPrompt => prompt;
    public bool IsEnabled { get; private set; } = true;

    public bool CanInteract(GameObject interactor)
    {
        return !requireEnabled || IsEnabled;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        interacted?.Invoke();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
