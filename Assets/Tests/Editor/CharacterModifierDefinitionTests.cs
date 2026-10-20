using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The modifier ability definitions and the ground boost definition. Expected values come from the 1.10.1
    // x86-64 constructors and bodies cited per test; the packed field defaults are read from the movabs immediates
    // of the constructors and the keyframe constant from the binary's data section (VA 0x29a6be0), never from the
    // implementation.
    public sealed class CharacterModifierDefinitionTests
    {
        private readonly List<Object> m_owned = new List<Object>();

        private T Create<T>() where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            m_owned.Add(definition);
            return definition;
        }

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }

        private static readonly Type[] ModifierDefinitions =
        {
            typeof(CharacterAbilityDefinition_ModifierCollectableAttract), typeof(CharacterAbilityDefinition_ModifierCollectableBoost),
            typeof(CharacterAbilityDefinition_ModifierCollectableDamage), typeof(CharacterAbilityDefinition_ModifierCollectableRevive),
            typeof(CharacterAbilityDefinition_ModifierCollectableStart), typeof(CharacterAbilityDefinition_ModifierRingSpeed),
            typeof(CharacterAbilityDefinition_ModifierSwitchAvailable),
        };

        // The shared base constructor is inlined into every leaf (0x545410, 0x5454F0, 0x5455E0, 0x5456D0, 0x5457C0,
        // 0x545910, 0x545D90): each allocates the empty list of modified ability types at +0x48.
        [Test]
        public void EveryModifierStartsWithAnEmptyModifyAbilitiesList()
        {
            foreach (Type type in ModifierDefinitions)
            {
                var definition = (CharacterAbilityDefinition_Modifier)ScriptableObject.CreateInstance(type);
                m_owned.Add(definition);
                Assert.That(definition.ModifyAbilities, Is.Not.Null.And.Empty, type.Name);
            }
        }

        // The ring collectable (CollectableType.Ring, 0x46b6e73d) is the low dword of the movabs immediate stored at +0x50
        // by 0x5454F0, 0x5455E0, 0x5456D0 and 0x5457C0; the high dword is the second field: 0x3c23d70a (0.01f), 0xa (10),
        // 0x3f000000 (0.5f) and 0x64 (100).
        [Test]
        public void CollectableModifiersStoreTheNativePackedDefaults()
        {
            var boost = Create<CharacterAbilityDefinition_ModifierCollectableBoost>();
            Assert.That((boost.CollectableType, boost.StaminaMultiplier, boost.CollectAnimationDefinition), Is.EqualTo((CollectableType.Ring, 0.01f, (ActorAnimationDefinition)null)));
            var damage = Create<CharacterAbilityDefinition_ModifierCollectableDamage>();
            Assert.That((damage.CollectableType, damage.MaxDamage), Is.EqualTo((CollectableType.Ring, 10)));
            var revive = Create<CharacterAbilityDefinition_ModifierCollectableRevive>();
            Assert.That((revive.CollectableType, revive.Multiplier), Is.EqualTo((CollectableType.Ring, 0.5f)));
            var start = Create<CharacterAbilityDefinition_ModifierCollectableStart>();
            Assert.That((start.CollectableType, start.StartingCount), Is.EqualTo((CollectableType.Ring, 100)));
        }

        // 0x545410 stores nothing for its own fields: the attract tiers and the animation stay unset.
        [Test]
        public void AttractStoresNoDefaultsOfItsOwn()
        {
            var attract = Create<CharacterAbilityDefinition_ModifierCollectableAttract>();
            Assert.That((attract.CollectableTypes, attract.TierIndex, attract.CollectAnimationDefinition), Is.EqualTo(((CollectableType[])null, 0, (ActorAnimationDefinition)null)));
        }

        // 0x545C50 builds the two statics with Keyframe(time, value) from the constant 1.0f (VA 0x29a6be0): the start key
        // is (0, 1) and the end key (1, 1). 0x545910 gives each curve its own AnimationCurve of the two keys.
        [Test]
        public void RingSpeedCurvesRunFlatAtOneAndAreDistinct()
        {
            var definition = Create<CharacterAbilityDefinition_ModifierRingSpeed>();
            AnimationCurve[] curves =
            {
                definition.AccelerationMultiplier, definition.SpeedMinMultiplier, definition.SpeedMaxMultiplier, definition.DecelerationMultiplier,
            };
            foreach (AnimationCurve curve in curves)
            {
                Assert.That(curve.length, Is.EqualTo(2));
                Assert.That((curve[0].time, curve[0].value), Is.EqualTo((0f, 1f)));
                Assert.That((curve[1].time, curve[1].value), Is.EqualTo((1f, 1f)));
            }
            // AnimationCurve.Equals compares keys, so distinct instances are counted by reference.
            Assert.That(curves.Where((curve, index) => curves.Take(index).Any(earlier => ReferenceEquals(earlier, curve))), Is.Empty, "one curve per field");
            Assert.That(definition.TierDefinitions, Is.Not.Null.And.Empty);

            Assert.That(Get<Keyframe>(definition, "DefaultKeyframeStart"), Is.EqualTo(new Keyframe(0f, 1f)));
            Assert.That(Get<Keyframe>(definition, "DefaultKeyframeEnd"), Is.EqualTo(new Keyframe(1f, 1f)));
        }

        // 0x5458B0: the first tier's ring count when there is a tier, otherwise zero; it is the first entry, not the smallest.
        [Test]
        public void RingSpeedTierRingCountMinIsTheFirstTier()
        {
            var definition = Create<CharacterAbilityDefinition_ModifierRingSpeed>();
            Assert.That(definition.TierRingCountMin, Is.EqualTo(0));
            definition.TierDefinitions.Add(Tier(7));
            Assert.That(definition.TierRingCountMin, Is.EqualTo(7));
            definition.TierDefinitions.Add(Tier(3));
            Assert.That(definition.TierRingCountMin, Is.EqualTo(7), "the later, smaller tier does not lower it");
        }

        internal static CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition Tier(int ringCount, ActorAnimationDefinition animation = null)
        {
            var tier = new CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition();
            Set(tier, "m_ringCount", ringCount);
            Set(tier, "m_animationDefinition", animation);
            return tier;
        }

        // 0x545D00 and 0x545D10 return the two private serialized fields.
        [Test]
        public void TierDefinitionReadsItsSerializedFields()
        {
            ActorAnimationDefinition animation = Create<ActorAnimationDefinition>();
            CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition tier = Tier(12, animation);
            Assert.That((tier.RingCount, tier.AnimationDefinition), Is.EqualTo((12, animation)));
            Assert.That(new CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition().RingCount, Is.EqualTo(0));
        }

        // ---- Declaration shape (not behaviour): the original script types, bases, menu entries and members ----

        [Test]
        public void DeclarationShapeDefinitionsNameTheirOwnAbilityScript()
        {
            foreach ((Type definition, Type ability) in new[]
            {
                (typeof(CharacterAbilityDefinition_ModifierCollectableAttract), typeof(CharacterAbility_ModifierCollectableAttract)),
                (typeof(CharacterAbilityDefinition_ModifierCollectableBoost), typeof(CharacterAbility_ModifierCollectableBoost)),
                (typeof(CharacterAbilityDefinition_ModifierCollectableDamage), typeof(CharacterAbility_ModifierCollectableDamage)),
                (typeof(CharacterAbilityDefinition_ModifierCollectableRevive), typeof(CharacterAbility_ModifierCollectableRevive)),
                (typeof(CharacterAbilityDefinition_ModifierCollectableStart), typeof(CharacterAbility_ModifierCollectableStart)),
                (typeof(CharacterAbilityDefinition_ModifierRingSpeed), typeof(CharacterAbility_ModifierRingSpeed)),
                (typeof(CharacterAbilityDefinition_ModifierSwitchAvailable), typeof(CharacterAbility_ModifierSwitchAvailable)),
                (typeof(CharacterAbilityDefinition_MovementGroundBoost), typeof(CharacterAbility_MovementGroundBoost)),
            })
            {
                var instance = (AbilityDefinition)ScriptableObject.CreateInstance(definition);
                m_owned.Add(instance);
                Assert.That(instance.ScriptType, Is.EqualTo(ability), definition.Name);
            }
        }

        [Test]
        public void DeclarationShapeDefinitionsKeepTheirOriginalBases()
        {
            foreach (Type type in ModifierDefinitions)
                Assert.That(type.BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_Modifier)), type.Name);
            Assert.That(typeof(CharacterAbilityDefinition_MovementGroundBoost).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_MovementGroundBoostBase)));
            Type tier = typeof(CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition);
            Assert.That(tier.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(tier.IsNestedPublic && tier.IsSerializable, Is.True);
        }

        // Metadata attribute order of the Il2CppSetOption pair (1 = NullChecks, 2 = ArrayBoundsChecks) differs per type.
        [Test]
        public void DeclarationShapeIl2CppOptionsKeepTheirMetadataOrder()
        {
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierCollectableAttract), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierCollectableBoost), 2, 1);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierCollectableDamage), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierCollectableRevive), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierCollectableStart), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierRingSpeed), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierSwitchAvailable), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_MovementGroundBoost), 1, 2);
            AssertIl2CppOptions(typeof(CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition), 1, 2);
        }

        [Test]
        public void DeclarationShapeDefinitionsAreCreatableFromTheAssetMenu()
        {
            foreach ((Type type, string menu) in new[]
            {
                (typeof(CharacterAbilityDefinition_ModifierCollectableAttract), "ModifierCollectableAttract"),
                (typeof(CharacterAbilityDefinition_ModifierCollectableBoost), "ModifierCollectableBoost"),
                (typeof(CharacterAbilityDefinition_ModifierCollectableDamage), "ModifierCollectableDamage"),
                (typeof(CharacterAbilityDefinition_ModifierCollectableRevive), "ModifierCollectableRevive"),
                (typeof(CharacterAbilityDefinition_ModifierCollectableStart), "ModifierCollectableStart"),
                (typeof(CharacterAbilityDefinition_ModifierRingSpeed), "ModifierRingSpeed"),
                (typeof(CharacterAbilityDefinition_ModifierSwitchAvailable), "ModifierSwitch"),
                (typeof(CharacterAbilityDefinition_MovementGroundBoost), "MovementGroundBoost"),
            })
            {
                CreateAssetMenuAttribute attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);
                Assert.That(attribute.fileName, Is.EqualTo(type.Name), type.Name);
                Assert.That(attribute.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + menu), type.Name);
                Assert.That(attribute.order, Is.EqualTo(1), type.Name);
            }
        }

        [Test]
        public void DeclarationShapeFieldsKeepTheirOriginalOrderAndAuthoring()
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] Names(Type type) => type.GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray();
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierCollectableAttract)), Is.EqualTo(new[] { "CollectableTypes", "TierIndex", "CollectAnimationDefinition" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierCollectableBoost)), Is.EqualTo(new[] { "CollectableType", "StaminaMultiplier", "CollectAnimationDefinition" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierCollectableDamage)), Is.EqualTo(new[] { "CollectableType", "MaxDamage" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierCollectableRevive)), Is.EqualTo(new[] { "CollectableType", "Multiplier" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierCollectableStart)), Is.EqualTo(new[] { "CollectableType", "StartingCount" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierRingSpeed)), Is.EqualTo(new[]
            {
                "DefaultKeyframeStart", "DefaultKeyframeEnd", "AccelerationMultiplier", "SpeedMinMultiplier", "SpeedMaxMultiplier", "DecelerationMultiplier", "TierDefinitions",
            }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition)), Is.EqualTo(new[] { "m_ringCount", "m_animationDefinition" }));
            Assert.That(Names(typeof(CharacterAbilityDefinition_ModifierSwitchAvailable)), Is.Empty);
            Assert.That(Names(typeof(CharacterAbilityDefinition_MovementGroundBoost)), Is.Empty);

            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_ModifierCollectableRevive), "Multiplier")), Is.EqualTo("Fraction of the currently held collectable type that will be lost."));
            UnityEngine.RangeAttribute range = Field(typeof(CharacterAbilityDefinition_ModifierCollectableRevive), "Multiplier").GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That((range.min, range.max), Is.EqualTo((0f, 1f)));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_ModifierCollectableAttract), "CollectableTypes")),
                Is.EqualTo("Collectable types to set attract tier on.  NB: Specifying none will default to applying to all types."));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_ModifierCollectableAttract), "TierIndex")),
                Is.EqualTo("Collectable attract collision trigger tier index.  NB: 0 based."));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_ModifierCollectableDamage), "MaxDamage")),
                Is.EqualTo("Maximum amount that can be lost to non-instant death hazards."));
            Assert.That(Tooltip(Field(typeof(CharacterAbilityDefinition_ModifierRingSpeed), "TierDefinitions")),
                Is.EqualTo("Defines each ring tier's audio and pfx to trigger."));
            Assert.That(Field(typeof(CharacterAbilityDefinition_ModifierRingSpeed.TierDefinition), "m_ringCount").GetCustomAttribute<SerializeField>(), Is.Not.Null);
            FieldInfo start = Field(typeof(CharacterAbilityDefinition_ModifierRingSpeed), "DefaultKeyframeStart");
            Assert.That(start.IsPrivate && start.IsStatic && start.IsInitOnly, Is.True);
        }
    }
}
