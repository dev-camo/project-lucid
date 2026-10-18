using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CrossPromoUiAccess;

namespace ProjectLucid.Tests
{
    // Hardlight.CrossPromoFullScreenPlacement (TypeDef 0x0200001c, x86-64 0x1A15EA0..0x1A16670) on
    // an inactive owned placement whose view child is never shown. The CrossPromo system is the
    // real class over an in-memory resource manager; lifecycle cases own the CrossPromo registry
    // row and release it. Not covered: Update, whose Escape test reads live keyboard input.
    public sealed class OriginalCrossPromoFullScreenPlacementTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly CrossPromoUiObjects Objects = new CrossPromoUiObjects("lucid-crosspromo-placement");
            public readonly CrossPromoFullScreenPlacement Placement;
            public readonly GameObject ViewObject;

            public Fixture()
            {
                Placement = Objects.Child("placement", null, typeof(CrossPromoFullScreenPlacement)).GetComponent<CrossPromoFullScreenPlacement>();
                ViewObject = Objects.Child("view", Placement.transform, typeof(CrossPromoFullScreenView));
                Set(Placement, "m_fullScreenView", ViewObject.GetComponent<CrossPromoFullScreenView>());
            }

            public void Invoke(string method, params object[] args) => Call(typeof(CrossPromoFullScreenPlacement), Placement, method, args);

