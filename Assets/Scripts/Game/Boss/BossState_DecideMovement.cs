using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000205 and its arguments class 02000206; bodies translated from the 1.10.1 x86-64 binary.
    // Chooses where the crab boss moves next: the point on the movement-range circle around the boss's origin that lies opposite
    // the character (both projected onto the boss's ground plane), dropped onto the track by a ray cast. It is recalculated on
    // entry and on every update, and stored as the desired movement position.
    [GraphNodeDefaultName("DecideMovement")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_DecideMovement : ActorState_Effect
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class RangeJSONCtorArgs : BaseJSONCtorArgs
        {
            public float BossMovementRange;

            public RangeJSONCtorArgs()
            {
            }
        }

        private const float RaycastHeightBoost = 50f;

        private float m_movementRange;

        protected BossState_DecideMovement(FiniteStateMachine fsm, FSMIdentifier stateId, RangeJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_movementRange = ctorArgs.BossMovementRange;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_DecideMovement(fsm, transitionId, JsonUtility.FromJson<RangeJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            CalculateMovementTarget(user);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            CalculateMovementTarget(user);
        }

        // Nothing happens without a current character. The target is the circle point whose angle is that of the line from the
        // character through the origin (the origin mirrored through the character, both on the boss's ground plane).
        private void CalculateMovementTarget(IGraphUser user)
        {
            Boss boss = user.GetAs<Boss>();
            if (!ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out Character character))
                return;
            Vector3 origin = boss.Storage.GetValue(BossFSMKeys.OriginPoint, Vector3.zero, true);
            Vector3 characterPosition = Vector3.ProjectOnPlane(character.WorldPosition, boss.UpDirection);
            origin = Vector3.ProjectOnPlane(origin, boss.UpDirection);
            Vector3 mirrored = origin + origin - characterPosition;
            float angle = Mathf.Atan2(mirrored.z - origin.z, mirrored.x - origin.x);
            Vector3 target = origin.PointOnCircleWithFixedY(angle, m_movementRange, boss.WorldPosition.y + RaycastHeightBoost);
            target.y = GetHeightFromRaycast(boss, target);
            boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, target);
        }

        // The ground height under the position: a ray down the boss's gravity from the position, twice its height long, on the boss's
        // track mask. Without a hit the position's own height is kept.
        private float GetHeightFromRaycast(Boss boss, Vector3 worldPosition)
        {
            if (Physics.Raycast(new Ray(worldPosition, -boss.UpDirection), out RaycastHit hit, worldPosition.y * 2f, boss.GetTraits().TrackMask))
                return hit.point.y;
            return worldPosition.y;
        }
    }
}
