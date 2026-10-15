using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // An owned member, definition and UIManager registry row. No UIManager is
    // created; an owned non-UIManager system makes the typed row valid.
    public sealed class OriginalUIVisibilityGroupMemberGapPlayTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // UIVisibilityGroupMember.Awake 0x06000005, x86-64 0x1a39f20, defers
        // OnUIManagerValid through SystemRef.InvokeOnValid 0x1a3a0bc when no
        // manager is valid. OnDestroy 0x06000006, x86-64 0x1a3a150, only tests
        // IsValid 0x1a3a193 and unregisters through a valid manager; it never
        // removes that waiting callback. A later valid system therefore still
        // calls the destroyed member: the typed reference is null for an
        // incompatible system, so the retained callback faults. Removing the
        // waiting callback in OnDestroy lets the registration succeed and fails here.
        [Test]
        public void DestroyedWaitingMemberKeepsItsDeferredManagerCallback()
        {
            string name = ProcessManager.GetDefaultName<UIManager>();
            IDictionary registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (registry.Contains(name))
                throw new InvalidOperationException("Fixture requires an absent system row: " + name);
            UIVisibilityGroupDefinition definition = null;
            GameObject owner = null;
            object row = null;
            var stub = new OwnedSystem();
            try
            {
                definition = ScriptableObject.CreateInstance<UIVisibilityGroupDefinition>();
                owner = new GameObject("Lucid owned waiting visibility member");
                owner.SetActive(false);
                UIVisibilityGroupMember member = owner.AddComponent<UIVisibilityGroupMember>();
                typeof(UIVisibilityGroupMember).GetField("m_visibilityGroupDefinition", AnyInstance).SetValue(member, definition);
                owner.SetActive(true);
                row = registry[name];
                Assert.That(row, Is.Not.Null, "Awake created the manager row.");
                Assert.That(owner.activeSelf, Is.False, "Initial false visibility deactivated the owner during Awake.");

                Object.DestroyImmediate(owner);
                Assert.That(member == null, Is.True);
                Assert.Throws<NullReferenceException>(() => ProcessManager.RegisterSystem(stub, name),
                    "The destroyed member's waiting callback runs with a null manager.");
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
                if (definition != null) Object.DestroyImmediate(definition);
                // The row (and the faulted callback it still holds) exists only
                // for this test; revoke the owned stub and discard the whole row.
                if (row != null)
                {
                    Assert.That(registry[name], Is.SameAs(row), "Fixture row was replaced: " + name);
                    if (ReferenceEquals(ProcessManager.GetSystemRef(name, false).GetSafe(), stub))
                        ProcessManager.UnregisterSystem(name);
                    Assert.That(ProcessManager.IsSystemNull(name), Is.True);
                    registry.Remove(name);
                }
            }
        }

        private sealed class OwnedSystem : ISystem { }
    }
}
