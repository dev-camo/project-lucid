using System;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;
using Spline = Hardlight.Spline;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime DreamteckSplineToHLSpline (TypeDef 0x02000a02, x86-64 0x6B08B0..0x6B0983). Components are
    // real, on owned inactive hosts that never wake; Awake and OnEnable are not used by this type.
    public sealed class OriginalDreamteckSplineToHLSplineTests
    {
        private static void Run(Action<Owned, DreamteckSplineToHLSpline> body) => Owned.Run(owned =>
            body(owned, owned.Host("Lucid owned dreamteck to HL spline", false).AddComponent<DreamteckSplineToHLSpline>()));

        private static SplineComputer[] Computers(Owned owned, int count) =>
            Enumerable.Range(0, count).Select(i => owned.Host("Lucid owned computer " + i, false).AddComponent<SplineComputer>()).ToArray();

        // 0x6b0970: the constructor stores base width 1.0f (0x3f800000 at +0x30) and calls MonoBehaviour..ctor.
        [Test]
        public void ConstructionDefaultsTheBaseRibbonWidthToOne()
        {
            Run((owned, converter) =>
            {
                Assert.That(converter.BaseRibbonWidth, Is.EqualTo(1f));
                Assert.That(converter.LastUsedBaseRibbonWidth, Is.EqualTo(0f));
                Assert.That(converter.LocalOffset, Is.EqualTo(Vector3.zero));
                Assert.That(converter.SplineComputers, Is.Null);
                Assert.That(Get<Object>(converter, "m_splineComponent") == null, Is.True);
                Assert.That(Get<Object>(converter, "m_ribbonComponent") == null, Is.True);
            });
        }

        // 0x6b08b0, 0x6b08c0, 0x6b08d0: the getters return the serialized fields at +0x40, +0x30 and +0x34.
        [Test]
        public void GettersReturnTheSerializedFields()
        {
            Run((owned, converter) =>
            {
                SplineComputer[] computers = Computers(owned, 2);
                Set(converter, "m_splineComputers", computers);
                Set(converter, "m_baseRibbonWidth", 2.5f);
                Set(converter, "m_localOffset", new Vector3(1f, 2f, 3f));
                Assert.That(converter.SplineComputers, Is.SameAs(computers));
                Assert.That(converter.BaseRibbonWidth, Is.EqualTo(2.5f));
                Assert.That(converter.LocalOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        // 0x6b08e0: stores the computers and the spline only; the width fields and the ribbon are untouched.
        [Test]
        public void InitialiseWithASplineStoresTheSplineAndComputersOnly()
        {
            Run((owned, converter) =>
            {
                Spline spline = owned.Host("Lucid owned spline", false).AddComponent<Spline>();
                SplineComputer[] computers = Computers(owned, 2);
                converter.Initialise(spline, computers);
                Assert.That(Get<Spline>(converter, "m_splineComponent"), Is.SameAs(spline));
                Assert.That(converter.SplineComputers, Is.SameAs(computers));
                Assert.That(Get<Object>(converter, "m_ribbonComponent") == null, Is.True);
                Assert.That((converter.BaseRibbonWidth, converter.LastUsedBaseRibbonWidth), Is.EqualTo((1f, 0f)));
            });
        }

        // 0x6b0920: stores the computers, the ribbon and the width, which is also the last used width.
        [Test]
        public void InitialiseWithARibbonStoresTheWidthAsTheLastUsedWidth()
        {
            Run((owned, converter) =>
            {
                Ribbon ribbon = owned.Host("Lucid owned ribbon", false).AddComponent<Ribbon>();
                SplineComputer[] computers = Computers(owned, 3);
                converter.Initialise(ribbon, computers, 0.25f);
                Assert.That(Get<Ribbon>(converter, "m_ribbonComponent"), Is.SameAs(ribbon));
                Assert.That(converter.SplineComputers, Is.SameAs(computers));
                Assert.That((converter.BaseRibbonWidth, converter.LastUsedBaseRibbonWidth), Is.EqualTo((0.25f, 0.25f)));
                Assert.That(Get<Object>(converter, "m_splineComponent") == null, Is.True);
            });
        }

        // The DummyDll attributes of the serialized fields: SerializeField on five, Min, Tooltip and Header
        // values, HideInInspector on the public last used width.
        [Test]
        public void SerializedFieldsKeepTheirOriginalAttributes()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = typeof(DreamteckSplineToHLSpline);
            FieldInfo[] fields = type.GetFields(all | BindingFlags.DeclaredOnly);
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[]
            {
                "m_splineComponent", "m_ribbonComponent", "m_baseRibbonWidth", "m_localOffset", "m_splineComputers", "LastUsedBaseRibbonWidth"
            }));
            Assert.That(fields.Take(5).All(f => f.IsDefined(typeof(SerializeField), false) && f.IsPrivate), Is.True);
            Assert.That(type.GetField("m_baseRibbonWidth", all).GetCustomAttribute<MinAttribute>().min, Is.EqualTo(0.01f));
            Assert.That(type.GetField("m_localOffset", all).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("Offset will be applied to each knot, relative to the dreamteck sample rotation."));
            Assert.That(type.GetField("m_splineComputers", all).GetCustomAttribute<HeaderAttribute>().header,
                Is.EqualTo("Splines must be in position order."));
            FieldInfo last = type.GetField("LastUsedBaseRibbonWidth", all);
            Assert.That(last.IsPublic && last.IsDefined(typeof(HideInInspector), false), Is.True);
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }
    }
}
