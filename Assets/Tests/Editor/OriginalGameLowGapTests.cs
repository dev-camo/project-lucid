using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Apple.Arcade;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Low-priority Game.Runtime and FairPlay gaps, each checked against the
    // shipped x86-64 body named in its comment. IL-order checks are used only
    // where the original order has no other observable effect.
    public sealed class OriginalGameLowGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // ApplicationTransitionUIMessageCloseUIContainer.ConstructInstance
        // 0x060008a4 (x86-64 0x666f80) calls UIContainerIdentifier.FindByName
        // (0x666fdd) before UIModernEvent.FindByName (0x666feb), although the
        // constructor takes the event first. Both lookups only read Resources,
        // so the compiled order is inspected. Swapping the lookups fails.
        [Test]
        public void CloseContainerFactoryResolvesTheContainerBeforeTheEvent()
        {
            MethodInfo construct = typeof(ApplicationTransitionUIMessageCloseUIContainer).GetMethod("ConstructInstance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            string[] lookups = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(construct))
                .Where(name => name.EndsWith(".FindByName", StringComparison.Ordinal)).ToArray();
            Assert.That(lookups, Is.EqualTo(new[] { "UIContainerIdentifier.FindByName", "UIModernEvent.FindByName" }));
        }

        // FairPlay.RegisterWithFairPlay 0x06000004 (x86-64 0x505430) resolves
        // and calls the import inside the try, and its catch calls
        // Debug.LogException (0x5054fc) before Application.Quit (0x505520).
        // The import and Quit are never run here. Reversing the handler calls,
        // or quitting without logging, fails.
        [Test]
        public void FairPlayCatchLogsTheExceptionBeforeRequestingQuit()
        {
            MethodInfo wrapper = typeof(FairPlay).GetMethod(nameof(FairPlay.RegisterWithFairPlay));
            ExceptionHandlingClause clause = wrapper.GetMethodBody().ExceptionHandlingClauses.Single();
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(wrapper);
            string[] inTry = OriginalILCallSequence.Names(calls.Where(call =>
                call.Offset >= clause.TryOffset && call.Offset < clause.TryOffset + clause.TryLength));
            string[] inHandler = OriginalILCallSequence.Names(calls.Where(call =>
                call.Offset >= clause.HandlerOffset && call.Offset < clause.HandlerOffset + clause.HandlerLength));
            Assert.That(inTry, Is.EqualTo(new[] { "FairPlay.DoRegisterAppWithFairPlay" }));
            Assert.That(inHandler, Is.EqualTo(new[] { "Debug.LogException", "Application.Quit" }));
        }

        // ConeVolume.IsPositionInsideCone 0x06003944 (x86-64 0x6af510) divides
        // the squared local offsets by the squared scaled tangents (divps
        // 0x6af54f) with no zero guard, then ucomiss/setae against 1.0
        // (0x6af562), which is false for NaN. At zero distance the center is
        // 0/0 and any offset is x/0, so both are outside. A zero-radius guard
        // that admits the center fails.
        [Test]
        public void ZeroDistanceConeAdmitsNoPointIncludingItsCenter()
        {
            var origin = new Vector3(3f, -2f, 5f);
            var tangents = new Vector2(2f, 1f);
            Assert.That(ConeVolume.IsPositionInsideCone(origin, origin, tangents, 0f, Quaternion.identity), Is.False);
            Assert.That(ConeVolume.IsPositionInsideCone(origin + Vector3.right, origin, tangents, 0f, Quaternion.identity), Is.False);
            Assert.That(ConeVolume.IsPositionInsideCone(origin + Vector3.forward, origin, tangents, 0f, Quaternion.identity), Is.False);
        }

        private sealed class OwnedEntity : IEntityActivatable
        {
            public OwnedEntity(EntityActivationDefinition definition, Transform transform)
            {
                Definition = definition;
                Transform = transform;
            }
            public EntityActivationDefinition Definition { get; }
            public Transform Transform { get; }
        }

        // EntityActivationManager.GetDebugInfo 0x06003bb5 (x86-64 0x6c34e0)
        // passes 'Active objects' then 'Inactive objects' with one AppendLine
        // between. GetDebugObjectsInfo 0x06003bb6 (0x6c3550) appends a line,
        // the header, ' ({0}): ' with the total, then per row '{0} ({1}): '
        // and each entity name plus ', ' (0x6c3b87) before a line, or the line
        // '<Empty>' (0x6c3bf0) for an empty row. Dropping the trailing
        // separator or the empty marker fails.
        [Test]
        public void DebugInfoKeepsTheExactHeadersCountsSeparatorsAndEmptyMarker()
        {
            string nl = Environment.NewLine;
            var manager = new EntityActivationManager();
            EntityActivationDefinition definition = null;
            GameObject owner = null;
            try
            {
                var empty = new StringBuilder("prior");
                manager.GetDebugInfo(empty);
                Assert.That(empty.ToString(), Is.EqualTo("prior" + nl + "Active objects (0): " + nl + nl + nl + "Inactive objects (0): " + nl));

                definition = ScriptableObject.CreateInstance<EntityActivationDefinition>();
                typeof(EntityActivationDefinition).GetField("m_type", Instance).SetValue(definition, EntityActivationType.Collectable);
                typeof(EntityActivationDefinition).GetField("m_activateLogicDefinitions", Instance).SetValue(definition, new List<EntityActivationLogicDefinition>());
                typeof(EntityActivationDefinition).GetField("m_deactivateLogicDefinitions", Instance).SetValue(definition, new List<EntityActivationLogicDefinition>());
                owner = new GameObject("Lucid debug entity");
                owner.SetActive(false);
                manager.Register(new OwnedEntity(definition, owner.transform), true);

                var populated = new StringBuilder();
                manager.GetDebugInfo(populated);
                Assert.That(populated.ToString(), Is.EqualTo(
                    nl + "Active objects (1): " + nl +
                    "Collectable (1): Lucid debug entity, " + nl +
                    nl +
                    nl + "Inactive objects (0): " + nl +
                    "Collectable (0): <Empty>" + nl));
            }
            finally
            {
                ProcessManager.UnsubscribeFromAllActions(manager);
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private sealed class ShutdownProbe : EntityActivationLogic
        {
            public int Shutdowns;
            public ShutdownProbe() { GC.SuppressFinalize(this); }
            public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity) =>
                throw new NotSupportedException("This probe is never created from a definition.");
            public override void Shutdown() => ++Shutdowns;
        }

        // EntityActivationLogic finalizer 0x06003b6c (x86-64 0x6c0e70) makes a
        // virtual Shutdown call (slot +0x188, 0x6c0e97) and then Object.Finalize
        // in its finally (0x6c0ea3). The probe suppresses GC finalization; the
        // declared destructor is invoked directly once. Removing the
        // destructor, or calling the base Shutdown non-virtually, fails.
        [Test]
        public void DestructorDispatchesTheOverriddenShutdown()
        {
            MethodInfo finalize = typeof(EntityActivationLogic).GetMethod("Finalize", Instance | BindingFlags.DeclaredOnly);
            Assert.That(finalize, Is.Not.Null, "the original destructor is declared");
            var probe = new ShutdownProbe();
            finalize.Invoke(probe, null);
            Assert.That(probe.Shutdowns, Is.EqualTo(1));
        }
    }
}
