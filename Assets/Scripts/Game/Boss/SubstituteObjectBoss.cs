using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000445; bodies translated from the 1.10.1 x86-64 binary, 0x7A52B0..0x7A5810.
    // A cutscene stand-in for a boss: while it is enabled the real boss's visual proxy is hidden, and when it is disabled
    // the boss is shown again; its transform is moved to the track hit along the boss's down direction or, failing that,
    // its up direction (keeping x and z), and its movement is resumed from its cached world position with zero velocity.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SubstituteObjectBoss : SubstituteObject
    {
        [SerializeField] protected BossDefinition m_bossDefinition;
        private const float RaycastStartHeightBoost = 50f;
        private const float RaycastDistanceMultiplier = 2f;
        protected Boss m_replacedBoss;

        // 06001941 (1.10.1 RVA 0x7A52B0): the boss is looked up once by its definition. The null tests here and in
        // OnDisable (`??=`, `?.` on VisualProxy) are plain reference tests, not Unity's.
        protected override void OnEnable()
        {
            BossManager bossManager = ProcessManager.GetSystem<BossManager>(null, true);
            m_replacedBoss ??= bossManager.GetBossByDefinition(m_bossDefinition);
            m_replacedBoss.VisualProxy?.gameObject.SetActive(false);
            m_replacedObject = m_replacedBoss.gameObject;
            m_realAnimationRoot = m_replacedBoss.AnimationRoot;
            base.OnEnable();
        }

        // 06001942 (1.10.1 RVA 0x7A53B0)
        protected override void OnDisable()
        {
            base.OnDisable();
            m_replacedBoss.VisualProxy?.gameObject.SetActive(true);
            PlaceOnTheGround(m_replacedBoss, m_replacedObject.transform.position);
            m_replacedBoss.MovementResume(m_replacedBoss.WorldPosition, Vector3.zero);
        }

        // 06001943 (1.10.1 RVA 0x7A5530): casts along -UpDirection first and, if nothing is hit, along UpDirection.
        private void PlaceOnTheGround(Boss boss, Vector3 worldPosition)
        {
            float distance = (worldPosition.y + RaycastStartHeightBoost) * RaycastDistanceMultiplier;
            if (FindGroundAndMatch(-boss.UpDirection, worldPosition, distance, boss.GetTraits().TrackMask))
                return;
            FindGroundAndMatch(boss.UpDirection, worldPosition, distance, boss.GetTraits().TrackMask);
        }

        // 06001944 (1.10.1 RVA 0x7A5610): a ray along the (normalised) direction that hits the track moves the real
        // object to the hit height, keeping its x and z.
        private bool FindGroundAndMatch(Vector3 direction, Vector3 startPosition, float distance, LayerMask trackMask)
        {
            bool hit = Physics.Raycast(startPosition, direction.normalized, out RaycastHit hitInfo, distance, trackMask);
            if (hit)
                m_replacedObject.transform.position = new Vector3(startPosition.x, hitInfo.point.y, startPosition.z);
            return hit;
        }

        // 06001945 (1.10.1 RVA 0x7A5810) is the base-only constructor.
        public SubstituteObjectBoss()
        {
        }
    }
}
