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
        private float _noiseIntensity;
        private float _flashTimer;

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
            _flashTimer += Time.deltaTime * 5f;
            float flashAlpha = (Mathf.Sin(_flashTimer) + 1f) * 0.5f;

            if (life == PlayerLifeState.Spectating)
            {
                SetStatusBadge("ĐANG QUAN SÁT (SPECTATOR)", "#B388FF", new Color(0.7f, 0.5f, 1f, 0.3f));
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }
            }
            else if (life == PlayerLifeState.Eliminated)
            {
                SetStatusBadge("ĐÃ TỬ VONG", "#D50000", new Color(0.8f, 0.1f, 0.1f, 0.4f));
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }
            }
            else if (life == PlayerLifeState.Downed)
            {
                // Downed state with bleedout countdown and heartbeat pulse
                float bleedout = downState.BleedoutRemaining;
                float bleedout01 = downState.Bleedout01;

                Color flashColor = Color.Lerp(new Color(0.9f, 0.1f, 0.1f, 0.9f), new Color(0.5f, 0f, 0f, 0.4f), flashAlpha);
                SetStatusBadge("HẤP HỐI (DOWNED)", "#FF1744", flashColor);

                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(true);

                    // Dynamic Heartbeat Pulse: speeds up when bleedout is critically low (< 10s)
                    float pulseRate = bleedout < 10f ? 12f : 6f;
                    float pulseScale = 1f + 0.08f * Mathf.Sin(Time.time * pulseRate);
                    bleedoutContainer.transform.localScale = new Vector3(pulseScale, pulseScale, 1f);

                    if (bleedoutBarFill != null)
                    {
                        bleedoutBarFill.fillAmount = bleedout01;
                        bleedoutBarFill.color = Color.Lerp(new Color(1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.1f), bleedout01);
                    }

                    SetText(bleedoutTimerTmp, bleedoutTimerText, $"HẾT MÁU SAU: {bleedout:F1}s");
                }
            }
            else
            {
                // Active: check health
                if (bleedoutContainer != null)
                {
                    bleedoutContainer.SetActive(false);
                    bleedoutContainer.transform.localScale = Vector3.one;
                }

                if (downState.Health < 75f)
                {
                    SetStatusBadge("BỊ THƯƠNG", "#FFB300", new Color(1f, 0.7f, 0f, 0.25f));
                }
                else
                {
                    SetStatusBadge("BÌNH THƯỜNG", "#00E676", new Color(0f, 0.9f, 0.4f, 0.2f));
                }
            }
        }

        private void SetStatusBadge(string label, string hexColor, Color bgColor)
        {
            SetText(statusLabelTmp, statusLabelText, $"<color={hexColor}><b>{label}</b></color>");

            if (statusBadgeBg != null)
            {
                statusBadgeBg.color = bgColor;
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
                // Sound pulse wave animation
                float pulse = 1f + 0.22f * Mathf.Sin(Time.time * 16f);
                noiseIcon.transform.localScale = Vector3.one * (1f + (_noiseIntensity * 0.3f * pulse));
                noiseIcon.color = Color.Lerp(new Color(1f, 0.8f, 0.2f, 0.6f), new Color(1f, 0.15f, 0.15f, 1f), _noiseIntensity);
            }

            string noiseDesc = isCarryMoving
                ? "CẢNH BÁO TIẾNG ĐỘNG // VÁC NẶNG [BÁN KÍNH CAO]"
                : "CẢNH BÁO TIẾNG ĐỘNG // BƯỚC CHẠY GÂY CHÚ Ý STALKER";
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
