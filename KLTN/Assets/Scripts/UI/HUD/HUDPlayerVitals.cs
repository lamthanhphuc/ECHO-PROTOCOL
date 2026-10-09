using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EchoProtocol.Networking;

namespace EchoProtocol.UI.HUD
{
    public class HUDPlayerVitals : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private NetworkPlayerMovement networkMovement;
        [SerializeField] private PlayerDownState downState;
        [SerializeField] private PlayerEnergyCoreCarrier carrier;

        [Header("Stamina UI")]
        [SerializeField] private Image staminaBarFill;
        [SerializeField] private Text staminaValueText;
        [SerializeField] private TMP_Text staminaValueTmp;
        [SerializeField] private Color staminaNormalColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color staminaLowColor = new Color(1f, 0.3f, 0.1f, 1f);

        [Header("Status UI")]
        [SerializeField] private Image statusBadgeBg;
        [SerializeField] private Text statusLabelText;
        [SerializeField] private TMP_Text statusLabelTmp;
        [SerializeField] private GameObject bleedoutContainer;
        [SerializeField] private Image bleedoutBarFill;
        [SerializeField] private Text bleedoutTimerText;
        [SerializeField] private TMP_Text bleedoutTimerTmp;

        [Header("Noise Indicator UI")]
        [SerializeField] private Image noiseIcon;
        [SerializeField] private Text noiseLabel;
        [SerializeField] private TMP_Text noiseLabelTmp;
        [SerializeField] private CanvasGroup noiseCanvasGroup;

        private float _displayStamina = 1f;
        private Image _healthBarFill;
        private float _noiseIntensity;

        public void BindPlayer(
            PlayerMovement move,
            PlayerDownState down,
            PlayerEnergyCoreCarrier coreCarrier,
            NetworkPlayerMovement netMove = null)
        {
            movement = move;
            networkMovement = netMove != null
                ? netMove
                : move != null
                    ? move.GetComponent<NetworkPlayerMovement>()
                    : null;
            downState = down;
            carrier = coreCarrier;
            if (_healthBarFill != null) _healthBarFill.transform.parent.gameObject.SetActive(down != null);

            if (carrier != null)
            {
                carrier.CarryNoiseEmitted += OnCarryNoise;
            }
        }

        private void OnDestroy()
        {
            if (carrier != null)
            {
                carrier.CarryNoiseEmitted -= OnCarryNoise;
            }
        }

