using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TriggerCondition_CachedBase (TypeDef 0x0200074b, x86-64 0x5CCEB0..0x5CD408)
    // and its assets CachedAbilityGroupInUse (0x02000749), CachedAbilityInUse (0x0200074a),
    // CachedCharacterFSMValue`1 (0x0200074c, read from the <bool> instantiation 0xC81920..0xC81CA0) and
    // CachedCharacterFSMValueBool (0x0200074d). An asset's OnEnable runs when it is created, so each
    // case creates its assets inside TriggerConditionTestSupport.Run, after the static CharacterManager
    // reference has been replaced; its subscriptions then land on the case's own reference and manager.
    public sealed class OriginalCachedTriggerConditionTests : OwnedGraphNamesFixture
    {
        // The synthetic LucidCachedFlag storage key registers its name in GraphNameLookup.
        public OriginalCachedTriggerConditionTests() : base("LucidCachedFlag") { }

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static T Create<T>(Owned owned) where T : ScriptableObject => owned.Track(ScriptableObject.CreateInstance<T>());

        private static Dictionary<ActorAbilityType, ActorAbilityInUseData> InUse(params ActorAbilityType[] abilities)
        {
            var data = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
            foreach (ActorAbilityType ability in abilities) data[ability] = new ActorAbilityInUseData { InUse = true };
            return data;
        }

        private static Dictionary<ActorAbilityType, ActorAbilityInUseData> LastUsed(ActorAbilityType ability, float time)
        {
            var data = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
            data[ability] = new ActorAbilityInUseData { InUse = false, TimeLastUsedSeconds = time };
            return data;
        }

        private static CharacterAbilityTypeGroup Group(Owned owned, params ActorAbilityType[] abilities)
        {
            var group = Create<CharacterAbilityTypeGroup>(owned);
            foreach (ActorAbilityType ability in abilities)
            {
                var definition = new CharacterAbilityTypeGroup.AbilityTypeDefinition();
                Set(definition, "m_type", ability);
                group.Abilities.Add(definition);
            }
            return group;
        }

        // Whether the asset is among the character's pre-FSM subscribers, as OnCharacterChange 0x5CD0A0
        // leaves it (Character +0x3D0, a Delegate.Combine of OnCharacterPreFSM).
        private static bool ResetsOnPreFsm(object asset, Character character) => character.OnFixedUpdatePreFSM.GetInvocationList()
            .Any(callback => ReferenceEquals(callback.Target, asset) && callback.Method.Name == "OnCharacterPreFSM");

        private static void ResetCache(object asset, Character character) => Call(asset, "OnCharacterPreFSM", character);

        // Metadata of the five TypeDefs, the asset-menu attributes and the literal menu prefix.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertType(typeof(TriggerCondition_CachedBase), 0x00100081, typeof(ScriptableObjectWithGuid), new[] { 2, 1 },
                new[] { ("m_invert", 0x0001), ("s_characterManagerRef", 0x0034), ("m_hasCache", 0x0004), ("AssetMenu", 0x8054) },
                new[] { ("OnEnable", 0x01c4), ("OnCharacterManagerStartup", 0x0081), ("OnCharacterChange", 0x0081), ("OnCharacterPreFSM", 0x0081),
                        ("DoEvaluate", 0x05c4), ("Evaluate", 0x0086), ("DoEvaluate", 0x01c4), ("Evaluate", 0x0086),
                        ("TryGetCharacter", 0x0091), (".ctor", 0x1884), (".cctor", 0x1891) });
            Assert.That(Field(typeof(TriggerCondition_CachedBase), "AssetMenu").GetRawConstantValue(), Is.EqualTo("HardlightProject/TriggerConditions/"));
            AssertType(typeof(TriggerCondition_CachedAbilityGroupInUse), 0x00100001, typeof(TriggerCondition_CachedBase), new[] { 2, 1 },
                new[] { ("m_abilityTypeGroup", 0x0001), ("m_cachedValue", 0x0001) }, new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_CachedAbilityInUse), 0x00100001, typeof(TriggerCondition_CachedBase), new[] { 1, 2 },
                new[] { ("m_abilityType", 0x0001), ("m_usedWithinSeconds", 0x0001), ("m_cachedValue", 0x0001) },
                new[] { ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });
            AssertType(typeof(TriggerCondition_CachedCharacterFSMValue<>), 0x00100081, typeof(TriggerCondition_CachedBase), new[] { 2, 1 },
                new[] { ("m_valueName", 0x0001), ("m_valueThreshold", 0x0004), ("m_comparisonType", 0x0004), ("m_fsmVarName", 0x0001), ("m_cachedValue", 0x0004) },
                new[] { ("OnEnable", 0x00c4), ("GetValue", 0x0084), ("GetValueDefault", 0x05c4), (".ctor", 0x1884) });
            Type parameter = typeof(TriggerCondition_CachedCharacterFSMValue<>).GetGenericArguments().Single();
            Assert.That((int)parameter.GenericParameterAttributes, Is.Zero);
            Assert.That(parameter.GetGenericParameterConstraints(), Is.Empty);
            AssertType(typeof(TriggerCondition_CachedCharacterFSMValueBool), 0x00100001, typeof(TriggerCondition_CachedCharacterFSMValue<bool>), new[] { 2, 1 },
                new (string, int)[0], new[] { ("GetValueDefault", 0x00c4), ("DoEvaluate", 0x00c4), (".ctor", 0x1886) });

            foreach (Type asset in new[] { typeof(TriggerCondition_CachedAbilityGroupInUse), typeof(TriggerCondition_CachedAbilityInUse), typeof(TriggerCondition_CachedCharacterFSMValueBool) })
            {
                var menu = (CreateAssetMenuAttribute)asset.GetCustomAttributes(typeof(CreateAssetMenuAttribute), false).Single();
                Assert.That(menu.menuName, Is.EqualTo("HardlightProject/TriggerConditions/" + asset.Name), asset.Name);
                Assert.That(menu.fileName, Is.EqualTo(asset.Name), asset.Name);
            }
            Assert.That(AttributeTypes(Field(typeof(TriggerCondition_CachedAbilityInUse), "m_abilityType")).Select(type => type.Name),
                Is.EquivalentTo(new[] { "HashEnumAttribute", "SerializeField" }));
            Assert.That(Tooltip(Field(typeof(TriggerCondition_CachedAbilityInUse), "m_usedWithinSeconds")),
                Is.EqualTo("Will return true if ability is in use or was last used this many seconds ago."));
        }

        // OnEnable 0x5CCEB0 adds OnCharacterManagerStartup to the reference's startup event and, when the
        // manager is there, registers OnCharacterChange with it, which calls it at once for a valid
        // character. OnCharacterChange 0x5CD0A0 adds OnCharacterPreFSM to that character's pre-FSM
        // event and OnCharacterPreFSM 0x5CD190 clears m_hasCache. Nothing ever unsubscribes.
        [Test]
        public void EnablingSubscribesToTheCurrentCharactersPreFsmUpdate()
        {
            Run((owned, scene) =>
            {
                var asset = Create<TriggerConditionCachedProbe>(owned);
                Character other = NewCharacter(owned, new FSMStorage(0));
                Assert.That(ResetsOnPreFsm(asset, scene.Character), Is.True);
                Assert.That(ResetsOnPreFsm(asset, other), Is.False, "only the current character is followed");

                Set(asset, "m_hasCache", true);
                scene.Character.OnFixedUpdatePreFSM(scene.Character);
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.False, "the pre-FSM update drops the cache");
                Set(asset, "m_hasCache", true);
                other.OnFixedUpdatePreFSM(other);
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.True);
            });
        }

        // OnCharacterChange 0x5CD0A0 combines its handler (Delegate.Combine, call 0x5CD11C) into Character
        // +0x3D0: every earlier subscriber, the field initializer's included, stays in the list.
        [Test]
        public void SubscribingAddsToThePreFsmListInsteadOfReplacingIt()
        {
            Run((owned, scene) =>
            {
                Delegate[] before = scene.Character.OnFixedUpdatePreFSM.GetInvocationList();
                var first = Create<TriggerConditionCachedProbe>(owned);
                var second = Create<TriggerConditionCachedProbe>(owned);
                Delegate[] after = scene.Character.OnFixedUpdatePreFSM.GetInvocationList();
                Assert.That(after.Length, Is.EqualTo(before.Length + 2));
                Assert.That(after.Take(before.Length), Is.EqualTo(before), "earlier subscribers are kept in order");
                Assert.That(ResetsOnPreFsm(first, scene.Character), Is.True);
                Assert.That(ResetsOnPreFsm(second, scene.Character), Is.True);
            });
        }

        // OnCharacterManagerStartup 0x5CD020 registers OnCharacterChange with the manager the startup
        // event passes, which follows that manager's current character. An asset enabled while the
        // current manager has no valid character follows nobody until then.
        [Test]
        public void ALaterCharacterManagerStartupRegistersWithItsOwnCharacter()
        {
            Run((owned, scene) =>
            {
                scene.SetCurrent(null, false);
                var asset = Create<TriggerConditionCachedProbe>(owned);
                Assert.That(ResetsOnPreFsm(asset, scene.Character), Is.False);

                Character later = NewCharacter(owned, new FSMStorage(0));
                CharacterManager second = scene.Characters.NewManager();
                Set(second, "m_currentCharacter", later);
                Set(second, "<CharacterValid>k__BackingField", true);
                Scene.ReplaceWith(scene.CachedReference, second);
                Assert.That(ResetsOnPreFsm(asset, later), Is.True);
                Assert.That(ResetsOnPreFsm(asset, scene.Character), Is.False);
            });
        }

        // DoEvaluate() 0x5CD1D0 is false without calling the abstract overload unless
        // TryGetCurrentCharacter succeeds; Evaluate() 0x5CD1A0 xors m_invert; Evaluate(Character)
        // 0x5CD350 xors it for the given character without the manager.
        [Test]
        public void AssetEvaluationNeedsAValidCurrentCharacterAndXorsTheInversion()
        {
            Run((owned, scene) =>
            {
                var asset = Create<TriggerConditionCachedProbe>(owned);
                asset.UseBase = true;
                foreach (var row in new[]
                {
                    (valid: true, answer: true, invert: false, expected: true),
                    (valid: true, answer: true, invert: true, expected: false),
                    (valid: true, answer: false, invert: true, expected: true),
                    (valid: false, answer: true, invert: false, expected: false),
                    (valid: false, answer: true, invert: true, expected: true),
                })
                {
                    scene.SetCurrent(scene.Character, row.valid);
                    asset.CharacterResult = row.answer;
                    Set(asset, "m_invert", row.invert);
                    asset.CharacterEvaluations = 0;
                    Assert.That(asset.Evaluate(), Is.EqualTo(row.expected), row.ToString());
                    Assert.That(asset.CharacterEvaluations, Is.EqualTo(row.valid ? 1 : 0), row.ToString());
                }
                asset.UseBase = false;
                scene.SetCurrent(null, false);
                Set(asset, "m_invert", true);
                asset.CharacterResult = true;
                Assert.That(asset.Evaluate(scene.Character), Is.False);
                asset.CharacterResult = false;
                Assert.That(asset.Evaluate(scene.Character), Is.True);
            });
        }

        // TriggerCondition_CachedAbilityInUse.DoEvaluate 0x5CCE10 returns m_cachedValue (+0x2C) while
        // m_hasCache (+0x21) is set; otherwise it stores IsAbilityInUse(character, m_abilityType,
        // m_usedWithinSeconds) and sets the flag. Evaluate xors the inversion after, so the cache
        // holds the raw answer, and a cached false is kept as well.
        [Test]
        public void CachedAbilityInUseAnswersOnceUntilTheCacheIsReset()
        {
            Run((owned, scene) =>
            {
                var asset = Create<TriggerCondition_CachedAbilityInUse>(owned);
                Assert.That(Get<float>(asset, "m_usedWithinSeconds"), Is.EqualTo(0.2f));
                ActorAbilityType ability = ActorAbilityType.Boss_AttackArea;
                Set(asset, "m_abilityType", ability);

                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(ability));
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.False);
                Assert.That(asset.Evaluate(scene.Character), Is.True);
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.True);
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse());
                Assert.That(asset.Evaluate(scene.Character), Is.True, "the cached answer is kept");
                Set(asset, "m_invert", true);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "the inversion applies to the cached answer");
                Assert.That(Get<bool>(asset, "m_cachedValue"), Is.True, "the cache holds the raw answer");
                Set(asset, "m_invert", false);

                ResetCache(asset, scene.Character);
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.False);
                Assert.That(asset.Evaluate(scene.Character), Is.False);
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(ability));
                Assert.That(asset.Evaluate(scene.Character), Is.False, "a cached false is kept as well");
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True);
            });
        }

        // TriggerCondition_CachedAbilityGroupInUse.DoEvaluate 0x5CCC60 recomputes only without a cache:
        // it clears m_cachedValue (+0x30), sets it when any ability of the group (+0x28) is in use,
        // then sets the flag. The group is not read while the cache holds.
        [Test]
        public void CachedAbilityGroupRecomputesFromScratchAfterAReset()
        {
            Run((owned, scene) =>
            {
                var asset = Create<TriggerCondition_CachedAbilityGroupInUse>(owned);
                ActorAbilityType first = ActorAbilityType.Boss_AttackArea;
                ActorAbilityType second = ActorAbilityType.Boss_AttackMelee;
                Set(asset, "m_abilityTypeGroup", Group(owned, first, second));

                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(second));
                Assert.That(asset.Evaluate(scene.Character), Is.True, "the group's last ability is in use");
                Assert.That(Get<bool>(asset, "m_hasCache"), Is.True);
                Set(asset, "m_abilityTypeGroup", null);
                Assert.That(asset.Evaluate(scene.Character), Is.True, "the group is not read while the cache holds");

                Set(asset, "m_abilityTypeGroup", Group(owned, first, second));
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse());
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "the previous true does not survive a recompute");
                Assert.That(Get<bool>(asset, "m_cachedValue"), Is.False);
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, InUse(first));
                Assert.That(asset.Evaluate(scene.Character), Is.False, "a cached false is kept");
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True);

                Set(asset, "m_abilityTypeGroup", null);
                ResetCache(asset, scene.Character);
                Assert.Throws<NullReferenceException>(() => asset.Evaluate(scene.Character), "a recompute reads the group");
            });
        }

        // The cached group asset passes each definition's GracePeriod to IsAbilityInUse (call at
        // 0x5CCC60..) like the uncached condition. The ability was last used 0.5s before the
        // character's fixed time (10) and is not in use, so a grace of 1.0 holds and 0.05 does not.
        // The loop stops at the first ability in use, so a null entry after the match is never read.
        [Test]
        public void CachedAbilityGroupUsesEachGracePeriodAndStopsAtTheFirstAbilityInUse()
        {
            Run((owned, scene) =>
            {
                Set(scene.Character, "m_totalFixedTime", 10f);
                ActorAbilityType ability = ActorAbilityType.Boss_AttackArea;
                CharacterAbilityTypeGroup group = Group(owned, ability);
                var asset = Create<TriggerCondition_CachedAbilityGroupInUse>(owned);
                Set(asset, "m_abilityTypeGroup", group);
                scene.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, LastUsed(ability, 9.5f));

                Set(group.Abilities[0], "m_gracePeriod", 0.05f);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "outside the definition's grace period");
                Set(group.Abilities[0], "m_gracePeriod", 1.0f);
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True, "inside the definition's grace period");

                group.Abilities.Add(null);
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True, "the entry after the match is never read");
                group.Abilities.Insert(0, null);
                ResetCache(asset, scene.Character);
                Assert.Throws<NullReferenceException>(() => asset.Evaluate(scene.Character), "an entry before the match is read");
            });
        }

        // CachedCharacterFSMValue<bool>.OnEnable 0xC81920 rebuilds m_fsmVarName from m_valueName after
        // the base OnEnable; GetValue 0xC81960 caches the stored bool (+0x48) or GetValueDefault, which
        // is false (0x5CD410). DoEvaluate 0x5CD420 compares the cached value with m_valueThreshold
        // through the integer Evaluate on every call, so the comparison itself is never cached.
        [Test]
        public void CachedFsmBoolCachesTheStoredValueButNotTheComparison()
        {
            Run((owned, scene) =>
            {
                var asset = Create<TriggerCondition_CachedCharacterFSMValueBool>(owned);
                Assert.That(Get<ComparisonType>(asset, "m_comparisonType"), Is.EqualTo(ComparisonType.Equal));
                Set(asset, "m_valueName", "LucidCachedFlag");
                Call(asset, "OnEnable");
                var key = Get<GraphStorageKey>(asset, "m_fsmVarName");
                Assert.That(key, Is.EqualTo(new GraphStorageKey("LucidCachedFlag")));

                Set(asset, "m_valueThreshold", true);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "an absent value reads as the default false");
                scene.Storage.SetValue(key, true);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "the default is cached");
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True);
                Set(asset, "m_comparisonType", ComparisonType.NotEqual);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "the comparison is evaluated again over the cached value");
                scene.Storage.SetValue(key, false);
                Assert.That(asset.Evaluate(scene.Character), Is.False, "the stored change is not seen before a reset");
                ResetCache(asset, scene.Character);
                Assert.That(asset.Evaluate(scene.Character), Is.True, "a reset reads the changed value: false differs from the true threshold");
                Set(asset, "m_valueThreshold", false);
                Assert.That(asset.Evaluate(scene.Character), Is.False);
                Set(asset, "m_valueThreshold", true);
                Assert.That(asset.Evaluate(scene.Character), Is.True);
            });
        }
    }
}
