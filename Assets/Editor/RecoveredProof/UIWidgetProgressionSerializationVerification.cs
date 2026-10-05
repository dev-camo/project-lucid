// Actual Unity only. This bounded proof covers the original three authored
// fields and two runtime fields; it loads no authored prefab/asset or UI graph.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight.UI.Binding;
using HardlightProject;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class UIWidgetProgressionSerializationVerification
    {
        private static readonly string[] Authored = { "m_animation", "m_animateInClip", "m_animateOutClip" };
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static int checks;

        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            ++checks;
        }

        private static FieldInfo Field(string name) => typeof(UIWidgetProgression).GetField(name, Declared);

        private static void CheckOriginalFields()
        {
            Type type = typeof(UIWidgetProgression);
            Check(type.Assembly.GetName().Name == "Game.Runtime", "original progression assembly");
            Check(type.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name)
                .SequenceEqual(Authored.Concat(new[] { "AnimateOutComplete", "MaskedTexture" })), "exact original five-field order");
            foreach (string name in Authored)
            {
                FieldInfo field = Field(name);
                Check(field != null && field.Attributes == FieldAttributes.Private, name + " original private-only flags");
                Check(field.GetCustomAttributes(false).Length == 1 && field.IsDefined(typeof(SerializeField), false)
                    && !field.IsNotSerialized && !field.IsDefined(typeof(SerializeReference), false), name + " only original SerializeField attribute");
            }
            Check(Field("m_animation").FieldType == typeof(Animation), "genuine Animation field type");
            Check(Field("m_animateInClip").FieldType == typeof(AnimationClip) && Field("m_animateOutClip").FieldType == typeof(AnimationClip), "genuine AnimationClip field types");
            FieldInfo callback = Field("AnimateOutComplete"), binder = Field("MaskedTexture");
            Check(callback.Attributes == FieldAttributes.Public && callback.FieldType == typeof(Action<UIWidgetProgression>), "original callback public-only flags and closed Action type");
            Check(callback.GetCustomAttributes(false).Length == 0 && !callback.IsNotSerialized, "callback has no added NonSerialized/serialization attributes");
            Check(callback.FieldType.BaseType == typeof(MulticastDelegate) && typeof(Delegate).IsAssignableFrom(callback.FieldType), "real loaded delegate ancestry");
            Check(binder.Attributes == (FieldAttributes.Public | FieldAttributes.InitOnly) && binder.FieldType == typeof(Bindable<Texture>), "original readonly binder flags and genuine generic type");
            Check(binder.GetCustomAttributes(false).Length == 0 && !binder.IsNotSerialized, "binder has no added serialization attributes");
            Check(typeof(Bindable<Texture>).Assembly.GetName().Name == "HLBinders.Runtime", "original binder assembly");
        }

        private static void SetAuthored(SerializedObject data, Animation animation, AnimationClip incoming, AnimationClip outgoing)
        {
            UnityEngine.Object[] objects = { animation, incoming, outgoing };
            for (int i = 0; i < Authored.Length; ++i)
            {
                SerializedProperty property = data.FindProperty(Authored[i]);
                Check(property != null && property.propertyType == SerializedPropertyType.ObjectReference, "real serialized engine reference: " + Authored[i]);
                property.objectReferenceValue = objects[i];
            }
            Check(data.ApplyModifiedPropertiesWithoutUndo(), "real authored-reference change applied");
            data.Update();
        }

        private static void CheckAuthored(UIWidgetProgression widget, SerializedObject data, Animation animation, AnimationClip incoming, AnimationClip outgoing)
        {
            UnityEngine.Object[] objects = { animation, incoming, outgoing };
            data.Update();
            for (int i = 0; i < Authored.Length; ++i)
            {
                UnityEngine.Object expected = objects[i];
                var actual = (UnityEngine.Object)Field(Authored[i]).GetValue(widget);
                // Unity may materialize an empty serialized reference as a managed
                // wrapper with instanceID zero. Require genuine engine null, while
                // retaining exact managed identity for populated references.
                if (expected == null && !ReferenceEquals(actual, null))
                    Debug.Log("Project Lucid empty " + Authored[i] + " wrapper=" + actual.GetType().FullName
                        + "; instanceID=" + actual.GetInstanceID() + "; engineNull=" + (actual == null));
                Check(expected == null ? actual == null && (ReferenceEquals(actual, null) || actual.GetInstanceID() == 0)
                    : ReferenceEquals(actual, expected), "exact component field engine identity: " + Authored[i]);
                SerializedProperty property = data.FindProperty(Authored[i]);
                Check(property != null && property.propertyType == SerializedPropertyType.ObjectReference
                    && (expected == null ? property.objectReferenceValue == null : ReferenceEquals(property.objectReferenceValue, expected))
                    && property.objectReferenceInstanceIDValue == (expected == null ? 0 : expected.GetInstanceID()), "exact SerializedObject reference/instanceID: " + Authored[i]);
            }
        }

        private static void CheckVisible(SerializedObject data)
        {
            var names = new HashSet<string>();
            SerializedProperty property = data.GetIterator();
            bool descend = true;
            while (property.NextVisible(descend)) { names.Add(property.name); descend = false; }
            foreach (string name in Authored) Check(names.Contains(name), "authored field appears in actual visible tree: " + name);
            Check(!names.Contains("AnimateOutComplete") && !names.Contains("MaskedTexture"), "runtime delegate/readonly binder absent from actual visible tree");
        }

        private static void CheckRuntimeExcluded(SerializedObject data)
        {
            data.Update();
            Check(data.FindProperty("AnimateOutComplete") == null && data.FindProperty("MaskedTexture") == null,
                "actual Unity excludes original callback and readonly binder without added attributes");
        }

        private static void CheckJson(string json)
        {
            foreach (string name in Authored) Check(json.Contains("\"" + name + "\":"), "JsonUtility retains authored field: " + name);
            Check(!json.Contains("\"AnimateOutComplete\"") && !json.Contains("\"MaskedTexture\""), "JsonUtility excludes runtime callback and readonly binder");
        }

        public static int Run()
        {
            checks = 0;
            GameObject sourceOwner = null, targetOwner = null;
            AnimationClip incoming = null, outgoing = null, priorIncoming = null, priorOutgoing = null;
            Texture2D sourceTexture = null, targetTexture = null;
            try
            {
                CheckOriginalFields();
                sourceOwner = new GameObject("Project Lucid progression source serialization proof");
                targetOwner = new GameObject("Project Lucid progression target serialization proof");
                UIWidgetProgression source = sourceOwner.AddComponent<UIWidgetProgression>();
                UIWidgetProgression target = targetOwner.AddComponent<UIWidgetProgression>();
                Check(ReferenceEquals(source.gameObject, sourceOwner) && ReferenceEquals(target.gameObject, targetOwner), "genuine components on distinct owned GameObjects");
                Bindable<Texture> sourceBinder = source.MaskedTexture, targetBinder = target.MaskedTexture;
                Check(sourceBinder != null && targetBinder != null && !ReferenceEquals(sourceBinder, targetBinder)
                    && sourceBinder.GetType() == typeof(Bindable<Texture>) && targetBinder.GetType() == typeof(Bindable<Texture>), "each real constructor creates its own original binder");
                Check(sourceBinder.Value == null && targetBinder.Value == null && source.AnimateOutComplete == null && target.AnimateOutComplete == null, "constructor runtime defaults");
                Check(Authored.All(name => Field(name).GetValue(source) == null && Field(name).GetValue(target) == null), "constructor authored-reference defaults");

                Animation sourceAnimation = sourceOwner.AddComponent<Animation>();
                Animation targetAnimation = targetOwner.AddComponent<Animation>();
                incoming = new AnimationClip { name = "ProjectLucidSerializationIncoming", legacy = true };
                outgoing = new AnimationClip { name = "ProjectLucidSerializationOutgoing", legacy = true };
                priorIncoming = new AnimationClip { name = "ProjectLucidSerializationPriorIncoming", legacy = true };
                priorOutgoing = new AnimationClip { name = "ProjectLucidSerializationPriorOutgoing", legacy = true };
                sourceAnimation.AddClip(incoming, incoming.name);
                sourceAnimation.AddClip(outgoing, outgoing.name);
                targetAnimation.AddClip(priorIncoming, priorIncoming.name);
                targetAnimation.AddClip(priorOutgoing, priorOutgoing.name);
                Check(ReferenceEquals(sourceAnimation.GetClip(incoming.name), incoming) && ReferenceEquals(sourceAnimation.GetClip(outgoing.name), outgoing)
                    && ReferenceEquals(targetAnimation.GetClip(priorIncoming.name), priorIncoming) && ReferenceEquals(targetAnimation.GetClip(priorOutgoing.name), priorOutgoing), "real Animation components retain their real legacy clips without playing");

                int sourceInstance = source.GetInstanceID(), targetInstance = target.GetInstanceID();
                Transform sourceTransform = source.transform, targetTransform = target.transform;
                sourceTexture = new Texture2D(2, 2) { name = "ProjectLucidSerializationSourceTexture" };
                targetTexture = new Texture2D(2, 2) { name = "ProjectLucidSerializationTargetTexture" };
                int sourceChanges = 0, targetChanges = 0, sourceCalls = 0, targetCalls = 0;
                UIWidgetProgression sourceArgument = null, targetArgument = null;
                sourceBinder.AddListener(() => ++sourceChanges);
                targetBinder.AddListener(() => ++targetChanges);
                source.SetMaskedImage(sourceTexture);
                target.SetMaskedImage(targetTexture);
                Check(ReferenceEquals(sourceBinder.Value, sourceTexture) && ReferenceEquals(targetBinder.Value, targetTexture)
                    && sourceChanges == 1 && targetChanges == 1, "genuine runtime binder texture values/listeners initialized independently");
                source.AnimateOutComplete = value => { ++sourceCalls; sourceArgument = value; };
                target.AnimateOutComplete = value => { ++targetCalls; targetArgument = value; };
                Action<UIWidgetProgression> sourceCallback = source.AnimateOutComplete, targetCallback = target.AnimateOutComplete;

                using (var sourceData = new SerializedObject(source))
                using (var targetData = new SerializedObject(target))
                {
                    SetAuthored(sourceData, sourceAnimation, incoming, outgoing);
                    SetAuthored(targetData, targetAnimation, priorIncoming, priorOutgoing);
                    CheckAuthored(source, sourceData, sourceAnimation, incoming, outgoing);
                    CheckAuthored(target, targetData, targetAnimation, priorIncoming, priorOutgoing);
                    CheckVisible(sourceData);
                    CheckVisible(targetData);
                    CheckRuntimeExcluded(sourceData);
                    CheckRuntimeExcluded(targetData);
                    UnityEngine.Object sourceScript = sourceData.FindProperty("m_Script").objectReferenceValue;
                    UnityEngine.Object targetScript = targetData.FindProperty("m_Script").objectReferenceValue;
                    Check(sourceScript != null && ReferenceEquals(sourceScript, targetScript)
                        && ((MonoScript)sourceScript).GetClass() == typeof(UIWidgetProgression), "real original concrete MonoScript identity");

                    string json = JsonUtility.ToJson(source);
                    CheckJson(json);
                    Check(sourceCalls == 0 && targetCalls == 0 && sourceChanges == 1 && targetChanges == 1, "serialization does not invoke runtime delegates or binder listeners");
                    JsonUtility.FromJsonOverwrite(json, target);
                    CheckAuthored(target, targetData, sourceAnimation, incoming, outgoing);
                    CheckAuthored(source, sourceData, sourceAnimation, incoming, outgoing);
                    Check(source.GetInstanceID() == sourceInstance && target.GetInstanceID() == targetInstance, "JSON overwrite retains component identities");
                    Check(ReferenceEquals(source.gameObject, sourceOwner) && ReferenceEquals(target.gameObject, targetOwner)
                        && ReferenceEquals(source.transform, sourceTransform) && ReferenceEquals(target.transform, targetTransform), "JSON overwrite retains real owning GameObject/Transform references");
                    Check(ReferenceEquals(sourceData.FindProperty("m_Script").objectReferenceValue, sourceScript)
                        && ReferenceEquals(targetData.FindProperty("m_Script").objectReferenceValue, targetScript), "JSON overwrite retains actual script references");
                    Check(ReferenceEquals(source.MaskedTexture, sourceBinder) && ReferenceEquals(target.MaskedTexture, targetBinder)
                        && ReferenceEquals(sourceBinder.Value, sourceTexture) && ReferenceEquals(targetBinder.Value, targetTexture), "overwrite preserves each constructor binder and its own runtime texture");
                    Check(ReferenceEquals(source.AnimateOutComplete, sourceCallback) && ReferenceEquals(target.AnimateOutComplete, targetCallback), "overwrite preserves independent runtime callback identities");
                    Check(sourceCalls == 0 && targetCalls == 0 && sourceChanges == 1 && targetChanges == 1, "overwrite invokes no runtime callbacks or binder changes");
                    CheckRuntimeExcluded(targetData);

                    source.Action_AnimateOutComplete();
                    target.Action_AnimateOutComplete();
                    Check(sourceCalls == 1 && targetCalls == 1, "original callbacks remain callable after overwrite");
                    Check(ReferenceEquals(sourceArgument, source) && ReferenceEquals(targetArgument, target), "runtime callbacks receive their own real component identities");
                    sourceBinder.MarkChanged();
                    targetBinder.MarkChanged();
                    Check(sourceChanges == 2 && targetChanges == 2, "original constructor-binder listeners survive overwrite");

                    SetAuthored(sourceData, null, null, null);
                    source.AnimateOutComplete = null;
                    CheckRuntimeExcluded(sourceData);
                    string nullJson = JsonUtility.ToJson(source);
                    CheckJson(nullJson);
                    JsonUtility.FromJsonOverwrite(nullJson, target);
                    CheckAuthored(source, sourceData, null, null, null);
                    CheckAuthored(target, targetData, null, null, null);
                    Check(target.GetInstanceID() == targetInstance && ReferenceEquals(target.gameObject, targetOwner)
                        && ReferenceEquals(target.transform, targetTransform) && ReferenceEquals(targetData.FindProperty("m_Script").objectReferenceValue, targetScript), "null-reference overwrite retains engine component/owner/script identities");
                    Check(ReferenceEquals(source.MaskedTexture, sourceBinder) && ReferenceEquals(target.MaskedTexture, targetBinder)
                        && ReferenceEquals(sourceBinder.Value, sourceTexture) && ReferenceEquals(targetBinder.Value, targetTexture)
                        && source.AnimateOutComplete == null && ReferenceEquals(target.AnimateOutComplete, targetCallback), "null authored overwrite excludes binder textures and null source callback");
                    Check(sourceCalls == 1 && targetCalls == 1 && sourceChanges == 2 && targetChanges == 2, "null-reference overwrite invokes no runtime callbacks/listeners");
                    CheckRuntimeExcluded(targetData);
                    source.Action_AnimateOutComplete();
                    target.Action_AnimateOutComplete();
                    Check(sourceCalls == 1 && targetCalls == 2 && ReferenceEquals(targetArgument, target), "null source delegate is not copied over target runtime callback");
                    sourceBinder.MarkChanged();
                    targetBinder.MarkChanged();
                    Check(sourceChanges == 3 && targetChanges == 3, "binder listener identity survives both authored overwrites");
                }
            }
            finally
            {
                if (sourceOwner != null) UnityEngine.Object.DestroyImmediate(sourceOwner);
                if (targetOwner != null) UnityEngine.Object.DestroyImmediate(targetOwner);
                if (incoming != null) UnityEngine.Object.DestroyImmediate(incoming);
                if (outgoing != null) UnityEngine.Object.DestroyImmediate(outgoing);
                if (priorIncoming != null) UnityEngine.Object.DestroyImmediate(priorIncoming);
                if (priorOutgoing != null) UnityEngine.Object.DestroyImmediate(priorOutgoing);
                if (sourceTexture != null) UnityEngine.Object.DestroyImmediate(sourceTexture);
                if (targetTexture != null) UnityEngine.Object.DestroyImmediate(targetTexture);
            }
            Check(sourceOwner == null && targetOwner == null && incoming == null && outgoing == null
                && priorIncoming == null && priorOutgoing == null && sourceTexture == null && targetTexture == null, "all owned engine fixtures destroyed in finally");
            Debug.Log("Project Lucid original progression serialization checks=" + checks + "; isolated real engine references, no authored UI binding/parity claim");
            return checks;
        }
    }
}
