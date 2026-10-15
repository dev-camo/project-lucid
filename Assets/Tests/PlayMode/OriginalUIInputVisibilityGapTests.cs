using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine play-mode lifecycle for UIInputTypeVisibilityController over a real
    // UIRuntimeConfiguration, real overriders and a real UIManager, with a
    // test-only InputBridge probe. The registry, the typed stack operation and
    // the static input action are fixture-owned and restored by reference.
    public sealed class OriginalUIInputVisibilityGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // OnUIRuntimeConfigurationValid 0x06002ff4 (x86-64 0x60b0e0) queues closure
        // 0x06002ff9 (0x60b350) on the UIManager reference; OnDisable 0x06002ff5
        // (0x60b210) only subtracts the static callback and cancels nothing. The
        // closure stores the bridge (+0x30) before virtual GetLastInputType
        // (0x60b3ed), applies the overrides, and only then adds the static
        // callback (0x60b489). A controller disabled before UIManager is valid is
        // therefore still subscribed when UIManager registers. A disabled guard
        // in the closure, or OnDisable revoking the pending callback, leaves the
        // action empty; subscribing before the update would be seen by the probe.
        [Test]
        public void DisabledControllerStillSubscribesWhenUIManagerBecomesValidLater()
        {
            FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            var logicalAnds = (IDictionary)typeof(StackableData).GetField("LogicalAnds", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            Type visibilityType = typeof(Dictionary<string, bool>);
            bool hadOperation = logicalAnds.Contains(visibilityType);
            object previousOperation = hadOperation ? logicalAnds[visibilityType] : null;
            FastAction<UIInputType> previousInputAction = InputBridge.OnUpdateLastInputType;
            var allocations = new List<UObject>();
            try
            {
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                InputBridge.OnUpdateLastInputType = null;

                UIVisibilityGroupOverrider pointer = Overrider(allocations, "pointer");
                UIVisibilityGroupOverrider controller = Overrider(allocations, "controller");

                var probe = ScriptableObject.CreateInstance<PlayModeInputBridgeProbe>();
                allocations.Add(probe);
                probe.LastInputType = UIInputType.Mouse;
                probe.SelectionInputType = UIInputType.DefaultController;
                GameObject configurationOwner = Owner(allocations, "Lucid owned UI runtime configuration");
                Set(configurationOwner.AddComponent<UIRuntimeConfiguration>(), "m_inputBridge", probe);
                configurationOwner.SetActive(true);
                Assert.That(ProcessManager.IsSystemValid<UIRuntimeConfiguration>(), Is.True);
                Assert.That(ProcessManager.IsSystemValid<UIManager>(), Is.False);

                GameObject controllerOwner = Owner(allocations, "Lucid owned input visibility controller");
                UIInputTypeVisibilityController visibility = controllerOwner.AddComponent<UIInputTypeVisibilityController>();
                Set(visibility, "m_pointerUIOverrides", pointer);
                Set(visibility, "m_controllerUIOverrides", controller);
                probe.Calls.Clear();

                // Configuration is valid, so the UIManager callback is queued at once.
                controllerOwner.SetActive(true);
                controllerOwner.SetActive(false);
                Assert.That(probe.Calls, Is.Empty);
                Assert.That(InputBridge.OnUpdateLastInputType, Is.Null);
                Assert.That(Get(visibility, "m_inputBridge"), Is.Null);

                object bridgeAtQuery = null;
                int subscribersAtQuery = -1;
                probe.BeforeLastInputType = () =>
                {
                    bridgeAtQuery = Get(visibility, "m_inputBridge");
                    subscribersAtQuery = InputBridge.OnUpdateLastInputType == null ? 0 : InputBridge.OnUpdateLastInputType.GetInvocationListCount();
                };

                // A real UIManager registers in Awake and releases the pending closure.
                GameObject managerOwner = Owner(allocations, "Lucid owned input visibility UI host");
                UIManager manager = managerOwner.AddComponent<UIManager>();
                Set(manager, "m_visibilityGroupDefinitions", new List<UIVisibilityGroupDefinition>());
                Set(manager, "m_guiCamera", Owner(allocations, "Lucid owned input visibility camera", true).AddComponent<Camera>());
                foreach (string parent in new[] { "m_freeParent", "m_linkedParent", "m_singleParent", "m_stackParent", "m_transitionParent", "m_debugParent" })
                    Set(manager, parent, Owner(allocations, "Lucid owned " + parent, true).transform);
                managerOwner.SetActive(true);

                Assert.That(controllerOwner.activeSelf, Is.False);
                Assert.That(ReferenceEquals(bridgeAtQuery, probe), Is.True, "bridge stored before the last-type query");
                Assert.That(subscribersAtQuery, Is.Zero, "no subscription before the update");
                Assert.That(probe.Calls, Is.EqualTo(new[] { "GetLastInputType", "DoesInputTypeRequireSelection Mouse" }));
                Assert.That(Get(pointer, "m_stackableDataHandle"), Is.Not.Null, "pointer overrides applied");
                Assert.That(Get(controller, "m_stackableDataHandle"), Is.Null);
                Assert.That(InputBridge.OnUpdateLastInputType, Is.Not.Null);
                Assert.That(InputBridge.OnUpdateLastInputType.GetInvocationListCount(), Is.EqualTo(1), "disabled controller is subscribed");

                // The leaked subscription still drives the disabled controller.
                InputBridge.OnUpdateLastInputType.Invoke(UIInputType.DefaultController);
                Assert.That(Get(pointer, "m_stackableDataHandle"), Is.Null);
                Assert.That(Get(controller, "m_stackableDataHandle"), Is.Not.Null);
            }
            finally
            {
                try { DestroyAll(allocations, allocations.Count - 1); }
                finally
                {
                    try
                    {
                        if (hadOperation) logicalAnds[visibilityType] = previousOperation;
                        else logicalAnds.Remove(visibilityType);
                    }
                    finally
                    {
                        InputBridge.OnUpdateLastInputType = previousInputAction;
                        registryField.SetValue(null, previousRegistry);
                    }
                }
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }

        // Stands in for authored serialized references; no lifecycle is invoked.
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);

        private static GameObject Owner(List<UObject> allocations, string name, bool active = false)
        {
            var owner = new GameObject(name);
            allocations.Add(owner);
            owner.SetActive(active);
            return owner;
        }

        // An authored overrider with no rows; Awake caches the UIManager reference.
        private static UIVisibilityGroupOverrider Overrider(List<UObject> allocations, string label)
        {
            GameObject owner = Owner(allocations, "Lucid owned " + label + " visibility overrider");
            UIVisibilityGroupOverrider overrider = owner.AddComponent<UIVisibilityGroupOverrider>();
            Set(overrider, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride>());
            owner.SetActive(true);
            return overrider;
        }

        private static void DestroyAll(List<UObject> objects, int index)
        {
            if (index < 0) return;
            try { if (objects[index] != null) UObject.DestroyImmediate(objects[index]); }
            finally { DestroyAll(objects, index - 1); }
        }
    }
}
