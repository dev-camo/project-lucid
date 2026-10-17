using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.GreyscaleAndBrightnessModifier (HLUnityUI.Runtime
    // 0x060002e1..0x060002e7) against the shipped x86-64 bodies. Every case owns
    // its GameObjects and VertexHelpers and destroys or disposes them; the
    // dirty-vertices callback lives on the owned Image and dies with it.
    [TestFixture]
    public sealed class OriginalGreyscaleAndBrightnessModifierTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 11921: attribute, field, property and method
        // order, accessibility and argument literals. The TypeDef flags of
        // 0x02000061 (0x100001) and the MethodDef flags of 0x060002e1..e7 are
        // the global-metadata values, as copied into the dumper's DummyDll
        // HLUnityUI.Runtime.dll and the pinned LibCpp2IL export.
        [Test]
        public void KeepsTheOriginalDeclaration()
        {
            Type type = typeof(GreyscaleAndBrightnessModifier);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.GreyscaleAndBrightnessModifier"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(BaseMeshEffect)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577), "public, beforefieldinit, not sealed or abstract");
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(AddComponentMenu), typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(attributes[0].ConstructorArguments.Select(a => a.Value).ToArray(),
                Is.EqualTo(new object[] { "Hardlight UI/Effects/Greyscale And Brightness Modifier", 15 }));
            Assert.That(attributes[0].NamedArguments, Is.Empty);
            // ArrayBoundsChecks (2) is declared before NullChecks (1).
            int[] options = { 2, 1 };
            for (int i = 0; i < 2; i++)
            {
                CustomAttributeData option = attributes[i + 1];
                Assert.That(option.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                Assert.That(Convert.ToInt32(option.ConstructorArguments[0].Value), Is.EqualTo(options[i]));
                Assert.That(option.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(option.NamedArguments, Is.Empty);
            }

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_greyscaleFactor", "m_brightness" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(float), typeof(float) }));
            Assert.That(fields.Select(f => (int)f.Attributes).ToArray(), Is.EqualTo(new[] { 1, 1 }), "private instance");
            CheckFieldAttributes(fields[0], new[] { typeof(SerializeField), typeof(UnityEngine.RangeAttribute) }, 1f);
            CheckFieldAttributes(fields[1], new[] { typeof(UnityEngine.RangeAttribute), typeof(SerializeField) }, 100f);

            PropertyInfo[] properties = type.GetProperties(Declared);
            Assert.That(properties.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "GreyscaleFactor", "Brightness" }));
            Assert.That(properties.All(p => p.PropertyType == typeof(float) && p.GetCustomAttributesData().Count == 0), Is.True);

            // MethodDef token order 0x060002e1..e7 fixes the declaration order.
            MethodBase[] members = type.GetMethods(Declared).Cast<MethodBase>()
                .Concat(type.GetConstructors(Declared)).OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(members.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "get_GreyscaleFactor", "set_GreyscaleFactor", "get_Brightness", "set_Brightness",
                "ModifyMesh", "SetGreyscaleFactorAndBrightness", ".ctor",
            }));
            int[] flags = { 2182, 2182, 2182, 2182, 198, 134, 6278 };
            string[][] parameters =
            {
                new string[0], new[] { "value" }, new string[0], new[] { "value" },
                new[] { "vh" }, new[] { "greyscaleFactor", "brightness" }, new string[0],
            };
            for (int i = 0; i < members.Length; i++)
            {
                Assert.That((int)members[i].Attributes, Is.EqualTo(flags[i]), members[i].Name);
                Assert.That(members[i].GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(parameters[i]), members[i].Name);
                Assert.That(members[i].GetCustomAttributesData(), Is.Empty, members[i].Name);
            }
            MethodInfo modifyMesh = type.GetMethod("ModifyMesh", Declared);
            Assert.That(modifyMesh.GetParameters().Single().ParameterType, Is.EqualTo(typeof(VertexHelper)));
            Assert.That(modifyMesh.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(BaseMeshEffect)));
            Assert.That(type.GetMethod("SetGreyscaleFactorAndBrightness", Declared).GetParameters().Select(p => p.ParameterType).ToArray(),
                Is.EqualTo(new[] { typeof(float), typeof(float) }));
        }

        // .ctor 0x1b4f010 stores 0x3f800000 (1f) to m_brightness (+0x2c) and
        // leaves m_greyscaleFactor (+0x28) zero. Hardlight.Gradient's own
        // m_brightness defaults to 0, so a copied default would fail here.
        [Test]
        public void ConstructionDefaultsBrightnessToOne()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-defaults", typeof(RectTransform));
                owner.SetActive(false);
                owner.AddComponent<Image>();
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                Assert.That(modifier.GreyscaleFactor, Is.Zero);
                Assert.That(modifier.Brightness, Is.EqualTo(1f));
                using (SerializedObject serialized = new SerializedObject(modifier))
                {
                    serialized.Update();
                    Assert.That(serialized.FindProperty("m_greyscaleFactor").floatValue, Is.Zero);
                    Assert.That(serialized.FindProperty("m_brightness").floatValue, Is.EqualTo(1f));
                }
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // 0x1b4ebf0 / 0x1b4eca0 / 0x1b4ef60: store without clamping, then call
        // graphic.SetVerticesDirty (Graphic vtable slot 28) once when graphic
        // != null. Getters 0x1b4ebe0 / 0x1b4ec90 only read the field.
        [Test]
        public void SettersStoreUnclampedValuesAndDirtyTheGraphicOncePerCall()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-setters", typeof(RectTransform));
                Image image = owner.AddComponent<Image>();
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                Assert.That(image.IsActive(), Is.True, "SetVerticesDirty only notifies for an active graphic");
                int dirtied = 0;
                image.RegisterDirtyVerticesCallback(() => dirtied++);

                modifier.GreyscaleFactor = 2.5f;
                Assert.That(dirtied, Is.EqualTo(1));
                modifier.Brightness = -3f;
                Assert.That(dirtied, Is.EqualTo(2));
                Assert.That(modifier.GreyscaleFactor, Is.EqualTo(2.5f));
                Assert.That(modifier.Brightness, Is.EqualTo(-3f));
                Assert.That(dirtied, Is.EqualTo(2), "getters do not dirty");

                // One dirty for both fields, not one per field.
                modifier.SetGreyscaleFactorAndBrightness(-0.75f, 150f);
                Assert.That(dirtied, Is.EqualTo(3));
                Assert.That(modifier.GreyscaleFactor, Is.EqualTo(-0.75f));
                Assert.That(modifier.Brightness, Is.EqualTo(150f));
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // The op_Inequality guard skips the dirty call when no Graphic exists.
        [Test]
        public void SettersWithoutAGraphicOnlyStoreValues()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-no-graphic", typeof(RectTransform));
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                Assert.That(owner.GetComponent<Graphic>(), Is.Null);
                Assert.DoesNotThrow(() => modifier.GreyscaleFactor = 0.5f);
                Assert.DoesNotThrow(() => modifier.Brightness = 7f);
                Assert.That(modifier.GreyscaleFactor, Is.EqualTo(0.5f));
                Assert.That(modifier.Brightness, Is.EqualTo(7f));
                Assert.DoesNotThrow(() => modifier.SetGreyscaleFactorAndBrightness(0.125f, 42f));
                Assert.That(modifier.GreyscaleFactor, Is.EqualTo(0.125f));
                Assert.That(modifier.Brightness, Is.EqualTo(42f));
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // 0x1b4ed40 returns at once when the virtual IsActive (slot 9) is false.
        [Test]
        public void InactiveModifyMeshLeavesTheHelperUntouched()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-inactive", typeof(RectTransform));
                owner.SetActive(false);
                owner.AddComponent<Image>();
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                modifier.SetGreyscaleFactorAndBrightness(0.25f, 3f);
                Assert.That(modifier.IsActive(), Is.False);
                CheckModifyMeshLeavesTheQuadUntouched(modifier);
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // The other half of isActiveAndEnabled: a disabled modifier on an
        // active GameObject takes the same IsActive (slot 9) early return.
        [Test]
        public void DisabledModifyMeshLeavesTheHelperUntouched()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-disabled", typeof(RectTransform));
                owner.AddComponent<Image>();
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                modifier.SetGreyscaleFactorAndBrightness(0.25f, 3f);
                Assert.That(modifier.IsActive(), Is.True, "control: the enabled modifier is active");
                modifier.enabled = false;
                Assert.That(owner.activeInHierarchy, Is.True);
                Assert.That(modifier.IsActive(), Is.False);
                CheckModifyMeshLeavesTheQuadUntouched(modifier);
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        static void CheckModifyMeshLeavesTheQuadUntouched(GreyscaleAndBrightnessModifier modifier)
        {
            using (VertexHelper vertices = Quad(true))
            {
                List<UIVertex> before = Stream(vertices);
                modifier.ModifyMesh(vertices);
                // Shared quad vertices survive: no Clear and no re-add.
                Assert.That(vertices.currentVertCount, Is.EqualTo(4));
                Assert.That(vertices.currentIndexCount, Is.EqualTo(6));
                List<UIVertex> after = Stream(vertices);
                Assert.That(after.Count, Is.EqualTo(6));
                for (int i = 0; i < after.Count; i++)
                {
                    CheckOtherChannels(before[i], after[i]);
                    Assert.That(after[i].uv1, Is.EqualTo(before[i].uv1));
                }
            }
        }

        // 0x1b4ee20 loop: each 0x6c-byte UIVertex is copied whole, only uv1
        // (+0x3c) is replaced by the 8 bytes at this+0x28 with zeroed z/w, then
        // Clear and AddUIVertexTriangleStream rebuild the helper unshared.
        [Test]
        public void ActiveModifyMeshRewritesOnlyUV1AndUnsharesTheStream()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("GreyscaleAndBrightnessModifier-active", typeof(RectTransform));
                owner.AddComponent<Image>();
                GreyscaleAndBrightnessModifier modifier = owner.AddComponent<GreyscaleAndBrightnessModifier>();
                Assert.That(modifier.IsActive(), Is.True);
                modifier.SetGreyscaleFactorAndBrightness(-1.25f, 37.5f);
                using (VertexHelper vertices = Quad(true))
                {
                    List<UIVertex> before = Stream(vertices);
                    modifier.ModifyMesh(vertices);
                    Assert.That(vertices.currentVertCount, Is.EqualTo(6));
                    Assert.That(vertices.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Stream(vertices);
                    Assert.That(after.Count, Is.EqualTo(before.Count));
                    for (int i = 0; i < after.Count; i++)
                    {
                        CheckOtherChannels(before[i], after[i]);
                        Assert.That(after[i].uv1, Is.EqualTo(new Vector4(-1.25f, 37.5f, 0f, 0f)), "vertex " + i);
                    }
                }
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // The je after GetUIVertexStream (0x1b4edf7) skips Clear when the
        // triangle stream is empty, so untriangulated vertices survive. Control:
        // Hardlight.Gradient ModifyMesh (0x060002ce) clears unconditionally, proving the
        // vertex-count observation tells the two bodies apart.
        [Test]
        public void ActiveModifyMeshWithAnEmptyStreamKeepsUntriangulatedVertices()
        {
            var owners = new List<GameObject>();
            try
            {
                GreyscaleAndBrightnessModifier modifier = ActiveOwner(owners).AddComponent<GreyscaleAndBrightnessModifier>();
                modifier.SetGreyscaleFactorAndBrightness(0.5f, 2f);
                using (VertexHelper vertices = Quad(false))
                {
                    Assert.That(Stream(vertices), Is.Empty);
                    modifier.ModifyMesh(vertices);
                    Assert.That(vertices.currentVertCount, Is.EqualTo(4));
                    Assert.That(vertices.currentIndexCount, Is.Zero);
                    UIVertex kept = new UIVertex();
                    vertices.PopulateUIVertex(ref kept, 2);
                    Assert.That(kept.uv1, Is.EqualTo(new Vector4(5f, 6f, 7f, 8f)));
                    Assert.That(kept.position, Is.EqualTo(new Vector3(2f, 2f, 7f)));
                }

                Hardlight.Gradient control = ActiveOwner(owners).AddComponent<Hardlight.Gradient>();
                Assert.That(control.IsActive(), Is.True);
                using (VertexHelper vertices = Quad(false))
                {
                    control.ModifyMesh(vertices);
                    Assert.That(vertices.currentVertCount, Is.Zero);
                }
            }
            finally
            {
                for (int i = owners.Count - 1; i >= 0; i--)
                    if (owners[i] != null) Object.DestroyImmediate(owners[i]);
            }
        }

        static GameObject ActiveOwner(List<GameObject> owners)
        {
            var owner = new GameObject("GreyscaleAndBrightnessModifier-empty-stream", typeof(RectTransform));
            owners.Add(owner);
            owner.AddComponent<Image>();
            return owner;
        }

        static void CheckFieldAttributes(FieldInfo field, Type[] order, float max)
        {
            IList<CustomAttributeData> data = field.GetCustomAttributesData();
            Assert.That(data.Select(a => a.AttributeType).ToArray(), Is.EqualTo(order), field.Name);
            CustomAttributeData range = data.Single(a => a.AttributeType == typeof(UnityEngine.RangeAttribute));
            Assert.That(range.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(float), typeof(float) }));
            Assert.That(range.ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { 0f, max }), field.Name);
            Assert.That(data.All(a => a.NamedArguments.Count == 0), Is.True);
        }

        // Four distinct vertices; with triangles they share 0 and 2 across a quad.
        static VertexHelper Quad(bool triangulate)
        {
            var vertices = new VertexHelper();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    UIVertex vertex = UIVertex.simpleVert;
                    vertex.position = new Vector3(i == 1 || i == 2 ? 2f : 0f, i >= 2 ? 2f : 0f, i + 5f);
                    vertex.normal = new Vector3(0f, 0f, -1f);
                    vertex.tangent = new Vector4(1f, 0f, 0f, -1f);
                    vertex.color = new Color32((byte)(10 + i), 20, 30, 40);
                    vertex.uv0 = new Vector4(i, 2f, 3f, 4f);
                    vertex.uv1 = new Vector4(5f, 6f, 7f, 8f);
                    vertex.uv2 = new Vector4(9f, i, 11f, 12f);
                    vertex.uv3 = new Vector4(13f, 14f, i, 16f);
                    vertices.AddVert(vertex);
                }
                if (triangulate)
                {
                    vertices.AddTriangle(0, 1, 2);
                    vertices.AddTriangle(2, 3, 0);
                }
                return vertices;
            }
            catch { vertices.Dispose(); throw; }
        }

        static List<UIVertex> Stream(VertexHelper vertices)
        {
            var result = new List<UIVertex>();
            vertices.GetUIVertexStream(result);
            return result;
        }

        static void CheckOtherChannels(UIVertex before, UIVertex after)
        {
            Assert.That(after.position, Is.EqualTo(before.position));
            Assert.That(after.normal, Is.EqualTo(before.normal));
            Assert.That(after.tangent, Is.EqualTo(before.tangent));
            Assert.That(after.color, Is.EqualTo(before.color));
            Assert.That(after.uv0, Is.EqualTo(before.uv0));
            Assert.That(after.uv2, Is.EqualTo(before.uv2));
            Assert.That(after.uv3, Is.EqualTo(before.uv3));
        }
    }
}
