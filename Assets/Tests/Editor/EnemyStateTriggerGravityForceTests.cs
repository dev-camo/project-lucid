using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.EnemyAbilityTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The enemy gravity force trigger state. Expected values come from the 1.10.1 x86-64 bodies cited per test; the
    // state is built through its own ConstructInstance and entered with a real Enemy whose SimplifiedBody records the
    // request (the body keeps the flag only when its authored CanBeEnabled switch allows it).
    public sealed class EnemyStateTriggerGravityForceTests
    {
        private static IFSMState Build(string json) =>
            EnemyState_TriggerGravityForce.ConstructInstance(NewFsm(), new FSMIdentifier(Name()), json);

        private static string Json(int eventType, bool gravityForce) =>
            "{\"ActiveEventType\":" + eventType + ",\"GravityForceEnabled\":" + (gravityForce ? "true" : "false") + "}";

        private static void WithEnemyBody(Action<Owned, Enemy, SimplifiedBody> body) => Owned.Run(owned =>
        {
            Enemy enemy = NewEnemy(owned);
            SimplifiedBody simplifiedBody = AddSimplifiedBody(enemy.gameObject);
            Set(enemy, "m_simplifiedBody", simplifiedBody);
            body(owned, enemy, simplifiedBody);
        });

        // 0x577E20: GetAs<Enemy>(), a null test, then Enemy.SetGravityForceEnabled with the stored flag (+0x28, read at
        // 0x577E97). The body starts with the force off, so the first state turns it on and a second state with the flag
        // false turns it off again.
        [Test]
        public void TriggerStartForwardsTheConfiguredFlagToTheEnemyBody()
        {
            WithEnemyBody((owned, enemy, body) =>
            {
                Assert.That(body.GravityForceEnabled, Is.False);
                Build(Json(0, true)).OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.True);
                Build(Json(0, false)).OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.False);
            });
        }

        // 0x577EB4: the argument class constructor stores 1 in GravityForceEnabled (+0x14), so omitting it enables the force.
        [Test]
        public void TheFlagDefaultsToEnabled()
        {
            WithEnemyBody((owned, enemy, body) =>
            {
                Build("{}").OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.True);
            });
        }

        // The state only forwards the request: a body whose authored CanBeEnabled switch is off keeps the force off.
        [Test]
        public void TheBodyDecidesWhetherTheForceCanBeEnabled()
        {
            WithEnemyBody((owned, enemy, body) =>
            {
                Set(body, "m_gravityForceCanBeEnabled", false);
                Build(Json(0, true)).OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.False);
            });
        }

        // The constructor stores the event type of the trigger argument class at +0x24 through the base: EnterStart (0)
        // fires on entering, LeaveStart (1) on leaving, and EnterStartLeaveEnd (2) fires on entering and ends on leaving
        // with the base's empty InvokeTriggerEnd.
        [Test]
        public void TheEventTypeDecidesWhenTheStateFires()
        {
            WithEnemyBody((owned, enemy, body) =>
            {
                IFSMState onLeave = Build(Json(1, true));
                onLeave.OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.False, "LeaveStart does nothing on entering");
                onLeave.OnLeave(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.True, "LeaveStart fires on leaving");

                Build(Json(0, false)).OnEnter(enemy, null);
                IFSMState both = Build(Json(2, true));
                both.OnEnter(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.True, "EnterStartLeaveEnd fires on entering");
                Build(Json(0, false)).OnEnter(enemy, null);
                both.OnLeave(enemy, null);
                Assert.That(body.GravityForceEnabled, Is.False, "its end does not start the force again");
            });
        }

        // 0x577E89 tests the user with UnityEngine.Object.op_Equality, so no user and an enemy that was destroyed are both
        // skipped: the destroyed enemy still holds its body reference but the request never reaches it.
        [Test]
        public void ANullOrDestroyedEnemyIsSkipped()
        {
            WithEnemyBody((owned, enemy, body) =>
            {
                IFSMState state = Build(Json(0, true));
                Assert.That(() => state.OnEnter(null, null), Throws.Nothing);
                Object.DestroyImmediate(enemy);
                Assert.That(() => state.OnEnter(enemy, null), Throws.Nothing);
                Assert.That(body.GravityForceEnabled, Is.False);
            });
        }

        // ---- declarations ----

        [Test]
        public void NodeCarriesItsOriginalGraphAuthoringAttributes()
        {
            Type type = typeof(EnemyState_TriggerGravityForce);
            Assert.That(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Enemy/Movement/{0}"));
            Assert.That(type.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo("TriggerGravityForce"));
            AssertIl2CppOptions(type, 2, 1);
        }

        // The factory finds the node by its static ConstructInstance and takes the argument class nested in the state,
        // which is named JSONCtorArgs.
        [Test]
        public void ClassFactoryRegistersTheNodeWithItsArgumentType()
        {
            Type type = typeof(EnemyState_TriggerGravityForce);
            Assert.That(FSMClassFactory.States.TryGetValue(type.Name, out JSONFactoryClass registered), Is.True);
            Assert.That(registered.ClassType, Is.EqualTo(type));
            Assert.That(registered.ConstructInstanceMethod.DeclaringType, Is.EqualTo(type));
            Assert.That(registered.JSONCtorArgsType.Name, Is.EqualTo("JSONCtorArgs"));
            Assert.That(registered.JSONCtorArgsType.DeclaringType, Is.EqualTo(type));
        }

        // Flags straight from the original metadata: one private readonly flag, a protected constructor, the public static
        // factory and the protected start hook; the nested argument class is serialisable, protected, derived from the
        // trigger argument class, and holds the one public flag.
        [Test]
        public void DeclarationShapeKeepsFlagsBasesAndArgumentClass()
        {
            AssertMembers(typeof(EnemyState_TriggerGravityForce), 0x00100001, new[] { ("m_gravityForceEnabled", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("InvokeTriggerStart", 0x00c4) });
            Assert.That(typeof(EnemyState_TriggerGravityForce).BaseType, Is.EqualTo(typeof(FSMState_Trigger)));
            Type arguments = typeof(EnemyState_TriggerGravityForce).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            AssertMembers(arguments, 0x00102004, new[] { ("GravityForceEnabled", 0x0006) }, new[] { (".ctor", 0x1886) });
            Assert.That(arguments.BaseType, Is.EqualTo(typeof(FSMState_Trigger).GetNestedType("TriggerJSONCtorArgs", BindingFlags.NonPublic)));
        }
    }
}
