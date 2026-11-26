using EchoProtocol.Audio;
using EchoProtocol.Salvage;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public static class SalvagePresentation
    {
        private static SalvageEffects _effects;
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> Clips = new();
        public static Camera ViewCamera
        {
            get
            {
                var site=SalvageRunState.Instance!=null?SalvageRunState.Instance.Site:null;
                var camera=site!=null&&site.GameplayCamera!=null?site.GameplayCamera.GetComponent<Camera>():null;
                return camera!=null&&camera.isActiveAndEnabled?camera:Camera.main;
            }
        }
        private static SalvageEffects Effects
        {
            get
            {
                if (_effects == null && Application.isPlaying && SalvageSite.IsActive)
                { var prefab = Resources.Load<SalvageEffects>("Salvage/Fx/SalvageEffects"); if (prefab != null) _effects = Object.Instantiate(prefab); }
                return _effects;
            }
        }
        public static void Sound(string clip, Vector3 position, float volume = .65f)
        {
            if (!Clips.TryGetValue(clip, out var asset)) { asset = Resources.Load<AudioClip>("Salvage/Audio/" + clip); Clips[clip] = asset; }
            GameAudioRuntime.AtPoint(asset, position, volume);
        }
        public static void Scan(Vector3 position, float range, bool found, bool sound = true)
        { if (sound) Sound(found ? "ScannerPulse" : "ScannerEmpty", position); Effects?.Pulse(position, range, new Color(.35f,.85f,.85f), 1); }
        public static void Impact(Vector3 position,SalvageCargoMaterial material,int loss,bool broken)
        {
            float severity=Mathf.Clamp01(loss/50f);
            Sound(material==SalvageCargoMaterial.Organic?"OrganicContact":material==SalvageCargoMaterial.Plastic?"PlasticContact":"MetalContact",position,Mathf.Lerp(.3f,.8f,severity));
            Color color=material==SalvageCargoMaterial.Metal?new Color(1,.72f,.35f):material==SalvageCargoMaterial.Organic?new Color(.45f,.55f,.26f):new Color(.72f,.82f,.85f);
            Effects?.Burst(position,color,broken?28:Mathf.RoundToInt(Mathf.Lerp(4,14,severity)));
            if(broken)Effects?.Pulse(position,.5f,new Color(1,.28f,.16f),.35f,true);
        }
        public static void Cue(int kind, Vector3 position, SalvageCargoMaterial material)
        {
            switch (kind)
            {
                case 0: Sound("Grip", position, .35f); break;
                case 1: GameAudioRuntime.AtPoint(GameAudioRuntime.FindClip("energy_core/deposit_into_sector_box"),position,.3f); Effects?.Pulse(position,.22f,new Color(.25f,1,.65f),.4f,true); break;
                case 2: Sound(material == SalvageCargoMaterial.Organic ? "OrganicContact" : material == SalvageCargoMaterial.Plastic ? "PlasticContact" : "MetalContact", position, .4f); break;
                case 3: GameAudioRuntime.AtPoint(GameAudioRuntime.FindClip("ui/confirm"),position,.5f); Effects?.Pulse(position,.3f,new Color(.3f,1,.45f),.45f,true); break;
                case 4: GameAudioRuntime.UI("ui/confirm"); break;
                case 5: Impact(position,material,10,false); break;
                case 6: Sound("ExtractionStart", position, .45f); Effects?.Pulse(position, 7, new Color(.6f,.8f,.7f)); break;
                case 7: GameAudioRuntime.UI("ui/confirm"); Effects?.Pulse(position, 7, new Color(.6f,.8f,.7f)); break;
                case 8: Sound("Resonance", position, .4f); Effects?.Pulse(position, 1, new Color(.8f,.6f,.3f)); break;
                case 9: Sound("Grip",position,.4f); Effects?.Pulse(position,.12f,new Color(.2f,1,.85f),.22f,true); break;
                case 10: Sound("BrakeLatch",position,.22f); break;
                case 11: GameAudioRuntime.UI("ui/inventory_pickup"); break;
            }
        }
    }
}
