using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HardlightProject.PlatformComponent (TypeDef 0x02000774), PlatformFlexibleGroup (0x02000775),
    // PlatformGroup (0x02000776), PlatformPiece (0x02000777), PlatformVariant (0x02000778),
    // PlatformVariantInstance (0x02000779), PlatformVariantDefinition (0x02000592) and
    // PlatformVariantDefinitionGroup (0x02000595), against the 1.10.1 x86-64 bodies at
    // 0x554D30..0x554F08 and 0x5D4AC0..0x5D58A0. Pieces hold owned GameObjects; every copy a
    // piece constructor instantiates is tracked and destroyed with them.
    public sealed class OriginalPlatformTests
    {
        // PlatformPiece's only constructor copies another piece, so a hand-filled piece starts
        // uninitialised.
        private static PlatformPiece Piece(Owned owned, params string[] names)
        {
            var source = (PlatformPiece)FormatterServices.GetUninitializedObject(typeof(PlatformPiece));
            source.GameObjects = names.Select(name => owned.Host(name)).ToArray();
            return source;
        }

        private static void TrackInstances(Owned owned, IEnumerable<PlatformPiece> pieces)
        {
            foreach (PlatformPiece piece in pieces)
                if (piece != null)
                    foreach (GameObject gameObject in piece.GameObjects)
                        owned.Track(gameObject);
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            AssertMembers(typeof(PlatformPiece), 0x00102001, new[] { ("GameObjects", 0x0006) },
                new[] { (".ctor", 0x1886), ("SetActive", 0x0086), ("SetHeight", 0x0086), ("SetLocalPosition", 0x0086),
                        ("SetLocalRotation", 0x0086), ("SetLocalScale", 0x0086), ("SetComponentType", 0x0086), ("Destroy", 0x0086),
                        ("RegisterUndo", 0x0081) });
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformPiece).GetMethod("RegisterUndo", Declared)), Is.EqualTo("ConditionalAttribute(BUILD_DEVELOPMENT)"));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformPiece).GetField("GameObjects")),
                Is.EqualTo("TooltipAttribute(Links to all game objects, including LODs) SerializeField()"));

            AssertMembers(typeof(PlatformVariant), 0x00102001,
                new[] { ("m_topPiece", 0x0001), ("m_midPiece", 0x0001), ("m_midPieceInstances", 0x0001) },
                new[] { ("get_VariantInstance", 0x0886), ("SetHeight", 0x0086), ("RemoveUnusedMidPieceInstances", 0x0086), (".ctor", 0x1886) });
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformVariant).GetField("m_midPieceInstances", Declared)), Is.EqualTo("HideInInspector() SerializeField()"));

            AssertMembers(typeof(PlatformVariantInstance), 0x00102001,
                new[] { ("m_name", 0x0001), ("m_assetType", 0x0001), ("PlatformVariant", 0x0006) },
                new[] { ("get_AssetType", 0x0886), (".ctor", 0x1886), ("OnBeforeSerialize", 0x01e6), ("OnAfterDeserialize", 0x01e6) });
            Assert.That(typeof(PlatformVariantInstance).GetInterfaces(), Is.EqualTo(new[] { typeof(ISerializationCallbackReceiver) }));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformVariantInstance).GetField("m_name", Declared)), Is.EqualTo("SerializeField() HideInInspector()"));

            AssertMembers(typeof(PlatformComponent), 0x00100001,
                new[] { ("m_componentType", 0x0001), ("m_activeAssetType", 0x0001), ("m_height", 0x0001), ("m_variants", 0x0001),
                        ("m_basePieceHeight", 0x0001), ("m_pieceSize", 0x0001), ("m_cachedAssetType", 0x0001) },
                new[] { ("get_ActiveAssetType", 0x0886), ("set_ActiveAssetType", 0x0886), ("get_ComponentType", 0x0886),
                        ("set_ComponentType", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(PlatformComponent), 2, 1);
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformComponent).GetField("m_height", Declared)), Is.EqualTo("InspectorReadOnlyAttribute() SerializeField()"));

            AssertMembers(typeof(PlatformGroup), 0x00100001,
                new[] { ("m_platformAssetType", 0x0004), ("m_basePieceHeight", 0x0004), ("m_pieceSize", 0x0004),
                        ("m_platformComponents", 0x0004), ("m_height", 0x0004) },
                new[] { (".ctor", 0x1886) });
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformGroup)), Is.EqualTo(
                "SelectionBaseAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformGroup).GetField("m_platformComponents", Declared)),
                Is.EqualTo("SerializeField() InspectorReadOnlyAttribute() TooltipAttribute(List of platform components is auto-populated)"));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformGroup).GetField("m_height", Declared)), Is.EqualTo("RangeAttribute(0,25) SerializeField()"));

            AssertMembers(typeof(PlatformFlexibleGroup), 0x00100001,
                new[] { ("m_width", 0x0004), ("m_length", 0x0004), ("m_gridSize", 0x0004), ("m_startingPoint", 0x0004),
                        ("m_useSimpleCentrePiece", 0x0004), ("m_cornerBackLeft", 0x0001), ("m_cornerBackRight", 0x0001),
                        ("m_cornerFrontLeft", 0x0001), ("m_cornerFrontRight", 0x0001), ("m_edgePiece", 0x0001), ("m_edgePiece2", 0x0001),
                        ("m_edgePiece4", 0x0001), ("m_edgePiece6", 0x0001), ("m_centerPiece", 0x0001), ("m_constructedInstances", 0x0001) },
                new[] { (".ctor", 0x1886) });
            Assert.That(typeof(PlatformFlexibleGroup).BaseType, Is.EqualTo(typeof(PlatformGroup)));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformFlexibleGroup).GetField("m_width", Declared)), Is.EqualTo("SerializeField() RangeAttribute(2,25)"));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformFlexibleGroup).GetField("m_length", Declared)), Is.EqualTo("RangeAttribute(2,25) SerializeField()"));

            AssertMembers(typeof(PlatformVariantDefinition), 0x00100001,
                new[] { ("m_assetType", 0x0001), ("m_components", 0x0001) },
                new[] { ("get_AssetType", 0x0886), ("Get", 0x0086), (".ctor", 0x1886) });
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformVariantDefinition)), Is.EqualTo(
                "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False) CreateAssetMenuAttribute()"));
            Type definition = typeof(PlatformVariantDefinition.PlatformComponentDefinition);
            AssertMembers(definition, 0x00102002, new[] { ("Type", 0x0006), ("PrefabSource", 0x0006) }, new[] { (".ctor", 0x1886) });
            Type closure = typeof(PlatformVariantDefinition).GetNestedType("<>c__DisplayClass4_0", Declared);
            Assert.That(closure, Is.Not.Null, "Get is member ordinal 4, after the property and its getter");
            AssertMembers(closure, 0x00100103, new[] { ("componentType", 0x0006) }, new[] { (".ctor", 0x1886), ("<Get>b__0", 0x0083) });

            AssertMembers(typeof(PlatformVariantDefinitionGroup), 0x00100001, new (string, int)[0],
                new[] { ("GetElementKey", 0x00c4), ("GetKeyComparer", 0x00c4), (".ctor", 0x1886) });
            Assert.That(typeof(PlatformVariantDefinitionGroup).BaseType, Is.EqualTo(typeof(DefinitionDataType<PlatformAssetTypes, PlatformVariantDefinition>)));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformVariantDefinitionGroup)), Is.EqualTo(
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)"));
            foreach (var (type, fileName, menuName) in new[]
            {
                (typeof(PlatformVariantDefinition), "PlatformVariantDefinition", "HardlightProject/DefinitionData/Definitions/PlatformVariantDefinition"),
                (typeof(PlatformVariantDefinitionGroup), "PlatformVariantDefinitionGroup", "HardlightProject/DefinitionData/Groups/PlatformVariantDefinitionGroup"),
            })
            {
                var menu = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
                Assert.That((menu.fileName, menu.menuName), Is.EqualTo((fileName, menuName)), type.Name);
            }
        }

        // PlatformComponent .ctor 0x5D4B00 writes 0x1640e7ae8 over +0x24/+0x28 (Debug, height 1)
        // and a new list. PlatformGroup .ctor 0x5D4C80 writes the pair at 0x29a7a40 (-9f, 6f) to
        // +0x24 and 1 to +0x38. PlatformFlexibleGroup .ctor 0x5D4B90 writes 2, 2 and 6 from +0x3C,
        // Vector3.zero, true and a new list, then the base values. PlatformVariant .ctor 0x5D5750
        // allocates the instance list.
        [Test]
        public void ConstructorsStoreTheNativeDefaults()
        {
            Owned.Run(owned =>
            {
                var component = owned.Host("Lucid platform component").AddComponent<PlatformComponent>();
                Assert.That(component.ActiveAssetType, Is.EqualTo(PlatformAssetTypes.Debug));
                Assert.That((int)component.ActiveAssetType, Is.EqualTo(0x640e7ae8));
                Assert.That(Get<int>(component, "m_height"), Is.EqualTo(1));
                Assert.That(Get<List<PlatformVariantInstance>>(component, "m_variants"), Is.Empty);
                Assert.That((int)component.ComponentType, Is.Zero);
                Assert.That((int)Get<PlatformAssetTypes>(component, "m_cachedAssetType"), Is.Zero);
                Assert.That(Get<float>(component, "m_basePieceHeight"), Is.Zero);

                var group = owned.Host("Lucid platform group").AddComponent<PlatformGroup>();
                Assert.That((Get<float>(group, "m_basePieceHeight"), Get<float>(group, "m_pieceSize"), Get<int>(group, "m_height")), Is.EqualTo((-9f, 6f, 1)));
                Assert.That((int)Get<PlatformAssetTypes>(group, "m_platformAssetType"), Is.Zero);

                var flexible = owned.Host("Lucid flexible platform group").AddComponent<PlatformFlexibleGroup>();
                Assert.That((Get<int>(flexible, "m_width"), Get<int>(flexible, "m_length"), Get<int>(flexible, "m_gridSize")), Is.EqualTo((2, 2, 6)));
                Assert.That(Get<Vector3>(flexible, "m_startingPoint"), Is.EqualTo(Vector3.zero));
                Assert.That(Get<bool>(flexible, "m_useSimpleCentrePiece"), Is.True);
                Assert.That(Get<List<PlatformPiece>>(flexible, "m_constructedInstances"), Is.Empty);
                Assert.That((Get<float>(flexible, "m_basePieceHeight"), Get<float>(flexible, "m_pieceSize"), Get<int>(flexible, "m_height")), Is.EqualTo((-9f, 6f, 1)));

                var variant = owned.Host("Lucid platform variant").AddComponent<PlatformVariant>();
                Assert.That(Get<List<PlatformPiece>>(variant, "m_midPieceInstances"), Is.Empty);
                Assert.That(variant.VariantInstance, Is.SameAs(variant.gameObject), "get_VariantInstance 0x5D5270 tail-calls get_gameObject");
            });
        }

        // The setters 0x5D4AD0/0x5D4AF0 store +0x24 and +0x20; the getters read them back.
        [Test]
        public void ComponentAccessorsStoreTheirOwnFields()
        {
            Owned.Run(owned =>
            {
                var component = owned.Host("Lucid platform component").AddComponent<PlatformComponent>();
                component.ActiveAssetType = PlatformAssetTypes.Stone;
                component.ComponentType = PlatformComponentTypes.Edge4;
                Assert.That(Get<PlatformAssetTypes>(component, "m_activeAssetType"), Is.EqualTo(PlatformAssetTypes.Stone));
                Assert.That(Get<PlatformComponentTypes>(component, "m_componentType"), Is.EqualTo(PlatformComponentTypes.Edge4));
                Assert.That(component.ActiveAssetType, Is.EqualTo(PlatformAssetTypes.Stone));
                Assert.That(component.ComponentType, Is.EqualTo(PlatformComponentTypes.Edge4));
            });
        }

        // .ctor 0x5D4CA0 sizes a new array from the source and fills it with
        // Object.Instantiate(source[i], source[i].transform.parent, true) (edx 1 at 0x5D4D50).
        [Test]
        public void CopyConstructorInstantiatesEachObjectUnderTheSameParent()
        {
            Owned.Run(owned =>
            {
                Transform parent = owned.Host("Lucid piece parent").transform;
                parent.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                PlatformPiece source = Piece(owned, "Lucid piece lod0", "Lucid piece lod1");
                foreach (GameObject gameObject in source.GameObjects)
                {
                    gameObject.transform.SetParent(parent, false);
                    gameObject.transform.localPosition = new Vector3(1f, 2f, 3f);
                }
                source.GameObjects[1].transform.SetParent(null, true);

                var copy = new PlatformPiece(source);
                TrackInstances(owned, new[] { copy });
                Assert.That(copy.GameObjects, Has.Length.EqualTo(2));
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(copy.GameObjects[i], Is.Not.SameAs(source.GameObjects[i]));
                    Assert.That(copy.GameObjects[i].name, Is.EqualTo(source.GameObjects[i].name + "(Clone)"));
                    Assert.That(copy.GameObjects[i].transform.parent, Is.SameAs(source.GameObjects[i].transform.parent));
                    WorldMemberAccess.AreClose(source.GameObjects[i].transform.position, copy.GameObjects[i].transform.position, "world position " + i);
                }
                Assert.That(copy.GameObjects[1].transform.parent, Is.Null);
            });
        }

        // Each setter 0x5D4DD0..0x5D5190 walks GameObjects by index, skips entries that compare equal
        // to null (op_Equality), and applies to the rest. SetHeight replaces only the local y
        // (insertps lane 1 at 0x5D4F24). SetComponentType 0x5D5190 only calls
        // GetComponentsInChildren<PlatformComponent>() and discards the result, so no component
        // type changes. Destroy 0x5D5250 is a bare RET and leaves every object alive.
        [Test]
        public void PieceSettersSkipDestroyedObjectsAndSetHeightKeepsXAndZ()
        {
            Owned.Run(owned =>
            {
                PlatformPiece piece = Piece(owned, "Lucid piece a", "Lucid piece destroyed", "Lucid piece b");
                Object.DestroyImmediate(piece.GameObjects[1]);
                Transform a = piece.GameObjects[0].transform, b = piece.GameObjects[2].transform;
                a.localPosition = new Vector3(1f, 2f, 3f);
                b.localPosition = new Vector3(-4f, 5f, -6f);

                piece.SetHeight(7.5f);
                Assert.That(a.localPosition, Is.EqualTo(new Vector3(1f, 7.5f, 3f)));
                Assert.That(b.localPosition, Is.EqualTo(new Vector3(-4f, 7.5f, -6f)));
                piece.SetActive(false);
                Assert.That(piece.GameObjects[0].activeSelf || piece.GameObjects[2].activeSelf, Is.False);
                piece.SetActive(true);
                Assert.That(piece.GameObjects[0].activeSelf && piece.GameObjects[2].activeSelf, Is.True);
                piece.SetLocalPosition(new Vector3(9f, 8f, 7f));
                Assert.That((a.localPosition, b.localPosition), Is.EqualTo((new Vector3(9f, 8f, 7f), new Vector3(9f, 8f, 7f))));
                Quaternion rotation = Quaternion.Euler(10f, 20f, 30f);
                piece.SetLocalRotation(rotation);
                Assert.That(Quaternion.Angle(a.localRotation, rotation) < 1e-3f && Quaternion.Angle(b.localRotation, rotation) < 1e-3f, Is.True);
                piece.SetLocalScale(new Vector3(2f, 3f, 4f));
                Assert.That((a.localScale, b.localScale), Is.EqualTo((new Vector3(2f, 3f, 4f), new Vector3(2f, 3f, 4f))));

                var child = owned.Host("Lucid piece component", true, a).AddComponent<PlatformComponent>();
                child.ComponentType = PlatformComponentTypes.Corner;
                piece.SetComponentType(PlatformComponentTypes.Center);
                Assert.That(child.ComponentType, Is.EqualTo(PlatformComponentTypes.Corner));

                piece.Destroy();
                Assert.That(piece.GameObjects[0] != null && piece.GameObjects[2] != null, Is.True);
            });
        }

        // SetHeight 0x5D5280 with basePieceHeight b and pieceSize s: the top piece's local y is
        // (b + 0.5f * s) + height * s (constant 0.5 at 0x29a6c08); the mid piece is active iff
        // height > 0 (setg at 0x5D539D); an empty mid piece returns (0x5D53C4). For i in
        // 2..height (index i - 2) a missing list slot is appended as null, a null entry becomes
        // new PlatformPiece(m_midPiece), and the entry is activated, raised to (b + 2s) + index * s and
        // stored back. Then every entry from Count - 1 down to max(height - 1, 0) is hidden.
        [Test]
        public void VariantStacksMidInstancesAndHidesThoseAboveTheHeight()
        {
            Owned.Run(owned =>
            {
                var variant = owned.Host("Lucid platform variant").AddComponent<PlatformVariant>();
                PlatformPiece top = Piece(owned, "Lucid variant top");
                PlatformPiece mid = Piece(owned, "Lucid variant mid");
                top.GameObjects[0].transform.localPosition = new Vector3(1f, 0f, 2f);
                mid.GameObjects[0].transform.localPosition = new Vector3(3f, 100f, 4f);
                Set(variant, "m_topPiece", top);
                Set(variant, "m_midPiece", mid);
                List<PlatformPiece> instances = Get<List<PlatformPiece>>(variant, "m_midPieceInstances");
                const float b = -9f, s = 6f;

                variant.SetHeight(3, b, s);
                TrackInstances(owned, instances);
                Assert.That(top.GameObjects[0].transform.localPosition, Is.EqualTo(new Vector3(1f, 12f, 2f)), "-9 + 3 + 3 * 6");
                Assert.That(mid.GameObjects[0].activeSelf, Is.True);
                Assert.That(mid.GameObjects[0].transform.localPosition.y, Is.EqualTo(100f), "the mid piece itself keeps its height");
                Assert.That(instances, Has.Count.EqualTo(2));
                Assert.That(instances.Select(instance => instance.GameObjects[0].transform.localPosition),
                    Is.EqualTo(new[] { new Vector3(3f, 3f, 4f), new Vector3(3f, 9f, 4f) }), "-9 + 12 + i * 6");
                Assert.That(instances.All(instance => instance.GameObjects[0].activeSelf), Is.True);
                GameObject first = instances[0].GameObjects[0], second = instances[1].GameObjects[0];

                variant.SetHeight(2, b, s);
                Assert.That(top.GameObjects[0].transform.localPosition.y, Is.EqualTo(6f));
                Assert.That(instances[0].GameObjects[0], Is.SameAs(first), "an existing instance is reused");
                Assert.That((first.activeSelf, second.activeSelf), Is.EqualTo((true, false)), "index 1 is at height - 1");

                variant.SetHeight(1, b, s);
                Assert.That(top.GameObjects[0].transform.localPosition.y, Is.EqualTo(0f));
                Assert.That((mid.GameObjects[0].activeSelf, first.activeSelf, second.activeSelf), Is.EqualTo((true, false, false)));

                variant.SetHeight(0, b, s);
                Assert.That(top.GameObjects[0].transform.localPosition.y, Is.EqualTo(-6f));
                Assert.That((mid.GameObjects[0].activeSelf, first.activeSelf, second.activeSelf), Is.EqualTo((false, false, false)));
                Assert.That(instances, Has.Count.EqualTo(2), "hidden, not removed");

                instances[1] = null;
                variant.SetHeight(3, b, s);
                TrackInstances(owned, instances);
                Assert.That(instances[1], Is.Not.Null, "a null slot is rebuilt from the mid piece");
                Assert.That(instances[1].GameObjects[0], Is.Not.SameAs(second));
                Assert.That(instances[1].GameObjects[0].transform.localPosition, Is.EqualTo(new Vector3(3f, 9f, 4f)));
                Assert.That(second.activeSelf, Is.False, "the dropped instance is left as it was");

                // The stacking counter runs from 2 while it is <= height (cmp/jg at 0x5D552E behind
                // the height >= 2 guard at 0x5D53CA), so no height - 2 is formed: int.MinValue and
                // int.MinValue + 1 stack nothing. The hiding walk compares with the wrapped
                // height - 1 (0x5D53E3), which is int.MaxValue for int.MinValue (nothing hidden)
                // and int.MinValue for int.MinValue + 1 (everything hidden).
                PlatformPiece[] stacked = instances.ToArray();
                variant.SetHeight(int.MinValue, b, s);
                Assert.That(instances, Is.EqualTo(stacked), "int.MinValue appends nothing");
                Assert.That((mid.GameObjects[0].activeSelf, stacked[0].GameObjects[0].activeSelf, stacked[1].GameObjects[0].activeSelf),
                    Is.EqualTo((false, true, true)), "int.MinValue hides nothing");
                variant.SetHeight(int.MinValue + 1, b, s);
                Assert.That(instances, Is.EqualTo(stacked), "int.MinValue + 1 appends nothing");
                Assert.That((stacked[0].GameObjects[0].activeSelf, stacked[1].GameObjects[0].activeSelf),
                    Is.EqualTo((false, false)), "int.MinValue + 1 hides every instance");
                variant.SetHeight(3, b, s);
                Assert.That(instances, Is.EqualTo(stacked));

                // With no mid objects, SetHeight returns before stacking or hiding anything.
                mid.GameObjects = new GameObject[0];
                variant.SetHeight(0, b, s);
                Assert.That(instances.All(instance => instance.GameObjects[0].activeSelf), Is.True);
                Assert.That(top.GameObjects[0].transform.localPosition.y, Is.EqualTo(-6f));
            });
        }

        // RemoveUnusedMidPieceInstances 0x5D5690 walks from Count - 1 down while i >= height - 1 and
        // i >= 0, reading each entry (PlatformPiece.Destroy is empty) before RemoveAt(i). The
        // removed pieces' objects stay alive.
        [Test]
        public void RemoveUnusedMidPieceInstancesDropsTheEntriesFromHeightMinusOne()
        {
            Owned.Run(owned =>
            {
                var variant = owned.Host("Lucid platform variant").AddComponent<PlatformVariant>();
                List<PlatformPiece> instances = Get<List<PlatformPiece>>(variant, "m_midPieceInstances");
                PlatformPiece[] pieces = Enumerable.Range(0, 4).Select(i => Piece(owned, "Lucid mid instance " + i)).ToArray();
                instances.AddRange(pieces);

                variant.RemoveUnusedMidPieceInstances(3);
                Assert.That(instances, Is.EqualTo(pieces.Take(2)));
                variant.RemoveUnusedMidPieceInstances(5);
                Assert.That(instances, Is.EqualTo(pieces.Take(2)), "nothing at or above index 4");
                variant.RemoveUnusedMidPieceInstances(-7);
                Assert.That(instances, Is.Empty, "the walk stops at index 0");
                Assert.That(pieces.All(piece => piece.GameObjects[0] != null), Is.True);
            });
        }

        // .ctor 0x5D57E0 stores both arguments; OnBeforeSerialize 0x5D5830 names the instance with
        // HardlightEnumExtensions.GetString(m_assetType); OnAfterDeserialize 0x5D58A0 is a bare RET.
        [Test]
        public void VariantInstanceStoresItsArgumentsAndIsNamedAfterItsAssetType()
        {
            Owned.Run(owned =>
            {
                var variant = owned.Host("Lucid platform variant").AddComponent<PlatformVariant>();
                var instance = new PlatformVariantInstance(PlatformAssetTypes.Grass, variant);
                Assert.That(instance.AssetType, Is.EqualTo(PlatformAssetTypes.Grass));
                Assert.That(instance.PlatformVariant, Is.SameAs(variant));
                Assert.That(Get<string>(instance, "m_name"), Is.Null);
                instance.OnBeforeSerialize();
                Assert.That(Get<string>(instance, "m_name"), Is.EqualTo("Grass"));
                Set(instance, "m_name", "kept");
                instance.OnAfterDeserialize();
                Assert.That(Get<string>(instance, "m_name"), Is.EqualTo("kept"));
            });
        }

        // Get 0x554D40 runs Array.Find with the closure's Type comparison (0x554E40, which raises a
        // null reference on a null entry) and returns the match's PrefabSource, or null without a
        // match. GetElementKey 0x554E60 reads the definition's +0x18 asset type and GetKeyComparer
        // 0x554E70 returns HardlightEnumComparers.PlatformAssetTypesComparer (+0x298).
        [Test]
        public void DefinitionReturnsTheFirstMatchingPrefabAndTheGroupKeysByAssetType()
        {
            Owned.Run(owned =>
            {
                var first = owned.Host("Lucid variant first").AddComponent<PlatformVariant>();
                var second = owned.Host("Lucid variant second").AddComponent<PlatformVariant>();
                var definition = owned.Track(ScriptableObject.CreateInstance<PlatformVariantDefinition>());
                Set(definition, "m_assetType", PlatformAssetTypes.Zone4);
                PlatformVariantDefinition.PlatformComponentDefinition Entry(PlatformComponentTypes type, PlatformVariant prefab) =>
                    new PlatformVariantDefinition.PlatformComponentDefinition { Type = type, PrefabSource = prefab };
                Set(definition, "m_components", new[] { Entry(PlatformComponentTypes.Edge, first), Entry(PlatformComponentTypes.Corner, null), Entry(PlatformComponentTypes.Edge, second) });

                Assert.That(definition.AssetType, Is.EqualTo(PlatformAssetTypes.Zone4));
                Assert.That(definition.Get(PlatformComponentTypes.Edge), Is.SameAs(first));
                Assert.That(definition.Get(PlatformComponentTypes.Corner), Is.Null);
                Assert.That(definition.Get(PlatformComponentTypes.Center), Is.Null);
                Set(definition, "m_components", new[] { Entry(PlatformComponentTypes.Corner, first), null, Entry(PlatformComponentTypes.Edge, second) });
                Assert.That(definition.Get(PlatformComponentTypes.Corner), Is.SameAs(first), "the null entry is never reached");
                Assert.Throws<NullReferenceException>(() => definition.Get(PlatformComponentTypes.Edge));

                var group = owned.Track(ScriptableObject.CreateInstance<PlatformVariantDefinitionGroup>());
                Assert.That(Call(group, "GetElementKey", definition), Is.EqualTo(PlatformAssetTypes.Zone4));
                Assert.That(Call(group, "GetKeyComparer"), Is.SameAs(HardlightEnumComparers.PlatformAssetTypesComparer));
                Assert.That(group.m_elements, Is.Null, "the constructor 0x554ED0 only runs the base");
            });
        }
    }
}
