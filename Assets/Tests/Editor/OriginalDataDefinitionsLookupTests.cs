using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // DataDefinitions.Get 0x06001c29 (shared x86-64 0x8fd480) and GetGroup
    // 0x06001c2a (shared x86-64 0x8fd660) over owned in-memory groups built with
    // the genuine element constructor. Island keys are the Unity object names.
    public sealed class OriginalDataDefinitionsLookupTests
    {
        private static DefinitionDataType<string, GameplayIslandDefinition>.DefinitionElement<GameplayIslandDefinition> Element(
            GameplayIslandDefinition data) =>
            new DefinitionDataType<string, GameplayIslandDefinition>.DefinitionElement<GameplayIslandDefinition>(data);

        // 0x8fd4fa..0x8fd51d skips null slots and other closed groups; the first
        // GetData result is kept (0x8fd538) and later ones are merged through
        // CollectionExtensions.AddRange 0x8fd4c5. No match returns null 0x8fd555.
        // Catches a fresh result dictionary (empty instead of null), indexer
        // merging (no duplicate fault) and a last-group-wins rule.
        [Test]
        public void GetMergesLaterGroupsIntoFirstAndFaultsOnDuplicateKeys()
        {
            using (var owned = new Owned())
            {
                GameplayIslandDefinition first = owned.Island("Lucid island A");
                GameplayIslandDefinition second = owned.Island("Lucid island B");
                GameplayIslandDefinition duplicate = owned.Island("Lucid island A");
                GameplayIslandDefinitionGroup groupA = owned.Group(first);
                GameplayIslandDefinitionGroup groupB = owned.Group(second);
                GameplayIslandDefinitionGroup groupDuplicate = owned.Group(duplicate);
                // Same key type, different data type: never matched or read.
                UIModernEventDefinitionGroup other = owned.Add(ScriptableObject.CreateInstance<UIModernEventDefinitionGroup>());
                Assert.IsNull(other.m_elements);

                DataDefinitions definitions = owned.Add(ScriptableObject.CreateInstance<DataDefinitions>());
                definitions.DataDefinitionsArray = new DefinitionDataType[] { other, null };
                Assert.IsNull(definitions.Get<string, GameplayIslandDefinition>());

                definitions.DataDefinitionsArray = new DefinitionDataType[] { null, groupA, other, groupB };
                Dictionary<string, GameplayIslandDefinition> merged = definitions.Get<string, GameplayIslandDefinition>();
                CollectionAssert.AreEqual(new[] { "Lucid island A", "Lucid island B" }, merged.Keys.ToArray());
                Assert.AreSame(first, merged["Lucid island A"]);
                Assert.AreSame(second, merged["Lucid island B"]);

                definitions.DataDefinitionsArray = new DefinitionDataType[] { groupA, groupB, groupDuplicate };
                Assert.Throws<ArgumentException>(() => definitions.Get<string, GameplayIslandDefinition>());
                // Each call rebuilds from the groups; the groups are unchanged.
                Assert.AreSame(first, groupA.m_elements.Single().Data);
                Assert.AreSame(duplicate, groupDuplicate.m_elements.Single().Data);
            }
        }

        // Enum-key Get (shared x86-64 0x8fd390) keeps the first GetData result
        // object itself (0x8fd448 `mov r14, rax`) and AddRanges later results into
        // it (0x8fd3d5). GetData (x86-64 0xf301d0) passes the GetKeyComparer slot
        // result (0xf301ee) to the Dictionary constructor (0xf30234), and
        // FadeTransitionDefinitionGroup.GetKeyComparer 0x06001cce (x86-64 0x54cd90)
        // loads HardlightEnumComparers static +0x180, the 49th static field,
        // FadeTransitionTypeComparer. Island groups return a null comparer, so only
        // a group like this one catches a result copied into a fresh dictionary,
        // which would take the default comparer instead.
        [Test]
        public void GetKeepsTheFirstGroupDictionaryWithItsKeyComparer()
        {
            using (var owned = new Owned())
            {
                FadeTransitionDefinition black = owned.Fade(FadeTransitionType.Black);
                FadeTransitionDefinition white = owned.Fade(FadeTransitionType.White);
                DataDefinitions definitions = owned.Add(ScriptableObject.CreateInstance<DataDefinitions>());
                definitions.DataDefinitionsArray = new DefinitionDataType[] { owned.FadeGroup(black), owned.FadeGroup(white) };

                Dictionary<FadeTransitionType, FadeTransitionDefinition> merged =
                    definitions.Get<FadeTransitionType, FadeTransitionDefinition>();
                Assert.IsNotNull(HardlightEnumComparers.FadeTransitionTypeComparer);
                Assert.AreNotSame(EqualityComparer<FadeTransitionType>.Default, HardlightEnumComparers.FadeTransitionTypeComparer);
                Assert.AreSame(HardlightEnumComparers.FadeTransitionTypeComparer, merged.Comparer);
                CollectionAssert.AreEqual(new[] { FadeTransitionType.Black, FadeTransitionType.White }, merged.Keys.ToArray());
                Assert.AreSame(black, merged[FadeTransitionType.Black]);
                Assert.AreSame(white, merged[FadeTransitionType.White]);
            }
        }

        // Both Get bodies test only the running result for null (0x8fd43f/0x8fd52f
        // `test r14, r14`) and never the GetData count, so an empty first group's
        // dictionary is kept and returned even when nothing is merged into it.
        // Catches skipping empty matches (null when every match is empty) and a
        // fresh dictionary for an empty result (default comparer).
        [Test]
        public void GetKeepsAnEmptyFirstGroupDictionaryAndItsKeyComparer()
        {
            using (var owned = new Owned())
            {
                FadeTransitionDefinition black = owned.Fade(FadeTransitionType.Black);
                FadeTransitionDefinitionGroup empty = owned.FadeGroup();
                Assert.AreEqual(0, empty.m_elements.Length);
                DataDefinitions definitions = owned.Add(ScriptableObject.CreateInstance<DataDefinitions>());

                definitions.DataDefinitionsArray = new DefinitionDataType[] { null, empty };
                Dictionary<FadeTransitionType, FadeTransitionDefinition> onlyEmpty =
                    definitions.Get<FadeTransitionType, FadeTransitionDefinition>();
                Assert.IsNotNull(onlyEmpty);
                Assert.AreEqual(0, onlyEmpty.Count);
                Assert.AreSame(HardlightEnumComparers.FadeTransitionTypeComparer, onlyEmpty.Comparer);

                definitions.DataDefinitionsArray = new DefinitionDataType[] { empty, owned.FadeGroup(black), owned.FadeGroup() };
                Dictionary<FadeTransitionType, FadeTransitionDefinition> merged =
                    definitions.Get<FadeTransitionType, FadeTransitionDefinition>();
                Assert.AreSame(HardlightEnumComparers.FadeTransitionTypeComparer, merged.Comparer);
                CollectionAssert.AreEqual(new[] { FadeTransitionType.Black }, merged.Keys.ToArray());
                Assert.AreSame(black, merged[FadeTransitionType.Black]);

                // String-key body: an empty island group alone also yields an empty dictionary.
                definitions.DataDefinitionsArray = new DefinitionDataType[] { owned.Group() };
                Dictionary<string, GameplayIslandDefinition> islands = definitions.Get<string, GameplayIslandDefinition>();
                Assert.IsNotNull(islands);
                Assert.AreEqual(0, islands.Count);
            }
        }

        // 0x8fd6c3 IsInst on each slot returns the first CLR instance without a
        // Unity null test, so a destroyed group is still returned. Catches a Unity
        // equality guard (would return the later live group) and a last-match rule.
        [Test]
        public void GetGroupReturnsFirstClrMatchIncludingDestroyedGroup()
        {
            using (var owned = new Owned())
            {
                GameplayIslandDefinitionGroup destroyed = ScriptableObject.CreateInstance<GameplayIslandDefinitionGroup>();
                UnityEngine.Object.DestroyImmediate(destroyed);
                Assert.IsTrue(destroyed == null);
                Assert.IsFalse(ReferenceEquals(destroyed, null));
                GameplayIslandDefinitionGroup live = owned.Add(ScriptableObject.CreateInstance<GameplayIslandDefinitionGroup>());
                UIModernEventDefinitionGroup other = owned.Add(ScriptableObject.CreateInstance<UIModernEventDefinitionGroup>());

                DataDefinitions definitions = owned.Add(ScriptableObject.CreateInstance<DataDefinitions>());
                definitions.DataDefinitionsArray = new DefinitionDataType[] { null, other, destroyed, live };
                Assert.IsTrue(ReferenceEquals(destroyed, definitions.GetGroup<GameplayIslandDefinitionGroup>()));
                Assert.IsTrue(ReferenceEquals(other, definitions.GetGroup<UIModernEventDefinitionGroup>()));
                Assert.IsTrue(ReferenceEquals(other, definitions.GetGroup<DefinitionDataType>()));
                Assert.IsNull(definitions.GetGroup<CutsceneDefinitionGroup>());

                definitions.DataDefinitionsArray = new DefinitionDataType[0];
                Assert.IsNull(definitions.GetGroup<GameplayIslandDefinitionGroup>());
            }
        }

        private sealed class Owned : IDisposable
        {
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

            public T Add<T>(T value) where T : UnityEngine.Object
            {
                objects.Add(value);
                return value;
            }

            public GameplayIslandDefinition Island(string name)
            {
                GameplayIslandDefinition island = Add(ScriptableObject.CreateInstance<GameplayIslandDefinition>());
                island.name = name;
                return island;
            }

            public GameplayIslandDefinitionGroup Group(params GameplayIslandDefinition[] islands)
            {
                GameplayIslandDefinitionGroup group = Add(ScriptableObject.CreateInstance<GameplayIslandDefinitionGroup>());
                group.m_elements = islands.Select(Element).ToArray();
                return group;
            }

            // m_type is the serialized key field behind FadeTransitionDefinition.Type.
            public FadeTransitionDefinition Fade(FadeTransitionType type)
            {
                FadeTransitionDefinition fade = Add(ScriptableObject.CreateInstance<FadeTransitionDefinition>());
                typeof(FadeTransitionDefinition).GetField("m_type", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(fade, type);
                Assert.AreEqual(type, fade.Type);
                return fade;
            }

            public FadeTransitionDefinitionGroup FadeGroup(params FadeTransitionDefinition[] fades)
            {
                FadeTransitionDefinitionGroup group = Add(ScriptableObject.CreateInstance<FadeTransitionDefinitionGroup>());
                group.m_elements = fades
                    .Select(fade => new DefinitionDataType<FadeTransitionType, FadeTransitionDefinition>.DefinitionElement<FadeTransitionDefinition>(fade))
                    .ToArray();
                return group;
            }

            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
