using UnityEngine;

namespace EchoProtocol.Audio
{
    public static class PurgeVfx
    {
        public static bool InRange(Vector3 position, float range)
        {
            var camera = Camera.main;
            return camera != null && (camera.transform.position - position).sqrMagnitude < range * range;
        }

        public static bool Cull(ParticleSystem particles, float range)
        {
            if (particles == null) return true;
            if (InRange(particles.transform.position, range)) return false;
            if (particles.isPlaying || particles.particleCount > 0)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return true;
        }

        public static void SetLoop(ParticleSystem particles, bool wanted, float range, float rate)
        {
            if (Cull(particles, range)) return;
            if (rate >= 0f) { var emission = particles.emission; emission.rateOverTime = rate; }
            if (wanted && !particles.isEmitting) particles.Play(true);
            else if (!wanted && particles.isEmitting) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public static void Burst(ParticleSystem particles, float range)
        { if (!Cull(particles, range)) particles.Play(true); }

        public static void Emit(ParticleSystem particles, int count, float range)
        { if (!Cull(particles, range)) { particles.Play(true); particles.Emit(count); } }
    }
}
