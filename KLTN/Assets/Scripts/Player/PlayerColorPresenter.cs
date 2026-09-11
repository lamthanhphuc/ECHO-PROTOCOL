using System;
using EchoProtocol.Networking;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class PlayerColorPresenter : MonoBehaviour
{
    [Serializable]
    public struct CharacterColorSet
    {
        public string name;
        public Material suitMaterial;
        public Material helmetBackpackMaterial;
        public Material visorMaterial;
    }

    [Header("Color Sets (0=P1 Default, 1=P2 Orange, 2=P3 Green, 3=P4 Purple)")]
    [SerializeField] private CharacterColorSet[] colorSets = new CharacterColorSet[4];

    [Header("References")]
    [SerializeField] private LobbyPlayerState lobbyState;

    private int _appliedTeamId = -1;

    public int AppliedTeamId => _appliedTeamId;

    private void Awake()
    {
        if (lobbyState == null)
        {
            lobbyState = GetComponent<LobbyPlayerState>() ?? GetComponentInParent<LobbyPlayerState>();
        }
        EnsureColorSetsLoaded();
    }

    private void OnEnable()
    {
        LobbyPlayerState.AnyStateChanged += HandleAnyStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LobbyPlayerState.AnyStateChanged -= HandleAnyStateChanged;
    }

    private void Start()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        int currentTeamId = GetCurrentTeamId();
        if (_appliedTeamId != currentTeamId)
        {
            Refresh();
        }
    }

    private void HandleAnyStateChanged()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (lobbyState == null)
        {
            lobbyState = GetComponent<LobbyPlayerState>() ?? GetComponentInParent<LobbyPlayerState>();
        }

        if (lobbyState == null || lobbyState.Object == null || !lobbyState.Object.IsValid)
        {
            return;
        }

        int teamId = GetCurrentTeamId();
        ApplyColor(teamId);
    }

    private int GetCurrentTeamId()
    {
        if (lobbyState == null || lobbyState.Object == null || !lobbyState.Object.IsValid)
        {
            return 0;
        }

        return lobbyState.TeamId;
    }

    public void ApplyColor(int teamId)
    {
        EnsureColorSetsLoaded();

        if (colorSets == null || colorSets.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(teamId, 0, colorSets.Length - 1);
        CharacterColorSet set = colorSets[index];

        if (set.suitMaterial == null && set.helmetBackpackMaterial == null)
        {
            return;
        }

        Transform root = transform.root;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null || r.gameObject == root.gameObject)
            {
                continue;
            }

            string rName = r.name.ToLowerInvariant();

            // Backpack equipment
            if (rName.Contains("backpack"))
            {
                if (set.helmetBackpackMaterial != null)
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = set.helmetBackpackMaterial;
                    }
                    r.sharedMaterials = mats;
                }
            }
            // Helmet equipment
            else if (rName.Contains("helmet"))
            {
                Material[] mats = r.sharedMaterials;
                if (mats != null && mats.Length > 0)
                {
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string matName = mats[i] != null ? mats[i].name.ToLowerInvariant() : string.Empty;
                        bool isVisor = matName.Contains("visor") || (mats.Length >= 4 && i == 3) || (mats.Length == 3 && i == 2);
                        mats[i] = isVisor && set.visorMaterial != null ? set.visorMaterial : set.helmetBackpackMaterial;
                    }
                    r.sharedMaterials = mats;
                }
            }
            // Suit and body parts (suit, gloves, balaclava, boots, face, FirstPersonArms, etc.)
            else if (rName.Contains("suit") || rName.Contains("arm") || rName.Contains("hand") ||
                     rName.Contains("glove") || rName.Contains("boot") || rName.Contains("balaclava") ||
                     rName.Contains("face") || rName.Contains("body"))
            {
                if (set.suitMaterial != null)
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = set.suitMaterial;
                    }
                    r.sharedMaterials = mats;
                }
            }
        }

        _appliedTeamId = teamId;
    }

    private void EnsureColorSetsLoaded()
    {
        bool needLoad = colorSets == null || colorSets.Length < 4;
        if (!needLoad)
        {
            for (int i = 0; i < 4; i++)
            {
                if (colorSets[i].suitMaterial == null)
                {
                    needLoad = true;
                    break;
                }
            }
        }

        if (!needLoad) return;

#if UNITY_EDITOR
        PopulateFromEditor();
#endif
    }

#if UNITY_EDITOR
    private void Reset()
    {
        PopulateFromEditor();
    }

    [ContextMenu("Populate Color Sets")]
    public void PopulateFromEditor()
    {
        colorSets = new CharacterColorSet[4];
        string[] variants = { "PF_PlayerCharacter_P1_Default", "PF_PlayerCharacter_P2_Orange", "PF_PlayerCharacter_P3_Green", "PF_PlayerCharacter_P4_Purple" };
        for (int i = 0; i < 4; i++)
        {
            colorSets[i].name = variants[i];
            colorSets[i].suitMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/PlayerCharacter/M_{variants[i]}_Suit.mat");
            colorSets[i].helmetBackpackMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/PlayerCharacter/M_{variants[i]}_HelmetBackpackBase.mat");
            colorSets[i].visorMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/PlayerCharacter/M_{variants[i]}_Visor.mat");
        }
        EditorUtility.SetDirty(this);
    }
#endif
}
