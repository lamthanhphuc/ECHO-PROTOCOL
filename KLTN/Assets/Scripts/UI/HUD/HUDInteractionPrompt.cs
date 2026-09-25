using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EchoProtocol.Networking;

namespace EchoProtocol.UI.HUD
{
    public class HUDInteractionPrompt : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private NetworkPlayerInteractor networkPlayerInteractor;
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private Image promptBackgroundPlate;
        [SerializeField] private Text promptText;
        [SerializeField] private TMP_Text promptTmp;
        [SerializeField] private GameObject holdProgressContainer;
        [SerializeField] private Image holdProgressRing;
        [SerializeField] private Text holdProgressText;
        [SerializeField] private TMP_Text holdProgressTmp;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 14f;
        [SerializeField] private Color normalPromptColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color holdPromptColor = new Color(1f, 0.7f, 0.1f, 1f);

        private float _targetAlpha;
        private bool _completedTriggered;

        public void BindInteraction(PlayerInteraction interaction)
        {
            playerInteraction = interaction;
        }

        private void Awake()
        {
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = GetComponent<CanvasGroup>();
                if (promptCanvasGroup == null)
                {
                    promptCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = 0f;
                promptCanvasGroup.interactable = false;
                promptCanvasGroup.blocksRaycasts = false;
            }

            ResolveComponents();
        }

