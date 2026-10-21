using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterStaminaTrigger`1 (TypeDef 0x0200034e, shared generic code at
    // 0xE69C80..0xE69F4C), CharacterChaosStaminaTrigger (0x0200034a, 0x76F2B0..0x76F6DD),
    // CharacterRailAttachTrigger (0x0200073b, 0x5CAB30..0x5CAEEF), CharacterTransporterAttachTrigger
    // (0x0200073d, 0x5CB0D0..0x5CB17F), TriggerCondition_Nested (0x02000755, 0x5CD890..0x5CDC0D) and
    // CharacterTypeCollisionTrigger (0x02000763, 0x5CED60..0x5CEF27). Components are real, on owned hosts
    // that never wake (the Editor sends no Awake); a CharacterManager is built by its real constructor and
    // registered into its absent row before a component caches its reference.
    public sealed class CharacterStaminaTriggerTests
    {
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static T Add<T>(Owned owned, string name) where T : Component
        {
            GameObject host = owned.Host(name, false);
            // CharacterTrigger requires a Collider, which is abstract.
            host.AddComponent<BoxCollider>();
            return host.AddComponent<T>();
        }

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        // The declarations come from the original DummyDll metadata of each type.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes()
        {
            Type staminaTrigger = typeof(CharacterStaminaTrigger<>);
            AssertType(staminaTrigger, 0x00100081, typeof(CharacterTrigger), new[] { 1, 2 },
                new[] { ("m_abilityType", 0x0004), ("<Ability>k__BackingField", 0x0001) },
                new[]
                {
                    ("get_Ability", 0x0884), ("set_Ability", 0x0881), ("get_Stamina", 0x0dc4), ("Start", 0x0081),
                    ("OnDestroy", 0x0081), ("OnCharacterChange", 0x0081), (".ctor", 0x1884)
                });
            Assert.That(staminaTrigger.GetGenericArguments().Single().GenericParameterAttributes,
                Is.EqualTo(GenericParameterAttributes.ReferenceTypeConstraint), "T is a class");
            FieldInfo abilityType = Field(staminaTrigger, "m_abilityType");
            Assert.That(AttributeTypes(abilityType), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(abilityType), Is.EqualTo("Ability to use for stamina definition."));

            AssertType(typeof(CharacterChaosStaminaTrigger), 0x00100001, typeof(CharacterStaminaTrigger<IAbilityBurstAttackChaos>), new[] { 2, 1 },
                new[] { ("m_triggerAbility", 0x0004) },
                new[] { ("get_Stamina", 0x08c4), ("Trigger", 0x0086), (".ctor", 0x1886) });
            FieldInfo triggerAbility = Field(typeof(CharacterChaosStaminaTrigger), "m_triggerAbility");
            Assert.That(AttributeTypes(triggerAbility), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(triggerAbility), Is.EqualTo("Triggers ability immediately."));

            AssertType(typeof(CharacterRailAttachTrigger), 0x00100001, typeof(CharacterSplineAttachTrigger), new[] { 1, 2 },
                new (string, int)[0],
                new[] { ("DoTriggerEnter", 0x00c4), ("GetFSMKey", 0x00c4), (".ctor", 0x1886) });

            AssertType(typeof(CharacterTransporterAttachTrigger), 0x00100001, typeof(CharacterSplineAttachTrigger), new[] { 2, 1 },
                new (string, int)[0],
                new[] { ("DoTriggerEnter", 0x00c4), ("GetFSMKey", 0x00c4), (".ctor", 0x1886) });

            AssertType(typeof(CharacterTypeCollisionTrigger), 0x00100001, typeof(EventCollisionTrigger), new[] { 1, 2 },
                new[] { ("m_anyCharacter", 0x0001), ("m_characters", 0x0001), ("m_characterManagerRef", 0x0021) },
                new[] { ("FilterCollision", 0x00c4), ("OnValidate", 0x0081), (".ctor", 0x1886) });
            Assert.That(AttributeTypes(Field(typeof(CharacterTypeCollisionTrigger), "m_anyCharacter")), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterTypeCollisionTrigger), "m_characters")), Is.EqualTo(new[] { typeof(SerializeField) }));

            AssertType(typeof(TriggerCondition_Nested), 0x00100001, typeof(TriggerCondition), new[] { 2, 1 },
                new[] { ("m_checkType", 0x0001), ("m_conditions", 0x0001) },
                new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_Nested), "m_checkType")), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_Nested), "m_conditions")), Is.EqualTo(new[] { typeof(SerializeField) }));
        }

        // The constructors of the rail and transporter triggers write true into CharacterSplineAttachTrigger's
        // legacy-logic flag (+0x59, 0x5CAEE4 and 0x5CB174) and nothing else; the chaos trigger's flag is clear.
        [Test]
        public void ConstructorsKeepTheNativeDefaults()
        {
            WithCharacter((owned, character) =>
            {
                Assert.That(Get<bool>(Add<CharacterRailAttachTrigger>(owned, "Lucid rail attach"), "m_useLegacyLogic"), Is.True);
                Assert.That(Get<bool>(Add<CharacterTransporterAttachTrigger>(owned, "Lucid transporter attach"), "m_useLegacyLogic"), Is.True);
                Assert.That(Get<bool>(Add<CharacterRailAttachTrigger>(owned, "Lucid rail attach 2"), "m_biDirectional"), Is.False);
                Assert.That(Get<bool>(Add<CharacterChaosStaminaTrigger>(owned, "Lucid chaos stamina"), "m_triggerAbility"), Is.False);
                Assert.That(Get<object>(Add<TriggerCondition_Nested>(owned, "Lucid nested condition"), "m_conditions"), Is.Null);
            });
            WithManager((owned, manager, character) =>
            {
                var trigger = Add<CharacterTypeCollisionTrigger>(owned, "Lucid type collision");
                Assert.That(Get<bool>(trigger, "m_anyCharacter"), Is.False);
                Assert.That(Get<List<CharacterId>>(trigger, "m_characters"), Is.Not.Null.And.Empty);
            });
        }

        // GetFSMKey 0x5CAE80 returns ActorFSMKeys.RailAutoAttach (static +0x5b0) and 0x5CB110 returns
        // TransporterAutoAttach (+0x610); the keys differ from each other and from RailActive.
        [Test]
        public void SplineAttachKeysAreTheRailAndTransporterAutoAttachKeys()
        {
            WithCharacter((owned, character) =>
            {
                var rail = Add<CharacterRailAttachTrigger>(owned, "Lucid rail attach");
                var transporter = Add<CharacterTransporterAttachTrigger>(owned, "Lucid transporter attach");
                Assert.That(Call(rail, "GetFSMKey"), Is.EqualTo(ActorFSMKeys.RailAutoAttach));
                Assert.That(Call(transporter, "GetFSMKey"), Is.EqualTo(ActorFSMKeys.TransporterAutoAttach));
                Assert.That(Call(rail, "GetFSMKey"), Is.Not.EqualTo(ActorFSMKeys.TransporterAutoAttach));
                Assert.That(Call(rail, "GetFSMKey"), Is.Not.EqualTo(ActorFSMKeys.RailActive));
            });
        }

        // A trigger at the origin with a Spline facade over an InertSpline, under an inactive parent so that
        // activating it wakes nothing. The legacy flag is set, so the attach changes only the storage.
        private static T NewSplineTrigger<T>(Owned owned, out InertSpline inert) where T : CharacterSplineAttachTrigger
        {
            var trigger = Add<T>(owned, "Lucid spline attach");
            GameObject parent = owned.Host("Lucid spline attach parent", false);
            GameObject splineObject = owned.Host("Lucid spline attach spline", true, parent.transform);
            inert = splineObject.AddComponent<InertSpline>();
            var facade = splineObject.AddComponent<Spline>();
            Set(facade, "m_inertSpline", inert);
            Set(facade, "m_activeSplineType", SplineVariantType.Inert);
            Set(trigger, "m_spline", facade);
            return trigger;
        }

        private static void WithStorage(Action<Owned, Character, FSMStorage> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            var storage = new FSMStorage(0);
            body(owned, NewCharacter(owned, storage), storage);
        });

        // DoTriggerEnter 0x5CAB30 reads ActorFSMKeys.RailActive from the character's storage with a false
        // default and storeDefault (GetValue<bool>, ecx=0, r8d=1, 0x5cac25): a true flag returns without the
        // base attach (0x5cac35), while a clear or absent one continues into it (jmp 0x5cac60). The absent
        // flag is stored as false by the read.
        [Test]
        public void RailAttachIsSkippedWhileARailIsActiveAndOtherwiseAttaches()
        {
            WithStorage((owned, character, storage) =>
            {
                var trigger = NewSplineTrigger<CharacterRailAttachTrigger>(owned, out InertSpline inert);

                storage.SetValue(ActorFSMKeys.RailActive, true);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(storage.HasValue(ActorFSMKeys.RailAutoAttach), Is.False, "a rail is already active");
                Assert.That(storage.GetValue(ActorFSMKeys.RailActive, false, false), Is.True, "the flag is left alone");

                storage.SetValue(ActorFSMKeys.RailActive, false);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(storage.GetValue<ISpline>(ActorFSMKeys.RailAutoAttach, null, false), Is.SameAs(inert), "the spline is attached");
                storage.RemoveValue<ISpline>(ActorFSMKeys.RailAutoAttach);
                storage.RemoveValue<bool>(ActorFSMKeys.RailActive);

                Assert.That(storage.HasValue(ActorFSMKeys.RailActive), Is.False);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(storage.GetValue<ISpline>(ActorFSMKeys.RailAutoAttach, null, false), Is.SameAs(inert), "an absent flag attaches");
                Assert.That(storage.HasValue(ActorFSMKeys.RailActive), Is.True, "the read stores its default");
                Assert.That(storage.GetValue(ActorFSMKeys.RailActive, true, false), Is.False, "the stored default is false");
            });
        }

        // DoTriggerEnter 0x5CB0D0 calls IsAbilityInUse(character, 0xa596f57d, 0) with no grace period (xorps xmm0)
        // before the base attach: only the transporter movement ability, running or last used at the character's
        // current fixed time, blocks it. The default grace period (0.2s) would still block a use 0.1s ago.
        [Test]
        public void TransporterAttachIsBlockedOnlyByTheTransporterAbilityWithoutGrace()
        {
            WithStorage((owned, character, storage) =>
            {
                var trigger = NewSplineTrigger<CharacterTransporterAttachTrigger>(owned, out InertSpline inert);
                Set(character, "m_totalFixedTime", 10f);
                ActorAbilityType transporter = ActorAbilityType.Character_MovementTransporter;
                var comparer = HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer;

                bool Attaches(Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)
                {
                    if (abilities != null) storage.SetValue(ActorFSMKeys.AbilitiesInUse, abilities);
                    storage.RemoveValue<ISpline>(ActorFSMKeys.TransporterAutoAttach);
                    Call(trigger, "DoTriggerEnter", character);
                    bool attached = storage.HasValue(ActorFSMKeys.TransporterAutoAttach);
                    if (attached) Assert.That(storage.GetValue<ISpline>(ActorFSMKeys.TransporterAutoAttach, null, false), Is.SameAs(inert));
                    return attached;
                }

                Dictionary<ActorAbilityType, ActorAbilityInUseData> Abilities(ActorAbilityType type, bool inUse, float lastUsed)
                {
                    var data = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(comparer);
                    data[type] = new ActorAbilityInUseData { InUse = inUse, TimeLastUsedSeconds = lastUsed };
                    return data;
                }

                Assert.That(Attaches(null), Is.True, "no ability table");
                Assert.That(Attaches(new Dictionary<ActorAbilityType, ActorAbilityInUseData>(comparer)), Is.True, "empty table");
                Assert.That(Attaches(Abilities(transporter, true, 0f)), Is.False, "transporter running");
                Assert.That(Attaches(Abilities(ActorAbilityType.Character_ClimbingGround, true, 0f)), Is.True, "another ability running");
                Assert.That(Attaches(Abilities(transporter, false, 10f)), Is.False, "last used at the current fixed time");
                Assert.That(Attaches(Abilities(transporter, false, 9.9f)), Is.True, "last used 0.1s ago is outside a zero grace period");
            });
        }

        // FilterCollision 0x5CED60 needs a cached manager with a current character (TryGetCurrentCharacter, whose flag
        // alone decides), rejects every collider but the character's collision collider (op_Inequality, 0x5cedfb), and
        // then accepts any character (+0x58) or one whose IdType (+0x35c) is in the list (List.Contains, 0x5cee25).
        [Test]
        public void TypeCollisionAcceptsOnlyTheCurrentCharactersColliderOfAWantedType()
        {
            WithManager((owned, manager, character) =>
            {
                var trigger = Add<CharacterTypeCollisionTrigger>(owned, "Lucid type collision");
                Collider own = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", own);
                Collider other = owned.Host("Lucid other collider", false).AddComponent<BoxCollider>();
                typeof(Character).GetProperty("IdType").GetSetMethod(true).Invoke(character, new object[] { CharacterId.Sonic });
                var characters = Get<List<CharacterId>>(trigger, "m_characters");
                bool Filter(Collider collider) => (bool)Call(trigger, "FilterCollision", collider);

                SetCurrent(manager, character, false);
                Set(trigger, "m_anyCharacter", true);
                Assert.That(Filter(own), Is.False, "no valid character");

                SetCurrent(manager, character, true);
                Assert.That(Filter(own), Is.True, "any character");
                Assert.That(Filter(other), Is.False, "another collider, even for any character");

                Set(trigger, "m_anyCharacter", false);
                Assert.That(Filter(own), Is.False, "an empty list matches no character");
                characters.Add(CharacterId.Tails);
                Assert.That(Filter(own), Is.False, "the character's id is not listed");
                characters.Add(CharacterId.Sonic);
                Assert.That(Filter(own), Is.True, "the character's id is listed");
                Assert.That(Filter(other), Is.False, "another collider, even for a listed id");
            });
        }

        // FilterCollision reads the cached SystemRef's system directly (0x5CED60: test rdi,rdi; je 0x5cee2d) and
        // returns false when it is null, without resolving or reporting anything.
        [Test]
        public void TypeCollisionWithoutAManagerMatchesNothingAndReportsNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<CharacterManager>();
                var trigger = Add<CharacterTypeCollisionTrigger>(owned, "Lucid type collision");
                Set(trigger, "m_anyCharacter", true);
                Collider collider = owned.Host("Lucid other collider", false).AddComponent<BoxCollider>();

                Assert.That(Call(trigger, "FilterCollision", collider), Is.False);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            });
        }

        // DoEvaluate 0x5CD890 forwards its character, the check type (+0x24) and the conditions (+0x28) to
        // CharacterConditionalTriggerEvent.EvaluateConditions (0x5CD8B0) and nothing else. Nested conditions combine
        // by their own check types; the invert flag of TriggerCondition still applies to the whole.
        [Test]
        public void NestedConditionsCombineByTheirOwnCheckType()
        {
            InitialiseTriggerConditions();
            WithManager((owned, manager, character) =>
            {
                TriggerConditionProbe Probe(bool answer)
                {
                    var probe = Add<TriggerConditionProbe>(owned, "Lucid nested probe");
                    probe.CharacterResult = answer;
                    return probe;
                }

                TriggerCondition_Nested Nested(CharacterConditionalTriggerEvent.CheckType check, params TriggerCondition[] conditions)
                {
                    var nested = Add<TriggerCondition_Nested>(owned, "Lucid nested condition");
                    Set(nested, "m_checkType", check);
                    Set(nested, "m_conditions", conditions);
                    return nested;
                }

                const bool T = true, F = false;
                var all = CharacterConditionalTriggerEvent.CheckType.All;
                var any = CharacterConditionalTriggerEvent.CheckType.Any;
                (CharacterConditionalTriggerEvent.CheckType check, bool[] answers, bool expected, int evaluated)[] table =
                {
                    (all, new bool[0], T, 0),
                    (all, new[] { T, T }, T, 2),
                    (all, new[] { T, F }, F, 2),
                    (all, new[] { F, T }, F, 1),
                    (any, new bool[0], T, 0),
                    (any, new[] { F, F }, F, 2),
                    (any, new[] { F, T }, T, 2),
                    (any, new[] { T, F }, T, 1),
                };
                foreach ((CharacterConditionalTriggerEvent.CheckType check, bool[] answers, bool expected, int evaluated) in table)
                {
                    TriggerConditionProbe[] probes = answers.Select(Probe).ToArray();
                    TriggerCondition_Nested nested = Nested(check, probes);
                    string label = check + " over " + string.Join(",", answers);
                    Assert.That(nested.Evaluate(character), Is.EqualTo(expected), label);
                    Assert.That(probes.Sum(probe => probe.CharacterEvaluations), Is.EqualTo(evaluated), label + ": conditions evaluated");
                    Assert.That(probes.Where(probe => probe.CharacterEvaluations > 0).All(probe => probe.LastCharacter == character), Is.True, label);
                    Set(nested, "m_invert", true);
                    Assert.That(nested.Evaluate(character), Is.EqualTo(!expected), label + ": inverted");
                }

                TriggerCondition_Nested someOfTwo = Nested(any, Probe(F), Probe(T));
                TriggerCondition_Nested bothOfTwo = Nested(all, Probe(T), Probe(T));
                TriggerCondition_Nested noneOfTwo = Nested(any, Probe(F), Probe(F));
                Assert.That(Nested(all, someOfTwo, bothOfTwo).Evaluate(character), Is.True, "all of [some, both]");
                Assert.That(Nested(all, someOfTwo, noneOfTwo).Evaluate(character), Is.False, "all of [some, none]");
                Assert.That(Nested(any, noneOfTwo, someOfTwo).Evaluate(character), Is.True, "any of [none, some]");
            });
        }

        // ---- the stamina trigger family ----------------------------------------------------------

        private sealed class ChaosAbilityDouble : ActorAbility, IAbilityBurstAttackChaos
        {
            private readonly CharacterAbilityDefinition_BurstAttackChaos m_definition;
            public readonly List<bool> Recharges = new List<bool>();

            public ChaosAbilityDouble(CharacterAbilityDefinition_BurstAttackChaos definition) => m_definition = definition;

            public override AbilityDefinition DefinitionBase => m_definition;
            public CharacterAbilityDefinition_BurstAttackChaos BurstAttackDefinition => m_definition;
            public void OnStaminaRecharge(bool triggerAbility) => Recharges.Add(triggerAbility);
            public bool TryStopTimeDilation(bool interrupt) => false;
            public bool IsTimeDilationActive() => false;
        }

        // A current character holding both staminas, built by their real constructors against a real
        // CollectableManager, with the manager and save references that Character.Initialise would set.
        private static void WithStaminas(Action<Owned, CharacterManager, Character, CharacterBoostStamina, CharacterChaosStamina> body) =>
            WithManager((owned, manager, character) =>
            {
                owned.Register(new CollectableManager());
                var boost = new CharacterBoostStamina(character);
                var chaos = new CharacterChaosStamina(character);
                typeof(Character).GetProperty("BoostStamina").GetSetMethod(true).Invoke(character, new object[] { boost });
                typeof(Character).GetProperty("ChaosStamina").GetSetMethod(true).Invoke(character, new object[] { chaos });
                Set(character, "m_characterManager", manager);
                Set(character, "m_saveManagerRef", ProcessManager.GetSystemRef<SaveManager>(null, true));
                SetCurrent(manager, character, true);
                try
                {
                    body(owned, manager, character, boost, chaos);
                }
                finally
                {
                    chaos.Close();
                    boost.Close();
                }
            });

        private static CharacterChaosStaminaTrigger NewChaosTrigger(Owned owned, bool triggerAbility)
        {
            var trigger = Add<CharacterChaosStaminaTrigger>(owned, "Lucid chaos stamina trigger");
            // Awake is what resolves the manager reference; the host never wakes in Edit Mode.
            Call(trigger, "Awake");
            trigger.SetTriggerWhenInactive(true);
            Set(trigger, "m_abilityType", ActorAbilityType.Boss_AttackArea);
            Set(trigger, "m_triggerAbility", triggerAbility);
            return trigger;
        }

        private static ChaosAbilityDouble NewChaosAbility(Owned owned, float activationCost)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_BurstAttackChaosAir>());
            definition.ChaosStaminaCostDefinition = new CharacterStamina.Definition
            {
                StaminaActivationCost = activationCost,
                OrientateToInputForward = false
            };
            return new ChaosAbilityDouble(definition);
        }

        private static void SetAbility(CharacterChaosStaminaTrigger trigger, object ability) =>
            typeof(CharacterStaminaTrigger<IAbilityBurstAttackChaos>).GetProperty("Ability", AnyMember).GetSetMethod(true)
                .Invoke(trigger, new[] { ability });

        private static object GetAbility(CharacterChaosStaminaTrigger trigger) =>
            typeof(CharacterStaminaTrigger<IAbilityBurstAttackChaos>).GetProperty("Ability", AnyMember).GetGetMethod(true).Invoke(trigger, null);

        // get_Stamina 0x76F2B0 loads Character +0x390, the chaos stamina, from the current character; the boost
        // stamina (+0x388) is a different object.
        [Test]
        public void ChaosTriggerStaminaIsTheCurrentCharactersChaosStamina()
        {
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                var trigger = NewChaosTrigger(owned, false);
                object stamina = typeof(CharacterChaosStaminaTrigger).GetProperty("Stamina", AnyMember).GetValue(trigger);
                Assert.That(stamina, Is.SameAs(chaos));
                Assert.That(stamina, Is.Not.SameAs(boost));
            });
        }

        // Trigger 0x76F2D0 does nothing without a current character (TryGetCharacter, 0x76f323) or without an
        // ability (test r14,r14; je 0x76f459): the ability is not asked and the stamina stays untouched.
        [Test]
        public void ChaosTriggerDoesNothingWithoutACharacterOrAnAbility()
        {
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                var trigger = NewChaosTrigger(owned, true);
                ChaosAbilityDouble ability = NewChaosAbility(owned, 0.25f);

                trigger.Trigger();
                Assert.That(GetAbility(trigger), Is.Null);
                Assert.That(chaos.Active, Is.False, "no ability");
                Assert.That(Get<bool>(character, "m_retainImpulses"), Is.False, "the ability test comes before the retain call (0x76f337)");

                SetAbility(trigger, ability);
                SetCurrent(manager, character, false);
                trigger.Trigger();
                Assert.That(ability.Recharges, Is.Empty, "no valid character");
                Assert.That(chaos.Active, Is.False);
                Assert.That(chaos.Stamina, Is.EqualTo(1f));
                Assert.That(Get<bool>(character, "m_retainImpulses"), Is.False, "no character: nothing is retained");
            });
        }

        // Trigger 0x76F2D0 retains the character's current impulses (RetainCurrentImpulses, 0x76f3b3; the activation's
        // own EndImpulses consumes that flag again), activates the
        // chaos stamina with the burst definition's chaos cost (+0x100 of BurstAttackDefinition), the trigger's
        // ability type (+0x4c) and both overrides set (ecx=1, r8d=1, call 0x76f470), and then tells the ability
        // whether to trigger immediately (OnStaminaRecharge, esi = +0x58). The boost stamina is untouched.
        [Test]
        public void ChaosTriggerActivatesTheChaosStaminaWithTheBurstDefinitionAndNotifiesTheAbility()
        {
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                ChaosAbilityDouble ability = NewChaosAbility(owned, 0.25f);
                var trigger = NewChaosTrigger(owned, true);
                SetAbility(trigger, ability);

                // A queued jump shows which impulses the activation ends.
                var brain = new CharacterBrain_Replay();
                Set(character, "m_brain", brain);
                Call(brain, "SetState", GameAction.CharacterJump, true);
                Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterJump), Is.True);

                Assert.That(Get<bool>(character, "m_retainImpulses"), Is.False);
                trigger.Trigger();
                // Activate tail-calls Character.EndImpulses (jmp 0x734b80 at 0x76f682). The retain made before it
                // is consumed there instead of the brain's impulses ending: a retain after the activation would
                // leave the flag set, and none would end the jump.
                Assert.That(brain.ActionStateQueued.GetValue(GameAction.CharacterJump), Is.True, "the retained jump survives the activation");
                Assert.That(Get<bool>(character, "m_retainImpulses"), Is.False, "the activation's EndImpulses consumed the retain");
                Assert.That(chaos.Active, Is.True);
                Assert.That(chaos.Stamina, Is.EqualTo(0.75f), "the definition's activation cost was spent");
                Assert.That(Get<bool>(chaos, "m_overrideImpulse"), Is.True, "the impulse is overridden");
                Assert.That(Get<ActorAbilityType>(chaos, "m_abilityType"), Is.EqualTo(ActorAbilityType.Boss_AttackArea));
                Assert.That(ability.Recharges, Is.EqualTo(new[] { true }));
                Assert.That(boost.Active, Is.False);
                Assert.That(boost.Stamina, Is.EqualTo(1f), "the boost stamina is not the one used");

                Set(trigger, "m_triggerAbility", false);
                chaos.ActivateCooldown(5f);
                trigger.Trigger();
                Assert.That(chaos.Stamina, Is.EqualTo(0.5f), "the activation override ignores the cooldown");
                Assert.That(ability.Recharges, Is.EqualTo(new[] { true, false }));
            });
        }

        // Start 0xE69CB0 registers OnCharacterChange with the manager, immediately only when a character is valid
        // (edx=1 is invokeImmediatelyIfValid); OnDestroy (0xE69D50) releases the same handler.
        [Test]
        public void StartRegistersTheCharacterChangeHandlerAndDestroyReleasesIt()
        {
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                SetCurrent(manager, character, false);
                var trigger = NewChaosTrigger(owned, false);
                Assert.That(HandlerNames(Get<Delegate>(manager, "m_onCharacterChange")), Does.Not.Contain("OnCharacterChange"));

                Call(trigger, "Start");
                Assert.That(HandlerNames(Get<Delegate>(manager, "m_onCharacterChange")).Count(name => name == "OnCharacterChange"), Is.EqualTo(1));

                Call(trigger, "OnDestroy");
                Assert.That(HandlerNames(Get<Delegate>(manager, "m_onCharacterChange")), Does.Not.Contain("OnCharacterChange"));
            });
        }

        // Start 0xE69CB0 passes invokeImmediatelyIfValid = 1 (mov edx,1 at 0xe69d33): with a valid current character the
        // handler runs inside the registration, so the trigger already holds that character's ability.
        [Test]
        public void StartWithAValidCharacterStoresItsAbilityAtOnce()
        {
            InitialiseCollectable();
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                var trigger = NewChaosTrigger(owned, false);
                trigger.gameObject.AddComponent<Collectable>();
                ChaosAbilityDouble ability = NewChaosAbility(owned, 0.25f);
                Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities").Add(ActorAbilityType.Boss_AttackArea, ability);
                Assert.That(GetAbility(trigger), Is.Null);

                Call(trigger, "Start");
                Assert.That(GetAbility(trigger), Is.SameAs(ability), "the handler ran during the registration");

                Call(trigger, "OnDestroy");
            });
        }

        // OnDestroy 0xE69D50 tests the cached reference with SystemRef.IsNull (0xe69da3) and returns (0xe69dac) before
        // reading it: with no manager registered nothing is resolved, so nothing is reported.
        [Test]
        public void DestroyWithoutAManagerReleasesNothingAndReportsNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<CharacterManager>();
                var trigger = Add<CharacterChaosStaminaTrigger>(owned, "Lucid chaos stamina trigger");
                Call(trigger, "Awake");

                Assert.DoesNotThrow(() => Call(trigger, "OnDestroy"));
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            });
        }

        private static void InitialiseCollectable() => Owned.Run(owned =>
        {
            // Collectable caches typed references to these rows in static fields when it is first used.
            owned.LeaseRow<CollectableManager>();
            owned.LeaseRow<LevelManager>();
            owned.LeaseRow<CharacterManager>();
            RuntimeHelpers.RunClassConstructor(typeof(Collectable).TypeHandle);
        });

        // OnCharacterChange 0xE69E10 reads the Collectable on its own object (GetComponent, 0xE69E59), asks the stamina
        // about that collectable's type (vtable slot 4, IsCollectableType, answer unused), then stores the character's
        // ability of the trigger's type cast to T, null when it has none or it is not a T (IsInst, 0xE69EC7).
        [Test]
        public void CharacterChangeStoresTheCharactersAbilityOfTheTriggersTypeAsT()
        {
            InitialiseCollectable();
            WithStaminas((owned, manager, character, boost, chaos) =>
            {
                var trigger = NewChaosTrigger(owned, false);
                trigger.gameObject.AddComponent<Collectable>();
                ChaosAbilityDouble ability = NewChaosAbility(owned, 0.25f);
                var abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");

                abilities.Add(ActorAbilityType.Boss_AttackArea, ability);
                Call(trigger, "OnCharacterChange", character);
                Assert.That(GetAbility(trigger), Is.SameAs(ability));

                abilities.Remove(ActorAbilityType.Boss_AttackArea);
                Call(trigger, "OnCharacterChange", character);
                Assert.That(GetAbility(trigger), Is.Null, "a missing ability replaces the stored one");

                abilities.Add(ActorAbilityType.Boss_AttackArea, new PlainAbility());
                Call(trigger, "OnCharacterChange", character);
                Assert.That(GetAbility(trigger), Is.Null, "an ability that is not a T is stored as null");

                abilities.Clear();
                abilities.Add(ActorAbilityType.Boss_AttackMelee, ability);
                Call(trigger, "OnCharacterChange", character);
                Assert.That(GetAbility(trigger), Is.Null, "only the trigger's own ability type is looked up");
            });
        }

        private sealed class PlainAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }
    }
}
