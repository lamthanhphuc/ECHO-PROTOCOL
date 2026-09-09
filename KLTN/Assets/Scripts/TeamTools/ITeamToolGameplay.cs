using UnityEngine;

public interface ITeamToolGameplay
{
    void Equip(GameObject owner, Transform aimOrigin);
    void Unequip();
    bool TryUse();
}
