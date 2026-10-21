using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001f1; bodies translated from the 1.10.1 x86-64 binary (0x6FF8E0..0x700640).
    // The guardian circles the arena at a stated radius and stays a set arc ahead of the character: its angle is
    // advanced by the movement rate in the circling direction each update, then clamped between the minimum and maximum
    // radians ahead of the character's angle.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_StayAheadOfPlayer : ActorAbility, IAbilityBossMovement
    {
        private readonly Vector3 ArenaCentre = Vector3.zero;

        public BossAbilityDefinition_StayAheadOfPlayer Definition;

        private Boss m_boss;
        private CharacterManager m_characterManager;
        private Vector3 m_bossEntryPoint;
        private Vector3 m_currentTargetPosition;
        private Vector3 m_centerOfCircle;
        private float m_radiusOfCircle;
        private float m_finalArcRadians;
        private float m_progressionArcRadians;
        private bool m_atMaxRange;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b43
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000b44
        public float MovementAnimationAngleMultiplier => 0f; // 06000b45
        public float FaceAngleTolerance => 1f; // 06000b46
        public float TurnSpeed => Definition.TurnSpeed; // 06000b47
        public bool IsMomentum => false; // 06000b48
        public bool TurnToCharacter => true; // 06000b49
        public bool AtMaxRange => m_atMaxRange; // 06000b4a

        // 06000b4b: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_StayAheadOfPlayer;
            m_characterManager = ProcessManager.GetSystem<CharacterManager>();
            m_centerOfCircle = ArenaCentre;
            m_radiusOfCircle = Definition.Radius;
        }

        // 06000b4c
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            SetupAbility();
        }

        // 06000b4d: resumes from the stored angle when there is one, otherwise starts the minimum arc ahead of the character
        // (nothing else is set up without a character). The direction is published for the guardian's projectiles.
        private void SetupAbility()
        {
            AbilityFinished = false;
            m_centerOfCircle = ArenaCentre;
            m_radiusOfCircle = Definition.Radius;
            if (m_boss.Storage.HasValue(BossFSMKeys.RadiansCurrentPosition))
            {
                m_progressionArcRadians = m_boss.Storage.GetValue(BossFSMKeys.RadiansCurrentPosition, m_progressionArcRadians, true);
            }
            else
            {
                if (!m_characterManager.TryGetCurrentCharacter(out Character character))
                    return;
                m_progressionArcRadians = GetArcAngle(character.WorldPosition) * Mathf.Deg2Rad + Definition.MinRadiansAheadOfPlayer;
            }
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedYLHS(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
            m_atMaxRange = false;
            m_boss.Storage.SetValue(BossFSMKeys.GuardianIsClockwise, Definition.Clockwise);
        }

        // 06000b4e: the angle is kept for the next entry.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_boss.Storage.SetValue(BossFSMKeys.RadiansCurrentPosition, m_progressionArcRadians);
        }

        // 06000b4f: the clockwise angle in degrees from the forward axis (scaled by the radius) to the position on the ground
        // plane, in the range zero to 360.
        private float GetArcAngle(Vector3 position)
        {
            float angle = Vector3.SignedAngle(Vector3.forward * m_radiusOfCircle, new Vector3(position.x, 0f, position.z), Vector3.up);
            if (angle < 0f)
                angle = 360f + angle;
            return angle;
        }

        // 06000b50: the boss's angle moves by the movement rate (towards the character's side, wrapping a full turn) and is
        // kept between the minimum and maximum arc ahead of the character. At the upper limit it counts as at maximum range.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (AbilityFinished)
                return;
            m_radiusOfCircle = Definition.Radius;
            if (!m_characterManager.TryGetCurrentCharacter(out Character character))
                return;
            CalculateAngles(character, out float characterArcAngleRad, out float bossArcAngleDeg, out _, out _);

            float bossArcRadians = bossArcAngleDeg * Mathf.Deg2Rad;
            float movement = deltaTime * Definition.MovementPerSecond;
            float newBossArc;
            float lower;
            float upper;
            if (Definition.Clockwise)
            {
                newBossArc = bossArcRadians + movement;
                if (newBossArc < characterArcAngleRad)
                    newBossArc += Mathf.PI * 2f;
                lower = characterArcAngleRad + Definition.MinRadiansAheadOfPlayer;
                upper = characterArcAngleRad + Definition.MaxRadiansAheadOfPlayer;
            }
            else
            {
                newBossArc = bossArcRadians - movement;
                if (characterArcAngleRad < newBossArc)
                    newBossArc -= Mathf.PI * 2f;
                lower = characterArcAngleRad - Definition.MaxRadiansAheadOfPlayer;
                upper = characterArcAngleRad - Definition.MinRadiansAheadOfPlayer;
            }
            m_progressionArcRadians = Mathf.Clamp(newBossArc, lower, upper);
            m_atMaxRange = m_progressionArcRadians == upper;
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedYLHS(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
        }

        // 06000b51: the character's angle in radians, the boss's in degrees, and zero for the arc limits.
        private void CalculateAngles(Character character, out float characterArcAngleRad, out float bossArcAngleDeg,
            out float minArcRadians, out float maxArcRadians)
        {
            characterArcAngleRad = GetArcAngle(character.WorldPosition) * Mathf.Deg2Rad;
            bossArcAngleDeg = GetArcAngle(m_boss.WorldPosition);
            minArcRadians = 0f;
            maxArcRadians = 0f;
        }

        // 06000b52: true when the boss's angle (wrapped a full turn to the character's side) lies strictly inside the arc
        // ahead of the character. It leaves the wrapped angle as the progression angle.
        public bool ShouldAbilityActivate()
        {
            if (!m_characterManager.TryGetCurrentCharacter(out Character character))
                return false;
            CalculateAngles(character, out float characterArcAngleRad, out float bossArcAngleDeg, out _, out _);
            float bossArcRadians = bossArcAngleDeg * Mathf.Deg2Rad;
            m_progressionArcRadians = bossArcRadians;
            float lower;
            float upper;
            if (Definition.Clockwise)
            {
                if (characterArcAngleRad > bossArcRadians)
                {
                    bossArcRadians += Mathf.PI * 2f;
                    m_progressionArcRadians = bossArcRadians;
                }
                lower = characterArcAngleRad + Definition.MinRadiansAheadOfPlayer;
                upper = characterArcAngleRad + Definition.MaxRadiansAheadOfPlayer;
            }
            else
            {
                if (bossArcRadians > characterArcAngleRad)
                {
                    bossArcRadians -= Mathf.PI * 2f;
                    m_progressionArcRadians = bossArcRadians;
                }
                lower = characterArcAngleRad - Definition.MaxRadiansAheadOfPlayer;
                upper = characterArcAngleRad - Definition.MinRadiansAheadOfPlayer;
            }
            return bossArcRadians > lower && upper > bossArcRadians;
        }
    }
}
