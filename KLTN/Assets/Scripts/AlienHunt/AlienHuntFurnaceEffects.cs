using EchoProtocol.Audio;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoProtocol.Networking
{
    // Presentation only. Oil, flame and audio follow the replicated furnace state.
    public sealed class AlienHuntFurnaceEffects : MonoBehaviour
    {
        private GameObject oil;
        private ParticleSystem flame;
        private ParticleSystem smoke, embers, fuelDrops;
        private Light glow;
        private AudioSource fireAudio, actionAudio, phaseAudio;
        private GameAudioCatalog catalog;
        private Material oilMaterial, fireMaterial, softMaterial;
        private Mesh oilMesh;
        private Texture2D flameTexture;
        private HuntFurnaceState previous;
        private bool hasState;
        private float burnWeight, flashRemaining;
        private Vector3 oilPosition;
        public bool OilVisible => oil && oil.activeSelf;
        public bool FireVisible => flame && flame.isPlaying;

        public void Initialize(Material template)
        {
            if (oil) return;
            Renderer bowl = null;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (renderer.name == "LP_Cotl") { bowl = renderer; break; }
            if (!bowl) return;
            var bounds = bowl.bounds;
            var center = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * .18f, bounds.center.z);
            oil = new GameObject("OilSurface"); oil.transform.SetParent(transform, false);
            oil.transform.position = center; oilPosition = oil.transform.localPosition;
            oilMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            oilMaterial.SetColor("_BaseColor", new Color(.18f,.105f,.018f,1));
            oilMaterial.SetFloat("_Smoothness", .92f); oilMaterial.SetFloat("_Metallic", .25f);
            const int segments = 48;
            var vertices = new Vector3[segments+1]; var triangles = new int[segments*3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices[i+1] = oil.transform.InverseTransformPoint(center + new Vector3(
                    Mathf.Cos(a)*bounds.size.x*.35f, 0, Mathf.Sin(a)*bounds.size.z*.35f));
                triangles[i*3] = 0; triangles[i*3+1] = (i+1)%segments+1; triangles[i*3+2] = i+1;
            }
            oilMesh = new Mesh { name = "AlienHuntOilSurface", vertices = vertices, triangles = triangles };
            oilMesh.RecalculateNormals(); oilMesh.RecalculateBounds();
            oil.AddComponent<MeshFilter>().sharedMesh = oilMesh;
            var oilRenderer = oil.AddComponent<MeshRenderer>(); oilRenderer.sharedMaterial = oilMaterial;
            oilRenderer.shadowCastingMode = ShadowCastingMode.Off;
            oil.SetActive(false);
            if (!template) return;
            var fire = new GameObject("PanFlames"); fire.transform.SetParent(transform, false); fire.transform.position = center;
            flame = fire.AddComponent<ParticleSystem>();
            flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flame.main; main.playOnAwake = false; main.duration = 1; main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f,.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f,.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.15f,.32f); main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = flame.emission; emission.rateOverTime = 45;
            var shape = flame.shape; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Min(bounds.size.x,bounds.size.z)*.25f;
            shape.rotation = new Vector3(90,0,0);
            var velocity = flame.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = .6f;
            var color = flame.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1,.65f,.12f),0), new GradientColorKey(new Color(1,.13f,.01f),1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(.85f,.15f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            flameTexture = new Texture2D(32,32,TextureFormat.RGBA32,false);
            var pixels = new Color[1024];
            for(int y=0;y<32;y++) for(int x=0;x<32;x++)
            { float r = new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude; pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),1.5f)); }
            flameTexture.SetPixels(pixels); flameTexture.Apply();
            fireMaterial = new Material(template); fireMaterial.SetTexture("_BaseMap",flameTexture);
            var particleRenderer = fire.GetComponent<ParticleSystemRenderer>(); particleRenderer.sharedMaterial = fireMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            glow = fire.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 3;
            glow.color = new Color(1,.38f,.07f); glow.shadows = LightShadows.None; glow.enabled = false;
            catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            fireAudio = GameAudioRuntime.CreateSource(fire, true);
            fireAudio.loop = true; fireAudio.rolloffMode = AudioRolloffMode.Linear;
            fireAudio.maxDistance = 20f; fireAudio.volume = 0f;
            fireAudio.clip = catalog != null ? catalog.Find("purge_furnace/fire_loop") : null;
            PurgeSpatialAudio.Attach(fireAudio, transform);
            actionAudio = CreateAudio("Furnace Action", center);
            phaseAudio = CreateAudio("Furnace Phase", center);

            // Alpha-blended dark smoke/drops; additive flame material is reserved for hot embers.
            softMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            softMaterial.SetTexture("_BaseMap", flameTexture);
            softMaterial.SetFloat("_Surface", 1f);
            softMaterial.SetFloat("_Blend", 0f);
            softMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            softMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            softMaterial.SetFloat("_ZWrite", 0f);
            softMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            softMaterial.renderQueue = 3000;
            float radius = Mathf.Min(bounds.size.x, bounds.size.z) * .20f;
            smoke = CreateParticles("PanSmoke", center + Vector3.up * .15f, softMaterial,
                1.2f, 1.8f, .18f, .34f, new Color(.22f, .20f, .18f, .18f), radius);
            var smokeMain = smoke.main; smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .35f);
            var smokeSize = smoke.sizeOverLifetime; smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .5f, 1f, 1.7f));
            embers = CreateParticles("PanEmbers", center + Vector3.up * .10f, fireMaterial,
                .7f, 1.4f, .012f, .028f, new Color(1f, .40f, .08f, .7f), radius);
            var emberMain = embers.main; emberMain.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .5f);
            fuelDrops = CreateParticles("FuelDrops", center + Vector3.up * .13f, softMaterial,
                .18f, .38f, .025f, .045f, new Color(.34f, .20f, .045f, .85f), radius * .55f);
            var dropMain = fuelDrops.main; dropMain.loop = false; dropMain.gravityModifier = .7f;
            dropMain.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .6f);
        }

        private AudioSource CreateAudio(string name, Vector3 position)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            child.transform.position = position;
            var source = GameAudioRuntime.CreateSource(child, true);
            source.rolloffMode = AudioRolloffMode.Linear; source.maxDistance = 18f;
            PurgeSpatialAudio.Attach(source, transform);
            return source;
        }

        private ParticleSystem CreateParticles(string name, Vector3 position, Material material,
            float minLife, float maxLife, float minSize, float maxSize, Color tint, float radius)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            child.transform.position = position;
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize); main.startColor = tint;
            var emission = particles.emission; emission.rateOverTime = 0f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f; shape.radius = radius; shape.rotation = new Vector3(-90f, 0f, 0f);
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return particles;
        }

        private float EffectsVolume => catalog != null ? catalog.effectsVolume : 1f;

        private void PlayCue(AudioSource source, string key, float volume)
        {
            var clip = catalog != null ? catalog.Find(key) : null;
            var camera = Camera.main;
            if (source == null || clip == null || camera == null
                || (camera.transform.position - source.transform.position).sqrMagnitude > source.maxDistance * source.maxDistance) return;
            source.clip = clip;
            if (Application.isPlaying) source.PlayOneShot(clip, volume * EffectsVolume);
        }

        public void Present(HuntFurnaceState state, float delta)
        {
            if (!oil) return;
            bool burning = state.BurnRemaining > 0;
            oil.SetActive(state.Fuel > 0 || burning);
            if (oil.activeSelf) oil.transform.localPosition = oilPosition + Vector3.up * (Mathf.Sin(Time.time*2.5f)*.002f);
            if (!flame) return;
            bool wasBurning = previous.BurnRemaining > 0;
            if (hasState)
            {
                if (state.Fuel > previous.Fuel)
                {
                    PlayCue(actionAudio, "purge_furnace/fuel_pour", .65f);
                    PurgeVfx.Emit(fuelDrops, 14, 20f);
                }
                if (state.AlienSlot != 0 && state.AlienSlot != previous.AlienSlot)
                    PlayCue(actionAudio, "purge_furnace/pan_place", .70f);
                if (burning && !wasBurning)
                {
                    PlayCue(phaseAudio, "purge_furnace/ignition", .68f);
                    flashRemaining = .22f;
                    flame.Play();
                    flame.Emit(18);
                }
                if (state.BurnedCount > previous.BurnedCount)
                {
                    PlayCue(phaseAudio, "purge_alien/burn_complete", .65f);
                    PurgeVfx.Emit(embers, 18, 28f);
                }
            }
            // Initial/late-join state restores active loops without replaying one-shots.
            hasState = true; previous = state;
            flashRemaining = Mathf.Max(0f, flashRemaining - delta);
            burnWeight = burning ? 1f : Mathf.MoveTowards(burnWeight, 0f, delta / .8f);
            if (burning && !flame.isEmitting) flame.Play();
            if (!burning && flame.isEmitting) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            float flash = flashRemaining / .22f;
            var main = flame.main; main.startSizeMultiplier = Mathf.Lerp(.32f, .48f, flash);
            var emission = flame.emission; emission.rateOverTime = 45f * (1f + flash);
            PurgeVfx.SetLoop(smoke, burning, 28f, 3f);
            PurgeVfx.SetLoop(embers, burning, 28f, 3f);
            PurgeVfx.Cull(fuelDrops, 20f);
            glow.enabled = burnWeight > 0f && PurgeVfx.InRange(glow.transform.position, 35f);
            glow.intensity = burnWeight * (1.4f + Mathf.Sin(Time.time * 19f) * .2f + flash * 2f);
            fireAudio.volume = .32f * EffectsVolume * burnWeight;
            if (burnWeight > 0f && fireAudio.clip != null && !fireAudio.isPlaying && Application.isPlaying) fireAudio.Play();
            if (burnWeight <= 0f && fireAudio.isPlaying) fireAudio.Stop();
        }

        public void ResetPresentation()
        {
            hasState = false; previous = default; burnWeight = 0f; flashRemaining = 0f;
            if (oil) oil.SetActive(false);
            if (glow) glow.enabled = false;
            if (fireAudio) { fireAudio.Stop(); fireAudio.volume = 0f; }
            if (actionAudio) { actionAudio.Stop(); actionAudio.clip = null; }
            if (phaseAudio) { phaseAudio.Stop(); phaseAudio.clip = null; }
            if (flame) flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (embers) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fuelDrops) fuelDrops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() => ResetPresentation();

        private static void Release(Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }

        private void OnDestroy()
        { Release(oilMaterial); Release(fireMaterial); Release(softMaterial); Release(oilMesh); Release(flameTexture); }
    }
}
