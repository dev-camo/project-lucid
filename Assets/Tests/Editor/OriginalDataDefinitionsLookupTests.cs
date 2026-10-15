using System;
using System.Collections.Generic;
using System.Linq;
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

            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