        private void ResolveComponents()
        {
            if (promptTmp == null && promptText != null)
            {
                promptTmp = promptText.GetComponent<TMP_Text>();
            }
            if (holdProgressTmp == null && holdProgressText != null)
            {
                holdProgressTmp = holdProgressText.GetComponent<TMP_Text>();
            }
            if (promptBackgroundPlate == null)
            {
                // Find background plate if present
                var images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] != holdProgressRing && images[i].gameObject != gameObject)
                    {
                        promptBackgroundPlate = images[i];
                        break;
                    }
                }
            }

            if (promptBackgroundPlate != null)
            {
                // Ensure good contrast background plate
                Color plateColor = promptBackgroundPlate.color;
                if (plateColor.a < 0.6f)
                {
                    plateColor.a = 0.85f;
                    promptBackgroundPlate.color = plateColor;
                }
            }
        }

        private void Update()
        {
            if ((playerInteraction != null && playerInteraction.IsInteractionPromptSuppressed)
                || (networkPlayerInteractor != null && networkPlayerInteractor.IsInteractionPromptSuppressed))
            {
                HidePromptImmediate();
                return;
            }

            if (TryGetNetworkPrompt(out var networkPrompt, out var networkIsHold, out var networkProgress01))
            {
                ShowPrompt(networkPrompt, networkIsHold, networkProgress01);
                return;
            }

            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
                if (playerInteraction == null)
                {
                    SetAlpha(0f);
                    return;
                }
            }

            IInteractable interactable = playerInteraction.CurrentInteractable;
            string prompt = playerInteraction.CurrentPrompt;

            if (interactable == null || string.IsNullOrWhiteSpace(prompt))
            {
                HidePrompt();
                return;
            }

            // Check if hold interactable
            bool isHold = false;
            float progress01 = 0f;

            if (interactable is IHoldInteractable holdInteractable && holdInteractable.RequiresHold)
            {
                isHold = true;
            }

            if (interactable is SecurityTerminalDownload terminal)
            {
                isHold = true;
                progress01 = terminal.Progress01;
            }
            else if (interactable is PlayerReviveInteractable revive)
            {
                isHold = true;
                progress01 = revive.ReviveProgress01;
            }

            ShowPrompt(prompt, isHold, progress01);
        }

        private bool TryGetNetworkPrompt(out string prompt, out bool isHold, out float progress01)
        {
            prompt = null;
            isHold = false;
            progress01 = 0f;
            if (networkPlayerInteractor == null
                || networkPlayerInteractor.Object == null
                || !networkPlayerInteractor.Object.HasInputAuthority)
            {
                networkPlayerInteractor = FindOwnedNetworkInteractor();
            }

            if (networkPlayerInteractor != null && networkPlayerInteractor.IsInteractionPromptSuppressed)
            {
                return false;
            }

            var candidate = networkPlayerInteractor != null
                ? networkPlayerInteractor.CurrentCandidate
                : null;
            var reviveTarget = networkPlayerInteractor != null
                ? networkPlayerInteractor.CurrentReviveTarget
                : null;
            if (reviveTarget != null && reviveTarget.IsDowned)
            {
                prompt = "Giữ để Cứu Đồng Đội";
                isHold = true;
                progress01 = reviveTarget.ReviveProgress01;
                return true;
            }

            if (candidate == null)
            {
                return false;
            }

            prompt = candidate.InteractionPrompt;
            return !string.IsNullOrWhiteSpace(prompt);
        }

        private static NetworkPlayerInteractor FindOwnedNetworkInteractor()
        {
            foreach (var interactor in FindObjectsByType<NetworkPlayerInteractor>(
                         FindObjectsInactive.Exclude))
            {
                if (interactor.Object != null && interactor.Object.HasInputAuthority)
                {
                    return interactor;
                }
            }

            return null;
        }

        private void ShowPrompt(string prompt, bool isHold, float progress01)
        {
            _targetAlpha = 1f;
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = Mathf.MoveTowards(
                    promptCanvasGroup.alpha,
                    _targetAlpha,
                    fadeSpeed * Time.deltaTime);
            }

            string keyColorHex = isHold ? "#FFB300" : "#00E5FF";
            string keyLabel = "[E]";

            // Clean existing [E] or [E GIỮ] if present in source prompt
            string cleanPrompt = prompt.Replace("[E GIỮ]", "").Replace("[E]", "").Replace("[E ]", "").Trim();
            string formattedText = isHold
                ? $"<color={keyColorHex}><b>{keyLabel}</b></color>  {cleanPrompt} <color=#FFB300>(Giữ)</color>"
                : $"<color={keyColorHex}><b>{keyLabel}</b></color>  {cleanPrompt}";

            SetText(promptTmp, promptText, formattedText);

            // Update Radial Progress with enhanced feedback
            if (holdProgressContainer != null)
            {
                bool showProgress = isHold && progress01 > 0f;
                holdProgressContainer.SetActive(showProgress);

                if (showProgress)
                {
                    if (holdProgressRing != null)
                    {
                        holdProgressRing.fillAmount = progress01;
                        // Transition color from amber towards bright cyan/green as completion nears
                        holdProgressRing.color = Color.Lerp(holdPromptColor, new Color(0f, 1f, 0.6f, 1f), progress01);

                        // Subtle breathing pulse during hold
                        float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 12f);
                        holdProgressRing.transform.localScale = Vector3.one * pulse;
                    }

                    SetText(holdProgressTmp, holdProgressText, $"{Mathf.RoundToInt(progress01 * 100f)}%");

                    if (progress01 >= 0.99f && !_completedTriggered)
                    {
                        _completedTriggered = true;
                    }
                    else if (progress01 < 0.99f)
                    {
                        _completedTriggered = false;
                    }
                }
            }
        }

        private void HidePrompt()
        {
            _targetAlpha = 0f;
            _completedTriggered = false;
            SetAlpha(Mathf.MoveTowards(
                promptCanvasGroup != null ? promptCanvasGroup.alpha : 0f,
                0f,
                fadeSpeed * Time.deltaTime));

            if (holdProgressContainer != null && holdProgressContainer.activeSelf)
            {
                holdProgressContainer.SetActive(false);
            }
        }

        private void HidePromptImmediate()
        {
            _targetAlpha = 0f;
            _completedTriggered = false;
            SetAlpha(0f);
            if (holdProgressContainer != null && holdProgressContainer.activeSelf)
            {
                holdProgressContainer.SetActive(false);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = alpha;
            }
        }

        private static void SetText(TMP_Text tmp, Text legacy, string content)
        {
            if (tmp != null) tmp.text = content;
            if (legacy != null) legacy.text = content;
        }
    }
}
