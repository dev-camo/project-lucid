using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006f9: completes once every authored collectable reward has been collected and, while the
    // character is inside a proximity trigger, tracks how close and how well aligned it is to the first reward.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionCollectableRewardTracker : MissionTracker
    {
        [FormerlySerializedAs("m_orbs")]
        [SerializeField]
        private List<Collectable> m_collectableRewards = new List<Collectable>();
        [SerializeField] private CharacterEventTrigger[] m_proximityTriggers;
        [SerializeField] private float m_proximityStartDistance = 50f;
        [Min(0f)]
        [SerializeField]
        private float m_proximityDistanceWeighting = 1f;
        [SerializeField]
        [Min(0f)]
        private float m_proximityOrientationWeighting = 2f;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private string m_levelName;
        private string m_rewardGuid;
        private int m_collectedRewardCount;
        private bool m_registeredReward;
        private Coroutine m_proximityFunction;
        private readonly List<CharacterEventTrigger> m_activeProximityTriggers = new List<CharacterEventTrigger>();

        public IReadOnlyList<Collectable> CollectableRewards => m_collectableRewards;
        public float NormalisedWeightedProximity { get; private set; }
        public float NormalisedDistanceProximity { get; private set; }
        public bool CollectionInProgress { get; private set; }

        // 06002630 (0x5AAC20): the rewards are registered by this tracker as one group, so each opts out of its own
        // automatic registration.
        private void Start()
        {
            Validate();
            foreach (Collectable reward in m_collectableRewards)
            {
                reward.OnCollected += OnRewardCollected;
                reward.AutoRegisterToCollectionManager = false;
            }
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            if (!m_registeredReward)
                ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(OnCollectableManagerValid);
            InitialiseProximityVolumes();
        }

        // 06002631 (0x5AB070): without authored triggers a default one is made. The start distance becomes the extents
        // magnitude of the union of every trigger's bounds.
        private void InitialiseProximityVolumes()
        {
            if (m_proximityTriggers.Length == 0)
                CreateDefaultTrigger();
            Bounds bounds = m_proximityTriggers[0].GetBounds();
            foreach (CharacterEventTrigger trigger in m_proximityTriggers)
            {
                trigger.OnTriggerEnter += OnProximityTriggerEnter;
                trigger.OnTriggerExit += OnProximityTriggerExit;
                bounds.Encapsulate(trigger.GetBounds());
            }
            m_proximityStartDistance = bounds.extents.magnitude;
        }

        // 06002632 (0x5AB320): a sphere trigger of the authored start distance, centred on the first reward and
        // parented to this tracker.
        private void CreateDefaultTrigger()
        {
            GameObject triggerObject = new GameObject("ProximityTrigger");
            triggerObject.transform.SetParent(m_collectableRewards[0].transform, false);
            triggerObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            triggerObject.transform.SetParent(transform, true);
            triggerObject.layer = LayerMask.NameToLayer("CharacterCollision");
            SphereCollider sphere = triggerObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = m_proximityStartDistance;
            m_proximityTriggers = new CharacterEventTrigger[] { triggerObject.AddComponent<CharacterEventTrigger>() };
        }

        // 06002633 (0x5AB5A0): the rewards register once, counted by type of the first.
        private void OnCollectableManagerValid(CollectableManager collectableManager)
        {
            if (m_registeredReward) return;
            collectableManager.RegisterCollectable(m_collectableRewards[0].Type, m_collectableRewards.Count);
            m_registeredReward = true;
        }

        // 06002634 (0x5AB630)
        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.CanValidate(false))
                Validate();
        }

        // 06002635 (0x5AB670): destroyed rewards are skipped; the triggers are not null-checked.
        protected override void OnDestroy()
        {
            foreach (Collectable reward in m_collectableRewards)
            {
                if (reward == null) continue;
                reward.OnCollected -= OnRewardCollected;
            }
            foreach (CharacterEventTrigger trigger in m_proximityTriggers)
            {
                trigger.OnTriggerEnter -= OnProximityTriggerEnter;
                trigger.OnTriggerExit -= OnProximityTriggerExit;
            }
            base.OnDestroy();
        }

        // 06002636 (0x5AAF50): retail only enumerates the rewards; whatever the loop body checked is compiled out.
        private void Validate()
        {
            foreach (Collectable _ in m_collectableRewards)
            {
            }
        }

        // 06002637 (0x5AB970)
        private void OnRewardCollected(Collectable reward)
        {
            m_collectedRewardCount++;
            if (m_collectedRewardCount == m_collectableRewards.Count)
                MissionObjectivesCompleted();
        }

        // 06002638 (0x5AB9E0): completing stops the proximity tracking by emptying the active trigger set.
        public override void MissionObjectivesCompleted()
        {
            base.MissionObjectivesCompleted();
            m_activeProximityTriggers.Clear();
            UpdateProximityTrackingState();
        }

        // 06002639/0600263a (0x5ABB40, 0x5ABB90)
        public void OnProximityTriggerEnter(CharacterEventTrigger trigger)
        {
            m_activeProximityTriggers.AddUnique(trigger);
            UpdateProximityTrackingState();
        }

        public void OnProximityTriggerExit(CharacterEventTrigger trigger)
        {
            m_activeProximityTriggers.Remove(trigger);
            UpdateProximityTrackingState();
        }

        // 0600263b (0x5ABA50): tracking runs while any trigger is active; with none it stops and the values reset.
        private void UpdateProximityTrackingState()
        {
            if (m_activeProximityTriggers.Count > 0)
            {
                if (m_proximityFunction == null)
                    m_proximityFunction = StartCoroutine(TrackCharacterProximity());
            }
            else
            {
                this.SafeStopCoroutine(ref m_proximityFunction);
                CollectionInProgress = false;
                NormalisedWeightedProximity = 0f;
                NormalisedDistanceProximity = 0f;
            }
        }

        // 0600263c (0x5ABBE0) and its state machine (0x5ABD70): every frame, from the first reward and the character's
        // facing, the distance proximity (unclamped) and the weighted distance/orientation proximity are published.
        private IEnumerator TrackCharacterProximity()
        {
            while (true)
            {
                if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character) || m_collectableRewards[0] == null)
                {
                    yield return null;
                    continue;
                }
                Collectable reward = m_collectableRewards[0];
                CollectionInProgress = reward.HasBeenTriggered;
                Vector3 toReward = reward.transform.position - character.WorldPosition;
                float distance = toReward.magnitude;
                Vector3 direction = toReward / distance;
                // Single-precision locals keep each intermediate rounded to float as the native code does.
                float facing = Mathf.Clamp01(Vector3.Dot(direction, character.CameraForward));
                float orientation = facing * m_proximityOrientationWeighting;
                float distanceRatio = distance / m_proximityStartDistance;
                float distanceProximity = 1f - distanceRatio;
                NormalisedDistanceProximity = distanceProximity;
                float weightedDistance = distanceProximity * m_proximityDistanceWeighting;
                float weightedSum = weightedDistance + orientation;
                float totalWeight = m_proximityOrientationWeighting + m_proximityDistanceWeighting;
                NormalisedWeightedProximity = weightedSum / totalWeight;
                yield return null;
            }
        }

        public MissionCollectableRewardTracker() { }
    }
}
