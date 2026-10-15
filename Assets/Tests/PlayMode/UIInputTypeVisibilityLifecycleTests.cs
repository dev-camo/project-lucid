using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using UnityEngine;
using UnityEngine.Events;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine play-mode lifecycle for UIInputTypeVisibilityController over a real
    // UIManager, real visibility overriders and members, and a real
    // UIRuntimeConfiguration whose only stand-in is a test-only InputBridge probe.
    // The registry, the typed stack operation and the static input action are
    // fixture-owned and restored by exact reference, including after faults.
    public sealed class UIInputTypeVisibilityLifecycleTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // OnUpdateLastInputType 0x06002ff6 x86-64 0x60b2e0: when the bridge says the
        // type requires selection, pointer (+0x20) DeactivateOverrides precedes
        // controller (+0x28) ActivateOverrides; otherwise controller deactivates
        // before pointer activates. With one group per overrider, member events
        // expose that order. Swapping either branch's calls reorders the events.
        [Test]
        public void SelectionTypesHidePointerGroupBeforeShowingControllerGroupAndBack()
        {
            FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            var logicalAnds = (IDictionary)typeof(StackableData).GetField("LogicalAnds", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            Type visibilityType = typeof(Dictionary<string, bool>);
            bool hadOperation = logicalAnds.Contains(visibilityType);
            object previousOperation = hadOperation ? logicalAnds[visibilityType] : null;
            FastAction<UIInputType> previousInputAction = InputBridge.OnUpdateLastInputType;
            var allocations = new List<UObject>();
            var log = new List<string>();
            try
            {
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                InputBridge.OnUpdateLastInputType = null;
                UIVisibilityGroupDefinition pointerGroup = Group(allocations, "lucid-owned-pointer-group");
                UIVisibilityGroupDefinition controllerGroup = Group(allocations, "lucid-owned-controller-group");

                GameObject managerOwner = Owner(allocations, "Lucid owned input visibility UI host");
                UIManager manager = managerOwner.AddComponent<UIManager>();
                Set(manager, "m_visibilityGroupDefinitions", new List<UIVisibilityGroupDefinition> { pointerGroup, controllerGroup });
                Set(manager, "m_guiCamera", Owner(allocations, "Lucid owned input visibility camera", true).AddComponent<Camera>());
                foreach (string parent in new[] { "m_freeParent", "m_linkedParent", "m_singleParent", "m_stackParent", "m_transitionParent", "m_debugParent" })
                    Set(manager, parent, Owner(allocations, "Lucid owned " + parent, true).transform);
                managerOwner.SetActive(true);
                Assert.That(ReferenceEquals(ProcessManager.GetSystem<UIManager>(), manager), Is.True);

                Member(allocations, pointerGroup, "pointer", log);
                Member(allocations, controllerGroup, "controller", log);
                UIVisibilityGroupOverrider pointer = Overrider(allocations, pointerGroup);
                UIVisibilityGroupOverrider controller = Overrider(allocations, controllerGroup);

                var probe = ScriptableObject.CreateInstance<OwnedInputBridgeProbe>();
                allocations.Add(probe);
                probe.LastInputType = UIInputType.Mouse;
                probe.SelectionInputType = UIInputType.DefaultController;
                GameObject configurationOwner = Owner(allocations, "Lucid owned UI runtime configuration");
                Set(configurationOwner.AddComponent<UIRuntimeConfiguration>(), "m_inputBridge", probe);
                configurationOwner.SetActive(true);

                GameObject controllerOwner = Owner(allocations, "Lucid owned input visibility controller");
                UIInputTypeVisibilityController visibility = controllerOwner.AddComponent<UIInputTypeVisibilityController>();
                Set(visibility, "m_pointerUIOverrides", pointer);
                Set(visibility, "m_controllerUIOverrides", controller);
                log.Clear();
                probe.Calls.Clear();

                // OnEnable reaches both valid systems synchronously and applies the
                // bridge's last (pointer) type before subscribing.
                controllerOwner.SetActive(true);
                Assert.That(log, Is.EqualTo(new[] { "pointer shown" }));
                Assert.That(probe.Calls, Is.EqualTo(new[] { "GetLastInputType", "DoesInputTypeRequireSelection Mouse" }));
                Assert.That(InputBridge.OnUpdateLastInputType, Is.Not.Null);
                Assert.That(InputBridge.OnUpdateLastInputType.GetInvocationListCount(), Is.EqualTo(1));

                log.Clear();
                InputBridge.OnUpdateLastInputType.Invoke(UIInputType.DefaultController);
                Assert.That(log, Is.EqualTo(new[] { "pointer hidden", "controller shown" }));
                log.Clear();
                InputBridge.OnUpdateLastInputType.Invoke(UIInputType.Mouse);
                Assert.That(log, Is.EqualTo(new[] { "controller hidden", "pointer shown" }));
                log.Clear();
                InputBridge.OnUpdateLastInputType.Invoke(UIInputType.DefaultController);
                Assert.That(log, Is.EqualTo(new[] { "pointer hidden", "controller shown" }));
                Assert.That(probe.Calls.GetRange(2, 3), Is.EqualTo(new[] {
                    "DoesInputTypeRequireSelection DefaultController",
                    "DoesInputTypeRequireSelection Mouse",
                    "DoesInputTypeRequireSelection DefaultController" }));

                // Original OnDisable only unsubscribes; overrides stay applied.
                log.Clear();
                controllerOwner.SetActive(false);
                Assert.That(InputBridge.OnUpdateLastInputType.GetInvocationListCount(), Is.Zero);
                Assert.That(log, Is.Empty);
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

        private static GameObject Owner(List<UObject> allocations, string name, bool active = false)
        {
            var owner = new GameObject(name);
            allocations.Add(owner);
            owner.SetActive(active);
            return owner;
        }

        private static UIVisibilityGroupDefinition Group(List<UObject> allocations, string guid)
        {
            var definition = ScriptableObject.CreateInstance<UIVisibilityGroupDefinition>();
            allocations.Add(definition);
            Set(definition, "m_guid", guid);
            Set(definition, "m_initialVisibility", false);
            return definition;
        }

        private static void Member(List<UObject> allocations, UIVisibilityGroupDefinition group, string label, List<string> log)
        {
            GameObject owner = Owner(allocations, "Lucid owned " + label + " visibility member");
            UIVisibilityGroupMember member = owner.AddComponent<UIVisibilityGroupMember>();
            var shown = new UnityEvent(); shown.AddListener(() => log.Add(label + " shown"));
            var hidden = new UnityEvent(); hidden.AddListener(() => log.Add(label + " hidden"));
            Set(member, "m_visibilityGroupDefinition", group);
            Set(member, "m_setGameObjectActive", false);
            Set(member, "m_onBecomeVisible", shown);
            Set(member, "m_onBecomeInvisible", hidden);
            owner.SetActive(true);
        }

        private static UIVisibilityGroupOverrider Overrider(List<UObject> allocations, UIVisibilityGroupDefinition group)
        {
            GameObject owner = Owner(allocations, "Lucid owned visibility overrider");
            UIVisibilityGroupOverrider overrider = owner.AddComponent<UIVisibilityGroupOverrider>();
            var row = new UIVisibilityGroupOverrider.UIVisibilityGroupOverride();
            Set(row, "m_visibilityGroupDefinition", group);
            Set(row, "m_visible", true);
            Set(overrider, "m_overrides", new List<UIVisibilityGroupOverrider.UIVisibilityGroupOverride> { row });
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
