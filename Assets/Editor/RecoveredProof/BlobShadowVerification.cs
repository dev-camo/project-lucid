using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectLucid
{
    // Managed checks exercise declarations and the engine-free flag method.
    // Engine checks use owned Unity objects and real physics queries.
    public static class BlobShadowVerification
    {
        const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly Type T = typeof(BlobShadowRaycaster);
        static FieldInfo Field(string name) => T.GetField(name, Own) ?? throw new Exception(name);
        static void Set(object target, string name, object value) => Field(name).SetValue(target, value);
        static bool BoxedBoolean(CustomAttributeTypedArgument value) => (bool)(value.Value is CustomAttributeTypedArgument inner ? inner.Value : value.Value);
        static V Get<V>(object target, string name) => (V)Field(name).GetValue(target);
        static void Invoke(object target, string name) => T.GetMethod(name, Own).Invoke(target, null);
        static void Require(bool condition, string name, ref int count)
        { if (!condition) throw new Exception(name); count++; }
        public static int RunManaged()
        {
            int count = 0;
            string[] fields = { "m_gravityProxy", "m_raycastLayerMask", "m_projector", "m_normalDotThreshold", "m_updateInterval", "m_localPositionOnCharacterSwap", "m_cachedTransform", "m_layerMaskValue", "m_timer", "m_raycastHits", "m_hasGravityProxy", "m_enforceOnEnable" };
            Require(T.Assembly.GetName().Name == "Game.Runtime", "original assembly identity", ref count);
            Require(T.FullName == "HardlightProject.BlobShadowRaycaster", "original type identity", ref count);
            Require(T.BaseType == typeof(MonoBehaviour), "genuine engine base", ref count);
            Require(T.GetFields(Own).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(fields), "complete original twelve-field order", ref count);
            Require(T.GetFields(Own).All(f => f.IsPrivate), "original fields private", ref count);
            Require(T.GetMethods(Own).OrderBy(m => m.MetadataToken).Select(m => m.Name).SequenceEqual(new[] { "Awake", "Update", "UpdateForwardDirection", "EnforceLocalPosition", "OnEnable" }), "complete original five nonconstructor methods", ref count);
            Require(T.GetConstructors(Own).Length == 1 && T.GetConstructors(Own)[0].IsPublic && T.GetConstructors(Own)[0].GetParameters().Length == 0, "one original public constructor", ref count);
            Require(T.GetMethods(Own).Where(m => m.Name != "EnforceLocalPosition").All(m => m.IsPrivate), "original private lifecycle bodies", ref count);
            Require(T.GetMethod("EnforceLocalPosition", Own).IsPublic, "original public enforcement", ref count);
            Require(Field("m_projector").FieldType == typeof(DecalProjector), "genuine URP projector dependency", ref count);
            Require(Field("m_raycastHits").FieldType == typeof(RaycastHit[]), "genuine physics hit buffer", ref count);
            Require(T.GetFields(Own).Where(f => f.IsDefined(typeof(SerializeField), false)).Select(f => f.Name).SequenceEqual(fields.Take(6)), "exact six authored serialized fields", ref count);
            Require(Field("m_normalDotThreshold").GetCustomAttribute<TooltipAttribute>().tooltip == "If the dot between gravity and hit normal is greater than or equal to this value, then set this forward to the inverse of the normal.", "original tooltip", ref count);
            var options = T.GetCustomAttributesData();
            Require(options.Count == 2, "exact two original options", ref count);
            Require(Convert.ToInt32(options[0].ConstructorArguments[0].Value) == 2 && Convert.ToInt32(options[1].ConstructorArguments[0].Value) == 1, "array then null option order", ref count);
            Require(BoxedBoolean(options[0].ConstructorArguments[1]) == false && BoxedBoolean(options[1].ConstructorArguments[1]) == false, "original false options", ref count);
            // An uninitialized CLR instance is only used for the engine-free flag
            // method. It does not prove Unity construction, default values or lifecycle.
            object raw = FormatterServices.GetUninitializedObject(T);
            Set(raw, "m_timer", 2.5f);
            Require(!Get<bool>(raw, "m_enforceOnEnable"), "zeroed CLR flag before genuine method", ref count);
            ((BlobShadowRaycaster)raw).EnforceLocalPosition();
            Require(Get<bool>(raw, "m_enforceOnEnable"), "actual body schedules enforcement", ref count);
            Require(Get<float>(raw, "m_timer") == 2.5f, "schedule leaves timer untouched", ref count);
            Require(ReferenceEquals(Get<Transform>(raw, "m_cachedTransform"), null), "schedule performs no transform read", ref count);
            ((BlobShadowRaycaster)raw).EnforceLocalPosition();
            Require(Get<bool>(raw, "m_enforceOnEnable"), "repeated schedule retains sticky flag", ref count);
            return count;
        }
        public static int RunUnityEngine()
        {
            int count = 0;
            GameObject first = null, second = null, projectorObject = null, floor = null, gravityObject = null;
            try
            {
                first = new GameObject("Lucid blob shadow fixture"); first.SetActive(false);
                second = new GameObject("Lucid second blob shadow fixture"); second.SetActive(false);
                var shadow = (BlobShadowRaycaster)first.AddComponent(typeof(BlobShadowRaycaster));
                var other = (BlobShadowRaycaster)second.AddComponent(typeof(BlobShadowRaycaster));
                Require(Get<float>(shadow, "m_updateInterval") == 0.1f, "actual interval initializer", ref count);
                Require(Get<RaycastHit[]>(shadow, "m_raycastHits").Length == 1, "actual one-hit allocation", ref count);
                Require(!ReferenceEquals(Get<RaycastHit[]>(shadow, "m_raycastHits"), Get<RaycastHit[]>(other, "m_raycastHits")), "distinct owned hit buffers", ref count);
                Require(Get<float>(shadow, "m_normalDotThreshold") == 0f, "actual authored threshold default zero", ref count);
                Require(!Get<bool>(shadow, "m_enforceOnEnable"), "actual initial enforcement flag", ref count);
                Set(shadow, "m_raycastLayerMask", (LayerMask)(1 << 30));
                Invoke(shadow, "Awake");
                Require(Get<Transform>(shadow, "m_cachedTransform") == first.transform, "actual transform captured", ref count);
                Require(Get<int>(shadow, "m_layerMaskValue") == 1 << 30, "authored mask cached", ref count);
                Require(!Get<bool>(shadow, "m_hasGravityProxy"), "actual Unity-null cached proxy flag", ref count);
                first.transform.localPosition = new Vector3(1, 2, 3);
                Set(shadow, "m_localPositionOnCharacterSwap", new Vector3(4, 5, 6));
                Invoke(shadow, "OnEnable");
                Require(first.transform.localPosition == new Vector3(1, 2, 3), "unrequested enable leaves position", ref count);
                shadow.EnforceLocalPosition();
                Require(first.transform.localPosition == new Vector3(1, 2, 3), "request does not immediately move", ref count);
                Invoke(shadow, "OnEnable");
                Require(first.transform.localPosition == new Vector3(4, 5, 6), "next enable enforces authored position", ref count);
                first.transform.localPosition = new Vector3(7, 8, 9); Invoke(shadow, "OnEnable");
                Require(first.transform.localPosition == new Vector3(4, 5, 6) && Get<bool>(shadow, "m_enforceOnEnable"), "enforcement remains sticky", ref count);

                projectorObject = new GameObject("Lucid owned decal fixture"); projectorObject.SetActive(false);
                var projector = (DecalProjector)projectorObject.AddComponent(typeof(DecalProjector));
                projector.size = new Vector3(3, 3, 12); Set(shadow, "m_projector", projector);
                floor = new GameObject("Lucid owned shadow floor"); floor.layer = 30;
                floor.transform.position = new Vector3(9000, 9000, 9000);
                floor.transform.rotation = Quaternion.Euler(0, 0, 30);
                var collider = (BoxCollider)floor.AddComponent(typeof(BoxCollider)); collider.size = new Vector3(12, 0.2f, 12);
                first.transform.position = new Vector3(9000, 9004, 9000);
                Physics.SyncTransforms();
                Ray expectedRay = new Ray(first.transform.position - Vector3.down, Vector3.down);
                Require(Physics.Raycast(expectedRay, out RaycastHit expectedHit, 12f, 1 << 30, QueryTriggerInteraction.Ignore), "owned floor produces real physics hit", ref count);
                Set(shadow, "m_normalDotThreshold", 0.5f);
                first.transform.forward = Vector3.forward; Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, -expectedHit.normal) > 0.9999f, "actual accepted slope uses inverse hit normal", ref count);
                Set(shadow, "m_normalDotThreshold", 1.01f); first.transform.forward = Vector3.forward; Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, Vector3.down) > 0.9999f, "normal below ordered threshold falls back to gravity", ref count);
                Set(shadow, "m_normalDotThreshold", float.NaN); first.transform.forward = Vector3.forward; Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, Vector3.down) > 0.9999f, "unordered threshold falls back to gravity", ref count);
                Set(shadow, "m_normalDotThreshold", 0.5f); projector.size = new Vector3(3, 3, 0.5f);
                first.transform.forward = Vector3.forward; Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, Vector3.down) > 0.9999f, "actual projector z limits ray length", ref count);
                projector.size = new Vector3(3, 3, 12); collider.isTrigger = true; Physics.SyncTransforms();
                first.transform.forward = Vector3.forward; Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, Vector3.down) > 0.9999f, "trigger floor ignored", ref count);
                collider.isTrigger = false; Physics.SyncTransforms();
                gravityObject = new GameObject("Lucid owned gravity proxy");
                gravityObject.transform.rotation = Quaternion.Euler(0, 0, -90);
                Set(shadow, "m_gravityProxy", gravityObject.transform);
                Invoke(shadow, "Awake");
                Require(Get<bool>(shadow, "m_hasGravityProxy"), "real proxy presence cached", ref count);
                projector.size = new Vector3(3, 3, 0.1f); first.transform.forward = Vector3.forward;
                Invoke(shadow, "UpdateForwardDirection");
                Require(Vector3.Dot(first.transform.forward, -gravityObject.transform.up) > 0.9999f, "proxy selects genuine negative up", ref count);
                Set(shadow, "m_gravityProxy", null);
                Require(Get<bool>(shadow, "m_hasGravityProxy"), "later replacement does not recompute presence", ref count);
                Invoke(shadow, "Awake");

                Set(shadow, "m_timer", 1f); Set(shadow, "m_updateInterval", 0.1f); Invoke(shadow, "Update");
                Require(Get<float>(shadow, "m_timer") == 0f, "actual update resets after orientation work", ref count);
                Set(shadow, "m_timer", float.NaN); Invoke(shadow, "Update");
                Require(float.IsNaN(Get<float>(shadow, "m_timer")), "unordered timer does not reset", ref count);
                Set(shadow, "m_timer", 1f); Set(shadow, "m_updateInterval", float.NaN); Invoke(shadow, "Update");
                Require(Get<float>(shadow, "m_timer") >= 1f, "unordered interval does not reset", ref count);
                return count;
            }
            finally
            {
                try { if (gravityObject != null) UnityEngine.Object.DestroyImmediate(gravityObject); }
                finally { try { if (floor != null) UnityEngine.Object.DestroyImmediate(floor); }
                finally { try { if (projectorObject != null) UnityEngine.Object.DestroyImmediate(projectorObject); }
                finally { try { if (second != null) UnityEngine.Object.DestroyImmediate(second); }
                finally { if (first != null) UnityEngine.Object.DestroyImmediate(first); } } } }
            }
        }
    }
}
