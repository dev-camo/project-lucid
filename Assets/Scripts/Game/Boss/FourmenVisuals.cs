using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000662 with the iterator owners 02000663 (DrillOverTime) and
    // 02000664 (FlashBetweenFaces); 0x579CE0..0x57A930.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FourmenVisuals : MonoBehaviour
    {
        [SerializeField]
        [Header("Drill Bit")]
        private Transform m_boss;
        [SerializeField] private TransformRotator m_rotator;
        [SerializeField] private float m_depthToDrill;
        [SerializeField] private float m_drillDuration;
        [SerializeField] private float m_rotationDurationMultiplier;
        [Header("Hinged Helmet")]
        [SerializeField]
        private Animation m_animation;
        [SerializeField] private string m_openAnimName;
        [SerializeField] private string m_closeAnimName;
        [Header("Material States")]
        [SerializeField]
        private Renderer m_materialRenderer;
        [SerializeField] private Material m_neutralMaterial;
        [SerializeField] private Material m_attackingMaterial;
        [SerializeField] private Material m_deadMaterial;
        [SerializeField]
        [Header("Particles")]
        private ParticleEffectWrapper m_dangerousBossIndicator;
        private float m_maxDepth;
        private float m_surfaceDepth;
        private float m_rotateDuration;
        private Coroutine m_drillCoroutine;
        private Coroutine m_faceCoroutine;
        private TimeManager m_timeManager;

        // 06002267: the drill range and rotation time come from the boss' starting height.
        protected void Awake()
        {
            m_surfaceDepth = m_boss.transform.position.y;
            m_maxDepth = m_surfaceDepth - m_depthToDrill;
            m_rotateDuration = (float)(m_drillDuration * m_rotationDurationMultiplier);
            ProcessManager.GetSystemRef<TimeManager>(null, true).InvokeOnValid(manager => m_timeManager = manager);
        }

        // 06002268.
        private void Start() => m_dangerousBossIndicator.StartEffect();

        // 06002269: the rotator spins backwards while drilling down.
        public void Drill(bool down)
        {
            m_rotator.SetRotationSpeedMultiplier(down ? -1f : 1f);
            this.SafeStopCoroutine(ref m_drillCoroutine);
            m_drillCoroutine = StartCoroutine(DrillOverTime(down));
        }

        // 0600226a: waits for the time manager, then moves the boss between the surface and
        // maximum depth over the drill duration, untwisting its rotation while drilling down.
        private IEnumerator DrillOverTime(bool down)
        {
            while (m_timeManager == null) yield return null;
            float timer = 0f;
            float startY;
            float targetY;
            if (down)
            {
                startY = m_surfaceDepth;
                targetY = m_maxDepth;
            }
            else
            {
                startY = m_maxDepth;
                targetY = m_surfaceDepth;
            }
            Transform bossTransform = m_boss.transform;
            while (m_drillDuration > timer)
            {
                timer += m_timeManager.GetDeltaTime(TimeCategory.EnemyMovement);
                float t = timer / m_drillDuration;
                if (down && m_rotateDuration > timer)
                    bossTransform.rotation = Quaternion.Slerp(bossTransform.rotation, Quaternion.identity, t / m_rotateDuration);
                Vector3 position = bossTransform.position;
                position.y = Mathf.Lerp(startY, targetY, t);
                bossTransform.position = position;
                yield return null;
            }
            Vector3 finalPosition = bossTransform.position;
            finalPosition.y = targetY;
            bossTransform.position = finalPosition;
        }

        // 0600226b.
        public void OpenHelmet() => m_animation.Play(m_openAnimName);

        // 0600226c.
        public void CloseHelmet() => m_animation.Play(m_closeAnimName);

        // 0600226d..06002272: each face change first stops any running flash.
        public void SetFaceNeutral()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_materialRenderer.material = m_neutralMaterial;
        }

        public void SetFaceTargeting()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_faceCoroutine = StartCoroutine(FlashBetweenFaces(m_attackingMaterial, m_neutralMaterial, 0.2f));
        }

        public void SetFaceAttacking()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_materialRenderer.material = m_attackingMaterial;
        }

        public void SetFaceDying()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_faceCoroutine = StartCoroutine(FlashBetweenFaces(m_deadMaterial, m_neutralMaterial, 0.2f));
        }

        public void SetFaceCharging()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_faceCoroutine = StartCoroutine(FlashBetweenFaces(m_neutralMaterial, m_deadMaterial, 0.5f));
        }

        public void SetFaceDead()
        {
            this.SafeStopCoroutine(ref m_faceCoroutine);
            m_materialRenderer.material = m_deadMaterial;
        }

        // 06002273: swaps the two materials every time the interval has passed, until stopped.
        private IEnumerator FlashBetweenFaces(Material firstFace, Material secondFace, float interval)
        {
            while (m_timeManager == null) yield return null;
            float timer = 0f;
            bool firstFaceActive = true;
            m_materialRenderer.material = firstFace;
            while (true)
            {
                timer += m_timeManager.GetDeltaTime(TimeCategory.EnemyMovement);
                if (timer > interval)
                {
                    firstFaceActive = !firstFaceActive;
                    m_materialRenderer.material = firstFaceActive ? firstFace : secondFace;
                    timer = 0f;
                }
                yield return null;
            }
        }

        // 06002274.
        public void SetDangerousParticlesActive(bool active)
        {
            if (active)
                m_dangerousBossIndicator.StartEffect();
            else
                m_dangerousBossIndicator.StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 06002275.
        protected void OnDisable()
        {
            this.SafeStopCoroutine(ref m_drillCoroutine);
            this.SafeStopCoroutine(ref m_faceCoroutine);
        }
    }
}
