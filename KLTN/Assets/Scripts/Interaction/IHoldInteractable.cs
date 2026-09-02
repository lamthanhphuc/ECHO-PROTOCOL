using UnityEngine;

public interface IHoldInteractable : IInteractable
{
    bool RequiresHold { get; }
    void BeginHoldInteract(GameObject interactor);
    void EndHoldInteract(GameObject interactor);
}
