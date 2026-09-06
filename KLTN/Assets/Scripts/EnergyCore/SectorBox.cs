using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SectorBox : MonoBehaviour, IInteractable
{
    [SerializeField] private EnergyCoreObjectiveProgress objectiveProgress;
    [SerializeField] private string placePrompt = "Nạp Energy Core vào Sector Box";
    [SerializeField] private string completePrompt = "Đã nạp đủ Energy Core";

    [Header("Core Visual Sockets")]
    [SerializeField] private GameObject[] _coreVisuals = System.Array.Empty<GameObject>();
    [SerializeField] private Transform[] _coreSockets = System.Array.Empty<Transform>();
    [SerializeField, Min(1)] private int _maxCoreCapacity = 2;

    private int _placedCoreCount;

    public int MaxCoreCapacity => _maxCoreCapacity;
    public int PlacedCoreCount => Mathf.Max(_placedCoreCount, objectiveProgress != null ? objectiveProgress.PlacedCoreCount : 0);
    public Transform[] CoreSockets => _coreSockets;
    public GameObject[] CoreVisuals => _coreVisuals;

    public string InteractionPrompt
    {
        get
        {
            if (objectiveProgress != null && objectiveProgress.IsComplete)
            {
                return string.IsNullOrWhiteSpace(completePrompt) || completePrompt == "Sector Box complete" ? "Đã nạp đủ Energy Core" : completePrompt;
            }

            return string.IsNullOrWhiteSpace(placePrompt) || placePrompt == "Place Energy Core" ? "Nạp Energy Core vào Sector Box" : placePrompt;
        }
    }

    private void Awake()
    {
        if (objectiveProgress == null)
        {
            objectiveProgress = GetComponent<EnergyCoreObjectiveProgress>();
        }
        UpdateCoreVisuals(PlacedCoreCount);
    }

    private void OnEnable()
    {
        if (objectiveProgress != null)
        {
            objectiveProgress.ProgressChanged += OnProgressChanged;
        }
        UpdateCoreVisuals(PlacedCoreCount);
    }

    private void OnDisable()
    {
        if (objectiveProgress != null)
        {
            objectiveProgress.ProgressChanged -= OnProgressChanged;
        }
    }

    private void OnProgressChanged(int placed, int required)
    {
        UpdateCoreVisuals(Mathf.Max(_placedCoreCount, placed));
    }

    public void UpdateCoreVisuals(int placedCount)
    {
        _placedCoreCount = Mathf.Clamp(placedCount, 0, _maxCoreCapacity);
        if (_coreVisuals != null)
        {
            for (int i = 0; i < _coreVisuals.Length; i++)
            {
                if (_coreVisuals[i] != null)
                {
                    bool active = i < _placedCoreCount;
                    _coreVisuals[i].SetActive(active);
                    if (active)
                    {
                        foreach (Transform child in _coreVisuals[i].transform)
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        return carrier != null && carrier.IsCarrying && CanAcceptCore(carrier);
    }

    public void Interact(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        if (carrier != null)
        {
            carrier.PlaceCoreInSectorBox(this);
        }
    }

    public bool CanAcceptCore(PlayerEnergyCoreCarrier carrier)
    {
        return carrier != null
            && carrier.IsCarrying
            && (objectiveProgress == null || !objectiveProgress.IsComplete)
            && PlacedCoreCount < _maxCoreCapacity;
    }

    public void AcceptPlacedCore()
    {
        _placedCoreCount = Mathf.Clamp(_placedCoreCount + 1, 0, _maxCoreCapacity);
        UpdateCoreVisuals(_placedCoreCount);

        if (objectiveProgress != null)
        {
            objectiveProgress.RegisterCorePlaced();
        }
    }

    private static PlayerEnergyCoreCarrier GetCarrier(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInParent<PlayerEnergyCoreCarrier>() : null;
    }
}