        private void Start()
        {
            ResolveReferences();
            staminaNormalColor = HUDPresentationStyle.Accent;
            staminaLowColor = HUDPresentationStyle.Danger;
            EnsureHealthBar();
            if (noiseCanvasGroup != null)
            {
                noiseCanvasGroup.alpha = 0f;
            }
            if (bleedoutContainer != null)
            {
                bleedoutContainer.SetActive(false);
                bleedoutContainer.transform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if ((movement == null && networkMovement == null) || downState == null)
            {
                ResolveReferences();
            }

            UpdateStamina();
            UpdateStatus();
            UpdateNoise();
        }

        private void ResolveReferences()
        {
            if (movement == null) movement = FindAnyObjectByType<PlayerMovement>();
            if (networkMovement == null) networkMovement = FindAnyObjectByType<NetworkPlayerMovement>();
            if (downState == null) downState = FindAnyObjectByType<PlayerDownState>();
            if (carrier == null)
            {
                carrier = FindAnyObjectByType<PlayerEnergyCoreCarrier>();
                if (carrier != null)
                {
                    carrier.CarryNoiseEmitted += OnCarryNoise;
                }
            }

            if (staminaValueTmp == null && staminaValueText != null) staminaValueTmp = staminaValueText.GetComponent<TMP_Text>();
            if (statusLabelTmp == null && statusLabelText != null) statusLabelTmp = statusLabelText.GetComponent<TMP_Text>();
            if (bleedoutTimerTmp == null && bleedoutTimerText != null) bleedoutTimerTmp = bleedoutTimerText.GetComponent<TMP_Text>();
            if (noiseLabelTmp == null && noiseLabel != null) noiseLabelTmp = noiseLabel.GetComponent<TMP_Text>();
        }

        private void UpdateStamina()
        {
            if (movement == null && networkMovement == null) return;

            float current = networkMovement != null ? networkMovement.CurrentStamina : movement.CurrentStamina;
            float max = networkMovement != null ? networkMovement.MaxStamina : movement.MaxStamina;
            float target01 = max > 0f ? Mathf.Clamp01(current / max) : 1f;
            _displayStamina = Mathf.Lerp(_displayStamina, target01, Time.deltaTime * 14f);

            if (staminaBarFill != null)
            {
                staminaBarFill.fillAmount = _displayStamina;
                staminaBarFill.color = Color.Lerp(staminaLowColor, staminaNormalColor, Mathf.Clamp01(_displayStamina * 2.5f));
            }

            SetText(staminaValueTmp, staminaValueText, $"{Mathf.RoundToInt(_displayStamina * 100f)}%");
        }

        private void UpdateStatus()
        {
            if (downState == null) return;

            PlayerLifeState life = downState.State;
            EnsureHealthBar();
            if (_healthBarFill != null) _healthBarFill.transform.parent.gameObject.SetActive(
                life == PlayerLifeState.Active || life == PlayerLifeState.Downed);

            if (life == PlayerLifeState.Spectating)
            {
                SetStatusBadge("Đang quan sát", "#9AA5A3", Color.clear);
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }
            }
            else if (life == PlayerLifeState.Eliminated)
            {
                SetStatusBadge("Đã tử vong", "#D0685F", Color.clear);
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }
            }
            else if (life == PlayerLifeState.Downed)
            {
                // The health track becomes the bleedout timer while downed.
                float bleedout = downState.BleedoutRemaining;
                float bleedout01 = downState.Bleedout01;

                SetStatusBadge("Cần cứu trợ", "#D0685F", Color.clear);
                if (_healthBarFill != null)
                {
                    _healthBarFill.rectTransform.localScale = new Vector3(bleedout01, 1f, 1f);
                    _healthBarFill.color = HUDPresentationStyle.Danger;
                }

                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(true);

                    bleedoutContainer.transform.localScale = Vector3.one;

                    if (bleedoutBarFill != null)
                    {
                        bleedoutBarFill.fillAmount = bleedout01;
                        bleedoutBarFill.color = Color.Lerp(new Color(1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.1f), bleedout01);
                    }

                    SetText(bleedoutTimerTmp, bleedoutTimerText, $"Còn {Mathf.CeilToInt(bleedout)} giây");
                }
            }
            else if (life == PlayerLifeState.Caught)
            {
                SetStatusBadge("Bị bắt", "#D0685F", Color.clear);
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }
            }
            else
            {
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }

                UpdateHealthBar();
            }
        }

        private void EnsureHealthBar()
        {
            if (_healthBarFill != null || statusBadgeBg == null) return;

            var trackTransform = statusBadgeBg.transform.Find("HealthTrack");
            var trackObject = trackTransform != null ? trackTransform.gameObject
                : new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
            trackObject.transform.SetParent(statusBadgeBg.transform, false);
            var trackRect = trackObject.GetComponent<RectTransform>();
            trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, -31f);
            trackRect.sizeDelta = new Vector2(288f, 7f);
            var trackImage = trackObject.GetComponent<Image>();
            trackImage.sprite = HUDTextureUtility.WhitePixel;
            trackImage.color = HUDPresentationStyle.Track;
            trackImage.raycastTarget = false;

            var fillObject = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(trackObject.transform, false);
            var rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _healthBarFill = fillObject.GetComponent<Image>();
            _healthBarFill.sprite = HUDTextureUtility.WhitePixel;
            _healthBarFill.raycastTarget = false;
        }

        private void UpdateHealthBar()
        {
            float maxHealth = Mathf.Max(1f, downState.MaxHealth);
            float health = Mathf.Clamp(downState.Health, 0f, maxHealth);
            float amount = health / maxHealth;
            Color barColor = amount < 0.3f
                ? HUDPresentationStyle.Danger
                : amount < 0.6f
                    ? HUDPresentationStyle.Warning
                    : HUDPresentationStyle.Ink;

            if (_healthBarFill != null)
            {
                _healthBarFill.rectTransform.localScale = new Vector3(amount, 1f, 1f);
                _healthBarFill.color = barColor;
            }
            if (statusBadgeBg != null) statusBadgeBg.color = Color.clear;
            if (statusLabelText != null) statusLabelText.rectTransform.sizeDelta = new Vector2(288f, 23f);
            if (statusLabelTmp != null) statusLabelTmp.rectTransform.sizeDelta = new Vector2(288f, 23f);
            SetText(statusLabelTmp, statusLabelText,
                $"HP    {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}");
        }

        private void SetStatusBadge(string label, string hexColor, Color bgColor)
        {
            SetText(statusLabelTmp, statusLabelText, $"<color={hexColor}><b>{label}</b></color>");

            if (statusBadgeBg != null)
            {
                statusBadgeBg.color = Color.clear;
                if (statusLabelText != null) statusLabelText.rectTransform.sizeDelta = new Vector2(156f, 23f);
                if (statusLabelTmp != null) statusLabelTmp.rectTransform.sizeDelta = new Vector2(156f, 23f);
            }
        }

        private void UpdateNoise()
        {
            // Detect noise generation
            bool isSprintMoving = movement != null && movement.IsSprinting && movement.MoveInput.sqrMagnitude > 0.05f;
            bool isCarryMoving = carrier != null && carrier.IsCarrying && movement != null && movement.MoveInput.sqrMagnitude > 0.05f;

            if (isSprintMoving || isCarryMoving)
            {
                _noiseIntensity = Mathf.MoveTowards(_noiseIntensity, 1f, Time.deltaTime * 6f);
            }
            else
            {
                _noiseIntensity = Mathf.MoveTowards(_noiseIntensity, 0f, Time.deltaTime * 3f);
            }

            if (noiseCanvasGroup != null)
            {
                noiseCanvasGroup.alpha = Mathf.Clamp01(_noiseIntensity);
            }

            if (noiseIcon != null)
            {
                noiseIcon.transform.localScale = Vector3.one;
                noiseIcon.color = HUDPresentationStyle.Warning;
            }

            string noiseDesc = isCarryMoving
                ? "Vác nặng · Dễ bị phát hiện"
                : "Chạy gây tiếng động";
            SetText(noiseLabelTmp, noiseLabel, noiseDesc);
        }

        private void OnCarryNoise(PlayerEnergyCoreCarrier carrierObj)
        {
            _noiseIntensity = 1f;
        }

        private static void SetText(TMP_Text tmp, Text legacy, string content)
        {
            if (tmp != null) tmp.text = content;
            if (legacy != null) legacy.text = content;
        }
    }
}
