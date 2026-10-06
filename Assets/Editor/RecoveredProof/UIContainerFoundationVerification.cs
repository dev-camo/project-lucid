using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UObject = UnityEngine.Object;

namespace ProjectLucid
{
    public static class UIContainerFoundationVerification
    {
        private static int checks;
        private sealed class ReferenceParameters : IUIContainerParameters { }
        private sealed class OtherReferenceParameters : IUIContainerParameters { }
        private struct ValueParameters : IUIContainerParameters { public int Value; }
        private static void Check(bool value, string label) { if (!value) throw new Exception("UI container foundation verification: " + label); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void SetIdentifier(UIContainerIdentifier id, string name, object value) => Field(typeof(UIContainerIdentifier), name).SetValue(id, value);
        private static T ReadIdentifier<T>(UIContainerIdentifier id, string name) => (T)Field(typeof(UIContainerIdentifier), name).GetValue(id);
        private static void SetContainer(UIContainer container, string name, object value) => Field(typeof(UIContainer), name).SetValue(container, value);
        private static void Fail<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + label);
        }
        private static void Options(Type type, params Option[] order)
        {
            var attrs = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(attrs.Select(a => a.Option).SequenceEqual(order) && attrs.All(a => Equals(a.Value, false)), type.Name + " exact ordered original IL2CPP options");
        }
        private static void Declarations()
        {
            Check((int)typeof(UIContainer).Attributes == 1048705 && typeof(UIContainer).BaseType == typeof(MonoBehaviour), "complete original abstract MonoBehaviour base and flags");
            Check((int)typeof(UIContainerIdentifier).Attributes == 1048833 && typeof(UIContainerIdentifier).BaseType == typeof(ScriptableObjectWithGuid), "original sealed identifier and genuine GUID base");
            Check((int)typeof(UIVisibilityGroupDefinition).Attributes == 1048577 && typeof(UIVisibilityGroupDefinition).BaseType == typeof(ScriptableObjectWithGuid), "original visibility definition flags and genuine GUID base");
            Check((int)typeof(UIContainerSimple).Attributes == 1048833 && typeof(UIContainerSimple).BaseType == typeof(UIContainer), "genuine original concrete container, no fixture runtime subclass");
            Options(typeof(UIContainer), Option.NullChecks, Option.ArrayBoundsChecks); Options(typeof(UIContainerIdentifier), Option.ArrayBoundsChecks, Option.NullChecks); Options(typeof(UIVisibilityGroupDefinition), Option.ArrayBoundsChecks, Option.NullChecks); Options(typeof(UIContainerSimple), Option.NullChecks, Option.ArrayBoundsChecks);
            Check(typeof(UIContainer).GetCustomAttributes<DisallowMultipleComponent>(false).Count() == 1, "original inherited component multiplicity attribute");
            foreach (var row in new[] { new { Type = typeof(UIContainerIdentifier), File = "UIContainerIdentifier", Menu = "Hardlight/HLModernUI/UIContainerIdentifier", Order = 0 }, new { Type = typeof(UIVisibilityGroupDefinition), File = "UIVisibilityGroupDefinition", Menu = "Hardlight/HLModernUI/UIVisibilityGroupDefinition", Order = 2 } })
            {
                var attr = row.Type.GetCustomAttribute<CreateAssetMenuAttribute>(); Check(attr != null && attr.fileName == row.File && attr.menuName == row.Menu && attr.order == row.Order, row.Type.Name + " original asset menu");
            }
            var containerFields = typeof(UIContainer).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(containerFields.Select(f => f.Name).SequenceEqual(new[] { "m_identifier", "m_behaviour", "m_canvas" }) && containerFields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(UIContainerIdentifier), typeof(UIContainerBehaviour), typeof(Canvas) }), "original full ordered container fields");
            Check(containerFields.All(f => f.IsDefined(typeof(SerializeField), false) && !f.IsInitOnly) && containerFields[0].IsFamily && containerFields[1].IsPrivate && containerFields[2].IsFamily, "original serialized field mutability/access");
            var identifierFields = typeof(UIContainerIdentifier).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(identifierFields.Select(f => f.Name).SequenceEqual(new[] { "m_name", "m_prefab", "m_uiContainerAsset", "m_loadedUIContainer", "m_operationHandle", "m_refCount" }) && identifierFields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(string), typeof(UIContainer), typeof(AssetReferenceT<GameObject>), typeof(UIContainer), typeof(AsyncOperationHandle<GameObject>), typeof(int) }), "complete identifier six-field original graph and order");
            Check(identifierFields.All(f => f.IsPrivate && !f.IsInitOnly) && identifierFields.Take(3).All(f => f.IsDefined(typeof(SerializeField), false) && !f.IsNotSerialized) && identifierFields.Skip(3).All(f => f.IsNotSerialized && !f.IsDefined(typeof(SerializeField), false)), "exact original serialized/nonserialized identifier partition");
            Check(Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").IsPrivate && Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").IsDefined(typeof(SerializeField), false), "original sole visibility bool field");
            Check(typeof(UIContainerSimple).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "complete genuine simple container has no own fields");
            Check((int)typeof(IUIContainerParameters).Attributes == 161 && typeof(IUIContainerParameters).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0 && typeof(IUIContainerParameters).GetInterfaces().Length == 0, "genuine empty marker interface, no fabricated members or base interfaces");
            Check((int)typeof(IUIScreenTransition).Attributes == 161 && typeof(IUIScreenTransition).GetInterfaces().Length == 0, "genuine transition interface flags and no base interface");
            var contracts = typeof(IUIScreenTransition).GetMethods().OrderBy(m => m.MetadataToken).ToArray();
            Check(contracts.Select(m => m.Name).SequenceEqual(new[] { "StartTransition", "ContinueTransition", "get_HasReachedMidpoint" }) && contracts.All(m => m.IsAbstract && m.IsVirtual), "original three ordered transition contracts, zero method body credit");
            Check(contracts[0].GetParameters().Select(x => x.Name).SequenceEqual(new[] { "onReachMidpointCallback", "onCompletionCallback" }) && contracts[0].GetParameters().All(x => x.ParameterType == typeof(Action)) && contracts[1].GetParameters().Length == 0 && contracts[2].ReturnType == typeof(bool), "original transition names and complete callback signatures");
            var generic = typeof(IUIContainerParametersExtensions).GetMethod("GetAs").GetGenericArguments().Single();
            Check((int)typeof(IUIContainerParametersExtensions).Attributes == 1048961 && generic.GenericParameterAttributes == GenericParameterAttributes.None && generic.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(IUIContainerParameters) }), "original generic marker-only constraint, value types permitted");
            Check(typeof(IUIContainerParametersExtensions).GetMethod("GetAs").GetParameters().Single().Name == "parameters" && typeof(IUIContainerParametersExtensions).IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false) && typeof(IUIContainerParametersExtensions).GetMethod("GetAs").IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false), "original extension metadata and parameter name");
            Check(Enum.GetUnderlyingType(typeof(UIContainerBehaviour)) == typeof(int) && typeof(UIContainerBehaviour).GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "None", "Debug", "Free", "Linked", "Single", "Stack", "Transition" }), "complete original I4 enum declaration order");
            Check(new[] { (int)UIContainerBehaviour.None, (int)UIContainerBehaviour.Debug, (int)UIContainerBehaviour.Free, (int)UIContainerBehaviour.Linked, (int)UIContainerBehaviour.Single, (int)UIContainerBehaviour.Stack, (int)UIContainerBehaviour.Transition }.SequenceEqual(new[] { 0, 1678670568, -2075864189, -1728062059, -979361952, 1224951333, 58017178 }), "original authored behaviour hashes, no enum body credit");
        }
        public static int RunManaged()
        {
            checks = 0; Declarations();
            var reference = new ReferenceParameters(); IUIContainerParameters boxed = new ValueParameters { Value = 37 };
            Check(ReferenceEquals(reference.GetAs<ReferenceParameters>(), reference), "native generic reference cast preserves exact object");
            Check(((IUIContainerParameters)null).GetAs<ReferenceParameters>() == null, "native reference cast null returns null");
            Fail<InvalidCastException>(() => reference.GetAs<OtherReferenceParameters>(), "native wrong reference cast");
            Check(boxed.GetAs<ValueParameters>().Value == 37, "native fully shared UnBox_Any returns full value");
            Fail<InvalidCastException>(() => reference.GetAs<ValueParameters>(), "native wrong boxed value cast");
            Fail<NullReferenceException>(() => ((IUIContainerParameters)null).GetAs<ValueParameters>(), "CLR null unbox boundary, native memory fault parity not claimed");
            Check(ReferenceEquals(reference.GetAs<IUIContainerParameters>(), reference), "genuine marker-interface T satisfies complete constraint");
            var id = (UIContainerIdentifier)FormatterServices.GetUninitializedObject(typeof(UIContainerIdentifier));
            var container = (UIContainerSimple)FormatterServices.GetUninitializedObject(typeof(UIContainerSimple));
            SetIdentifier(id, "m_name", "own-name"); Check(id.Name == "own-name", "original own name field without Unity object name lookup");
            Check(ReferenceEquals(id.LoadedUIContainer, null) && ReadIdentifier<int>(id, "m_refCount") == 0, "CLR uninitialized own storage only, no engine constructor assertion");
            id.RegisterContainer(container); Check(ReferenceEquals(id.LoadedUIContainer, container) && ReadIdentifier<int>(id, "m_refCount") == 1, "register stores original object before count increment");
            id.RegisterContainer(null); Check(ReferenceEquals(id.LoadedUIContainer, null) && ReadIdentifier<int>(id, "m_refCount") == 2, "register null still acquires reference");
            SetIdentifier(id, "m_loadedUIContainer", container); id.UnregisterContainer(null); Check(ReferenceEquals(id.LoadedUIContainer, null) && ReadIdentifier<int>(id, "m_refCount") == 1, "unregister ignores passed object and clears loaded field");
            id.UnregisterContainer(container); Check(ReadIdentifier<int>(id, "m_refCount") == 0, "unregister decrements count");
            id.UnregisterContainer(container); Check(ReadIdentifier<int>(id, "m_refCount") == -1, "unregister leaves negative count rather than clamp");
            SetIdentifier(id, "m_refCount", int.MaxValue); id.RegisterContainer(null); Check(ReadIdentifier<int>(id, "m_refCount") == int.MinValue, "native unchecked increment wraps");
            id.UnregisterContainer(null); Check(ReadIdentifier<int>(id, "m_refCount") == int.MaxValue, "native unchecked decrement wraps");
            SetIdentifier(id, "m_refCount", 2); SetIdentifier(id, "m_loadedUIContainer", container); id.Close(); Check(ReadIdentifier<int>(id, "m_refCount") == 1 && ReferenceEquals(id.LoadedUIContainer, container), "close positive remainder keeps loaded object and returns before handle access");
            id.Close(); Check(ReadIdentifier<int>(id, "m_refCount") == 0 && ReferenceEquals(id.LoadedUIContainer, null), "close zero clears object then clamps count with default invalid handle");
            id.Close(); Check(ReadIdentifier<int>(id, "m_refCount") == 0, "close negative route clamps count");
            SetIdentifier(id, "m_refCount", int.MinValue); SetIdentifier(id, "m_loadedUIContainer", container); id.Close(); Check(ReadIdentifier<int>(id, "m_refCount") == int.MaxValue && ReferenceEquals(id.LoadedUIContainer, container), "close unchecked minimum decrement wraps onto early return");
            var invalidHandle = new AsyncOperationHandle<GameObject>(); SetIdentifier(id, "m_operationHandle", invalidHandle); SetIdentifier(id, "m_refCount", -9); id.Close(); Check(ReadIdentifier<AsyncOperationHandle<GameObject>>(id, "m_operationHandle").Equals(invalidHandle), "close retains original invalid handle field after validation");
            id.RegisterContainer(null); Check(ReadIdentifier<int>(id, "m_refCount") == 1, "registration remains usable after close");
            Check(ReferenceEquals(UIContainerIdentifier.FindByName(null), null) && ReferenceEquals(UIContainerIdentifier.FindByName(""), null), "genuine ObjectUtils null/empty boundary before engine resource lookup");
            var definition = (UIVisibilityGroupDefinition)FormatterServices.GetUninitializedObject(typeof(UIVisibilityGroupDefinition)); Check(!definition.InitialVisibility, "own default bool false in uninitialized storage"); Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").SetValue(definition, true); Check(definition.InitialVisibility, "own initial visibility getter retains exact field");
            return checks;
        }
        public static void Run()
        {
            int managed = RunManaged(); Engine(); Debug.Log("PASS genuine UI container foundation checks=" + checks + "; managed=" + managed + "; owned Canvas fixtures only, no authored Addressables/UI manager/App approval.");
        }
        private static void Engine()
        {
            UIContainerIdentifier id = null, copy = null; UIVisibilityGroupDefinition visibility = null; GameObject owner = null, child = null, cameraOwner = null, otherOwner = null; Canvas detached = null;
            try
            {
                id = ScriptableObject.CreateInstance<UIContainerIdentifier>(); copy = ScriptableObject.CreateInstance<UIContainerIdentifier>(); visibility = ScriptableObject.CreateInstance<UIVisibilityGroupDefinition>(); owner = new GameObject("Lucid owned container " + Guid.NewGuid().ToString("N")); owner.SetActive(false); child = new GameObject("owned child canvas"); child.transform.SetParent(owner.transform); cameraOwner = new GameObject("owned camera"); otherOwner = new GameObject("owned alternative canvas");
                var container = owner.AddComponent<UIContainerSimple>(); var canvas = owner.AddComponent<Canvas>(); var childCanvas = child.AddComponent<Canvas>(); detached = otherOwner.AddComponent<Canvas>(); var camera = cameraOwner.AddComponent<Camera>();
                Check(container != null && id != null && copy != null && visibility != null, "genuine engine creates original complete types");
                Check(!visibility.InitialVisibility && ReadIdentifier<int>(id, "m_refCount") == 0 && ReferenceEquals(id.LoadedUIContainer, null), "original constructors own bool/count/object defaults");
                SetContainer(container, "m_identifier", id); SetContainer(container, "m_behaviour", UIContainerBehaviour.Stack); Check(ReferenceEquals(container.Identifier, id) && container.Behaviour == UIContainerBehaviour.Stack, "true engine component original field getters");
                SetContainer(container, "m_canvas", null); container.AssignCamera(camera); container.SetEnabled(false); Check(canvas.enabled, "missing canvas routes return without mutating actual canvas");
                typeof(UIContainer).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(container, null); Check(ReferenceEquals(Field(typeof(UIContainer), "m_canvas").GetValue(container), canvas), "OnValidate discovers same GameObject canvas rather than child");
                container.AssignCamera(camera); Check(ReferenceEquals(canvas.worldCamera, camera), "native worldCamera assignment on genuine Canvas");
                container.SetEnabled(false); Check(!canvas.enabled && !owner.activeSelf && childCanvas.enabled, "SetEnabled controls only Canvas.enabled and preserves GameObject activity/child canvas");
                container.SetEnabled(true); Check(canvas.enabled && !owner.activeSelf, "SetEnabled true leaves inactive owner unchanged");
                SetContainer(container, "m_canvas", detached); typeof(UIContainer).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(container, null); Check(ReferenceEquals(Field(typeof(UIContainer), "m_canvas").GetValue(container), detached), "assigned live canvas wins validation without local replacement");
                UObject.DestroyImmediate(detached); container.AssignCamera(camera); container.SetEnabled(false); Check(canvas.enabled, "destroyed original canvas uses genuine Unity null guard");
                typeof(UIContainer).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(container, null); Check(ReferenceEquals(Field(typeof(UIContainer), "m_canvas").GetValue(container), canvas), "destroyed field is replaced by genuine local component");
                container.Setup(null); Check(container.Identifier == id && container.Behaviour == UIContainerBehaviour.Stack, "genuine native RET setup does not change state");
                id.RegisterContainer(container); Check(id.IsLoaded && ReferenceEquals(id.Prefab, container) && ReadIdentifier<int>(id, "m_refCount") == 1, "Prefab loaded route does not acquire another reference");
                Check(ReferenceEquals(id.GetOrCreateContainer(), container) && ReadIdentifier<int>(id, "m_refCount") == 2, "GetOrCreate loaded route acquires before return");
                id.Close(); Check(id.IsLoaded && ReadIdentifier<int>(id, "m_refCount") == 1, "close retains actual loaded component while references remain");
                id.Close(); Check(!id.IsLoaded && ReferenceEquals(id.LoadedUIContainer, null) && ReadIdentifier<int>(id, "m_refCount") == 0, "actual close invalid-handle route clears loaded object");
                id.name = "Lucid identifier owned lookup " + Guid.NewGuid().ToString("N"); SetIdentifier(id, "m_name", "original own label"); Check(ReferenceEquals(UIContainerIdentifier.FindByName(id.name), id) && id.Name == "original own label", "genuine resource lookup uses Object.name while own Name remains independent");
                Check(UIContainerIdentifier.FindByName(id.name.ToUpperInvariant()) == null, "genuine lookup is case sensitive");
                SetIdentifier(id, "m_prefab", container); SetIdentifier(id, "m_uiContainerAsset", new AssetReferenceT<GameObject>("00000000000000000000000000000000")); id.RegisterContainer(container);
                string json = JsonUtility.ToJson(id); Check(json.Contains("m_name") && json.Contains("m_prefab") && json.Contains("m_uiContainerAsset") && !json.Contains("m_loadedUIContainer") && !json.Contains("m_operationHandle") && !json.Contains("m_refCount"), "actual serializer respects complete original own field partition");
                JsonUtility.FromJsonOverwrite(json, copy); Check(copy.Name == "original own label" && ReferenceEquals(ReadIdentifier<UIContainer>(copy, "m_prefab"), container) && copy.LoadedUIContainer == null && ReadIdentifier<int>(copy, "m_refCount") == 0, "owned original references roundtrip while runtime ownership remains independent");
                string componentJson = JsonUtility.ToJson(container); Check(componentJson.Contains("m_identifier") && componentJson.Contains("m_behaviour") && componentJson.Contains("m_canvas"), "actual genuine component serializes full three own fields");
                Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").SetValue(visibility, true); string visibilityJson = JsonUtility.ToJson(visibility); Field(typeof(UIVisibilityGroupDefinition), "m_initialVisibility").SetValue(visibility, false); JsonUtility.FromJsonOverwrite(visibilityJson, visibility); Check(visibility.InitialVisibility, "actual visibility own bool roundtrip");
                SetIdentifier(id, "m_loadedUIContainer", container); UObject.DestroyImmediate(container); Check(!id.IsLoaded && !ReferenceEquals(id.LoadedUIContainer, null), "IsLoaded uses Unity inequality while Loaded returns destroyed wrapper");
            }
            finally
            {
                try { if (otherOwner != null) UObject.DestroyImmediate(otherOwner); }
                finally { try { if (cameraOwner != null) UObject.DestroyImmediate(cameraOwner); }
                finally { try { if (child != null) UObject.DestroyImmediate(child); }
                finally { try { if (owner != null) UObject.DestroyImmediate(owner); }
                finally { try { if (visibility != null) UObject.DestroyImmediate(visibility); }
                finally { try { if (copy != null) UObject.DestroyImmediate(copy); }
                finally { if (id != null) UObject.DestroyImmediate(id); } } } } } }
            }
        }
    }
}
