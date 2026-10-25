using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine active HLButton lifecycle and pointer routes. The fixture owns an
    // isolated ProcessManager registry, a real CoroutineUtils host, a real
    // UIRuntimeConfiguration whose InputBridge is a test-only probe, a real
    // EventSystem, and every static the button chain reads or writes
    // (CoroutineUtils.s_instance, HLSelectable highlight actions, HLButton sound
    // actions, ActiveSelectables scene rows). All are restored by reference.
    public sealed class OriginalHLButtonGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Action_OnButtonDown 0x1b6ce50 / Action_OnButtonUp 0x1b6cf00 (x86-64)
        // invoke the UnityEvent (+0x1b8 / +0x1c0) first and only then load the
        // static sound action and the id field (+0x198 / +0x19c), so a listener
        // that swaps both is honoured. Reading either before the event would
        // send the old id to the old action.
        [Test]
        public void ButtonDownAndUpReadSoundActionAndIdAfterTheirEvent()
        {
            using (var fixture = new ActiveButtonFixture())
            {
                HLButton button = fixture.Button;
                var heard = new List<string>();
                foreach (bool down in new[] { true, false })
                {
                    string prefix = down ? "down" : "up";
                    FastAction<HLAudioTypes> original = null;
                    original += id => heard.Add(prefix + " original " + (int)id);
                    FastAction<HLAudioTypes> replacement = null;
                    replacement += id => heard.Add(prefix + " replacement " + (int)id);
                    SetSfx(down, original);
                    Set(button, down ? "m_onDownSfxId" : "m_onUpSfxId", (HLAudioTypes)11);
                    UnityEvent buttonEvent = down ? button.OnButtonDown : button.OnButtonUp;
                    buttonEvent.AddListener(() =>
                    {
                        heard.Add(prefix + " event");
                        SetSfx(down, replacement);
                        Set(button, down ? "m_onDownSfxId" : "m_onUpSfxId", (HLAudioTypes)22);
                    });
                    if (down) button.Action_OnButtonDown(); else button.Action_OnButtonUp();
                }
                Assert.That(heard, Is.EqualTo(new[] { "down event", "down replacement 22", "up event", "up replacement 22" }));
            }
        }

        // OnDisable 0x1b6cc20: the inlined base chain (HLSelectable_OnDisable,
        // coroutine stops, ResetTransformToCache) runs first; then only the
        // literal Highlighted(1) state (+0xdc) tail-calls UnityEvent_Invoke on
        // OnDisabledSelected (+0x1c8) without a null test. A Normal button never
        // reads the event; a Highlighted one faults on the null default; with a
        // listener the button is already out of ActiveSelectables. A null guard
        // loses the expected exception; invoking before base sees the row.
        [Test]
        public void DisableInvokesDisabledSelectedOnlyWhenHighlightedAfterBaseWithoutNullGuard()
        {
            using (var fixture = new ActiveButtonFixture())
            {
                HLButton button = fixture.Button;
                var data = new PointerEventData(fixture.EventSystem);
                Assert.That(button.OnDisabledSelected, Is.Null);

                // Normal: no read of the null event.
                Assert.That(State(button), Is.EqualTo(0));
                button.gameObject.SetActive(false);
                Assert.That(fixture.Probe.DeselectionRequests, Is.EqualTo(new[] { button.gameObject }));
                button.gameObject.SetActive(true);

                // Pointer-inside unpressed is the original Highlighted state.
                button.OnPointerEnter(data);
                Assert.That(State(button), Is.EqualTo(1));
                LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
                button.gameObject.SetActive(false);
                Assert.That(ActiveSelectables.GetSelectables().Contains(button), Is.False, "base OnDisable completed before the fault");
                Assert.That(fixture.Probe.DeselectionRequests, Is.EqualTo(new[] { button.gameObject, button.gameObject }));

                var observed = new List<string>();
                button.OnDisabledSelected = new UnityEvent();
                button.OnDisabledSelected.AddListener(() => observed.Add(
                    "listed=" + ActiveSelectables.GetSelectables().Contains(button) + " state=" + State(button)));
                button.gameObject.SetActive(true);
                button.OnPointerEnter(data);
                button.gameObject.SetActive(false);
                Assert.That(observed, Is.EqualTo(new[] { "listed=False state=1" }));
                Assert.That(fixture.Probe.DeselectionRequests, Is.EqualTo(new[] { button.gameObject, button.gameObject, button.gameObject }));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // OnPointerUp 0x1b6d410 and OnPointerExit 0x1b6d6a0 capture
        // IsPointerDown(+0x111) && IsPointerInside(+0x110) before the inlined or
        // called base clears one of them, then invoke OnButtonUp from that
        // capture (a non-left Up still dispatches it). OnPointerEnter 0x1b6d4c0
        // reads EventSystem.current, its selected object and down&&!inside before
        // HLSelectable_OnPointerEnter (0x1b6d5ba), then invokes OnButtonDown only
        // when both prior booleans hold. Reading any state after the base call
        // loses every Up below and the selected re-entry Down.
        [Test]
        public void PointerUpExitAndEnterDispatchFromStateCapturedBeforeBase()
        {
            using (var fixture = new ActiveButtonFixture())
            {
                HLButton button = fixture.Button;
                var log = new List<string>();
                button.OnButtonDown.AddListener(() => log.Add("down"));
                button.OnButtonUp.AddListener(() => log.Add("up"));
                var left = new PointerEventData(fixture.EventSystem) { button = PointerEventData.InputButton.Left };
                var right = new PointerEventData(fixture.EventSystem) { button = PointerEventData.InputButton.Right };

                button.OnPointerEnter(left);
                Assert.That(log, Is.Empty, "unselected enter");
                button.OnPointerDown(left);
                Assert.That(log, Is.EqualTo(new[] { "down" }));
                Assert.That(State(button), Is.EqualTo(2));

                // Non-left: base returns before clearing down, Up still fires.
                button.OnPointerUp(right);
                Assert.That(log, Is.EqualTo(new[] { "down", "up" }));
                Assert.That(State(button), Is.EqualTo(2));

                button.OnPointerUp(left);
                Assert.That(log, Is.EqualTo(new[] { "down", "up", "up" }), "Up captured before base clears down");
                Assert.That(State(button), Is.EqualTo(1));

                button.OnPointerDown(left);
                button.OnPointerExit(left);
                Assert.That(log, Is.EqualTo(new[] { "down", "up", "up", "down", "up" }), "Up captured before base clears inside");

                // Pressed outside but not selected: no Down on re-entry.
                button.OnPointerEnter(left);
                Assert.That(log.Count, Is.EqualTo(5));
                button.OnPointerExit(left);
                Assert.That(log.Count, Is.EqualTo(6));

                fixture.EventSystem.SetSelectedGameObject(button.gameObject);
                Assert.That(fixture.EventSystem.currentSelectedGameObject, Is.SameAs(button.gameObject));
                button.OnPointerEnter(left);
                Assert.That(log, Is.EqualTo(new[] { "down", "up", "up", "down", "up", "up", "down" }), "selected re-entry uses pre-base down&&!inside");
                fixture.EventSystem.SetSelectedGameObject(null);
            }
        }

        private static int State(HLButton button) => Convert.ToInt32(Field(typeof(HLSelectable), "m_currentSelectionState").GetValue(button));

        private static void SetSfx(bool down, FastAction<HLAudioTypes> value) =>
            typeof(HLButton).GetField(down ? "OnDownSfx" : "OnUpSfx", AnyStatic).SetValue(null, value);

        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }

        // Stands in for authored serialized values only.
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        private sealed class ActiveButtonFixture : IDisposable
        {
            private static readonly FieldInfo Registry = typeof(ProcessManager).GetField("s_systemDictionary", AnyStatic);
            private static readonly FieldInfo CoroutineHost = typeof(CoroutineUtils).GetField("s_instance", AnyStatic);
            private static readonly FieldInfo ShouldHighlightChanged = typeof(HLSelectable).GetField("m_onShouldHighlightChanged", AnyStatic);
            private static readonly FieldInfo SceneRows = typeof(ActiveSelectables).GetField("s_selectables", AnyStatic);
            private static readonly string[] ButtonSounds = { "OnClickSfx", "OnDisabledClickSfx", "OnDownSfx", "OnUpSfx" };

            private readonly object previousRegistry = Registry.GetValue(null);
            private readonly object previousHost = CoroutineHost.GetValue(null);
            private readonly object previousShouldHighlight = ShouldHighlightChanged.GetValue(null);
            private readonly FastAction<FastAction<bool>, Action<bool>> previousInitShouldHighlight = HLSelectable.InitShouldHighlight;
            private readonly Dictionary<string, object> previousSounds = new Dictionary<string, object>();
            private readonly List<UObject> allocations = new List<UObject>();
            private string sceneName;
            private bool hadSceneRow;
            public readonly EventSystem EventSystem;
            public readonly PlayModeInputBridgeProbe Probe;
            public readonly HLButton Button;

            public ActiveButtonFixture()
            {
                foreach (string name in ButtonSounds) previousSounds.Add(name, typeof(HLButton).GetField(name, AnyStatic).GetValue(null));
                try
                {
                    Assert.That(UnityEngine.EventSystems.EventSystem.current == null, Is.True, "fixture requires no foreign EventSystem");
                    Registry.SetValue(null, Activator.CreateInstance(Registry.FieldType));
                    CoroutineHost.SetValue(null, null);
                    ShouldHighlightChanged.SetValue(null, null);
                    HLSelectable.InitShouldHighlight = null;

                    Owner("Lucid owned coroutine host", true).AddComponent<CoroutineUtils>();
                    EventSystem = Owner("Lucid owned event system", true).AddComponent<EventSystem>();
                    Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(EventSystem));

                    Probe = ScriptableObject.CreateInstance<PlayModeInputBridgeProbe>();
                    allocations.Add(Probe);
                    // Native InstantClearState (0x1b72370) tail-calls Deselect; this fixture owns its real forwarding bridge.
                    var deselectionBridge = ScriptableObject.CreateInstance<HLInputBridge>();
                    allocations.Add(deselectionBridge);
                    var module = Owner("Lucid owned button deselection module", false).AddComponent<HLInputModule>();
                    deselectionBridge.SetInputModule(module);
                    Probe.DeselectionBridge = deselectionBridge;
                    GameObject configurationOwner = Owner("Lucid owned UI runtime configuration", false);
                    Set(configurationOwner.AddComponent<UIRuntimeConfiguration>(), "m_inputBridge", Probe);
                    configurationOwner.SetActive(true);

                    GameObject buttonOwner = new GameObject("Lucid owned active button", typeof(RectTransform));
                    allocations.Add(buttonOwner);
                    buttonOwner.SetActive(false);
                    sceneName = buttonOwner.scene.name;
                    hadSceneRow = ((IDictionary)SceneRows.GetValue(null)).Contains(sceneName);
                    Button = buttonOwner.AddComponent<HLButton>();
                    // Press/release curves are outside these routes; their
                    // coroutines would need a VibrationManager system.
                    Button.AnimationDisabled = true;
                    buttonOwner.SetActive(true);
                    Assert.That(Button.IsActive(), Is.True);
                    Assert.That(ActiveSelectables.GetSelectables().Contains(Button), Is.True);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private GameObject Owner(string name, bool active)
            {
                var owner = new GameObject(name);
                allocations.Add(owner);
                owner.SetActive(active);
                return owner;
            }

            public void Dispose()
            {
                try { DestroyAll(allocations.Count - 1); }
                finally
                {
                    try
                    {
                        var rows = (IDictionary)SceneRows.GetValue(null);
                        if (sceneName != null && !hadSceneRow && rows.Contains(sceneName) && ((IList)rows[sceneName]).Count == 0)
                            rows.Remove(sceneName);
                    }
                    finally
                    {
                        foreach (KeyValuePair<string, object> sound in previousSounds)
                            typeof(HLButton).GetField(sound.Key, AnyStatic).SetValue(null, sound.Value);
                        HLSelectable.InitShouldHighlight = previousInitShouldHighlight;
                        ShouldHighlightChanged.SetValue(null, previousShouldHighlight);
                        CoroutineHost.SetValue(null, previousHost);
                        Registry.SetValue(null, previousRegistry);
                    }
                }
            }

            private void DestroyAll(int index)
            {
                if (index < 0) return;
                try { if (allocations[index] != null) UObject.DestroyImmediate(allocations[index]); }
                finally { DestroyAll(index - 1); }
            }
        }
    }
}
