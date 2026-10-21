using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the boss ability and boss state fixtures. A Boss is a real component on an owned, inactive host (no Awake runs
    // in Edit Mode) with the FSM storage Actor.Awake would create, an identity rotation (which also derives its up, forward and right
    // directions as Actor.SetWorldRotation does) and a position. Definitions are real ScriptableObjects owned by the case.
    internal static class BossAbilityTestSupport
    {
        internal const float Tolerance = 1e-4f;

        internal static Boss NewBoss(Owned owned, Vector3 position)
        {
            Boss boss = owned.Host("Lucid boss ability boss", false).AddComponent<Boss>();
            SetActorProperty(boss, "Storage", new FSMStorage(0));
            boss.SetWorldRotation(Quaternion.identity);
            boss.SetWorldPosition(position);
            return boss;
        }

        internal static T NewDefinition<T>(Owned owned, Action<T> setup = null) where T : AbilityDefinition
        {
            T definition = owned.Track(ScriptableObject.CreateInstance<T>());
            setup?.Invoke(definition);
            return definition;
        }

        // An initialised ability of a boss; the definition is applied by the ability's own Initialise.
        internal static TAbility NewAbility<TAbility>(Boss boss, AbilityDefinition definition) where TAbility : ActorAbility, new()
        {
            var ability = new TAbility();
            ability.Initialise(boss, definition);
            return ability;
        }

        internal static T Stored<T>(Boss boss, GraphStorageKey key) => boss.Storage.GetValue(key, default(T), false);

        internal static bool Has(Boss boss, GraphStorageKey key) => boss.Storage.GetCollection().ContainsKey(key);

        // A character the registered manager reports as current, standing at the position. The body write of
        // Character.SetWorldPosition needs a live rigidbody, so the position is stored as the Actor's own code does.
        internal static void MakeCurrent(CharacterManager manager, Character character, Vector3 position)
        {
            SetActorProperty(character, "WorldPosition", position);
            SetCurrent(manager, character, true);
        }

        internal static void AssertNear(Vector3 actual, Vector3 expected, string label, float tolerance = Tolerance)
        {
            NUnit.Framework.Assert.That(actual.x, NUnit.Framework.Is.EqualTo(expected.x).Within(tolerance), label + " x");
            NUnit.Framework.Assert.That(actual.y, NUnit.Framework.Is.EqualTo(expected.y).Within(tolerance), label + " y");
            NUnit.Framework.Assert.That(actual.z, NUnit.Framework.Is.EqualTo(expected.z).Within(tolerance), label + " z");
        }

        // A real Animator driven by a controller with the given bool parameters, wrapped by the genuine ActorAnimator constructor
        // and installed as the boss's animator.
        internal static Animator AttachBossAnimator(Owned owned, Boss boss, params string[] bools)
        {
            var animator = owned.Host("Lucid boss animator", true).AddComponent<Animator>();
            var controller = owned.Track(new AnimatorController());
            controller.AddLayer("Base");
            foreach (string name in bools) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetActorProperty(boss, "Animator", new ActorAnimator(animator));
            return animator;
        }

        // An animation definition mapping each animation type to the named animator parameter.
        internal static ActorAnimationDefinition NewAnimationDefinition(Owned owned, params (ActorAnimationType type, string parameter)[] map)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
            Set(definition, "m_parameters", map.Select(entry => new ActorAnimationDefinition.AnimationParameter
            {
                AnimationType = entry.type,
                ParameterName = Parameter(entry.parameter),
            }).ToArray());
            Call(definition, "RebuildAnimationParameters");
            return definition;
        }

        // A FourmenManager registered into its absent row that has received its BossManager through its own callback, with the
        // given bosses listed on that manager. Taken before anything that caches a typed FourmenManager reference.
        internal sealed class FourmenRig
        {
            public FourmenManager Fourmen;
            public BossManager Manager;
            public List<Boss> Bosses = new List<Boss>();
        }

        internal static FourmenRig NewFourmenRig(Owned owned, params Vector3[] bossPositions)
        {
            // BossManager's field initialiser caches a typed SaveManager reference.
            owned.LeaseRow<SaveManager>();
            var rig = new FourmenRig
            {
                Manager = owned.Register(owned.Host("Lucid fourmen boss manager", false).AddComponent<BossManager>()),
            };
            rig.Fourmen = owned.Register(owned.Host("Lucid fourmen manager", false).AddComponent<FourmenManager>());
            Call(rig.Fourmen, "OnBossManagerValid", rig.Manager);
            foreach (Vector3 position in bossPositions)
            {
                Boss boss = NewBoss(owned, position);
                rig.Bosses.Add(boss);
                rig.Manager.Bosses.Add(boss);
            }
            return rig;
        }

        // The boss body dictionary entry the boss itself would hold for an attach point, with its destroyed flag.
        internal static void AddBodyPart(Boss boss, ActorAttachPointType point, bool destroyed)
        {
            Type part = typeof(Boss).GetNestedType("BossBodyComponents", System.Reflection.BindingFlags.NonPublic);
            object record = Activator.CreateInstance(part);
            part.GetField("AttachPoint").SetValue(record, point);
            part.GetMethod("Initialise").Invoke(record, null);
            if (destroyed)
                part.GetMethod("RegisterHit").Invoke(record, null);
            ((System.Collections.IDictionary)Get<object>(boss, "m_attachPointDictionary")).Add(point, record);
        }
    }
}
