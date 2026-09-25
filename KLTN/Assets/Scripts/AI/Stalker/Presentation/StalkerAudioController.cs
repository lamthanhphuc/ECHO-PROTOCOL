using System.Collections;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Presentation
{
    using Debug = UnityEngine.Debug;
    /// <summary>
    /// Central audio controller for the Stalker monster.
    ///
    /// ARCHITECTURE
    ///   - FSM / StalkerAnimatorPresenter calls the public API (Enter*/Exit*/Play*)
    ///     at state-transition points – never from Update().
    ///   - Animation Events call PlayFootstep / PlaySniff / PlayBite / PlayPunch /
    ///     PlayJumpOut / PlayJumpIn at the exact frame of physical impact.
    ///   - Four dedicated AudioSources prevent sources from interrupting each other:
    ///       voiceSource      – one-shot vocalisation  (Detect / Search / Sniff / Bite / Punch)
    ///       movementSource   – one-shot foot impact   (Walk, Chase footstep, JumpIn, JumpOut)
    ///       breathingSource  – looping idle breath
    ///       chaseSource      – looping Conveyor tension (fades in/out)
    ///
    /// INSPECTOR SETUP
    ///   1. Add StalkerAudioController to the Stalker root GameObject
    ///      (same object as StalkerAnimatorPresenter, or a child).
    ///   2. Create four child GameObjects named Voice / Movement / Breathing / Chase
    ///      and assign an AudioSource component to each.
    ///   3. Drag those AudioSources into the matching serialized fields below.
    ///   4. Drag audio clips from Assets/Audio/stalker/ into the clip fields.
    ///   5. "Breathing Source" – set Loop = true, Play On Awake = false in Inspector.
    ///   6. "Chase Source"     – set Loop = true, Play On Awake = false in Inspector.
    ///      Leave Volume = 0 (it will be driven by fade).
    ///   7. All other sources    – Loop = false, Play On Awake = false.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StalkerAudioController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Serialised – AudioSources
        // ──────────────────────────────────────────────────────────────────────

        [Header("Audio Sources")]
        [Tooltip("Handles one-shot vocalisations: Detect, Search, Sniff, Bite, Punch.")]
        [SerializeField] private AudioSource voiceSource;

        [Tooltip("Handles one-shot foot/impact sounds: footstep, JumpIn, JumpOut.")]
        [SerializeField] private AudioSource movementSource;

        [Tooltip("Looping idle breath. Set Loop = true on the AudioSource.")]
        [SerializeField] private AudioSource breathingSource;

        [Tooltip("Looping Conveyor chase tension. Set Loop = true, Volume = 0 on the AudioSource.")]
        [SerializeField] private AudioSource chaseSource;

        // ──────────────────────────────────────────────────────────────────────
        // Serialised – Clips
        // ──────────────────────────────────────────────────────────────────────

        [Header("Clips – Voice")]
        [SerializeField] private AudioClip detectClip;
        [SerializeField] private AudioClip searchClip;
        [SerializeField] private AudioClip sniffClip;
        [SerializeField] private AudioClip biteClip;
        [SerializeField] private AudioClip punchClip;

        [Header("Clips – Movement")]
        [SerializeField] private AudioClip walkClip;
        [SerializeField] private AudioClip chaseFootstepClip;
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip jumpOutClip;

        [Header("Clips – Ambient")]
        [SerializeField] private AudioClip idleBreathingClip;  // idle_Breathing.mp3
        [SerializeField] private AudioClip conveyorLoopedClip; // Conveyor LOOPED.wav

        // ──────────────────────────────────────────────────────────────────────
        // Serialised – Tuning
        // ──────────────────────────────────────────────────────────────────────

        [Header("Footstep Tuning")]
        [SerializeField, Range(0f, 1f)]     private float footstepVolume     = 1.00f;

        [Header("JumpOut Tuning (monster leaps off metal)")]
        [SerializeField, Range(0f, 1f)]     private float jumpOutVolume      = 0.85f;

        [Header("JumpIn Tuning (monster impacts metal)")]
        [SerializeField, Range(0f, 1f)]     private float jumpInVolume       = 1.00f;

        [Header("Breathing Tuning")]
        [SerializeField, Range(0f, 1f)] private float breathingFullVolume    = 1.00f;
        [SerializeField, Range(0f, 1f)] private float breathingWalkVolume    = 0.75f;

        [Header("Chase Fade Tuning")]
        [SerializeField, Range(0f, 1f)] private float chasePeakVolume        = 1.00f;
        [SerializeField, Min(0.05f)]    private float chaseFadeInSeconds      = 0.80f;
        [SerializeField, Min(0.05f)]    private float chaseFadeOutSeconds     = 1.50f;

        [Header("Search Cooldown")]
        [Tooltip("Minimum seconds between consecutive Search voice plays to avoid spam.")]
        [SerializeField, Min(0f)] private float searchCooldownSeconds = 4.0f;

        [Header("3D Distance Audio Tuning (Audible from afar)")]
        [Tooltip("Ensure AudioSources use 3D linear rolloff with wide reach.")]
        [SerializeField] private bool autoConfigure3D = true;
        [SerializeField, Min(1f)] private float voiceMinDistance = 30f;
        [SerializeField, Min(10f)] private float voiceMaxDistance = 150f;
        [SerializeField, Min(1f)] private float movementMinDistance = 20f;
        [SerializeField, Min(10f)] private float movementMaxDistance = 90f;
        [SerializeField, Min(1f)] private float breathingMinDistance = 15f;
        [SerializeField, Min(10f)] private float breathingMaxDistance = 60f;
        [SerializeField, Min(1f)] private float chaseMinDistance = 30f;
        [SerializeField, Min(10f)] private float chaseMaxDistance = 150f;

        // ──────────────────────────────────────────────────────────────────────
        // Private runtime state
        // ──────────────────────────────────────────────────────────────────────

        private Coroutine _chaseFadeCoroutine;
        private float     _lastSearchPlayTime = float.NegativeInfinity;
        private bool      _chaseActive;
        private bool      _isMoving;
        private bool      _suppressDetectAnimationEvent;
        private bool      _biteAudioPlayedForEpisode;

        // ──────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            EchoProtocol.Audio.GameAudioRuntime.EnsureInitialized();
            if (chaseFootstepClip == null)
                chaseFootstepClip = EchoProtocol.Audio.GameAudioRuntime.FindClip("player/metal_footstep_01");
            if (jumpOutClip == null)
                jumpOutClip = EchoProtocol.Audio.GameAudioRuntime.FindClip("map_ambience/distant_metal_bang");
            ValidateSources();
            if (autoConfigure3D)
            {
                Configure3DSources();
            }
            InitBreathing();
            InitChase();
        }

        private void OnEnable()
        {
            ValidateSources();
            if (autoConfigure3D)
            {
                Configure3DSources();
            }
            InitBreathing();
            InitChase();
        }

        private void OnDisable()
        {
            // Stop all loops and kill any pending coroutines so nothing
            // continues playing on a disabled/despawned GameObject.
            StopAllLoops();

            if (_chaseFadeCoroutine != null)
            {
                StopCoroutine(_chaseFadeCoroutine);
                _chaseFadeCoroutine = null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Initialisation helpers
        // ──────────────────────────────────────────────────────────────────────

        private void ValidateSources()
        {
            voiceSource     = ResolveAudioChild(voiceSource,     "Audio_Voice");
            movementSource  = ResolveAudioChild(movementSource,  "Audio_Movement");
            breathingSource = ResolveAudioChild(breathingSource, "Audio_Breathing");
            chaseSource     = ResolveAudioChild(chaseSource,     "Audio_Chase");

            if (voiceSource     == null) Debug.LogError("[StalkerAudio] voiceSource is not assigned.",     this);
            if (movementSource  == null) Debug.LogError("[StalkerAudio] movementSource is not assigned.",  this);
            if (breathingSource == null) Debug.LogError("[StalkerAudio] breathingSource is not assigned.", this);
            if (chaseSource     == null) Debug.LogError("[StalkerAudio] chaseSource is not assigned.",     this);
        }

        private AudioSource ResolveAudioChild(AudioSource current, string childName)
        {
            if (current != null) return current;

            var child = transform.Find(childName);
            if (child != null)
            {
                var src = child.GetComponent<AudioSource>();
                if (src != null) return src;
                return child.gameObject.AddComponent<AudioSource>();
            }

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<AudioSource>();
        }

        private void Configure3DSources()
        {
            ConfigureSource3D(voiceSource,     voiceMinDistance,     voiceMaxDistance,     AudioRolloffMode.Linear);
            ConfigureSource3D(chaseSource,     chaseMinDistance,     chaseMaxDistance,     AudioRolloffMode.Linear);
            ConfigureSource3D(movementSource,  movementMinDistance,  movementMaxDistance,  AudioRolloffMode.Linear);
            ConfigureSource3D(breathingSource, breathingMinDistance, breathingMaxDistance, AudioRolloffMode.Linear);
        }

        private static void ConfigureSource3D(AudioSource src, float minDist, float maxDist, AudioRolloffMode rolloff)
        {
            if (src == null) return;
            src.spatialBlend = 1f; // Full 3D
            src.minDistance  = minDist;
            src.maxDistance  = maxDist;
            src.rolloffMode  = rolloff;
            src.dopplerLevel = 0f;
            src.spread       = 60f;
        }

        private void InitBreathing()
        {
            if (breathingSource == null || idleBreathingClip == null) return;
            breathingSource.clip   = idleBreathingClip;
            breathingSource.loop   = true;
            breathingSource.volume = breathingFullVolume;
            // breathing starts with Idle state – Awake does NOT call Play here;
            // EnterIdle() will start it.
        }

        private void InitChase()
        {
            if (chaseSource == null || conveyorLoopedClip == null) return;
            chaseSource.clip   = conveyorLoopedClip;
            chaseSource.loop   = true;
            chaseSource.volume = 0f;
            // Do NOT play yet.
        }

        // ──────────────────────────────────────────────────────────────────────
        // PUBLIC API – State Transitions (called by StalkerAnimatorPresenter)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call when the monster enters PATROL or IDLE state.
        /// Starts idle breathing loop if not already playing.
        /// Restores full breathing volume if it was dimmed during Walk.
        /// </summary>
        public void EnterIdle()
        {
            StartBreathing(_isMoving ? breathingWalkVolume : breathingFullVolume);
        }

        /// <summary>
        /// Call when monster movement changes (e.g. from animator presenter or locomotion).
        /// Lowers breathing volume slightly while moving, restores full volume when stationary.
        /// </summary>
        public void SetMoving(bool moving)
        {
            _isMoving = moving;
            if (_chaseActive) return;

            if (breathingSource != null && breathingSource.isPlaying)
            {
                breathingSource.volume = moving ? breathingWalkVolume : breathingFullVolume;
            }
        }

        /// <summary>
        /// Call when the monster exits PATROL/IDLE (e.g. entering Chase or Death).
        /// Does NOT stop breathing outright – used to dim volume during movement.
        /// Call StopAllLoops() on death/despawn instead.
        /// </summary>
        public void ExitIdle()
        {
            // Breathing volume is adjusted individually by EnterChase / StopAllLoops.
            // No action needed here by default.
        }

        /// <summary>
        /// Call when the monster enters CHASE state.
        /// Fades in Conveyor loop.  Dims breathing.
        /// </summary>
        public void EnterChase()
        {
            _chaseActive = true;

            // Dim breathing while chasing (keep loop going)
            if (breathingSource != null && breathingSource.isPlaying)
            {
                breathingSource.volume = breathingWalkVolume;
            }

            // Fade in the Conveyor chase loop
            StartChaseFade(fadeIn: true);
        }

        /// <summary>
        /// Call when the monster exits CHASE state.
        /// Fades out Conveyor loop.  Restores breathing volume.
        /// </summary>
        public void ExitChase()
        {
            _chaseActive = false;

            // Restore breathing volume
            if (breathingSource != null && breathingSource.isPlaying)
            {
                breathingSource.volume = breathingFullVolume;
            }

            // Fade out Conveyor
            StartChaseFade(fadeIn: false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // PUBLIC API – One-shot Voice (FSM transitions / Animation Events)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Play detect roar once on ENTER DETECT state.
        /// Guard: will not restart if already playing the detect clip.
        /// </summary>
        public void PlayDetect()
        {
            _suppressDetectAnimationEvent = true;
            PlayDetectClip();
        }

        public void BeginDetectAudioEntry()
        {
            _suppressDetectAnimationEvent = false;
        }

        public void PlayDetectFromAnimation()
        {
            if (_suppressDetectAnimationEvent) return;
            PlayDetectClip();
        }

        private void PlayDetectClip()
        {
            if (!ClipAndSourceReady(voiceSource, detectClip)) return;
            if (voiceSource.isPlaying && voiceSource.clip == detectClip) return;

            voiceSource.clip   = detectClip;
            voiceSource.pitch  = 1f;
            voiceSource.volume = 0.1f;
            voiceSource.Play();
        }

        /// <summary>
        /// Play search audio once per search entry, with cooldown to avoid spam.
        /// </summary>
        public void PlaySearch()
        {
            if (!ClipAndSourceReady(voiceSource, searchClip)) return;
            if (Time.time - _lastSearchPlayTime < searchCooldownSeconds) return;

            _lastSearchPlayTime = Time.time;
            voiceSource.clip   = searchClip;
            voiceSource.pitch  = 1f;
            voiceSource.volume = 1f;
            voiceSource.Play();
        }

        // ──────────────────────────────────────────────────────────────────────
        // PUBLIC API – Animation Events
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggered by Animation Event on the foot-strike frame of Walk/Chase.
        /// Walk and chase use separate clips so the change in pace is audible.
        /// </summary>
        public void PlayFootstep()
        {
            var clip = _chaseActive ? chaseFootstepClip : walkClip;
            if (!ClipAndSourceReady(movementSource, clip)) return;

            movementSource.pitch  = 1f;
            movementSource.volume = footstepVolume;
            movementSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Triggered by Animation Event during the Sniff animation,
        /// at the frame the monster inhales sharply.
        /// </summary>
        public void PlaySniff()
        {
            if (!ClipAndSourceReady(voiceSource, sniffClip)) return;

            voiceSource.pitch  = 1f;
            voiceSource.volume = 1f;
            voiceSource.PlayOneShot(sniffClip);
        }

        /// <summary>
        /// Triggered by Animation Event at the exact bite-impact frame.
        /// Audio does NOT control damage; that is handled by the combat system.
        /// </summary>
        public void PlayBite()
        {
            if (_biteAudioPlayedForEpisode) return;
            if (!ClipAndSourceReady(voiceSource, biteClip)) return;

            _biteAudioPlayedForEpisode = true;

            voiceSource.pitch  = 1f;
            voiceSource.volume = 1f;
            voiceSource.PlayOneShot(biteClip);
        }

        public void BeginAttackAudioEpisode()
        {
            _biteAudioPlayedForEpisode = false;
        }

        public void PlayBiteFromAnimation()
        {
            PlayBite();
        }

        /// <summary>
        /// Triggered by Animation Event at the punch-impact frame.
        /// Suitable for door-punch or player punch animations.
        /// </summary>
        public void PlayPunch()
        {
            if (!ClipAndSourceReady(voiceSource, punchClip)) return;

            voiceSource.pitch  = 1f;
            voiceSource.volume = 1f;
            voiceSource.PlayOneShot(punchClip);
        }

        /// <summary>
        /// Triggered by Animation Event on the JumpOut frame where the monster
        /// pushes off the metal surface.
        /// Uses a separate push-off clip from the landing impact.
        /// </summary>
        public void PlayJumpOut()
        {
            if (!ClipAndSourceReady(movementSource, jumpOutClip)) return;

            movementSource.pitch  = 1f;
            movementSource.volume = jumpOutVolume;
            movementSource.PlayOneShot(jumpOutClip);
        }

        /// <summary>
        /// Triggered by Animation Event on the JumpIn frame where the monster
        /// lands on the metal surface with full weight.
        /// Uses the dedicated landing clip.
        /// </summary>
        public void PlayJumpIn()
        {
            if (!ClipAndSourceReady(movementSource, jumpClip)) return;

            movementSource.pitch  = 1f;
            movementSource.volume = jumpInVolume;
            movementSource.PlayOneShot(jumpClip);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Death / Despawn
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call on monster death or forced despawn.
        /// Immediately halts all loops; fade coroutine is cancelled by OnDisable.
        /// </summary>
        public void StopAllLoops()
        {
            if (breathingSource != null && breathingSource.isPlaying) breathingSource.Stop();
            if (chaseSource     != null && chaseSource.isPlaying)     chaseSource.Stop();
            if (_chaseFadeCoroutine != null)
            {
                StopCoroutine(_chaseFadeCoroutine);
                _chaseFadeCoroutine = null;
            }
            if (chaseSource != null) chaseSource.volume = 0f;
            _chaseActive = false;
            _isMoving = false;
            _suppressDetectAnimationEvent = false;
            _biteAudioPlayedForEpisode = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        private void StartBreathing(float targetVolume)
        {
            if (breathingSource == null || idleBreathingClip == null) return;

            breathingSource.volume = targetVolume;

            if (!breathingSource.isPlaying)
            {
                breathingSource.clip = idleBreathingClip;
                breathingSource.Play();
            }
        }

        /// <summary>
        /// Cancels any running chase fade coroutine and starts a new one.
        /// This prevents coroutines from stacking.
        /// </summary>
        private void StartChaseFade(bool fadeIn)
        {
            if (_chaseFadeCoroutine != null)
            {
                StopCoroutine(_chaseFadeCoroutine);
                _chaseFadeCoroutine = null;
            }

            _chaseFadeCoroutine = StartCoroutine(
                fadeIn ? FadeChaseIn() : FadeChaseOut());
        }

        private IEnumerator FadeChaseIn()
        {
            if (chaseSource == null) yield break;

            if (!chaseSource.isPlaying)
            {
                chaseSource.clip   = conveyorLoopedClip;
                chaseSource.volume = 0f;
                chaseSource.Play();
            }

            float startVolume = chaseSource.volume;
            float elapsed     = 0f;

            while (elapsed < chaseFadeInSeconds)
            {
                elapsed           += Time.deltaTime;
                chaseSource.volume = Mathf.Lerp(startVolume, chasePeakVolume,
                    elapsed / chaseFadeInSeconds);
                yield return null;
            }

            chaseSource.volume  = chasePeakVolume;
            _chaseFadeCoroutine = null;
        }

        private IEnumerator FadeChaseOut()
        {
            if (chaseSource == null) yield break;

            float startVolume = chaseSource.volume;
            float elapsed     = 0f;

            while (elapsed < chaseFadeOutSeconds)
            {
                elapsed           += Time.deltaTime;
                chaseSource.volume = Mathf.Lerp(startVolume, 0f,
                    elapsed / chaseFadeOutSeconds);
                yield return null;
            }

            chaseSource.volume  = 0f;
            chaseSource.Stop();
            _chaseFadeCoroutine = null;
        }

        /// <summary>Returns true only when both source and clip are valid.</summary>
        private static bool ClipAndSourceReady(AudioSource source, AudioClip clip)
        {
            if (source == null)
            {
                Debug.LogWarning("[StalkerAudio] AudioSource is null – skipping playback.");
                return false;
            }
            if (clip == null)
            {
                Debug.LogWarning("[StalkerAudio] AudioClip is null – skipping playback.");
                return false;
            }
            return true;
        }
    }
}
