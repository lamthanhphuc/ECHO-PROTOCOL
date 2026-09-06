using UnityEngine;
using UnityEngine.UI;
using EchoProtocol.Networking;

namespace EchoProtocol.UI.HUD
{
    public class HUDInteractionPrompt : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private NetworkPlayerInteractor networkPlayerInteractor;
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject holdProgressContainer;
        [SerializeField] private Image holdProgressRing;
        [SerializeField] private Text holdProgressText;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 12f;
        [SerializeField] private Color normalPromptColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color holdPromptColor = new Color(1f, 0.7f, 0.1f, 1f);

        private float _targetAlpha;

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
        }

        private void Update()
        {
            if (TryGetNetworkPrompt(out var networkPrompt))
            {
                ShowPrompt(networkPrompt, false, 0f);
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

        private bool TryGetNetworkPrompt(out string prompt)
        {
            prompt = null;
            if (networkPlayerInteractor == null
                || networkPlayerInteractor.Object == null
                || !networkPlayerInteractor.Object.HasInputAuthority)
            {
                networkPlayerInteractor = FindOwnedNetworkInteractor();
            }

            var candidate = networkPlayerInteractor != null
                ? networkPlayerInteractor.CurrentCandidate
                : null;
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

            if (promptText != null)
            {
                string keyColorHex = isHold ? "#FFB300" : "#00E5FF";
                string keyLabel = "[E]";
                
                // Clean existing [E] or [E GIỮ] if present in source prompt
                string cleanPrompt = prompt.Replace("[E GIỮ]", "").Replace("[E]", "").Replace("[E ]", "").Trim();
                promptText.text = $"<color={keyColorHex}><b>{keyLabel}</b></color>  {cleanPrompt}";
            }

            // Update Radial Progress
            if (holdProgressContainer != null)
            {
                bool showProgress = isHold && progress01 > 0f;
                holdProgressContainer.SetActive(showProgress);

                if (showProgress)
                {
                    if (holdProgressRing != null)
                    {
                        holdProgressRing.fillAmount = progress01;
                    }

                    if (holdProgressText != null)
                    {
                        holdProgressText.text = $"{Mathf.RoundToInt(progress01 * 100f)}%";
                    }
                }
            }
        }

        private void HidePrompt()
        {
            _targetAlpha = 0f;
            SetAlpha(Mathf.MoveTowards(
                promptCanvasGroup != null ? promptCanvasGroup.alpha : 0f,
                0f,
                fadeSpeed * Time.deltaTime));

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
    }
}
