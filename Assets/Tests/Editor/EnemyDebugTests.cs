using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.Enemies03TestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy debug overlay controller. Expected values come from the 1.10.1 x86-64 bodies cited per test. The
    // component is a real EnemyDebug on an active owned host with its UI container as a child; the controller binds
    // itself to the shared ControlMapping, so every fixture checks that the boss debug input is unbound before it
    // starts and removes the entries it made.
    public sealed class EnemyDebugTests
    {
        private const string ContainerName = "uicontainer_enemy_debug_info";

        private static readonly string[] MappingNames =
        {
            "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease", "s_onMultiTouch", "s_onMultiTouchRelease",
            "s_onVectorisedGameInput", "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis", "s_onAxisStart", "s_onAxisEnd", "s_onSwipe",
        };

        private static IEnumerable<(string name, IDictionary dictionary)> Mappings() =>
            Array.ConvertAll(MappingNames, name => (name,
                (IDictionary)typeof(ControlMapping).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)));

        private static bool IsBossDebugKey(object key) =>
            (GameInput)key.GetType().GetField("GameInput").GetValue(key) == GameInput.BossDebugInfo
            && (int)key.GetType().GetField("JoystickIndex").GetValue(key) == -1;

        private static void RemoveBossDebugEntries()
        {
            foreach ((string name, IDictionary dictionary) in Mappings())
            {
                var keys = new List<object>();
                foreach (DictionaryEntry entry in dictionary)
                    if (IsBossDebugKey(entry.Key)) keys.Add(entry.Key);
                foreach (object key in keys) dictionary.Remove(key);
            }
        }

        // The callbacks bound to the boss debug input under the given dictionary, as target and method name.
        private static List<(object target, string method)> Bound(string dictionaryName)
        {
            var bound = new List<(object, string)>();
            foreach ((string name, IDictionary dictionary) in Mappings())
            {
                if (name != dictionaryName) continue;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!IsBossDebugKey(entry.Key)) continue;
                    foreach (Action<float> callback in ((FastAction<float>)entry.Value).GetInvocationList())
                        bound.Add((callback.Target, callback.Method.Name));
                }
            }
            return bound;
        }

        private static void WithDebug(bool withContainer, Action<Owned, EnemyDebug, GameObject> body)
        {
            foreach ((string name, IDictionary dictionary) in Mappings())
                foreach (DictionaryEntry entry in dictionary)
                    Assert.That(IsBossDebugKey(entry.Key), Is.False, "the fixture needs the boss debug input unbound in " + name);
            try
            {
                Owned.Run(owned =>
                {
                    GameObject host = owned.Host(Name(), true);
                    GameObject container = null;
                    if (withContainer)
                    {
                        container = owned.Host(ContainerName, true, host.transform);
                    }
                    body(owned, host.AddComponent<EnemyDebug>(), container);
                });
            }
            finally
            {
                RemoveBossDebugEntries();
            }
        }

        // 0x576580 returns the literal.
        [Test]
        public void TheContainerIdentifierIsTheEnemyDebugInfoName()
        {
            WithDebug(false, (owned, debug, container) =>
                Assert.That(typeof(EnemyDebug).GetProperty("ContainerIdentifierName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(debug),
                    Is.EqualTo("uicontaineridentifier_enemy_debug_info")));
        }

        // 0x5765B0: the owner is stored first (0x576601), then the child named uicontainer_enemy_debug_info is looked up
        // (Transform.Find at 0x576626) and its game object kept, then Action<float>(OnEnemyInfo) is bound to
        // GameInput 0x878DD742, which is BossDebugInfo, as a Down trigger (edx = 1) for any joystick (ecx = -1): the
        // down button pair, then the shared swipe and scroll wheel subscriptions, and none of the held or up ones.
        [Test]
        public void InitTakesTheContainerAndBindsTheBossDebugInputToTheOverlay()
        {
            Owned.Run(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                WithDebug(true, (inner, debug, container) =>
                {
                    debug.Init(enemy);
                    Assert.That(Get<Actor>(debug, "m_actor"), Is.SameAs(enemy));
                    Assert.That(Get<GameObject>(debug, "m_debugUIContainer"), Is.SameAs(container));
                    foreach (string name in new[] { "s_onButtonsDown", "s_onAxisStart", "s_onSwipe", "s_onScrollWheel" })
                        Assert.That(Bound(name), Is.EqualTo(new[] { ((object)debug, "OnEnemyInfo") }), name);
                    foreach (string name in new[] { "s_onButtonsHeld", "s_onAxis", "s_onButtonsUp", "s_onAxisEnd", "s_onPointer", "s_onTap" })
                        Assert.That(Bound(name), Is.Empty, name);
                });
            });
        }

        // The owner is stored before the lookup, and a missing container faults on the null game object before anything
        // is bound.
        [Test]
        public void InitWithoutTheContainerKeepsTheOwnerAndBindsNothing()
        {
            Owned.Run(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                WithDebug(false, (inner, debug, container) =>
                {
                    Assert.That(() => debug.Init(enemy), Throws.InstanceOf<NullReferenceException>());
                    Assert.That(Get<Actor>(debug, "m_actor"), Is.SameAs(enemy));
                    Assert.That(Bound("s_onButtonsDown"), Is.Empty);
                });
            });
        }

        // 0x5766D0 stores the container's activeInHierarchy in IsEnabled (0x5766E4) and then sets the container active to
        // the opposite (xor al, 1 at 0x5766EB): IsEnabled is the state the toggle found, not the one it left.
        [Test]
        public void TogglingFlipsTheContainerAndRecordsTheStateItFound()
        {
            Owned.Run(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                WithDebug(true, (inner, debug, container) =>
                {
                    debug.Init(enemy);
                    Assert.That(container.activeInHierarchy, Is.True);
                    debug.ToggleDebugInfo();
                    Assert.That(container.activeInHierarchy, Is.False);
                    Assert.That(debug.IsEnabled, Is.True, "it found the container shown");
                    debug.ToggleDebugInfo();
                    Assert.That(container.activeInHierarchy, Is.True);
                    Assert.That(debug.IsEnabled, Is.False, "it found the container hidden");
                });
            });
        }

        // 0x5766B0 tail-calls the virtual toggle through slot 7 whatever the input amount is.
        [Test]
        public void TheInputCallbackTogglesWhateverTheAmount()
        {
            Owned.Run(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                WithDebug(true, (inner, debug, container) =>
                {
                    debug.Init(enemy);
                    foreach (float amount in new[] { 1f, 0f, -3f, float.NaN })
                    {
                        bool shown = container.activeInHierarchy;
                        Call(debug, "OnEnemyInfo", amount);
                        Assert.That(container.activeInHierarchy, Is.EqualTo(!shown), "amount " + amount);
                    }
                });
            });
        }

        // 0x576700 unsubscribes a new Action<float>(OnEnemyInfo) from the same input and joystick; the dictionaries keep
        // their emptied entries.
        [Test]
        public void DestroyingUnbindsTheOverlayCallback()
        {
            Owned.Run(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                WithDebug(true, (inner, debug, container) =>
                {
                    debug.Init(enemy);
                    Assert.That(Bound("s_onButtonsDown"), Has.Count.EqualTo(1));
                    Call(debug, "OnDestroy");
                    foreach ((string name, IDictionary dictionary) in Mappings())
                        Assert.That(Bound(name), Is.Empty, name);
                });
            });
        }

        // ---- declarations ----

        [Test]
        public void DeclarationShapeKeepsFlagsFieldsAndIl2CppOptions()
        {
            AssertMembers(typeof(EnemyDebug), 0x00100001, new[] { ("m_debugUIContainer", 0x0001) }, new[]
            {
                ("get_ContainerIdentifierName", 0x08c4), ("Init", 0x00c6), ("OnEnemyInfo", 0x0081), ("ToggleDebugInfo", 0x00c6),
                ("OnDestroy", 0x0081), (".ctor", 0x1886),
            });
            Assert.That(typeof(EnemyDebug).BaseType, Is.EqualTo(typeof(ActorDebug)));
            AssertIl2CppOptions(typeof(EnemyDebug), 1, 2);
        }
    }
}
