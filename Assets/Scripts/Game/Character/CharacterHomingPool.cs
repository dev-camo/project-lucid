using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original 0200032f: 30 owner API rows (one compiler local function), nine
    // fields. Whole provider candidate; real Character/Actor/message graph required.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterHomingPool
    {
        public ActiveHomingTarget ActiveTarget { get; private set; }
        private PooledTarget m_pendingTarget;
        private List<PooledTargetGroup> m_targetGroupsByPriority = new List<PooledTargetGroup>();
        private Dictionary<HomingTargetType, PooledTargetGroup> m_targetTypeToGroupMap =
            new Dictionary<HomingTargetType, PooledTargetGroup>(HardlightEnumComparers.HomingTargetTypeComparer);
        private Character m_character;
        private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
        private GameObject m_debugLocationRoot;
        private float m_targetGracePeriodTimer;
        private Stack<PooledTarget> m_pooledTargets = new Stack<PooledTarget>();

        public PooledTarget PendingTarget => m_pendingTarget; // 060013f0

        // 060013f1; original initializer allocation/registration precedes base ctor.
        public CharacterHomingPool(Character character, Dictionary<HomingTargetType, TargetingTypePriorityDefinition> priorityDefinitions)
        {
            m_character = character;
            foreach (KeyValuePair<HomingTargetType, TargetingTypePriorityDefinition> entry in priorityDefinitions)
            {
                int typePriority = entry.Value.Priority;
                int index = m_targetGroupsByPriority.FindIndex(group => group.Priority == typePriority);
                if (index >= 0)
                    m_targetTypeToGroupMap.Add(entry.Key, m_targetGroupsByPriority[index]);
                else
                {
                    PooledTargetGroup group = new PooledTargetGroup(typePriority);
                    m_targetGroupsByPriority.Add(group);
                    m_targetTypeToGroupMap.Add(entry.Key, group);
                }
            }
            m_targetGroupsByPriority.Sort();
            m_targetGroupsByPriority.Reverse();
        }

        // 060013f2; an existing target gains an ability without refreshing its data.
        public void AddPotentialTarget(GameObject gameObject, Vector3 position, Quaternion rotation,
            HomingTargetType type, IHomingAbility homingAbility, TargetObjectMetaData metaData = null)
        {
            List<PooledTarget> targets = GetListForTargetType(type);
            if (targets.TryFind(target => target.Object.Target == gameObject, out PooledTarget pooledTarget))
                pooledTarget.Abilities.AddUnique(homingAbility);
            else
            {
                CharacterTargetObjectData data = new CharacterTargetObjectData
                {
                    Target = gameObject, WorldPosition = position, WorldRotation = rotation,
                    Type = type, MetaData = metaData
                };
                pooledTarget = PooledTargetSpawn(data, homingAbility);
                targets.Add(pooledTarget);
            }
        }

        // 060013f3 + original 0600140b local helper. EndGracePeriod does not clear
        // the pending record. Only empty ability lists enter the recycled pool.
        public void RemoveTarget(IHomingAbility homingAbility, HomingTargetType targetType, GameObject gameObject)
        {
            bool IsMatch(PooledTarget pooledTarget) => pooledTarget.Object.Target == gameObject &&
                pooledTarget.Abilities != null && pooledTarget.Abilities.Contains(homingAbility);
            if (IsMatch(m_pendingTarget)) EndGracePeriod();
            List<PooledTarget> targets = GetListForTargetType(targetType);
            if (targets == null) return;
            foreach (PooledTarget target in targets)
                if (IsMatch(target)) target.Abilities.Remove(homingAbility);
            targets.RemoveAll(TryPooledTargetDespawn);
        }

        // 060013f4; this overload removes whole records without recycling them.
        public void RemoveTarget(IHomingAbility homingAbility)
        {
            if (m_pendingTarget.Abilities != null && m_pendingTarget.Abilities.Contains(homingAbility))
                EndGracePeriod();
            foreach (PooledTargetGroup group in m_targetGroupsByPriority)
            {
                List<PooledTarget> targets = group.PooledTargets;
                for (int i = targets.Count - 1; i >= 0; --i)
                    if (targets[i].Abilities.Contains(homingAbility)) targets.RemoveAt(i);
            }
        }

        public void Update(float deltaTime) => ProcessTargets(deltaTime); // 060013f5

        // 060013f6: retain the other pending-data fields and the map/pool/timer.
        public void Close()
        {
            m_targetGroupsByPriority.Clear();
            ActiveTarget = null;
            m_pendingTarget.Object.Target = null;
            m_pendingTarget.Abilities = null;
            PublishTargetingMessage(false, false, m_pendingTarget);
        }

        // 060013f7: publish precedes group mutation, grace, and triggered setters.
        public void ClearTarget(bool startGracePeriod = false)
        {
            m_pendingTarget = default;
            PublishTargetingMessage(false, false, m_pendingTarget);
            if (startGracePeriod)
            {
                foreach (PooledTargetGroup group in m_targetGroupsByPriority) group.PooledTargets.Clear();
                StartGracePeriod();
            }
            else EndGracePeriod();
            if (ActiveTarget != null)
            {
                foreach (IHomingAbility ability in ActiveTarget.Abilities) ability.IsTriggered = false;
                ActiveTarget = null;
            }
        }

        // 060013f8; copy the list, keep live original metadata, then publish.
        public bool LockPendingTarget(float speed, Vector3 restoreVelocity, float rotationalSpeed = 0f)
        {
            if (!IsTargetPending()) return false;
            ActiveTarget = new ActiveHomingTarget
            {
                Abilities = new List<IHomingAbility>(m_pendingTarget.Abilities),
                Object = m_pendingTarget.Object, RestoreVelocity = restoreVelocity,
                Speed = speed, RotationalSpeed = rotationalSpeed
            };
            if (CharacterMovementUtilities.AreRotationsReversed(m_character.WorldRotation, ActiveTarget.Object.WorldRotation))
                ActiveTarget.Object.WorldRotation = ActiveTarget.Object.WorldRotation * Actor.RotationReverseLocal;
            PublishTargetingMessage(true, true, m_pendingTarget);
            return true;
        }

        // 060013f9: no message, grace update, or reversal calculation in this path.
        public void SetTargetImmediately(IHomingAbility homingAbility, CharacterTargetObjectData targetObject,
            Vector3 restoreVelocity, float targetSpeed, float rotationalSpeed = 0f)
        {
            ActiveTarget = new ActiveHomingTarget
            {
                Abilities = new List<IHomingAbility> { homingAbility }, Object = targetObject,
                Speed = targetSpeed, RotationalSpeed = rotationalSpeed, RestoreVelocity = restoreVelocity
            };
        }

        public bool PendingTargetIsValidForAbility(IHomingAbility homingAbility) => // 060013fa
            homingAbility.CanActivate && IsTargetPending() && m_pendingTarget.Abilities.Contains(homingAbility);
        public bool IsTargetPending() => m_pendingTarget.Object.Target != null; // 060013fb Unity null
        public bool IsTargetActive() => ActiveTarget != null; // 060013fc managed null
        public bool IsTargetActive(CharacterAbility ability) // 060013fd
        {
            IHomingAbility homingAbility = ability as IHomingAbility;
            return homingAbility != null && ActiveTarget != null && ActiveTarget.Abilities.Contains(homingAbility);
        }

        // 060013fe: return the first triggered ability's value, even when false.
        public bool ActiveTargetOverridesTargeting()
        {
            if (ActiveTarget == null) return false;
            foreach (IHomingAbility ability in ActiveTarget.Abilities)
                if (ability.IsTriggered) return ability.OverridesTargeting;
            return false;
        }

        private void ProcessTargets(float deltaTime) // 060013ff
        {
            if (ActiveTarget != null) return;
            m_targetGracePeriodTimer -= deltaTime;
            // Original ARM FCMP unordered NZCV=0011 satisfies B.LE through N!=V;
            // original x86 UCOMISS unordered satisfies JBE through CF/ZF. Both skip
            // grace for NaN, matching this >0 comparison. The prior inference of
            // a backend difference was rejected; frozen predecessor bytes remain.
            if (m_targetGracePeriodTimer > 0f)
            {
                if (m_pendingTarget.Object.Target == null) return;
                Vector3 origin = m_character.ColliderCollision.bounds.center;
                Vector3 direction = m_pendingTarget.Object.WorldPosition - origin;
                float distance = direction.magnitude;
                direction /= distance;
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance,
                    m_character.CollisionMask, QueryTriggerInteraction.Ignore)) return;
                m_targetGracePeriodTimer = 0f;
            }
            bool isValid = TryFindBestPooledTarget(out PooledTarget bestTarget);
            bool changed = bestTarget.Object.Target != m_pendingTarget.Object.Target;
            m_pendingTarget = bestTarget;
            if (!changed) return;
            PublishTargetingMessage(isValid, false, bestTarget);
            if (IsTargetPending())
            {
                StartGracePeriod();
                m_pendingTarget.Object.MetaData?.DoOnTargeted();
            }
        }

        private void PublishTargetingMessage(bool isValid, bool isActive, PooledTarget target) // 06001400 + <>c 1410..12
        {
            HomingTargetUpdateMessage message = new HomingTargetUpdateMessage
            {
                IsValid = isValid, TargetValue = target, IsActive = isActive
            };
            MessageManager manager = m_messageManagerRef.GetSafe();
            if (manager != null)
            {
                Component character = m_character;
                manager.ComponentMessagesWithCompletion.PublishMessage(in character, in message, () => { }, -1);
            }
        }

        private bool TryFindBestPooledTarget(out PooledTarget bestTarget) // 06001401
        {
            bool innerVolumeOnly = m_character.Storage.GetValueOnly<bool>(ActorFSMKeys.HomingTargetInnerVolumeOnly, false);
            foreach (PooledTargetGroup group in m_targetGroupsByPriority)
                if (TryFindBestTarget(group.PooledTargets, innerVolumeOnly, out bestTarget)) return true;
            bestTarget = default;
            return false;
        }

        private bool TryFindBestTarget(List<PooledTarget> pooledTargets, bool innerVolumeOnly, out PooledTarget bestTarget) // 06001402
        {
            bestTarget = default;
            Vector3 origin = m_character.ColliderCollision.bounds.center;
            Vector3 intendedForward = m_character.HasControllerMovement ? m_character.RawIntendedForward : m_character.ForwardDirection;
            bool found = false;
            bool preferOnScreen = false;
            bool bestIsInner = false;
            float closestDistance = float.MaxValue;
            foreach (PooledTarget target in pooledTargets)
            {
                if (!target.Object.Target.activeInHierarchy) continue;
                IHomingAbility ability = target.Abilities[0];
                CharacterAbilityDefinition_Targeting definition = ability.TargetingDefinition;
                bool projectFromCamera = ability.ShouldProjectFromCamera();
                Vector3 direction = target.Object.WorldPosition - origin;
                float distance = direction.magnitude;
                direction /= distance;
                bool valid = TargetIsValid(target, direction, intendedForward);
                preferOnScreen |= projectFromCamera;
                if (!valid) continue;
                // Original arithmetic subtracts direction multiplied by the dot.
                Vector3 offset = direction - direction * Vector3.Dot(intendedForward, direction);
                bool isInner = offset.sqrMagnitude < definition.TargetInnerRadiusSqr;
                if (!isInner && (innerVolumeOnly || bestIsInner)) continue;
                bool onScreen = m_character.WorldPositionIsOnScreen(target.Object.WorldPosition, Vector2.zero);
                if (preferOnScreen && !onScreen) continue;
                if (!CanReachTarget(origin, direction, distance)) continue;
                if ((isInner && !bestIsInner) || distance < closestDistance || (onScreen && !preferOnScreen))
                {
                    bestTarget = target;
                    found = true;
                    bestIsInner = isInner;
                    closestDistance = distance;
                    preferOnScreen = onScreen;
                }
            }
            return found;
        }

        private bool TargetIsValid(PooledTarget target, Vector3 raycastDirection, Vector3 intendedForward) // 06001403
        {
            IHomingAbility ability = target.Abilities[0];
            if (ability.OverridesTargeting) return true;
            CharacterAbilityDefinition_Targeting definition = ability.TargetingDefinition;
            bool projectFromCamera = ability.ShouldProjectFromCamera();
            // ARM cset lt/x86 setb keep unordered values true for this comparison.
            bool withinPitch = !(Mathf.Abs(Vector3.Dot(raycastDirection, m_character.UpDirection)) >= definition.TargetMaxPitchCosine);
            if (!withinPitch || projectFromCamera) return withinPitch;
            Vector3 local = m_character.transform.InverseTransformDirection(raycastDirection);
            local.y = 0f;
            Vector3 world = m_character.transform.TransformDirection(local);
            return Vector3.Dot(intendedForward, world) > definition.InputDiscardTargetCosine;
        }

        private List<PooledTarget> GetListForTargetType(HomingTargetType targetType) => // 06001404
            m_targetTypeToGroupMap.TryGetValue(targetType, out PooledTargetGroup group) ? group.PooledTargets : null;
        private bool CanReachTarget(Vector3 raycastOrigin, Vector3 raycastDirection, float raycastDistance) => // 06001405
            !Physics.Raycast(raycastOrigin, raycastDirection, out RaycastHit hit, raycastDistance,
                m_character.CollisionMask, QueryTriggerInteraction.Ignore);
        private void StartGracePeriod() => m_targetGracePeriodTimer = m_character.Constants.HomingTargetGracePeriodSeconds; // 06001406
        private void EndGracePeriod() => m_targetGracePeriodTimer = 0f; // 06001407

        public void GetFSMTargetDebugInfo(StringBuilder stringInfoBuilder) // 06001408
        {
            GameObject target = ActiveTarget?.Object.Target;
            if (target == null)
            {
                stringInfoBuilder.AppendLine("FSM Target: None");
                return;
            }
            bool homingActive = ActiveTarget.Object.TryGetMetaDataAsType<HomingObjectMetaData>(out HomingObjectMetaData data) && data.HomingTarget.enabled;
            stringInfoBuilder.AppendLine("FSM Target: " + target.name);
            stringInfoBuilder.AppendLine(string.Format("Active = {0}, HomingActive = {1}", ActiveTarget != null, homingActive));
        }

        private PooledTarget PooledTargetSpawn(CharacterTargetObjectData targetedObject, IHomingAbility homingAbility) // 06001409
        {
            if (!m_pooledTargets.TryPop(out PooledTarget target))
                target = new PooledTarget { Abilities = new List<IHomingAbility>() };
            target.Object = targetedObject;
            target.Abilities.Add(homingAbility);
            return target;
        }

        private bool TryPooledTargetDespawn(PooledTarget pooledTarget) // 0600140a
        {
            if (pooledTarget.Abilities.Count != 0) return false;
            pooledTarget.Object = default;
            pooledTarget.Abilities.Clear();
            m_pooledTargets.Push(pooledTarget);
            return true;
        }

        public struct PooledTarget // 02000330: complete two-field zero-method value type.
        {
            public List<IHomingAbility> Abilities;
            public CharacterTargetObjectData Object;
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class PooledTargetGroup : IComparable<PooledTargetGroup> // 02000331
        {
            public int Priority { get; } // 0600140c
            public List<PooledTarget> PooledTargets { get; } // 0600140d
            public PooledTargetGroup(int priority) // 0600140e
            {
                Priority = priority;
                PooledTargets = new List<PooledTarget>();
            }
            public int CompareTo(PooledTargetGroup other) // 0600140f
            {
                if (ReferenceEquals(this, other)) return 0;
                if (ReferenceEquals(other, null)) return 1;
                return Priority.CompareTo(other.Priority);
            }
        }
    }
}
