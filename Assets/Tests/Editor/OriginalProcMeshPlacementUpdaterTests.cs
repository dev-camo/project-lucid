using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.ProcMeshPlacementUpdater (TypeDef 0x020007ed, x86-64 0x5F6E40..0x5F7100).
    // The constructor's typed TimeManager reference and the Shutdown action row are leased; no
    // TimeManager is registered, so the constructor is observed up to its Get fault and the
    // update and shutdown bodies stay covered only by their declarations here.
    public sealed class OriginalProcMeshPlacementUpdaterTests
    {
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Type type = typeof(ProcMeshPlacementUpdater);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(type.GetInterfaces(), Is.EquivalentTo(new[] { typeof(ITimeScaled), typeof(ISystem) }));
            Assert.That(Fields(type), Is.EqualTo(new[]
            {
                "<IsPaused>k__BackingField:Boolean:0001", "m_proceduralMeshPlacementManager:ProceduralMesh_PlacementManager:0021",
                "TimeCategory:TimeCategory:8051", "m_timeManagerRef:SystemRef`1:0021"
            }));
            Assert.That(type.GetField("TimeCategory", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue(),
                Is.EqualTo((int)TimeCategory.System));
            Assert.That(Methods(type), Is.EqualTo(new[]
            {
                "get_IsPaused:09e6", "set_IsPaused:09e6", ".ctor:1886", "OnUpdate:01e6", "OnFixedUpdate:01e6", "OnLateUpdate:01e6",
                "OnShutdown:0081"
            }));
        }

        // .ctor 0x5F6E60: GetSystemRef<TimeManager>(null, true) (0x5F6EF1) runs before the base
        // constructor, then SystemRef.Get (0x5F6F2E) faults without a TimeManager before Subscribe
        // and before SubscribeToAction(Shutdown) (0x5F6FD5), so no Shutdown subscriber is left.
        [Test]
        public void ConstructorFaultsWithoutATimeManagerBeforeRegisteringItsShutdown()
        {
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            {
                var shutdownRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    var missing = Assert.Throws<NullReferenceException>(() => new ProcMeshPlacementUpdater(null));
                    Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<TimeManager>() + "' is null"));
                }, shutdownRows.Dispose);
            }
        }
    }
}
