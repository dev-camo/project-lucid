using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000084: binder targets that write single
    // values into a particle system's modules.
    [RequireComponent(typeof(ParticleSystem))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ParticleEffectBinderTarget : MonoBehaviour
    {
        [SerializeField]
        private ParticleSystem m_particleSystem;

        private void Reset()
        {
            if (m_particleSystem != null) return;
            m_particleSystem = GetComponent<ParticleSystem>();
        }

        public void SetTransformScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, scale);
        }

        public void SetTransformScale(Vector3 scale)
        {
            transform.localScale = scale;
        }

        public void SetStartSpeed(float startSpeed)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.startSpeed = startSpeed;
        }

        public void SetDuration(float duration)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.duration = duration;
        }

        public void SetMaxParticles(int maxParticles)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.maxParticles = maxParticles;
        }

        public void SetStartLifetime(float startLifetime)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.startLifetime = startLifetime;
        }

        public void SetStartDelay(float startDelay)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.startDelay = startDelay;
        }

        public void SetEmitterVelocity(float emitterVelocity)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.emitterVelocity = new Vector3(emitterVelocity, emitterVelocity, emitterVelocity);
        }

        public void SetEmitterVelocity(Vector3 emitterVelocity)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.emitterVelocity = emitterVelocity;
        }

        public void SetStartSizeMultiplier(float startSizeMultiplier)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.startSizeMultiplier = startSizeMultiplier;
        }

        public void SetEmissionRateOverTimeMultiplier(float rateOverTimeMultiplier)
        {
            ParticleSystem.EmissionModule emission = m_particleSystem.emission;
            emission.rateOverTimeMultiplier = rateOverTimeMultiplier;
        }

        public void SetStartColour(Color colour)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            main.startColor = colour;
        }

        // Replaces the start colour with a single colour built from the
        // gradient's colour, so any gradient mode is lost.
        private void SetStartColourValueIndex(int index, float value)
        {
            ParticleSystem.MainModule main = m_particleSystem.main;
            Color colour = main.startColor.color;
            colour[index] = Mathf.Clamp01(value);
            main.startColor = colour;
        }

        public void SetStartColourRedValue(float red)
        {
            SetStartColourValueIndex(0, red);
        }

        public void SetStartColourGreenValue(float green)
        {
            SetStartColourValueIndex(1, green);
        }

        public void SetStartColourBlueValue(float blue)
        {
            SetStartColourValueIndex(2, blue);
        }

        public void SetStartColourAlphaValue(float alpha)
        {
            SetStartColourValueIndex(3, alpha);
        }

        public void SetEmission(bool enabled)
        {
            ParticleSystem.EmissionModule emission = m_particleSystem.emission;
            if (emission.enabled != enabled) emission.enabled = enabled;
        }
    }
}
