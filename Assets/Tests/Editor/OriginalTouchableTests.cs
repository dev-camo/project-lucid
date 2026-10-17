using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using OriginalTouchable = SonicDash.UI.Touchable;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime SonicDash.UI.Touchable (TypeDef
    // 0x02000014, methods 0x06000087..88) from the x86-64 bodies. Each case owns an
    // inactive GameObject and destroys it.
    [TestFixture]
    public sealed class OriginalTouchableTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 11844: no attributes, no fields, one override.
        [Test]
        public void DeclarationIsABareMaskableGraphicOverride()
        {
            Type type = typeof(OriginalTouchable);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("SonicDash.UI"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MaskableGraphic)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetCustomAttributesData(), Is.Empty);
            Assert.That(type.GetFields(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            MethodInfo method = type.GetMethods(Declared).Single();
            Assert.That(method.Name, Is.EqualTo("OnPopulateMesh"));
            Assert.That((int)method.Attributes, Is.EqualTo(196));
            Assert.That(method.GetParameters().Single().ParameterType, Is.EqualTo(typeof(VertexHelper)));
            Assert.That(method.GetParameters().Single().Name, Is.EqualTo("vh"));
            Assert.That(method.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(Graphic)));
            Assert.That((int)type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(6278));
        }

        // OnPopulateMesh 0x06000087 @0x1B3C730: only VertexHelper.Clear, so a filled helper
        // is emptied (Graphic's default would add the rect quad), and a null helper raises
        // NullReferenceException (0x1B3C743).
        [Test]
        public void PopulateMeshOnlyClearsTheHelper()
        {
            var owner = new GameObject("OriginalTouchable fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                owner.SetActive(false);
                ((RectTransform)owner.transform).sizeDelta = new Vector2(30f, 20f);
                var touchable = owner.AddComponent<OriginalTouchable>();
                MethodInfo populate = typeof(OriginalTouchable).GetMethod("OnPopulateMesh", Declared, null, new[] { typeof(VertexHelper) }, null);
                using (var vh = new VertexHelper())
                {
                    vh.AddVert(new Vector3(1f, 2f, 3f), new Color32(1, 2, 3, 4), Vector4.zero);
                    vh.AddVert(new Vector3(4f, 5f, 6f), new Color32(1, 2, 3, 4), Vector4.zero);
                    vh.AddVert(new Vector3(7f, 8f, 9f), new Color32(1, 2, 3, 4), Vector4.zero);
                    vh.AddTriangle(0, 1, 2);
                    populate.Invoke(touchable, new object[] { vh });
                    Assert.That(vh.currentVertCount, Is.EqualTo(0));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(0));
                    populate.Invoke(touchable, new object[] { vh });
                    Assert.That(vh.currentVertCount, Is.EqualTo(0));
                }
                var thrown = Assert.Throws<TargetInvocationException>(() => populate.Invoke(touchable, new object[] { null }));
                Assert.That(thrown.InnerException, Is.TypeOf<NullReferenceException>());
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
