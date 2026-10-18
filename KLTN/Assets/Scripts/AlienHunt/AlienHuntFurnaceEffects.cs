using UnityEngine;
using UnityEngine.Rendering;

namespace EchoProtocol.Networking
{
    // Presentation only. Oil, flame and audio follow the replicated furnace state.
    public sealed class AlienHuntFurnaceEffects : MonoBehaviour
    {
        private GameObject oil;
        private ParticleSystem flame;
        private Light glow;
        private AudioSource fireAudio;
        private Material oilMaterial, fireMaterial;
        private Mesh oilMesh;
        private Texture2D flameTexture;
        private static AudioClip fireLoop;
        private Vector3 oilPosition;
        public bool OilVisible => oil && oil.activeSelf;
        public bool FireVisible => flame && flame.isPlaying;

        public void Initialize(Material template)
        {
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
            fireAudio = fire.AddComponent<AudioSource>(); fireAudio.playOnAwake = false; fireAudio.loop = true;
            fireAudio.spatialBlend = 1; fireAudio.minDistance = 2; fireAudio.maxDistance = 20;
            fireAudio.rolloffMode = AudioRolloffMode.Linear; fireAudio.volume = .16f; fireAudio.dopplerLevel = 0;
            if (!fireLoop)
            {
                const int rate = 22050; var samples = new float[rate*2]; var random = new System.Random(719);
                float filtered = 0;
                for (int i=0;i<samples.Length;i++)
                { filtered = Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.16f); samples[i]=filtered*.6f; }
                fireLoop = AudioClip.Create("AlienHuntFireCrackle",samples.Length,1,rate,false); fireLoop.SetData(samples,0);
            }
            fireAudio.clip = fireLoop;
        }
        public void Present(HuntFurnaceState state)
        {
            if (!oil) return;
            bool burning = state.BurnRemaining > 0;
            oil.SetActive(state.Fuel > 0 || burning);
            if (oil.activeSelf) oil.transform.localPosition = oilPosition + Vector3.up * (Mathf.Sin(Time.time*2.5f)*.002f);
            if (!flame) return;
            if (burning && !flame.isPlaying) flame.Play();
            if (!burning && flame.isPlaying) flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            glow.enabled = burning; glow.intensity = 1.4f + Mathf.Sin(Time.time*19)*.2f;
            if (burning && !fireAudio.isPlaying) fireAudio.Play();
            if (!burning && fireAudio.isPlaying) fireAudio.Stop();
        }
        private void OnDestroy()
        { if(oilMaterial)Destroy(oilMaterial); if(fireMaterial)Destroy(fireMaterial); if(oilMesh)Destroy(oilMesh); if(flameTexture)Destroy(flameTexture); }
    }
}
