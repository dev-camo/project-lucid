using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using static ProjectLucid.VerificationFixtureOwnership;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalHLSelectableClearStateTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

        public sealed class ProbeSelectable : HLSelectable
        {
            public readonly List<string> Trace = new List<string>();
            public bool InsideAtDeselect, DownAtDeselect;
            public bool Inside => IsPointerInside;
            public bool Down => IsPointerDown;
            public void SetPointers() { IsPointerInside = true; IsPointerDown = true; }
            public void ClearState() => base.InstantClearState();
            public override void Deselect()
            {
                InsideAtDeselect = IsPointerInside;
                DownAtDeselect = IsPointerDown;
                Trace.Add("Deselect");
            }
            protected override void UpdateHighlight() => Trace.Add("UpdateHighlight");
            protected override void DoStateTransition(SelectionState state, bool instant) => Trace.Add("DoStateTransition");
        }

        public sealed class ForwardingBridge : HLInputBridge
        {
            public readonly List<GameObject> DeselectionRequests = new List<GameObject>();
            public override void DeselectObjectIfCurrentlySelected(GameObject target)
            {
                DeselectionRequests.Add(target);
                base.DeselectObjectIfCurrentlySelected(target);
            }
        }

        // HLUnityUI.Runtime 0x060006cb (0x1b72370) clears both flags before tail-calling virtual Deselect, slot 44.
        [Test]
        public void InstantClearStateClearsBothPointerFlagsThenDeselects()
        {
            var root = new GameObject("Lucid owned clear-state probe", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var probe = root.AddComponent<ProbeSelectable>();
                probe.SetPointers();
                FieldInfo current = typeof(HLSelectable).GetField("m_currentSelectionState", Instance);
                FieldInfo previous = typeof(HLSelectable).GetField("m_previousSelectionState", Instance);
                current.SetValue(probe, Enum.ToObject(current.FieldType, 1)); // Original Highlighted.
                previous.SetValue(probe, Enum.ToObject(previous.FieldType, 0)); // Original Normal.

                probe.ClearState();

                Assert.That(probe.Trace, Is.EqualTo(new[] { "Deselect" }));
                Assert.That(probe.InsideAtDeselect, Is.False);
                Assert.That(probe.DownAtDeselect, Is.False);
                Assert.That(probe.Inside, Is.False);
                Assert.That(probe.Down, Is.False);
                Assert.That(Convert.ToInt32(current.GetValue(probe)), Is.EqualTo(1));
                Assert.That(Convert.ToInt32(previous.GetValue(probe)), Is.EqualTo(0));
            }
            finally { UObject.DestroyImmediate(root); }
        }

        // HLInputModule 0x060000ea compares the active ISelectHandler owner with its remembered object before clearing.
        // A companion Button remains discoverable when HLSelectable is disabled; a sole disabled handler does not.
        [TestCase(true, TestName = "DisablingASelectedControlWithAnActiveCompanionHandlerClearsSelectionThroughTheBridge")]
        [TestCase(false, TestName = "DisablingTheSoleSelectedHandlerRequestsDeselectionButRetainsTheModuleGuard")]
        public void DisablingASelectedControlRequestsDeselectionThroughTheBridge(bool companionHandler)
        {
            EventSystem priorSystem = EventSystem.current;
            GameObject priorSelection = priorSystem != null ? priorSystem.currentSelectedGameObject : null;
            FieldInfo highlightField = typeof(HLSelectable).GetField("m_onShouldHighlightChanged", Static);
            var sceneRows = (Dictionary<string, List<HLSelectable>>)typeof(ActiveSelectables).GetField("s_selectables", Static).GetValue(null);
            var owned = new List<UObject>();
            GameObject selectableHost = null;
            string sceneName = null;
            bool hadSceneRow = false;
            RegistryRow runtimeRow = null;
            RunThenCleanUp(() =>
            {
                runtimeRow = new RegistryRow(ProcessManager.GetDefaultName<UIRuntimeConfiguration>(),
                    HLSelectable.InitShouldHighlight, highlightField.GetValue(null));
                GameObject Host(string name)
                {
                    var host = new GameObject(name, typeof(RectTransform));
                    host.SetActive(false);
                    owned.Add(host);
                    return host;
                }
                var system = Host("Lucid owned clear-state EventSystem").AddComponent<EventSystem>();
                system.gameObject.SetActive(true);
                EventSystem.current = system;
                // Inactive module data uses the original selection APIs without starting its unrelated input providers.
                var module = Host("Lucid owned clear-state input module").AddComponent<HLInputModule>();
                var bridge = ScriptableObject.CreateInstance<ForwardingBridge>();
                owned.Add(bridge);
                bridge.SetInputModule(module);
                var runtime = Host("Lucid owned clear-state UI configuration").AddComponent<UIRuntimeConfiguration>();
                typeof(UIRuntimeConfiguration).GetField("m_inputBridge", Instance).SetValue(runtime, bridge);
                typeof(UIRuntimeConfiguration).GetField("m_inputModule", Instance).SetValue(runtime, module);
                // Register the real inactive configuration without its unrelated global input subscriptions.
                runtimeRow.Register(runtime);

                selectableHost = Host("Lucid owned selected control");
                sceneName = selectableHost.scene.name;
                hadSceneRow = sceneRows.ContainsKey(sceneName);
                var selectable = selectableHost.AddComponent<HLSelectable>();
                if (companionHandler) selectableHost.AddComponent<UnityEngine.UI.Button>();
                selectableHost.SetActive(true);
                Assert.That(selectable.IsActive(), Is.True);
                Assert.That(ActiveSelectables.GetSelectables().Contains(selectable), Is.True);
                selectable.Select();
                Assert.That(system.currentSelectedGameObject, Is.SameAs(selectableHost));
                Assert.That(bridge.IsObjectSelected(selectableHost), Is.True, "selection was remembered through the original bridge");
                Assert.That(bridge.DeselectionRequests, Is.Empty);

                selectable.enabled = false;

                Assert.That(bridge.DeselectionRequests, Is.EqualTo(new[] { selectableHost }));
                Assert.That(ActiveSelectables.GetSelectables().Contains(selectable), Is.False);
                Assert.That(ExecuteEvents.GetEventHandler<ISelectHandler>(selectableHost),
                    companionHandler ? Is.SameAs(selectableHost) : Is.Null, "original module lookup precondition");
                Assert.That(system.currentSelectedGameObject,
                    companionHandler ? Is.Null : Is.SameAs(selectableHost));
            },
            () => { if (selectableHost != null) UObject.DestroyImmediate(selectableHost); },
            () =>
            {
                if (sceneName != null && !hadSceneRow && sceneRows.TryGetValue(sceneName, out var row))
                {
                    Assert.That(row, Is.Empty, "only the owned scene row may be removed");
                    sceneRows.Remove(sceneName);
                }
                var action = runtimeRow != null ? (FastAction<bool>)highlightField.GetValue(null) : null;
                if (action != null)
                {
                    Assert.That(action.GetInvocationListCount(), Is.Zero, "owned highlight listener was removed");
                    highlightField.SetValue(null, null); // The fixture admitted an absent action before activation.
                }
            },
            () => runtimeRow?.Dispose(),
            () =>
            {
                var cleanup = new List<Action>();
                for (int i = owned.Count - 1; i >= 0; i--)
                {
                    UObject item = owned[i];
                    cleanup.Add(() => { if (item != null) UObject.DestroyImmediate(item); });
                }
                RunThenCleanUp(() => { }, cleanup.ToArray());
            },
            () =>
            {
                EventSystem.current = priorSystem;
                if (priorSystem != null && priorSystem.currentSelectedGameObject != priorSelection)
                    priorSystem.SetSelectedGameObject(priorSelection);
            });
        }
    }
}
