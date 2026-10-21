using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000631; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EnemyAbility : ActorAbility, IAbilityTrigger
    {
        protected Enemy m_enemy;

        // 0600215e (1.10.1 RVA 0x56E4F0)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_enemy = actor as Enemy;
        }

        // 0600215f (1.10.1 RVA 0x56E5C0), 06002160 (1.10.1 RVA 0x56E5D0)
        public bool IsTriggered { get; protected set; }

        // 06002161, 06002162
        public abstract void UpdateMovement(float deltaTime);
        public abstract void UpdateMovementAnimation(ActorAnimationDefinition animation);

        // 06002163 (1.10.1 RVA 0x56E5E0): only an enemy whose visual proxy is detached steers it. On a spline or a
        // static-and-return navigation the proxy faces along the right axis of the tracked rotation (the spline's
        // location rotation, or the enemy's own) with the enemy's up direction; any other navigation passes the
        // enemy's rotation through.
        public virtual void UpdateVisualProxyMovement(float deltaTime)
        {
            if (!m_enemy.Definition.Traits.ShouldDetachVisualProxy)
            {
                return;
            }

            Quaternion rotation = m_enemy.WorldRotation;
            EnemyNavigation navigation = m_enemy.Navigation;
            if (navigation is EnemyNavigationSpline spline)
            {
                rotation = spline.GetLocationWorldRotation();
            }
            if (navigation is EnemyNavigationSpline || navigation is EnemyNavigationStaticAndReturnAfterChase)
            {
                rotation = Quaternion.LookRotation(rotation * Vector3.right, m_enemy.UpDirection);
            }
            m_enemy.VisualProxy.UpdateRotation(rotation, deltaTime);
        }

        // 06002164 (1.10.1 RVA 0x56E760)
        protected EnemyAbility() { }
    }
}