            public void Dispose() => Objects.Dispose();
        }

        private static string RowName => ProcessManager.GetDefaultName<CrossPromo>();

        private static Delegate[] Startup(SystemRef<CrossPromo> reference) => Handlers(reference, "OnSystemStartup");

        // dump.cs TypeDefIndex 13731 and the DummyDll field rows.
        [Test]
        public void DeclarationsKeepOriginalFields()
        {
            Type type = typeof(CrossPromoFullScreenPlacement);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(fields.Select(f => f.Name + ":" + f.FieldType.Name), Is.EqualTo(new[]
            {
                "m_fullScreenView:CrossPromoFullScreenView", "m_crossPromoRef:SystemRef`1", "m_crossPromo:CrossPromo"
            }));
            Assert.That(fields[1].FieldType, Is.EqualTo(typeof(SystemRef<CrossPromo>)));
            // Attribute rows of 0x0400007a in DummyDll metadata order; the other fields have none.
            Assert.That(AttributeRows(fields[0]), Is.EqualTo(new[] { "TooltipAttribute(The Full Screen View visual component)", "SerializeField()" }));
            Assert.That(AttributeRows(fields[1]).Concat(AttributeRows(fields[2])), Is.Empty);
        }

        // Awake 0x1A15EA0 before the system exists: it keeps the typed CrossPromo reference and
        // waits on OnSystemStartup. Registration runs InitialiseCrossPromo 0x1A15FC0, which leaves
        // OnSystemStartup, keeps the system and subscribes Show 0x1A16140 to
        // FullScreenViewRequested; OnDestroy 0x1A164F0 then removes Show.
        [Test]
        public void AwakeWaitsForTheSystemThenSubscribesShowUntilDestroyed()
        {
            using (var fixture = new Fixture())
            using (var row = new VerificationFixtureOwnership.RegistryRow(RowName))
            {
                row.Register(null);
                fixture.Invoke("Awake");
                var reference = (SystemRef<CrossPromo>)Get(fixture.Placement, "m_crossPromoRef");
                Assert.That(reference, Is.SameAs(ProcessManager.GetSystemRef<CrossPromo>()));
                Assert.That(Get(fixture.Placement, "m_crossPromo"), Is.Null);
                Delegate waiting = Startup(reference).Single();
                Assert.That(waiting.Target, Is.SameAs(fixture.Placement));
                Assert.That(waiting.Method.Name, Is.EqualTo("InitialiseCrossPromo"));

                var resources = new RecordingResourceManager();
                CrossPromo promo = NewCrossPromo(resources);
                row.Register(promo);
                Assert.That(Get(fixture.Placement, "m_crossPromo"), Is.SameAs(promo));
                Assert.That(Startup(reference), Is.Empty);
                Delegate show = Handlers(promo, "FullScreenViewRequested").Single();
                Assert.That(show.Target, Is.SameAs(fixture.Placement));
                Assert.That(show.Method.Name, Is.EqualTo("Show"));

                fixture.Invoke("OnDestroy");
                Assert.That(Handlers(promo, "FullScreenViewRequested"), Is.Empty);
                Assert.That(Startup(reference), Is.Empty);
            }
        }

        // Awake 0x1A15EA0 with the system already registered initialises at once and never waits
        // on OnSystemStartup.
        [Test]
        public void AwakeWithARegisteredSystemInitialisesImmediately()
        {
            using (var fixture = new Fixture())
            using (var row = new VerificationFixtureOwnership.RegistryRow(RowName))
            {
                CrossPromo promo = NewCrossPromo(new RecordingResourceManager());
                row.Register(promo);
                fixture.Invoke("Awake");
                Assert.That(Get(fixture.Placement, "m_crossPromo"), Is.SameAs(promo));
                Assert.That(Startup((SystemRef<CrossPromo>)Get(fixture.Placement, "m_crossPromoRef")), Is.Empty);
                Assert.That(Handlers(promo, "FullScreenViewRequested").Single().Target, Is.SameAs(fixture.Placement));
                fixture.Invoke("OnDestroy");
                Assert.That(Handlers(promo, "FullScreenViewRequested"), Is.Empty);
            }
        }

        // OnDestroy 0x1A164F0: without a reference it returns without touching either field; with
        // a reference but no system it leaves OnSystemStartup and keeps other observers.
        [Test]
        public void DestroyBeforeInitialisationOnlyLeavesTheStartupObserver()
        {
            using (var fixture = new Fixture())
            {
                fixture.Invoke("OnDestroy");
                Assert.That(Get(fixture.Placement, "m_crossPromoRef"), Is.Null);
                Assert.That(Get(fixture.Placement, "m_crossPromo"), Is.Null);

                var reference = new SystemRef<CrossPromo>("lucid-detached-crosspromo", null);
                Set(fixture.Placement, "m_crossPromoRef", reference);
                MethodInfo initialise = typeof(CrossPromoFullScreenPlacement).GetMethod("InitialiseCrossPromo", BindingFlags.Instance | BindingFlags.NonPublic);
                Action<CrossPromo> other = promo => { };
                reference.OnSystemStartup += other;
                reference.OnSystemStartup += (Action<CrossPromo>)Delegate.CreateDelegate(typeof(Action<CrossPromo>), fixture.Placement, initialise);
                fixture.Invoke("OnDestroy");
                Assert.That(Startup(reference), Is.EqualTo(new Delegate[] { other }));
            }
        }

        // OnEnable 0x1A16400 hides the view; Action_Close 0x1A164A0 hides it and raises
        // FullScreenViewClosed once.
        [Test]
        public void EnableHidesTheViewAndCloseAlsoRaisesClosed()
        {
            using (var fixture = new Fixture())
            {
                fixture.Invoke("OnEnable");
                Assert.That(fixture.ViewObject.activeSelf, Is.False);

                Set(fixture.Placement, "m_crossPromoRef", new SystemRef<CrossPromo>("lucid-detached-crosspromo", null));
                CrossPromo promo = NewCrossPromo(new RecordingResourceManager());
                fixture.Invoke("InitialiseCrossPromo", promo);
                int closed = 0;
                promo.FullScreenViewClosed += () => closed++;
                fixture.ViewObject.SetActive(true);
                fixture.Placement.Action_Close();
                Assert.That(fixture.ViewObject.activeSelf, Is.False);
                Assert.That(closed, Is.EqualTo(1));
                fixture.Invoke("OnDestroy");
            }
        }
    }
}
