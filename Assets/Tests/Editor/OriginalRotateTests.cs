using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OriginalRotate = Hardlight.Rotate;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime Hardlight.Rotate (TypeDef
    // 0x02000062, methods 0x060002e8..eb) and Rotate+Rotation (0x02000063) from the
    // x86-64 bodies. Every case owns its GameObject and destroys it; each dirty
    // callback is unregistered.
    [TestFixture]
    public sealed class OriginalRotateTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // dump.cs TypeDefIndex 11923: Il2CppSetOption(ArrayBoundsChecks) first, then
        // AddComponentMenu("Hardlight UI/Effects/Rotate Sprite", 2), then
        // Il2CppSetOption(NullChecks); fields +0x28 rotation [SerializeField], +0x30
        // ninety, +0x38 oneEighty, +0x40 twoSeventy. TypeDefIndex 11922: the nested enum.
        [Test]
        public void DeclarationKeepsTheOriginalAttributesFieldsAndNestedEnum()
        {
            Type type = typeof(OriginalRotate);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(BaseMeshEffect)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(AddComponentMenu), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(Convert.ToInt32(attributes[0].ConstructorArguments[0].Value), Is.EqualTo(2));
            Assert.That(attributes[0].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[1].ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { "Hardlight UI/Effects/Rotate Sprite", 2 }));
            Assert.That(Convert.ToInt32(attributes[2].ConstructorArguments[0].Value), Is.EqualTo(1));
            Assert.That(attributes[2].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes.All(a => a.NamedArguments.Count == 0), Is.True);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "rotation", "ninety", "oneEighty", "twoSeventy" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(OriginalRotate.Rotation), typeof(int[]), typeof(int[]), typeof(int[]) }));
            Assert.That(fields.All(f => f.Attributes == FieldAttributes.Private), Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));
            for (int i = 1; i < 4; ++i) Assert.That(fields[i].GetCustomAttributesData(), Is.Empty, fields[i].Name);

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "ModifyMesh", "ModifyVertices", "OnDidApplyAnimationProperties" }));
            CheckMethod(methods[0], 198, new[] { typeof(VertexHelper) }, new[] { "vh" });
            CheckMethod(methods[1], 134, new[] { typeof(List<UIVertex>) }, new[] { "verts" });
            CheckMethod(methods[2], 196, Type.EmptyTypes, new string[0]);
            Assert.That(methods[2].GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(UnityEngine.EventSystems.UIBehaviour)));
            Assert.That((int)type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(6278));

            Type rotation = type.GetNestedTypes(Declared).Single();
            Assert.That(rotation, Is.EqualTo(typeof(OriginalRotate.Rotation)));
            Assert.That((int)rotation.Attributes, Is.EqualTo(258));
            Assert.That(Enum.GetUnderlyingType(rotation), Is.EqualTo(typeof(int)));
            Assert.That(Enum.GetNames(rotation), Is.EqualTo(new[] { "None", "Quarter", "Half", "ThreeQuarter" }));
            Assert.That(Enum.GetValues(rotation).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        // .ctor 0x060002eb @0x1B4F400: three int[6] filled by RuntimeHelpers.InitializeArray
        // from <PrivateImplementationDetails> 6768..(metadata 0x72E2B0) -> ninety,
        // D143..(0x72F800) -> oneEighty, B585..(0x72F7E0) -> twoSeventy, before
        // BaseMeshEffect..ctor; rotation keeps its zero default (None).
        [Test]
        public void ConstructionFillsTheShippedIndexTablesAndDefaultsToNone()
        {
            using (var owned = new Owned(false))
            {
                Assert.That(Field("ninety").GetValue(owned.Rotate), Is.EqualTo(new[] { 1, 2, 4, 4, 0, 1 }));
                Assert.That(Field("oneEighty").GetValue(owned.Rotate), Is.EqualTo(new[] { 2, 4, 0, 0, 1, 2 }));
                Assert.That(Field("twoSeventy").GetValue(owned.Rotate), Is.EqualTo(new[] { 4, 0, 1, 1, 2, 4 }));
                Assert.That(Field("rotation").GetValue(owned.Rotate), Is.EqualTo(OriginalRotate.Rotation.None));
                using (var serialized = new SerializedObject(owned.Rotate))
                {
                    Assert.That(serialized.FindProperty("rotation").enumValueIndex, Is.EqualTo(0));
                    Assert.That(serialized.FindProperty("ninety"), Is.Null);
                }
            }
        }

        // ModifyVertices 0x060002e9: rotation (+0x28) is re-read per entry; 1/2/3 pick
        // ninety/oneEighty/twoSeventy[i], anything else i (0x1B4F1E1..0x1B4F201). A first
        // pass copies verts[index].position into a Vector3[Count]; a second pass writes
        // only .position back (0x1B4F220..0x1B4F355), so colour, uv, normal and tangent
        // stay on their own entry. Fails for a reversed table or a whole-vertex copy.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(0)]
        [TestCase(7)]
        public void ModifyVerticesMovesOnlyPositionsThroughTheTable(int rotation)
        {
            int[][] tables = { new[] { 0, 1, 2, 3, 4, 5 }, new[] { 1, 2, 4, 4, 0, 1 }, new[] { 2, 4, 0, 0, 1, 2 }, new[] { 4, 0, 1, 1, 2, 4 } };
            int[] sources = rotation < tables.Length ? tables[rotation] : tables[0];
            using (var owned = new Owned(true))
            {
                Field("rotation").SetValue(owned.Rotate, (OriginalRotate.Rotation)rotation);
                List<UIVertex> before = Stream();
                List<UIVertex> verts = Stream();
                owned.Rotate.ModifyVertices(verts);
                Assert.That(verts.Count, Is.EqualTo(6));
                for (int i = 0; i < 6; ++i)
                {
                    Assert.That(verts[i].position, Is.EqualTo(before[sources[i]].position), "entry " + i);
                    CheckOtherChannels(before[i], verts[i]);
                }
            }
        }

        // 0x1B4F12A..0x1B4F14A: the IsActive vcall (UIBehaviour slot 9) and an empty list
        // both return before the array is allocated or any entry is touched.
        [Test]
        public void ModifyVerticesReturnsForAnInactiveEffectOrAnEmptyList()
        {
            using (var owned = new Owned(true))
            {
                Field("rotation").SetValue(owned.Rotate, OriginalRotate.Rotation.Half);
                owned.Rotate.enabled = false;
                Assert.That(owned.Rotate.IsActive(), Is.False);
                List<UIVertex> before = Stream();
                List<UIVertex> verts = Stream();
                owned.Rotate.ModifyVertices(verts);
                for (int i = 0; i < 6; ++i) Assert.That(verts[i].position, Is.EqualTo(before[i].position), "entry " + i);

                owned.Rotate.enabled = true;
                Assert.That(owned.Rotate.IsActive(), Is.True);
                var empty = new List<UIVertex>();
                Assert.DoesNotThrow(() => owned.Rotate.ModifyVertices(empty));
                Assert.That(empty, Is.Empty);
            }
        }

        // ModifyMesh 0x060002e8: new List, GetUIVertexStream, ModifyVertices, Clear,
        // AddUIVertexTriangleStream, with no guard of its own. An indexed quad comes back
        // as six stream vertices, rotated a quarter turn when active and in place when not.
        [Test]
        public void ModifyMeshRebuildsTheHelperFromTheRotatedStream()
        {
            using (var owned = new Owned(true))
            {
                Field("rotation").SetValue(owned.Rotate, OriginalRotate.Rotation.Quarter);
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Read(vh);
                    owned.Rotate.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(6));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Read(vh);
                    int[] sources = { 1, 2, 4, 4, 0, 1 };
                    for (int i = 0; i < 6; ++i)
                    {
                        Assert.That(after[i].position, Is.EqualTo(before[sources[i]].position), "entry " + i);
                        Assert.That(after[i].color, Is.EqualTo(before[i].color), "entry " + i);
                    }
                }

                owned.Rotate.enabled = false;
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Read(vh);
                    owned.Rotate.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(6));
                    List<UIVertex> after = Read(vh);
                    for (int i = 0; i < 6; ++i) Assert.That(after[i].position, Is.EqualTo(before[i].position), "entry " + i);
                }
            }
        }

        // OnDidApplyAnimationProperties 0x060002ea: graphic != null -> SetVerticesDirty
        // (Graphic slot 28, [klass + 0x2f8]), then tail-calls
        // BaseMeshEffect.OnDidApplyAnimationProperties, which dirties again: two vertex
        // callbacks and no layout or material dirt. Without a graphic nothing throws.
        [Test]
        public void AnimationPropertiesDirtyTheGraphicVerticesTwice()
        {
            using (var owned = new Owned(true))
            {
                owned.Image = owned.Owner.AddComponent<Image>();
                int vertices = 0, layout = 0, material = 0;
                UnityEngine.Events.UnityAction onVertices = () => ++vertices;
                UnityEngine.Events.UnityAction onLayout = () => ++layout;
                UnityEngine.Events.UnityAction onMaterial = () => ++material;
                owned.Image.RegisterDirtyVerticesCallback(onVertices);
                owned.Image.RegisterDirtyLayoutCallback(onLayout);
                owned.Image.RegisterDirtyMaterialCallback(onMaterial);
                try
                {
                    Invoke(owned.Rotate, "OnDidApplyAnimationProperties");
                    Assert.That(vertices, Is.EqualTo(2));
                    Assert.That(layout, Is.EqualTo(0));
                    Assert.That(material, Is.EqualTo(0));
                }
                finally
                {
                    owned.Image.UnregisterDirtyVerticesCallback(onVertices);
                    owned.Image.UnregisterDirtyLayoutCallback(onLayout);
                    owned.Image.UnregisterDirtyMaterialCallback(onMaterial);
                }
            }
            using (var bare = new Owned(true))
            {
                Assert.DoesNotThrow(() => Invoke(bare.Rotate, "OnDidApplyAnimationProperties"));
            }
        }

        // Owns a GameObject with the Rotate (and optionally later an Image); active owners
        // make IsActive true. Disposing destroys the GameObject.
        sealed class Owned : IDisposable
        {
            public readonly GameObject Owner;
            public readonly OriginalRotate Rotate;
            public Image Image;

            public Owned(bool active)
            {
                Owner = new GameObject("OriginalRotate fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Owner.SetActive(false);
                    Rotate = Owner.AddComponent<OriginalRotate>();
                    if (active) Owner.SetActive(true);
                }
                catch { Object.DestroyImmediate(Owner); throw; }
            }

            public void Dispose() { if (Owner != null) Object.DestroyImmediate(Owner); }
        }

        static void Invoke(object target, string name)
        {
            try { typeof(OriginalRotate).GetMethod(name, Declared).Invoke(target, null); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        static FieldInfo Field(string name)
        {
            FieldInfo field = typeof(OriginalRotate).GetField(name, Instance);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        // A six-entry quad stream with distinct positions and per-entry channels.
        static List<UIVertex> Stream()
        {
            var result = new List<UIVertex>();
            for (int i = 0; i < 6; ++i)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(10f * i + 1f, -3f * i, i + 0.5f);
                vertex.color = new Color32((byte)(20 + i), 11, 13, 17);
                vertex.normal = new Vector3(0f, i, -1f);
                vertex.tangent = new Vector4(1f, 0f, i, -1f);
                vertex.uv0 = new Vector4(i, 2f, 3f, 4f);
                vertex.uv1 = new Vector4(5f, i, 7f, 8f);
                vertex.uv2 = new Vector4(9f, 10f, i, 12f);
                vertex.uv3 = new Vector4(13f, 14f, 15f, i);
                result.Add(vertex);
            }
            return result;
        }

        // Quad (0,0) (0,10) (20,10) (20,0), triangles 0-1-2 and 2-3-0: stream 0,1,2,2,3,0.
        static VertexHelper Quad()
        {
            var vh = new VertexHelper();
            try
            {
                Vector3[] corners = { new Vector3(0f, 0f, 1f), new Vector3(0f, 10f, 2f), new Vector3(20f, 10f, 3f), new Vector3(20f, 0f, 4f) };
                for (int i = 0; i < corners.Length; ++i) vh.AddVert(corners[i], new Color32((byte)(50 + i), 0, 0, 255), Vector4.zero);
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
                return vh;
            }
            catch { vh.Dispose(); throw; }
        }

        static List<UIVertex> Read(VertexHelper vh)
        {
            var result = new List<UIVertex>();
            vh.GetUIVertexStream(result);
            return result;
        }

        static void CheckOtherChannels(UIVertex before, UIVertex after)
        {
            Assert.That(after.color, Is.EqualTo(before.color));
            Assert.That(after.normal, Is.EqualTo(before.normal));
            Assert.That(after.tangent, Is.EqualTo(before.tangent));
            Assert.That(after.uv0, Is.EqualTo(before.uv0));
            Assert.That(after.uv1, Is.EqualTo(before.uv1));
            Assert.That(after.uv2, Is.EqualTo(before.uv2));
            Assert.That(after.uv3, Is.EqualTo(before.uv3));
        }

        static void CheckMethod(MethodInfo method, int flags, Type[] parameters, string[] names)
        {
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), method.Name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), method.Name);
            Assert.That(method.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(parameters), method.Name);
            Assert.That(method.GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(names), method.Name);
            Assert.That(method.GetCustomAttributesData(), Is.Empty, method.Name);
        }
    }
}
