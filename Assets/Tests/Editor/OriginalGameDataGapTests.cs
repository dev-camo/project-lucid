using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Managed data and declaration cases; no scene, registry or lifecycle state is touched.
    public sealed class OriginalGameDataGapTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // UniqueCollectableStateData.CollectId (0x06003b38, x86-64 0x6bdf30) increments the
        // count with a plain `inc dword ptr [r14+0x10]` (0x6bdfb4), so Int32.MaxValue wraps.
        // Fails under a checked increment (OverflowException) or a saturating clamp.
        [Test]
        public void FirstCollectionWrapsTheCountAtInt32MaxValue()
        {
            var data = new UniqueCollectableStateData();
            data.TryAddId("owned", out bool registered);
            Assert.That(registered, Is.False);
            typeof(UniqueCollectableStateData).GetProperty("Collected").GetSetMethod(true).Invoke(data, new object[] { int.MaxValue });
            data.CollectId("owned", out bool collected);
            Assert.That(collected, Is.True);
            Assert.That(data.Collected, Is.EqualTo(int.MinValue));
            data.CollectId("owned", out collected);
            Assert.That(collected, Is.False, "a repeated collection neither reports nor counts");
            Assert.That(data.Collected, Is.EqualTo(int.MinValue));
        }

        // SaveDataDreamPowerStore.IsNewState setter (0x06002bdc, x86-64 0x5d83a0) stores the
        // field and tail-jumps SaveDataItem.MarkDirty with no comparison, so even an
        // unchanged value dirties a saved store. Fails if MarkDirty is dropped or guarded by
        // an equality check.
        [Test]
        public void IsNewStateSetterMarksTheStoreDirtyEvenForAnUnchangedValue()
        {
            var store = new SaveDataDreamPowerStore();
            store.MarkSaved();
            Assert.That(store.HasChangesToSave(), Is.False);
            store.IsNewState = DreamPowerStoreIsNewState.None;
            Assert.That(store.IsNewState, Is.EqualTo(DreamPowerStoreIsNewState.None));
            Assert.That(store.HasChangesToSave(), Is.True, "unchanged value still marks dirty");
            store.MarkSaved();
            store.IsNewState = DreamPowerStoreIsNewState.Seen;
            Assert.That(store.IsNewState, Is.EqualTo(DreamPowerStoreIsNewState.Seen));
            Assert.That(store.HasChangesToSave(), Is.True);
        }

        // Raw Int32 literals as written by HardlightEnumExtensions..cctor (x86-64 0x19e7540):
        // 'Consented' 0xeca8303d, 'Rejected' 0xbb963fb6, 'Underage' 0x3e270050 (0x19e7699..
        // 0x19e76cd) and 'Boss' 0xf7947471, 'Challenge' 0x6c346841, 'Persistent' 0x537c9e1f,
        // 'Regular' 0x8acb0231 (0x19e774b..0x19e7799). A renumbered enum fails both the raw
        // comparison and the label lookup of the original raw value.
        [Test]
        public void AnalyticsEnumsKeepTheOriginalHashedLiteralsAndLabels()
        {
            var consent = new[]
            {
                (AnalyticsConsentState.None, 0, "None"),
                (AnalyticsConsentState.Consented, -324521923, "Consented"),
                (AnalyticsConsentState.Rejected, -1147781194, "Rejected"),
                (AnalyticsConsentState.Underage, 1042743376, "Underage"),
            };
            Assert.That(Enum.GetValues(typeof(AnalyticsConsentState)).Length, Is.EqualTo(consent.Length));
            foreach (var (value, raw, label) in consent)
            {
                Assert.That((int)value, Is.EqualTo(raw), label);
                Assert.That(((AnalyticsConsentState)raw).GetString(), Is.EqualTo(label));
            }

            var mission = new[]
            {
                (AnalyticsMissionType.Boss, -141265807, "Boss"),
                (AnalyticsMissionType.Challenge, 1815373889, "Challenge"),
                (AnalyticsMissionType.Persistent, 1400675871, "Persistent"),
                (AnalyticsMissionType.Regular, -1966407119, "Regular"),
            };
            Assert.That(Enum.GetValues(typeof(AnalyticsMissionType)).Length, Is.EqualTo(mission.Length));
            foreach (var (value, raw, label) in mission)
            {
                Assert.That((int)value, Is.EqualTo(raw), label);
                Assert.That(((AnalyticsMissionType)raw).GetString(), Is.EqualTo(label));
            }
        }

        // Game.Runtime metadata attribute spans for LedgeBrakeParameters fields
        // 0x04001194..0x0400119b: the first six are Tooltip then SerializeField, while
        // m_raycastDetectDistance (0x0400119a) and m_raycastDownDistance (0x0400119b) are
        // SerializeField then Tooltip. Fails if the two flipped fields are normalised.
        [Test]
        public void LedgeBrakeFieldsKeepTheOriginalPerFieldAttributeOrder()
        {
            Type[] tooltipFirst = { typeof(TooltipAttribute), typeof(SerializeField) };
            Type[] serializeFirst = { typeof(SerializeField), typeof(TooltipAttribute) };
            var expected = new (string, Type[])[]
            {
                ("m_maximumAngleToPlane", tooltipFirst),
                ("m_maximumVelocityThreshold", tooltipFirst),
                ("m_intentAngleBelowVelocity", tooltipFirst),
                ("m_intentAngleAboveVelocity", tooltipFirst),
                ("m_ledgeMinAngleLimit", tooltipFirst),
                ("m_raycastStopDistance", tooltipFirst),
                ("m_raycastDetectDistance", serializeFirst),
                ("m_raycastDownDistance", serializeFirst),
            };
            foreach (var (name, types) in expected)
            {
                FieldInfo field = typeof(LedgeBrakeParameters).GetField(name, Declared);
                Assert.That(field, Is.Not.Null, name);
                Assert.That(field.GetCustomAttributesData().Select(attribute => attribute.AttributeType), Is.EqualTo(types), name);
            }
        }

        // MeshGenerationUtilities.CreateMesh (0x060039ba, x86-64 0x6b2930) calls Mesh..ctor,
        // set_vertices, set_triangles, RecalculateNormals, RecalculateBounds, Optimize in that
        // order (0x6b2973..0x6b29ab). CreateMeshPyramid's index initializer is metadata field
        // 0x04002d8b, named by the SHA-256 of its 72 bytes. Fails if Optimize moves before
        // the recalculations, or the pre-Optimize index order changes.
        [Test]
        public void PyramidMeshUsesTheOriginalCallOrderAndIndexInitializer()
        {
            MethodInfo create = typeof(MeshGenerationUtilities).GetMethod("CreateMesh", Declared);
            Assert.That(create, Is.Not.Null);
            string[] calls = OriginalMethodBodyReader.Instructions(create).Select(instruction => instruction.Value).OfType<MethodBase>()
                .Where(method => method.DeclaringType == typeof(Mesh)).Select(method => method.Name).ToArray();
            Assert.That(calls, Is.EqualTo(new[] { ".ctor", "set_vertices", "set_triangles", "RecalculateNormals", "RecalculateBounds", "Optimize" }));

            MethodInfo pyramid = typeof(MeshGenerationUtilities).GetMethod("CreateMeshPyramid", Declared);
            FieldInfo[] initializers = OriginalMethodBodyReader.Instructions(pyramid).Select(instruction => instruction.Value).OfType<FieldInfo>().ToArray();
            Assert.That(initializers.Length, Is.EqualTo(1), "one static index initializer");
            int[] indices = new int[18];
            RuntimeHelpers.InitializeArray(indices, initializers[0].FieldHandle);
            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4, 2, 1, 0, 3, 2, 0 }));
            byte[] bytes = new byte[indices.Length * sizeof(int)];
            Buffer.BlockCopy(indices, 0, bytes, 0, bytes.Length);
            Assert.That(BitConverter.IsLittleEndian, Is.True);
            using (SHA256 sha = SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty),
                    Is.EqualTo("124DBEEBD32D0CE760E293BF46E477BAE35E7F666457E80A8B7F77AEDBC3221F"));
        }
    }
}
