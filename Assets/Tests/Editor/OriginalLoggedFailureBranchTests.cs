using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original failure branches that report through HLOutput.LogError and then
    // either return or keep going. Each case routes errors to an owned inactive
    // core so a silent variant of the branch is distinguishable.
    public sealed class OriginalLoggedFailureBranchTests
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;

        // CharacterRigLookup.TryGetReplacement 0x06001485, x86-64 0x76ca80. Neither
        // GameObject nor Component (0x76cb6c..0x76cbac) formats
        // '{0} is an unexpected type: {1}' with name and GetType (0x76cbb3..0x76cbca),
        // logs it and returns false (0x76cbea). A cached exact component missing from
        // the replacement cache formats '{0}, type "{1}" is not found in replacement
        // lookup' (0x76cd97) and also returns false. Catches a silent false on either
        // branch, or a missing component treated as success.
        [Test]
        public void RigReplacementLogsUnexpectedInputAndMissingReplacementComponent()
        {
            var owned = new List<Object>();
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    CharacterRigLookup original = Rig(owned, "Owned rig lookup original", out GameObject originalRoot);
                    CharacterRigLookup replacement = Rig(owned, "Owned rig lookup replacement", out GameObject replacementRoot);
                    BoxCollider collider = originalRoot.AddComponent<BoxCollider>();
                    original.Initialize();
                    replacement.Initialize();

                    var mesh = new Mesh { name = "Owned rig lookup mesh" };
                    owned.Add(mesh);
                    Assert.IsFalse(original.TryGetReplacement(mesh, replacement, out Object value));
                    Assert.IsNull(value);
                    CollectionAssert.AreEqual(new[] { "Owned rig lookup mesh is an unexpected type: UnityEngine.Mesh|0" }, capture.Errors);

                    capture.Errors.Clear();
                    Assert.IsFalse(original.TryGetReplacement(collider, replacement, out value));
                    Assert.IsNull(value);
                    CollectionAssert.AreEqual(new[] {
                        "Owned rig lookup original root, type \"UnityEngine.BoxCollider\" is not found in replacement lookup|0" }, capture.Errors);

                    // A present component still resolves without logging.
                    capture.Errors.Clear();
                    Assert.IsTrue(original.TryGetReplacement(originalRoot.transform, replacement, out value));
                    Assert.AreSame(replacementRoot.transform, value);
                    Assert.IsEmpty(capture.Errors);
                }
                finally
                {
                    for (int i = owned.Count - 1; i >= 0; --i)
                        if (owned[i] != null) Object.DestroyImmediate(owned[i]);
                }
            }
        }

        // Spline.SetSpline 0x060002f7, x86-64 0x1a75850. The old selection is read
        // first (0x1a758bd..0x1a758ee); an input that is not Editable, Inert or
        // Segmented logs 'Attempting to set unsupported spline type' (0x1a7597f) and
        // still falls through to the old provider's UpdateSplineType(input) interface
        // call (0x1a759e0..0x1a75a5c) with no field or discriminator write. Catches an
        // early return that skips the old-provider callback, a silent branch, or a
        // selection change on unsupported input.
        [Test]
        public void UnsupportedSplineLogsThenStillNotifiesTheUnchangedOldSelection()
        {
            GameObject facadeObject = null, editableObject = null;
            EditableSpline editable = null;
            Action<ISpline> observer = null;
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    facadeObject = Inactive("Owned unsupported spline facade");
                    editableObject = Inactive("Owned unsupported spline old selection");
                    Spline facade = facadeObject.AddComponent<Spline>();
                    editable = editableObject.AddComponent<EditableSpline>();
                    using (var serialized = new SerializedObject(facade))
                    {
                        serialized.FindProperty("m_editableSpline").objectReferenceValue = editable;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Assert.AreSame(editable, facade.GetSpline());

                    var unsupported = new UnsupportedSpline();
                    var received = new List<ISpline>();
                    int errorsAtCallback = -1;
                    ISpline selectedAtCallback = null;
                    observer = value =>
                    {
                        received.Add(value);
                        errorsAtCallback = capture.Errors.Count;
                        selectedAtCallback = facade.GetSpline();
                    };
                    editable.OnSplineTypeChanged += observer;

                    facade.SetSpline(unsupported);

                    CollectionAssert.AreEqual(new[] { "Attempting to set unsupported spline type|0" }, capture.Errors);
                    Assert.AreEqual(1, received.Count, "The old provider is still notified once.");
                    Assert.AreSame(unsupported, received[0]);
                    Assert.AreEqual(1, errorsAtCallback, "The error precedes the old-provider callback.");
                    Assert.AreSame(editable, selectedAtCallback);
                    Assert.AreSame(editable, facade.GetSpline());
                    using (var serialized = new SerializedObject(facade))
                    {
                        Assert.AreEqual(0, serialized.FindProperty("m_activeSplineType").intValue);
                        Assert.IsNull(serialized.FindProperty("m_inertSpline").objectReferenceValue);
                        Assert.IsNull(serialized.FindProperty("m_segmentedSpline").objectReferenceValue);
                    }
                }
                finally
                {
                    if (editable != null && observer != null) editable.OnSplineTypeChanged -= observer;
                    if (editableObject != null) Object.DestroyImmediate(editableObject);
                    if (facadeObject != null) Object.DestroyImmediate(facadeObject);
                }
            }
        }

        // ControlMapping.Subscribe(GameInput, Action<float>, InputTrigger, int)
        // 0x06000088, x86-64 0x1a1ae50. Held (0) subscribes static +0x50 buttonsHeld
        // and +0x68 axis; Down (1) +0x58/+0x70; Up (2) +0x60 buttonsUp and +0x78
        // axisEnd. Any other value formats "No subscription set for input trigger
        // '{0}'." (0x1a1b049..0x1a1b092) and then joins the common tail at 0x1a1aff0:
        // +0x80 swipe and +0x18 scroll wheel. Catches a return in the default case,
        // a silent default, or swapped Up/Held pairs.
        [Test]
        public void InvalidTriggerLogsYetSubscribesSwipeAndScrollWheelWhileUpAndHeldUseTheirPairs()
        {
            const int invalidJoystick = 87, upJoystick = 88, heldJoystick = 89;
            int[] owned = { invalidJoystick, upJoystick, heldJoystick };
            Dictionary<string, IDictionary> dictionaries = MappingDictionaries();
            foreach (KeyValuePair<string, IDictionary> pair in dictionaries)
                foreach (DictionaryEntry entry in pair.Value)
                    Assert.IsFalse(Array.IndexOf(owned, JoystickIndex(entry.Key)) >= 0,
                        "Fixture requires unused joystick keys in " + pair.Key);
            Action<float> invalid = _ => { }, up = _ => { }, held = _ => { };
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    ControlMapping.Subscribe(GameInput.UISubmit, invalid, (InputTrigger)99, invalidJoystick);
                    CollectionAssert.AreEqual(new[] { "No subscription set for input trigger '99'.|0" }, capture.Errors);
                    AssertSubscribed(dictionaries, invalidJoystick, invalid, "s_onSwipe", "s_onScrollWheel");

                    capture.Errors.Clear();
                    ControlMapping.Subscribe(GameInput.UISubmit, up, InputTrigger.Up, upJoystick);
                    AssertSubscribed(dictionaries, upJoystick, up, "s_onButtonsUp", "s_onAxisEnd", "s_onSwipe", "s_onScrollWheel");
                    ControlMapping.Subscribe(GameInput.UISubmit, held, InputTrigger.Held, heldJoystick);
                    AssertSubscribed(dictionaries, heldJoystick, held, "s_onButtonsHeld", "s_onAxis", "s_onSwipe", "s_onScrollWheel");
                    Assert.IsEmpty(capture.Errors);
                }
                finally
                {
                    // Remove only the owned joystick keys; foreign entries stay.
                    foreach (IDictionary dictionary in dictionaries.Values)
                    {
                        var remove = new List<object>();
                        foreach (DictionaryEntry entry in dictionary)
                            if (Array.IndexOf(owned, JoystickIndex(entry.Key)) >= 0) remove.Add(entry.Key);
                        foreach (object key in remove) dictionary.Remove(key);
                    }
                }
            }
        }

        private static void AssertSubscribed(Dictionary<string, IDictionary> dictionaries, int joystick, Action<float> callback, params string[] expected)
        {
            foreach (KeyValuePair<string, IDictionary> pair in dictionaries)
            {
                object value = null;
                foreach (DictionaryEntry entry in pair.Value)
                    if (JoystickIndex(entry.Key) == joystick) value = entry.Value;
                bool wanted = Array.IndexOf(expected, pair.Key) >= 0;
                if (!wanted)
                {
                    Assert.IsNull(value, pair.Key + " has no entry for joystick " + joystick);
                    continue;
                }
                Assert.IsNotNull(value, pair.Key + " has an entry for joystick " + joystick);
                var action = (FastAction<float>)value;
                CollectionAssert.AreEqual(new[] { callback }, action.GetInvocationList(), pair.Key);
            }
        }

        private static Dictionary<string, IDictionary> MappingDictionaries()
        {
            var result = new Dictionary<string, IDictionary>();
            foreach (string name in new[] {
                "s_onPointer", "s_onScrollWheel", "s_onTap", "s_onTouch", "s_onTouchRelease",
                "s_onMultiTouch", "s_onMultiTouchRelease", "s_onVectorisedGameInput",
                "s_onButtonsHeld", "s_onButtonsDown", "s_onButtonsUp", "s_onAxis",
                "s_onAxisStart", "s_onAxisEnd", "s_onSwipe" })
            {
                FieldInfo field = typeof(ControlMapping).GetField(name, StaticFields);
                Assert.IsNotNull(field, name);
                result.Add(name, (IDictionary)field.GetValue(null));
            }
            return result;
        }

        private static int JoystickIndex(object key) => (int)key.GetType().GetField("JoystickIndex").GetValue(key);

        private static GameObject Inactive(string name)
        {
            var value = new GameObject(name);
            value.SetActive(false);
            return value;
        }

        private static CharacterRigLookup Rig(List<Object> owned, string name, out GameObject root)
        {
            GameObject container = Inactive(name);
            owned.Add(container);
            root = new GameObject(name + " root");
            root.transform.SetParent(container.transform, false);
            var character = new GameObject(name + " character");
            character.transform.SetParent(container.transform, false);
            var mesh = new GameObject(name + " mesh");
            mesh.transform.SetParent(container.transform, false);
            CharacterRigLookup rig = container.AddComponent<CharacterRigLookup>();
            using (var serialized = new SerializedObject(rig))
            {
                serialized.FindProperty("m_characterRoot").objectReferenceValue = root;
                serialized.FindProperty("m_characterAnimator").objectReferenceValue = character.AddComponent<Animator>();
                serialized.FindProperty("m_meshAnimator").objectReferenceValue = mesh.AddComponent<Animator>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return rig;
        }

        // An ISpline that is none of the three genuine providers; SetSpline only
        // type-tests it and passes it to the old provider, so no member runs.
        private sealed class UnsupportedSpline : ISpline
        {
            private static Exception Unused() => new NotSupportedException("Unsupported spline fixture member");
            public LightweightTransform Transform => throw Unused();
            public SplineType SplineType => throw Unused();
            public int KnotCount => throw Unused();
            public ISplineKnot GetBackKnot() => throw Unused();
            public ISplineKnot GetHeadKnot() => throw Unused();
            public ISplineKnot GetKnot(int knotIndex) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw Unused();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public SplineLocation FindNearestSplineLocation(Vector3 worldPosition) => throw Unused();
            public SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward) => throw Unused();
            public SplineLocation GetSplineLocationFromDistance(float distance) => throw Unused();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => throw Unused();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT) => throw Unused();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => throw Unused();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT) => throw Unused();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => throw Unused();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw Unused();
            public LightweightTransform GetWorldTransformFromSplineT(float splineT) => throw Unused();
            public void KnotsUpdated() => throw Unused();
            public void UpdateSplineType(ISpline newSpline) => throw Unused();
            public event Action OnSplineKnotsUpdated { add => throw Unused(); remove => throw Unused(); }
            public event Action<ISpline> OnSplineTypeChanged { add => throw Unused(); remove => throw Unused(); }
            public DateTime GetTimestamp() => throw Unused();
            public MetadataGroups GetSurfaceMetadata() => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw Unused();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw Unused();
            public float Length => throw Unused();
            public float LengthInverse => throw Unused();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw Unused();
            public Vector3 TransformPoint(Vector3 point) => throw Unused();
            public Vector3 InverseTransformPoint(Vector3 point) => throw Unused();
            public bool IsActive() => throw Unused();
        }
    }
}
