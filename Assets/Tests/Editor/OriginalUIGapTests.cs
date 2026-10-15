using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned inactive components and assets only; no Awake, UI service or
    // input module lifecycle runs.
    public sealed class OriginalUIGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // HLInputBridge.SetInputModule 0x06000300, x86-64 0x1b50270: an inline
        // class-hierarchy isinst (0x1b502b2..0x1b50300) stores the result or
        // null, with no cast fault. A non-HL module is therefore stored as no
        // module. A direct cast (InvalidCastException), or keeping the previous
        // module, fails here.
        [Test]
        public void NonHLInputModuleIsStoredAsNoModule()
        {
            HLInputBridge bridge = null;
            GameObject owner = null;
            try
            {
                bridge = ScriptableObject.CreateInstance<HLInputBridge>();
                owner = new GameObject("Lucid owned inactive standalone module");
                owner.SetActive(false);
                var module = owner.AddComponent<StandaloneInputModule>();
                Assert.That(module, Is.Not.InstanceOf<HLInputModule>());
                bridge.SetInputModule(module);
                Assert.That(bridge.InputModuleExists(), Is.False);
                Assert.That(typeof(HLInputBridge).GetField("m_inputModule", AnyInstance).GetValue(bridge), Is.Null);
                Assert.Throws<NullReferenceException>(() => bridge.GetHardwareCursorVisibility(),
                    "Forwarding reaches the stored null module.");
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
                if (bridge != null) Object.DestroyImmediate(bridge);
            }
        }

        // UIPlatformVisibilityController.Start 0x06002ffe, x86-64 0x60b650: a
        // single tail jump to UIVisibilityGroupOverrider.ActivateOverrides
        // 0x60b65b on the serialized field, with no platform or null test. A
        // missing overrider faults, and a bound overrider is entered (its
        // unawakened registry reference faults inside ActivateOverrides). A
        // platform-conditional or null-guarded Start fails here.
        [Test]
        public void StartActivatesTheOverriderWithoutAPlatformOrNullTest()
        {
            MethodInfo start = typeof(UIPlatformVisibilityController).GetMethod("Start", AnyInstance);
            var roots = new GameObject[2];
            try
            {
                roots[0] = new GameObject("Lucid owned inactive platform visibility");
                roots[0].SetActive(false);
                var controller = roots[0].AddComponent<UIPlatformVisibilityController>();
                Exception missing = Assert.Throws<TargetInvocationException>(() => start.Invoke(controller, null)).InnerException;
                Assert.That(missing, Is.InstanceOf<NullReferenceException>());

                roots[1] = new GameObject("Lucid owned inactive macOS overrides");
                roots[1].SetActive(false);
                var overrides = roots[1].AddComponent<UIVisibilityGroupOverrider>();
                using (var serialized = new SerializedObject(controller))
                {
                    serialized.FindProperty("m_macOsUiOverrides").objectReferenceValue = overrides;
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                Assert.That(typeof(UIVisibilityGroupOverrider).GetField("m_uiManagerSystemRef", AnyInstance).GetValue(overrides), Is.Null,
                    "The overrider's Awake has not published a registry reference.");
                Exception bound = Assert.Throws<TargetInvocationException>(() => start.Invoke(controller, null)).InnerException;
                Assert.That(bound, Is.InstanceOf<NullReferenceException>(), "ActivateOverrides was entered.");
                Assert.That(typeof(UIVisibilityGroupOverrider).GetField("m_stackableDataHandle", AnyInstance).GetValue(overrides), Is.Null);
            }
            finally
            {
                foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            }
        }

        // Original metadata (global-metadata.dat) HLEventSystems.Runtime
        // 0x02000008 IScrollLeftHandler and 0x02000009 IScrollRightHandler:
        // flags 0xa1 (public abstract interface), global namespace, zero
        // methods, fields, properties and events. They are markers; their
        // OnScrollLeft/OnScrollRight receivers live only on
        // HLHorizontalScrollIndicator. Declaring a callback on either fails here.
        [Test]
        public void ScrollLeftHandlerIsAMemberlessEventMarker() => RequireMemberlessMarker(typeof(IScrollLeftHandler));

        [Test]
        public void ScrollRightHandlerIsAMemberlessEventMarker() => RequireMemberlessMarker(typeof(IScrollRightHandler));

        private static void RequireMemberlessMarker(Type marker)
        {
            const BindingFlags Own = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;
            Assert.That(marker.Assembly.GetName().Name, Is.EqualTo("HLEventSystems.Runtime"));
            Assert.That(marker.Namespace, Is.Null);
            Assert.That((int)marker.Attributes, Is.EqualTo(0xa1));
            Assert.That(marker.GetMembers(Own), Is.Empty);
            Assert.That(marker.GetInterfaces(), Is.EqualTo(new[] { typeof(IEventSystemHandler) }));
        }
    }
}
