using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000738 with nested DirectionType 02000739. Pushes characters inside
    // the volume along a direction taken from this transform, a spline or a ribbon, through two
    // velocity modifier overrides held for as long as the character stays. Native code inlines
    // GetDirectionInfluence into the enter and stay callbacks.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDirectionInfluencer : CharacterTrigger
    {
        // 02000739
        private enum DirectionType
        {
            SelfUp = 0,
            SelfDown = 1,
            SelfForward = 2,
            SelfRight = 3,
            SelfBack = 4,
            SelfLeft = 5,
            Spline = 6,
            Ribbon = 7
        }

        [SerializeField]
        [Tooltip("Time category for influencer to be applied in.")]
        private TimeCategory m_timeCategory = TimeCategory.Environment;
        [Tooltip("Value multiplied by direction.")]
        [SerializeField]
        private float m_speed;
        [SerializeField]
        private DirectionType m_directionType = DirectionType.SelfForward;
        [SerializeField]
        [Tooltip("Approximately the time it will take to reach the target direction. A smaller value will reach the target faster.")]
        private float m_smoothDampTimeSeconds;
        [SerializeField]
        [Tooltip("Constant - Direction will be applied as a baseline addition to the character velocity without affecting rotation.\nAcceleration - Direction will be continuously applied to character while in contact.\nOverride - Character velocity will be overriden by this direction.")]
        private DirectionModifierType m_directionModiferType = DirectionModifierType.Constant;
        [Tooltip("X Axis = time in contact\nY axis = Value multiplied by speed.")]
        [SerializeField]
        private AnimationCurve m_speedBlendCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("An upper clamp on the world velocity in the affected direction. Has no affect if modifier type is Override")]
        [SerializeField]
        private float m_maxResultantVelocity;
        [ShowIf("m_directionType", DirectionType.Spline)]
        [SerializeField]
        private Spline m_spline;
        [SerializeField]
        [ShowIf("m_directionType", DirectionType.Ribbon)]
        private Ribbon m_ribbon;
        private readonly Dictionary<Character, DirectionInfluenceData> m_influencingCharacters =
            new Dictionary<Character, DirectionInfluenceData>();
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        private Vector3 m_dampVelocity;

        // 060028af; the character is registered, its influence applied as the configured modifier
        // type and the resultant-velocity cap along the same direction applied beside it.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            TimeManager timeManager = m_timeManagerRef.Get();
            float deltaTime = timeManager.GetFixedDeltaTime(m_timeCategory);
            float timeScale = timeManager.GetTimescale(m_timeCategory);
            var influenceData = new DirectionInfluenceData();
            m_influencingCharacters.Add(character, influenceData);
            Vector3 influence = GetDirectionInfluence(character, influenceData, deltaTime, timeScale);
            influenceData.InfluenceModifierHandle =
                character.AddModifierOverride((int)m_directionModiferType, influence);
            influenceData.MaxVelocityModifierHandle = character.AddModifierOverride(
                (int)GameplayModifierType.MaxVelocityFromInfluence,
                GetDirection(character.WorldPosition) * m_maxResultantVelocity);
        }

        // 060028b0
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            TimeManager timeManager = m_timeManagerRef.Get();
            float deltaTime = timeManager.GetFixedDeltaTime(m_timeCategory);
            float timeScale = timeManager.GetTimescale(m_timeCategory);
            DirectionInfluenceData influenceData = m_influencingCharacters[character];
            influenceData.CurrentBlendTimeSeconds += deltaTime;
            Vector3 influence = GetDirectionInfluence(character, influenceData, deltaTime, timeScale);
            character.AdjustModifierOverrides(influenceData.InfluenceModifierHandle, (int)m_directionModiferType, influence);
        }

        // 060028b1
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            DirectionInfluenceData influenceData = m_influencingCharacters[character];
            character.RemoveModifierOverrides(influenceData.InfluenceModifierHandle);
            character.RemoveModifierOverrides(influenceData.MaxVelocityModifierHandle);
            m_influencingCharacters.Remove(character);
        }

        // 060028b2; the target velocity is the direction scaled by the speed for the time in contact,
        // eased towards with SmoothDamp, and scaled by the time scale for the modifier.
        private Vector3 GetDirectionInfluence(Character character, DirectionInfluenceData influenceData, float deltaTime, float timeScale)
        {
            Vector3 direction = GetDirection(character.WorldPosition);
            influenceData.TargetVelocity = direction * GetSpeed(character, influenceData);
            influenceData.CurrentVelocity = Vector3.SmoothDamp(influenceData.CurrentVelocity,
                influenceData.TargetVelocity, ref m_dampVelocity, m_smoothDampTimeSeconds, Mathf.Infinity, deltaTime);
            return influenceData.CurrentVelocity * timeScale;
        }

        // 060028b3; spline and ribbon directions are not drawn.
        private void OnDrawGizmos()
        {
            if (m_directionType == DirectionType.Spline || m_directionType == DirectionType.Ribbon)
                return;
            DebugArrow.DrawGizmo(transform.position, GetDirection(transform.position), Color.white);
        }

        // 060028b4
        private Vector3 GetDirection(Vector3 targetPosition)
        {
            switch (m_directionType)
            {
                case DirectionType.SelfUp:
                    return transform.up;
                case DirectionType.SelfDown:
                    return -transform.up;
                case DirectionType.SelfForward:
                    return transform.forward;
                case DirectionType.SelfRight:
                    return transform.right;
                case DirectionType.SelfBack:
                    return -transform.forward;
                case DirectionType.SelfLeft:
                    return -transform.right;
                case DirectionType.Spline:
                    return GetForwardFromSurface(targetPosition, m_spline.GetSpline());
                case DirectionType.Ribbon:
                    return GetForwardFromSurface(targetPosition, m_ribbon.GetRibbon());
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 060028b5
        protected virtual float GetSpeed(Character character, DirectionInfluenceData influenceData)
        {
            return m_speedBlendCurve.Evaluate(influenceData.CurrentBlendTimeSeconds) * m_speed;
        }

        // 060028b6; the surface's forward at the point nearest the target.
        private Vector3 GetForwardFromSurface(Vector3 targetPosition, ISurface surface)
        {
            return surface.FindNearestSurfaceLocationFromWorld(targetPosition).m_worldRotation * Vector3.forward;
        }

        // 060028b7
        private void OnCollisionEnter(Collision collision)
        {
            if (TryGetCharacter(collision.collider, out Character character))
                DoTriggerEnter(character);
        }

        // 060028b8
        private void OnCollisionStay(Collision collision)
        {
            if (TryGetCharacter(collision.collider, out Character character))
                DoTriggerStay(character);
        }

        // 060028b9
        private void OnCollisionExit(Collision collision)
        {
            if (TryGetCharacter(collision.collider, out Character character))
                DoTriggerExit(character);
        }

        // 060028ba
        public CharacterDirectionInfluencer() { }
    }
}
