using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterUiModifier (TypeDef 0x020009ec, x86-64 0x6AA320..0x6AA44F),
    // CharacterVisualProxyModifier (0x020009ed, 0x6AA450..0x6AA60F), CharacterTeleportLocal (0x02000ac3,
    // 0x6E64F0..0x6E689F), HomingAttack (0x02000ac5, 0x6E6930..0x6E6EAF), PlayerDamageableObject (0x02000a95,
    // 0x6D1D10..0x6D1D8F) and CharacterTriggerSurfaceTeleport (0x02000745, 0x5CC150..0x5CC77F). Components are
    // real, on owned hosts; a CharacterManager is built by its real constructor and registered into its absent
    // row before a component caches its reference.
    public sealed class CharacterLocalComponentTests
    {
        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static string[] AttributeNames(MemberInfo member) =>
            member.GetCustomAttributesData().Select(data => data.AttributeType.Name).ToArray();

        // The declarations come from the original DummyDll metadata of each type.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes()
        {
            AssertType(typeof(CharacterUiModifier), 0x00100101, typeof(MonoBehaviour), new[] { 1, 2 },
                new[] { ("m_characterManagerRef", 0x0001) },
                new[] { ("Start", 0x0081), ("Action_ShowUI", 0x0086), ("Action_HideUI", 0x0086), (".ctor", 0x1886) });

            AssertType(typeof(CharacterVisualProxyModifier), 0x00100101, typeof(ActorVisualProxyModifier), new[] { 1, 2 },
                new[] { ("m_characterManagerRef", 0x0021) },
                new[]
                {
                    ("Action_ShowVisualProxy", 0x0086), ("Action_HideVisualProxy", 0x0086),
                    ("Action_DisableVisualProxy", 0x0086), ("Action_EnableVisualProxy", 0x0086), (".ctor", 0x1886)
                });

            AssertType(typeof(CharacterTeleportLocal), 0x00100001, typeof(MonoBehaviour), new[] { 1, 2 },
                new[]
                {
                    ("m_levelStartPositionDefinition", 0x0001), ("m_triggerOnAwake", 0x0001),
                    ("m_levelManager", 0x0001), ("m_characterManager", 0x0001)
                },
                new[]
                {
                    ("OnEnable", 0x0081), ("OnLevelManagerValid", 0x0081), ("OnCharacterManagerValid", 0x0081),
                    ("Action_Teleport", 0x0086), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(CharacterTeleportLocal), "m_levelStartPositionDefinition")),
                Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterTeleportLocal), "m_triggerOnAwake")),
                Is.EqualTo(new[] { typeof(SerializeField) }));

            AssertType(typeof(HomingAttack), 0x00100001, typeof(MonoBehaviour), new[] { 2, 1 },
                new[] { ("m_collider", 0x0004), ("m_ability", 0x0001) },
                new[]
                {
                    ("Initialise", 0x0086), ("SetColliderDimensions", 0x0086), ("TryGetTarget", 0x0081),
                    ("OnTriggerEnter", 0x0081), ("OnTriggerExit", 0x0081), (".ctor", 0x1886)
                });
            Assert.That(AttributeNames(typeof(HomingAttack)),
                Is.EqualTo(new[] { "RequireComponent", "Il2CppSetOptionAttribute", "Il2CppSetOptionAttribute" }));
            FieldInfo collider = Field(typeof(HomingAttack), "m_collider");
            Assert.That(AttributeTypes(collider), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(collider), Is.EqualTo("The box collider to trigger targets being added to the homing attack ability."));

            AssertType(typeof(PlayerDamageableObject), 0x00100001, typeof(MonoBehaviour), new[] { 2, 1 },
                new[] { ("m_onHitEvent", 0x0001) },
                new[] { ("ReceiveHit", 0x01e6), ("RegisterHitPlayer", 0x01e6), (".ctor", 0x1886) });
            Assert.That(typeof(PlayerDamageableObject).GetInterfaces(), Is.EqualTo(new[] { typeof(IPlayerDamageable) }));
            Assert.That(AttributeTypes(Field(typeof(PlayerDamageableObject), "m_onHitEvent")), Is.EqualTo(new[] { typeof(SerializeField) }));

            AssertType(typeof(CharacterTriggerSurfaceTeleport), 0x00100001, typeof(CharacterCollisionModifier), new[] { 1, 2 },
                new[]
                {
                    ("m_localDirection", 0x0001), ("m_collider", 0x0001), ("m_rayDistance", 0x0001), ("m_colliderActive", 0x0001)
                },
                new[]
                {
                    ("Reset", 0x0081), ("OnValidate", 0x0081), ("OnTriggerEnter", 0x0081), ("DoTeleport", 0x0086),
                    ("OnTriggerExit", 0x0081), (".ctor", 0x1886)
                });
            Assert.That(AttributeNames(typeof(CharacterTriggerSurfaceTeleport)),
                Is.EqualTo(new[] { "Il2CppSetOptionAttribute", "Il2CppSetOptionAttribute", "RequireComponent" }));
            Assert.That(AttributeNames(Field(typeof(CharacterTriggerSurfaceTeleport), "m_rayDistance")),
                Is.EqualTo(new[] { "ReadOnlyAttribute", "SerializeField" }));
        }

        // ---- PlayerDamageableObject ----------------------------------------------------------------

        // The constructor 0x6D1D30 builds the hit event; ReceiveHit 0x6D1D10 tail-calls UnityEvent.Invoke on it
        // and ignores the collider; RegisterHitPlayer 0x6D1D20 is a bare return.
        [Test]
        public void ADamageableObjectRaisesItsHitEventOnlyWhenHit()
        {
            Owned.Run(owned =>
            {
                var damageable = owned.Host("Lucid player damageable", false).AddComponent<PlayerDamageableObject>();
                Collider collider = owned.Host("Lucid damaged collider", false).AddComponent<BoxCollider>();
                var hits = 0;
                Get<UnityEvent>(damageable, "m_onHitEvent").AddListener(() => hits++);

                damageable.ReceiveHit(collider);
                Assert.That(hits, Is.EqualTo(1));
                ((IPlayerDamageable)damageable).ReceiveHit(null);
                Assert.That(hits, Is.EqualTo(2), "through the interface, whatever collider is passed");

                damageable.RegisterHitPlayer(default, HazardType.Bomb);
                Assert.That(hits, Is.EqualTo(2), "registering a hit raises nothing");
            });
        }

        // ---- HomingAttack --------------------------------------------------------------------------

        private sealed class HomingAbilityDouble : IHomingAttack
        {
            public readonly List<string> Calls = new List<string>();
            public Vector3 Size = Vector3.one;
            public Vector3 Offset = Vector3.zero;
            public Vector3 TargetZoneSize { get { Calls.Add("TargetZoneSize"); return Size; } }
            public Vector3 TargetZoneOffset { get { Calls.Add("TargetZoneOffset"); return Offset; } }
            public void AddTarget(HomingTarget target) => Calls.Add("AddTarget:" + target.name);
            public void RemoveTarget(HomingTarget target) => Calls.Add("RemoveTarget:" + target.name);
            public void OnHomingTargetHit(HomingTarget target) => Calls.Add("OnHomingTargetHit");
            public bool IsHomingTargetValid(HomingTarget target) { Calls.Add("IsHomingTargetValid"); return true; }
            public void OnTriggerEnter(Collider otherCollider) => Calls.Add("OnTriggerEnter:" + otherCollider.name);
            public void OnTriggerExit(Collider otherCollider) => Calls.Add("OnTriggerExit:" + otherCollider.name);
        }

        private static HomingAttack NewHomingAttack(Owned owned, out BoxCollider box)
        {
            GameObject host = owned.Host("Lucid homing attack", false);
            box = host.AddComponent<BoxCollider>();
            var attack = host.AddComponent<HomingAttack>();
            Set(attack, "m_collider", box);
            return attack;
        }

        // Initialise 0x6E6930 stores the ability, reads its target zone size and then offset (interface slots 0 and
        // 1), sets the box size (BoxCollider.set_size, 0x6e6a64) and puts the centre at the offset plus
        // forward * (size.z * 0.5) (mulss with 0.5, 0x6e6aa3; centre set at 0x6e6adc); the character is unused.
        [Test]
        public void InitialiseSizesTheBoxFromTheAbilityAndCentresItHalfADepthAhead()
        {
            Owned.Run(owned =>
            {
                HomingAttack attack = NewHomingAttack(owned, out BoxCollider box);
                var ability = new HomingAbilityDouble { Size = new Vector3(2f, 4f, 6f), Offset = new Vector3(1f, 2f, 3f) };

                attack.Initialise(null, ability);

                Assert.That(Get<IHomingAttack>(attack, "m_ability"), Is.SameAs(ability));
                Assert.That(ability.Calls, Is.EqualTo(new[] { "TargetZoneSize", "TargetZoneOffset" }));
                Assert.That(box.size, Is.EqualTo(new Vector3(2f, 4f, 6f)));
                Assert.That(box.center, Is.EqualTo(new Vector3(1f, 2f, 6f)), "offset + forward * (6 * 0.5)");
            });
        }

        // SetColliderDimensions 0x6E6AF0 applies the same sizing to the arguments without touching the ability.
        [Test]
        public void SetColliderDimensionsCentresTheBoxHalfItsDepthAheadOfTheOffset()
        {
            Owned.Run(owned =>
            {
                HomingAttack attack = NewHomingAttack(owned, out BoxCollider box);

                attack.SetColliderDimensions(new Vector3(1f, 3f, 8f), new Vector3(-2f, 5f, 1f));
                Assert.That(box.size, Is.EqualTo(new Vector3(1f, 3f, 8f)));
                Assert.That(box.center, Is.EqualTo(new Vector3(-2f, 5f, 5f)), "offset + forward * 4");

                attack.SetColliderDimensions(new Vector3(1f, 1f, -4f), Vector3.zero);
                Assert.That(box.center, Is.EqualTo(new Vector3(0f, 0f, -2f)), "a negative depth centres behind the offset");
            });
        }

        private static void WithHomingTargets(Action<Owned> body) => Owned.Run(owned =>
        {
            // HomingTarget caches a CharacterManager reference, and its Editor OnValidate reads the time lookup cache.
            // Its TimeScaledComponent base type initializer (0x06000ec7) takes GetSystemRef<TimeManager>(null, true),
            // which appends the TimeManager row on first use, so that row is owned as well.
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<CharacterManager>();
            FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = config.GetValue(null);
            try
            {
                config.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                body(owned);
            }
            finally
            {
                config.SetValue(null, prior);
            }
        });

        private static (HomingTarget target, Collider collider) NewTarget(Owned owned, string name)
        {
            GameObject host = owned.Host(name);
            var collider = host.AddComponent<BoxCollider>();
            var target = host.AddComponent<HomingTarget>();
            Set(target, "m_collider", collider);
            return (target, collider);
        }

        // OnTriggerEnter 0x6E6BD0 always passes the collider to the ability first (slot 6), then adds the
        // HomingTarget on it (slot 2) only if one exists (TryGetComponent) and is enabled (Behaviour.get_enabled,
        // 0x6e6cbd). OnTriggerExit 0x6E6D40 passes the collider on (slot 7) and removes any target on it (slot 3),
        // enabled or not.
        [Test]
        public void TriggerCallbacksForwardToTheAbilityAndAddOrRemoveTheEnabledTargets()
        {
            WithHomingTargets(owned =>
            {
                HomingAttack attack = NewHomingAttack(owned, out BoxCollider box);
                var ability = new HomingAbilityDouble();
                attack.Initialise(null, ability);
                ability.Calls.Clear();
                (HomingTarget live, Collider liveCollider) = NewTarget(owned, "live");
                (HomingTarget disabled, Collider disabledCollider) = NewTarget(owned, "disabled");
                disabled.enabled = false;
                Collider plain = owned.Host("plain", false).AddComponent<BoxCollider>();

                Call(attack, "OnTriggerEnter", liveCollider);
                Call(attack, "OnTriggerEnter", disabledCollider);
                Call(attack, "OnTriggerEnter", plain);
                Assert.That(ability.Calls, Is.EqualTo(new[]
                {
                    "OnTriggerEnter:live", "AddTarget:live",
                    "OnTriggerEnter:disabled",
                    "OnTriggerEnter:plain"
                }));

                ability.Calls.Clear();
                Call(attack, "OnTriggerExit", liveCollider);
                Call(attack, "OnTriggerExit", disabledCollider);
                Call(attack, "OnTriggerExit", plain);
                Assert.That(ability.Calls, Is.EqualTo(new[]
                {
                    "OnTriggerExit:live", "RemoveTarget:live",
                    "OnTriggerExit:disabled", "RemoveTarget:disabled",
                    "OnTriggerExit:plain"
                }));
            });
        }

        // ---- CharacterUiModifier -------------------------------------------------------------------

        // Start 0x6AA320 resolves the CharacterManager reference; Action_ShowUI 0x6AA3A0 and Action_HideUI 0x6AA3F0
        // read the manager's current character field (+0x38, whatever the validity flag says) and call
        // Character.ToggleUI with 1 and 0.
        [Test]
        public void TheUiActionsToggleTheCurrentCharactersUi()
        {
            WithManager((owned, manager, character) =>
            {
                Character other = NewCharacter(owned, new FSMStorage(0));
                var modifier = owned.Host("Lucid ui modifier", false).AddComponent<CharacterUiModifier>();
                Call(modifier, "Start");
                SetCurrent(manager, character, false);
                Assert.That(Get<bool>(character, "m_uiEnabled"), Is.True);

                modifier.Action_HideUI();
                Assert.That(Get<bool>(character, "m_uiEnabled"), Is.False, "the validity flag does not matter");
                Assert.That(Get<bool>(other, "m_uiEnabled"), Is.True, "another character is left alone");

                modifier.Action_ShowUI();
                Assert.That(Get<bool>(character, "m_uiEnabled"), Is.True);

                SetCurrent(manager, other, true);
                modifier.Action_HideUI();
                Assert.That(Get<bool>(other, "m_uiEnabled"), Is.False, "the current character follows the manager");
                Assert.That(Get<bool>(character, "m_uiEnabled"), Is.True);
            });
        }

        // ---- CharacterVisualProxyModifier ----------------------------------------------------------

        private static SmoothedTransformProxy GiveVisuals(Owned owned, Character character)
        {
            Set(character, "m_formHandler", character.gameObject.AddComponent<ActorFormHandler>());
            var proxy = owned.Host("Lucid visual proxy").AddComponent<SmoothedTransformProxy>();
            SetActorProperty(character, "VisualProxy", proxy);
            return proxy;
        }

        // Show and Hide 0x6AA450/0x6AA4A0 tail-call Actor.ToggleFormRenderers with 1 and 0 on the manager's current
        // character; Enable and Disable 0x6AA540/0x6AA4F0 tail-call Actor.SetVisualProxyActive with 1 and 0.
        [Test]
        public void TheVisualProxyActionsToggleTheCurrentCharactersRenderersAndProxy()
        {
            WithManager((owned, manager, character) =>
            {
                Character other = NewCharacter(owned, new FSMStorage(0));
                SmoothedTransformProxy proxy = GiveVisuals(owned, character);
                SmoothedTransformProxy otherProxy = GiveVisuals(owned, other);
                SetCurrent(manager, character, false);
                var modifier = owned.Host("Lucid visual proxy modifier", false).AddComponent<CharacterVisualProxyModifier>();

                Assert.That(character.FormRenderersOn, Is.True);
                modifier.Action_HideVisualProxy();
                Assert.That(character.FormRenderersOn, Is.False, "the validity flag does not matter");
                Assert.That(other.FormRenderersOn, Is.True, "another character is left alone");
                modifier.Action_ShowVisualProxy();
                Assert.That(character.FormRenderersOn, Is.True);

                modifier.Action_DisableVisualProxy();
                Assert.That(proxy.gameObject.activeSelf, Is.False);
                Assert.That(otherProxy.gameObject.activeSelf, Is.True, "another character's proxy is left alone");
                Assert.That(character.FormRenderersOn, Is.True, "the proxy switch leaves the renderers alone");
                modifier.Action_EnableVisualProxy();
                Assert.That(proxy.gameObject.activeSelf, Is.True);

                SetCurrent(manager, other, true);
                modifier.Action_HideVisualProxy();
                modifier.Action_DisableVisualProxy();
                Assert.That(other.FormRenderersOn, Is.False, "the current character follows the manager");
                Assert.That(otherProxy.gameObject.activeSelf, Is.False);
                Assert.That(character.FormRenderersOn, Is.True);
                Assert.That(proxy.gameObject.activeSelf, Is.True);
            });
        }

        // ---- CharacterTeleportLocal ----------------------------------------------------------------

        // OnLevelManagerValid 0x6E65D0 stores the level manager (+0x30, write barrier 0x6e6637) and registers
        // OnCharacterManagerValid with the CharacterManager reference, which a registered manager answers at once;
        // the awake gate of the teleport is covered by the next case.
        [Test]
        public void LevelManagerCallbackStoresTheManagersItIsGiven()
        {
            WithManager((owned, manager, character) =>
            {
                var teleport = owned.Host("Lucid teleport local", false).AddComponent<CharacterTeleportLocal>();
                var levelManager = NewLevelManager(owned);

                Call(teleport, "OnLevelManagerValid", levelManager);
                Assert.That(Get<LevelManager>(teleport, "m_levelManager"), Is.SameAs(levelManager));
                Assert.That(Get<CharacterManager>(teleport, "m_characterManager"), Is.SameAs(manager),
                    "the registered manager answers the registration at once");
            });
        }

        // Action_Teleport 0x6E6700 asks m_levelManager (+0x30) for its current level first (TryGetCurrentLevel,
        // 0x6e674e) and returns when there is none, before m_characterManager (+0x38) is read; OnCharacterManagerValid
        // reaches that code only when m_triggerOnAwake is set (cmp byte [rbx+0x28], jmp 0x6e6700).
        [Test]
        public void TeleportStopsWithoutACurrentLevelAndAwakeTriggersIt()
        {
            WithManager((owned, manager, character) =>
            {
                var teleport = owned.Host("Lucid teleport local", false).AddComponent<CharacterTeleportLocal>();
                var levelManager = NewLevelManager(owned);
                Assert.That(levelManager.CurrentLevelValid, Is.False);
                Set(teleport, "m_levelManager", levelManager);

                Assert.DoesNotThrow(() => teleport.Action_Teleport(), "no current level: nothing else is read");
                Assert.That(Get<CharacterManager>(teleport, "m_characterManager"), Is.Null);

                Set(teleport, "m_levelManager", null);
                Call(teleport, "OnCharacterManagerValid", manager);
                Assert.That(Get<CharacterManager>(teleport, "m_characterManager"), Is.SameAs(manager), "not triggered on awake");

                Set(teleport, "m_triggerOnAwake", true);
                Assert.Throws<NullReferenceException>(() => Call(teleport, "OnCharacterManagerValid", manager),
                    "triggered on awake, the teleport reads the level manager");
            });
        }

        // An unawakened LevelManager; its field initialisers take the MissionManager, AudioManager and SaveManager
        // references (0x0600247e). The manager fixture already holds the MissionManager row and the registered
        // SaveManager, so only the AudioManager row is leased here.
        private static LevelManager NewLevelManager(Owned owned)
        {
            owned.LeaseRow<AudioManager>();
            return owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
        }

        // ---- CharacterTriggerSurfaceTeleport -------------------------------------------------------

        private static CharacterTriggerSurfaceTeleport NewSurfaceTeleport(Owned owned, Vector3 boxSize, out BoxCollider box)
        {
            GameObject host = owned.Host("Lucid surface teleport");
            box = host.AddComponent<BoxCollider>();
            box.size = boxSize;
            var teleport = host.AddComponent<CharacterTriggerSurfaceTeleport>();
            Physics.SyncTransforms();
            return teleport;
        }

        // The constructor 0x5CC6C0 writes Vector3.forward (+0x48 of the static block) into the local direction.
        // Reset 0x5CC150 takes the Collider on its own object; OnValidate 0x5CC1A0 (CanValidate is true in this
        // release) sets the ray distance to the length of the collider's bounds size (extents doubled).
        [Test]
        public void ResetAndValidateDeriveTheColliderAndTheRayDistance()
        {
            WithManager((owned, manager, character) =>
            {
                CharacterTriggerSurfaceTeleport teleport = NewSurfaceTeleport(owned, new Vector3(3f, 4f, 12f), out BoxCollider box);
                Assert.That(Get<Vector3>(teleport, "m_localDirection"), Is.EqualTo(Vector3.forward));
                // The Editor may already have sent Reset and OnValidate while the component was added.
                Set(teleport, "m_collider", null);
                Set(teleport, "m_rayDistance", 1f);

                Call(teleport, "Reset");
                Assert.That(Get<Collider>(teleport, "m_collider"), Is.SameAs(box));

                Call(teleport, "OnValidate");
                Assert.That(Get<float>(teleport, "m_rayDistance"), Is.EqualTo(13f).Within(1e-4f), "sqrt(3^2 + 4^2 + 12^2)");
            });
        }

        // OnTriggerEnter 0x5CC270 and OnTriggerExit 0x5CC620 set and clear the active flag (+0x44) only for the current
        // character's collision collider (IsCurrentCharacterColliderCollision, then TryGetCurrentCharacter).
        [Test]
        public void ThePlayersColliderEntersAndLeavesTheTeleport()
        {
            WithManager((owned, manager, character) =>
            {
                CharacterTriggerSurfaceTeleport teleport = NewSurfaceTeleport(owned, Vector3.one, out BoxCollider box);
                Collider own = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", own);
                Collider other = owned.Host("Lucid other collider", false).AddComponent<BoxCollider>();

                SetCurrent(manager, character, false);
                Call(teleport, "OnTriggerEnter", own);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.False, "no valid character");

                SetCurrent(manager, character, true);
                Call(teleport, "OnTriggerEnter", other);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.False, "another collider");
                Call(teleport, "OnTriggerEnter", own);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.True);

                Call(teleport, "OnTriggerExit", other);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.True, "another collider leaving changes nothing");
                SetCurrent(manager, character, false);
                Call(teleport, "OnTriggerExit", own);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.True, "no valid character");
                SetCurrent(manager, character, true);
                Call(teleport, "OnTriggerExit", own);
                Assert.That(Get<bool>(teleport, "m_colliderActive"), Is.False);
            });
        }

        // DoTeleport 0x5CC310 does nothing unless the active flag is set and a character is current. It casts a ray
        // from the character along its rotated local direction for the ray distance, then casts back from that far
        // point against the collider (Collider.Raycast, maximum distance float.MaxValue, 0x5cc5e2) and moves the
        // character (virtual SetWorldPosition, slot 35) to the hit, the collider face on the far side from the character.
        [Test]
        public void DoTeleportMovesTheCharacterToTheFarSurfaceAlongItsDirection()
        {
            WithManager((owned, manager, character) =>
            {
                CharacterTriggerSurfaceTeleport teleport = NewSurfaceTeleport(owned, new Vector3(10f, 10f, 10f), out BoxCollider box);
                Call(teleport, "Reset");
                Set(teleport, "m_rayDistance", 40f);
                // The body write is deferred during the Actor fixed update, so the case needs no Rigidbody.
                SetActorProperty(character, "FixedUpdateActive", true);
                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, -20f));
                SetCurrent(manager, character, true);

                teleport.DoTeleport();
                Assert.That(character.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, -20f)), "inactive: nothing moves");

                Set(teleport, "m_colliderActive", true);
                SetCurrent(manager, character, false);
                teleport.DoTeleport();
                Assert.That(character.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, -20f)), "no valid character: nothing moves");

                SetCurrent(manager, character, true);
                teleport.DoTeleport();
                Assert.That(Vector3.Distance(character.WorldPosition, new Vector3(0f, 0f, 5f)), Is.LessThan(1e-3f),
                    "the face towards the far point, not the one the character faces");

                // A direction the box does not cross: the far point's cast back misses, so the character stays.
                SetActorProperty(character, "WorldRotation", Quaternion.Euler(0f, 90f, 0f));
                SetActorProperty(character, "WorldPosition", new Vector3(0f, 30f, -20f));
                teleport.DoTeleport();
                Assert.That(character.WorldPosition, Is.EqualTo(new Vector3(0f, 30f, -20f)), "a miss leaves the character");
            });
        }

        // The direction is WorldRotation * m_localDirection (Quaternion.op_Multiply, 0x5cc3e9, rotation first).
        // Turned 90 degrees about y the character at (-20,0,0) casts towards +x: the far point is (20,0,0) and the
        // cast back meets the box's +x face at x = 5. Ignoring the rotation would cast along +z from x = -20, past
        // the box, and leave the character where it is.
        [Test]
        public void DoTeleportCastsAlongTheCharactersRotatedDirection()
        {
            WithManager((owned, manager, character) =>
            {
                CharacterTriggerSurfaceTeleport teleport = NewSurfaceTeleport(owned, new Vector3(10f, 10f, 10f), out BoxCollider box);
                Call(teleport, "Reset");
                Set(teleport, "m_rayDistance", 40f);
                Set(teleport, "m_colliderActive", true);
                SetActorProperty(character, "FixedUpdateActive", true);
                SetActorProperty(character, "WorldRotation", Quaternion.Euler(0f, 90f, 0f));
                SetActorProperty(character, "WorldPosition", new Vector3(-20f, 0f, 0f));
                SetCurrent(manager, character, true);

                teleport.DoTeleport();
                Assert.That(Vector3.Distance(character.WorldPosition, new Vector3(5f, 0f, 0f)), Is.LessThan(1e-3f));
            });
        }
    }
}
