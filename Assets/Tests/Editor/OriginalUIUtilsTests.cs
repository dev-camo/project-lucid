// Controlled rebuilt public algorithms only; original native/provider/source-form/FP parity and full GUI closure remain held.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    public sealed class OriginalUIUtilsTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        [Test]
        public void OriginalUIUtilsRetainsCompleteGenericDefaultAndAnnotationContracts()
        {
            Type type = typeof(UIUtils);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UIUtils"));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048961));
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(type.GetFields(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Assert.That(type.GetConstructors(Declared), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            var attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(2));
            RequireOption(attributes[0], Option.ArrayBoundsChecks);
            RequireOption(attributes[1], Option.NullChecks);
            MethodInfo[] methods = type.GetMethods(Declared).OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(methods.Select(MethodRow), Is.EqualTo(ExpectedMethods));
            Assert.That(methods.Sum(method => method.GetParameters().Length), Is.EqualTo(55));
            foreach (MethodInfo method in methods)
            {
                Assert.That(method.Attributes, Is.EqualTo((MethodAttributes)150));
                Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
                Assert.That(method.GetCustomAttributesData(), Is.Empty);
                Assert.That(method.ReturnParameter.GetCustomAttributesData(), Is.Empty);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    bool optional = parameter.Name == "enabled" || parameter.Name == "numberFormat";
                    bool output = parameter.Name == "isBehind";
                    ParameterAttributes expected = optional ? ParameterAttributes.Optional | ParameterAttributes.HasDefault : output ? ParameterAttributes.Out : ParameterAttributes.None;
                    Assert.That(parameter.Attributes, Is.EqualTo(expected));
                    Assert.That(parameter.HasDefaultValue, Is.EqualTo(optional));
                    var annotations = parameter.GetCustomAttributesData();
                    if (optional)
                    {
                        Assert.That(parameter.RawDefaultValue, Is.EqualTo(parameter.Name == "enabled" ? (object)true : null));
                        Assert.That(annotations.Count, Is.EqualTo(1));
                        Assert.That(annotations[0].AttributeType, Is.EqualTo(typeof(OptionalAttribute)));
                        Assert.That(annotations[0].ConstructorArguments, Is.Empty);
                        Assert.That(annotations[0].NamedArguments, Is.Empty);
                    }
                    else if (output)
                    {
                        Assert.That(annotations.Count, Is.EqualTo(1));
                        Assert.That(annotations[0].AttributeType, Is.EqualTo(typeof(OutAttribute)));
                        Assert.That(annotations[0].ConstructorArguments, Is.Empty);
                        Assert.That(annotations[0].NamedArguments, Is.Empty);
                    }
                    else Assert.That(annotations, Is.Empty);
                }
                Type[] generics = method.GetGenericArguments();
                bool generic = method.Name == "SetStruct" || method.Name == "SetClass";
                Assert.That(generics.Length, Is.EqualTo(generic ? 1 : 0));
                if (generic)
                {
                    Type parameter = generics[0];
                    Assert.That(parameter.Name, Is.EqualTo("T"));
                    Assert.That(parameter.GenericParameterPosition, Is.Zero);
                    Assert.That(parameter.DeclaringMethod, Is.EqualTo(method));
                    Assert.That(parameter.GenericParameterAttributes, Is.EqualTo((GenericParameterAttributes)(method.Name == "SetStruct" ? 24 : 4)));
                    Assert.That(parameter.GetGenericParameterConstraints(), Is.EqualTo(method.Name == "SetStruct" ? new[] { typeof(ValueType) } : Type.EmptyTypes));
                }
            }
        }

        [Test]
        public void OriginalFormattingUsesSingleConversionAndGenuineCultureProviders()
        {
            CultureInfo priorCulture = CultureInfo.CurrentCulture;
            CultureInfo priorUICulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                // Compare to the genuine Single provider independently of UIUtils.
                // Mono's F0 decimal rendering need not equal an exact Int32 string.
                string positiveSingle = 16777216f.ToString("F0", CultureInfo.InvariantCulture);
                string negativeSingle = (-16777216f).ToString("F0", CultureInfo.InvariantCulture);
                Assert.That(positiveSingle, Is.Not.EqualTo(16777217.ToString("F0", CultureInfo.InvariantCulture)));
                Assert.That(negativeSingle, Is.Not.EqualTo((-16777217).ToString("F0", CultureInfo.InvariantCulture)));
                Assert.That(UIUtils.FormatInt("F0", 16777217, CultureInfo.InvariantCulture), Is.EqualTo(positiveSingle));
                Assert.That(UIUtils.FormatInt("F0", -16777217, CultureInfo.InvariantCulture), Is.EqualTo(negativeSingle));
                Assert.That(UIUtils.FormatInt("F2", 12), Is.EqualTo("12,00"));
                Assert.That(UIUtils.FormatFloat("F2", 1234.5f), Is.EqualTo("1234,50"));
                Assert.That(UIUtils.FormatFloat("F2", -12.25f, CultureInfo.InvariantCulture), Is.EqualTo("-12.25"));
                Assert.That(UIUtils.FormatFloat(null, 7.5f, CultureInfo.GetCultureInfo("fr-FR")), Is.EqualTo("7,5"));
                Assert.Throws<FormatException>(() => UIUtils.FormatFloat("Q", 2f, CultureInfo.InvariantCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = priorCulture;
                CultureInfo.CurrentUICulture = priorUICulture;
            }
            Assert.That(ReferenceEquals(CultureInfo.CurrentCulture, priorCulture), Is.True);
            Assert.That(ReferenceEquals(CultureInfo.CurrentUICulture, priorUICulture), Is.True);
        }

        [Test]
        public void OriginalRefColourUsesExactChannelsAndRetainsManagedNaNBranch()
        {
            Color current = new Color(0.25f, 0.5f, 0.75f, 1f);
            Assert.That(UIUtils.SetColor(ref current, current), Is.False);
            Color close = new Color(0.25000012f, 0.5f, 0.75f, 1f);
            Assert.That(close.r == current.r, Is.False);
            Assert.That(UIUtils.SetColor(ref current, close), Is.True);
            Assert.That(current.r, Is.EqualTo(close.r));
            Assert.That(UIUtils.SetColor(ref current, close), Is.False);
            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0);
            current = new Color(negativeZero, 0f, 0f, 0f);
            Assert.That(UIUtils.SetColor(ref current, new Color(0f, 0f, 0f, 0f)), Is.False);
            Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(current.r), 0), Is.EqualTo(unchecked((int)0x80000000)));
            Color unordered = new Color(float.NaN, 0.5f, 0.75f, 1f);
            Assert.That(UIUtils.SetColor(ref current, unordered), Is.True);
            Assert.That(float.IsNaN(current.r), Is.True);
            Assert.That(UIUtils.SetColor(ref current, unordered), Is.True);
            // This is reconstructed managed comparison behavior, not a hardware conversion/native FP parity oracle.
        }

        [Test]
        public void OriginalGenericSettersUseOwnedObjectEqualityDispatch()
        {
            var currentValue = new EqualityValue(7, 0);
            var sameValue = new EqualityValue(7, 90);
            Assert.That(UIUtils.SetStruct(ref currentValue, sameValue), Is.False);
            Assert.That(currentValue.ObjectCalls, Is.EqualTo(1));
            Assert.That(currentValue.Value, Is.EqualTo(7));
            var different = new EqualityValue(11, 3);
            Assert.That(UIUtils.SetStruct(ref currentValue, different), Is.True);
            Assert.That(currentValue.Value, Is.EqualTo(11));
            Assert.That(currentValue.ObjectCalls, Is.EqualTo(3));

            EqualityReference currentReference = null;
            Assert.That(UIUtils.SetClass(ref currentReference, (EqualityReference)null), Is.False);
            var first = new EqualityReference(false);
            Assert.That(UIUtils.SetClass(ref currentReference, first), Is.True);
            Assert.That(first.Calls, Is.Zero);
            Assert.That(UIUtils.SetClass(ref currentReference, first), Is.True);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(first.LastArgument, Is.SameAs(first));
            first.Answer = true;
            var next = new EqualityReference(false);
            Assert.That(UIUtils.SetClass(ref currentReference, next), Is.False);
            Assert.That(currentReference, Is.SameAs(first));
            Assert.That(first.LastArgument, Is.SameAs(next));
            first.Answer = false;
            Assert.That(UIUtils.SetClass(ref currentReference, (EqualityReference)null), Is.True);
            Assert.That(currentReference, Is.Null);
            Assert.That(first.LastArgument, Is.Null);
            Assert.That(next.Calls, Is.Zero);
        }

        [Test]
        public void OriginalVisualHelpersForwardToGenuineOwnedUnityComponents()
        {
            using (var owned = new OwnedUnityObjects())
            {
                GameObject root = owned.Add(new GameObject("Owned original UI helpers", typeof(RectTransform)));
                root.SetActive(false);
                Image image = root.AddComponent<Image>();
                CanvasGroup group = root.AddComponent<CanvasGroup>();
                Texture2D texture = owned.Add(new Texture2D(4, 4, TextureFormat.RGBA32, false));
                Sprite sprite = owned.Add(Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f));
                Shader shader = Shader.Find("UI/Default");
                Assert.That(shader, Is.Not.Null, "Requires the genuine Unity UI shader; no replacement shader is supplied.");
                Material material = owned.Add(new Material(shader));
                UIUtils.SetSprite(image, sprite, false);
                Assert.That(image.sprite, Is.SameAs(sprite));
                Assert.That(image.enabled, Is.False);
                UIUtils.SetSprite(image, null, true);
                Assert.That(image.sprite, Is.SameAs(sprite));
                Assert.That(image.enabled, Is.False);
                UIUtils.SetSprite(image, sprite);
                Assert.That(image.enabled, Is.True);
                Color color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                UIUtils.SetColor((Graphic)image, color);
                UIUtils.SetAlpha((Graphic)image, 0.35f);
                Assert.That(image.color, Is.EqualTo(new Color(color.r, color.g, color.b, 0.35f)));
                UIUtils.SetAlpha(group, 0.625f);
                Assert.That(group.alpha, Is.EqualTo(0.625f));
                foreach (float fill in new[] { -0.25f, 0.375f, 1.25f })
                {
                    UIUtils.SetFill(image, fill);
                    Assert.That(image.fillAmount, Is.EqualTo(Mathf.Clamp01(fill)));
                }
                UIUtils.SetMaterial(image, material);
                Assert.That(image.material, Is.SameAs(material));
                UIUtils.SetMaterial(image, null);
                using (var serialized = new SerializedObject(image))
                {
                    serialized.Update();
                    SerializedProperty field = serialized.FindProperty("m_Material");
                    Assert.That(field, Is.Not.Null);
                    Assert.That(field.objectReferenceValue, Is.Null);
                }
                UIUtils.SetActive((MonoBehaviour)image, false);
                Assert.That(root.activeSelf, Is.False);
                GameObject plain = owned.Add(new GameObject("Owned original active forwarding"));
                UIUtils.SetActive(plain, false);
                Assert.That(plain.activeSelf, Is.False);
                UIUtils.SetActive(plain, true);
                Assert.That(plain.activeSelf, Is.True);
                UIUtils.SetSprite(null, sprite);
                UIUtils.SetColor((Graphic)null, color);
                UIUtils.SetAlpha((Graphic)null, 0.5f);
                UIUtils.SetAlpha((CanvasGroup)null, 0.5f);
                UIUtils.SetMaterial(null, material);
                UIUtils.SetFill(null, 0.5f);
                UIUtils.SetActive((GameObject)null, false);
                UIUtils.SetActive((MonoBehaviour)null, false);
                UIUtils.SetInteractable((Selectable)null, false);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalFrontPlaneProjectionUsesGenuineOwnedCamera()
        {
            using (var owned = new OwnedUnityObjects())
            {
                // An owned target keeps the pixel rect independent of the editor screen
                // size; it is added first so it is destroyed after the camera.
                RenderTexture target = owned.Add(new RenderTexture(1024, 1024, 0));
                GameObject root = owned.Add(new GameObject("Owned original projection camera"));
                root.SetActive(false);
                Camera camera = root.AddComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = 4f / 3f;
                camera.pixelRect = new Rect(10f, 20f, 640f, 480f);
                // Fixture preconditions for the literal points below: identity camera at
                // the origin, half extents 20/3 by 5, so screen = (330 + 48x, 260 + 48y).
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(camera.pixelRect, Is.EqualTo(new Rect(10f, 20f, 640f, 480f)));
                Assert.That(camera.aspect, Is.EqualTo(4f / 3f));
                // Literal expectations from 0x060005d5 (x86-64 0x1b6bb20, arm64 0x1b73f14):
                // the camera-space z is compared with -0.001f by ucomiss/seta (fcmp/cset gt),
                // so only z > -0.001f is behind; a behind point is rebuilt through
                // cameraToWorldMatrix with z = -0.001f, i.e. world z 0.001 here, before
                // WorldToScreenPoint. Rows: point, expected behind, expected screen point.
                var cases = new[]
                {
                    (new Vector3(1f, 2f, 2f), false, new Vector3(378f, 356f, 2f)),
                    (new Vector3(-1f, 1f, -2f), true, new Vector3(282f, 308f, 0.001f)),
                    (new Vector3(0f, 0f, 0.0005f), true, new Vector3(330f, 260f, 0.001f)),
                    (new Vector3(0f, 0f, 0.001f), false, new Vector3(330f, 260f, 0.001f)),
                };
                foreach (var (point, expectedBehind, expected) in cases)
                {
                    bool behind;
                    Vector3 actual = UIUtils.ProjectToFrontPlaneish(point, camera, out behind);
                    Assert.That(behind, Is.EqualTo(expectedBehind), "behind flag for " + point);
                    Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), "screen x for " + point);
                    Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), "screen y for " + point);
                    Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.000001f), "screen z for " + point);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // 0x060005d7 x86-64 0x1b6bd20: scale = reference/sprite PPU; the default
        // pivot is the literal (0.5,0.5) divided by the scaled sprite size; a
        // bounded height above the box shrinks sizeDelta, resets localScale and
        // returns before the virtual SetNativeSize. A centred default pivot
        // (height 32 fits the 50-unit box) would reach SetNativeSize instead.
        [Test]
        public void OriginalSpritePivotDividesDefaultHalfAndShrinkReturnsBeforeNativeSize()
        {
            PropertyInfo instance = typeof(MonoSingleton<HLUIEffectManager>).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setInstance = instance.GetSetMethod(true);
            HLUIEffectManager previous = HLUIEffectManager.Instance;
            try
            {
                using (var owned = new OwnedUnityObjects())
                {
                    GameObject managerOwner = owned.Add(new GameObject("Owned inactive UI effect reference"));
                    managerOwner.SetActive(false);
                    HLUIEffectManager manager = managerOwner.AddComponent<HLUIEffectManager>();
                    Assert.That(manager.ReferencePixelsPerUnit, Is.EqualTo(100f));
                    // Owned singleton slot only; Awake/registry are never entered.
                    setInstance.Invoke(null, new object[] { manager });
                    Texture2D texture = owned.Add(new Texture2D(64, 32, TextureFormat.RGBA32, false));
                    Sprite centred = owned.Add(Sprite.Create(texture, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 50f));
                    Sprite custom = owned.Add(Sprite.Create(texture, new Rect(0, 0, 64, 32), new Vector2(0.25f, 0.75f), 50f));
                    Image image = OwnedImage(owned, centred);
                    RectTransform target = image.rectTransform;
                    RectTransform box = owned.Add(new GameObject("Owned bounding box", typeof(RectTransform))).GetComponent<RectTransform>();
                    box.sizeDelta = new Vector2(10f, 50f);
                    // scale 100/50 = 2: scaled size (128,64); pivot (0.5/128, 0.5/64).
                    UIUtils.ApplySpriteCustomPivotToTransform(image, box, false, true);
                    Assert.That(target.pivot, Is.EqualTo(new Vector2(0.00390625f, 0.0078125f)));
                    Assert.That(target.anchoredPosition, Is.EqualTo(Vector2.zero));
                    Vector2 shrunk = new Vector2(128f, 64f) * (50f / (64f - 64f * 0.0078125f));
                    Assert.That(target.sizeDelta.x, Is.EqualTo(shrunk.x).Within(1e-4f));
                    Assert.That(target.sizeDelta.y, Is.EqualTo(shrunk.y).Within(1e-4f));
                    Assert.That(target.localScale, Is.EqualTo(Vector3.one));
                    // SetNativeSize was not reached: it would collapse the anchors.
                    Assert.That(target.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(target.anchorMax, Is.EqualTo(Vector2.one));

                    // Fitting height and no native size: only pivot and position change.
                    Image fitted = OwnedImage(owned, centred);
                    box.sizeDelta = new Vector2(10f, 100f);
                    UIUtils.ApplySpriteCustomPivotToTransform(fitted, box, false, false);
                    Assert.That(fitted.rectTransform.pivot, Is.EqualTo(new Vector2(0.00390625f, 0.0078125f)));
                    Assert.That(fitted.rectTransform.sizeDelta, Is.EqualTo(new Vector2(3f, 5f)));
                    Assert.That(fitted.rectTransform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));

                    // Custom pixel pivot (16,24)*2 over (128,64), then the genuine native size.
                    Image native = OwnedImage(owned, custom);
                    UIUtils.ApplySpriteCustomPivotToTransform(native, null, true, true);
                    Assert.That(native.rectTransform.pivot.x, Is.EqualTo(0.25f).Within(1e-6f));
                    Assert.That(native.rectTransform.pivot.y, Is.EqualTo(0.75f).Within(1e-6f));
                    Assert.That(native.rectTransform.sizeDelta, Is.EqualTo(new Vector2(128f, 64f)));
                    Assert.That(native.rectTransform.anchorMax, Is.EqualTo(native.rectTransform.anchorMin));
                    Assert.That(ReferenceEquals(HLUIEffectManager.Instance, manager), Is.True);
                }
            }
            finally { setInstance.Invoke(null, new object[] { previous }); }
            Assert.That(ReferenceEquals(HLUIEffectManager.Instance, previous), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static Image OwnedImage(OwnedUnityObjects owned, Sprite sprite)
        {
            GameObject root = owned.Add(new GameObject("Owned original pivot image", typeof(RectTransform)));
            root.SetActive(false);
            Image image = root.AddComponent<Image>();
            image.sprite = sprite;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(7f, 9f);
            rect.sizeDelta = new Vector2(3f, 5f);
            rect.localScale = new Vector3(2f, 3f, 4f);
            return image;
        }

        // These ordinary owned values are generic test arguments, not substitute UI/service providers.
        private struct EqualityValue : IEquatable<EqualityValue>
        {
            public int Value;
            public int ObjectCalls;
            public EqualityValue(int value, int calls) { Value = value; ObjectCalls = calls; }
            public bool Equals(EqualityValue other) { throw new InvalidOperationException("IEquatable dispatch is not the original Object.Equals path"); }
            public override bool Equals(object other) { ObjectCalls++; return other is EqualityValue value && Value == value.Value; }
            public override int GetHashCode() => Value;
        }

        private sealed class EqualityReference
        {
            public bool Answer;
            public int Calls;
            public object LastArgument;
            public EqualityReference(bool answer) { Answer = answer; }
            public override bool Equals(object other) { Calls++; LastArgument = other; return Answer; }
            public override int GetHashCode() => 0;
        }

        private sealed class OwnedUnityObjects : IDisposable
        {
            private readonly List<Object> values = new List<Object>();
            public T Add<T>(T value) where T : Object { values.Add(value); return value; }
            public void Dispose()
            {
                var errors = new List<Exception>();
                for (int index = values.Count - 1; index >= 0; index--)
                {
                    try { if (values[index] != null) Object.DestroyImmediate(values[index]); }
                    catch (Exception exception) { errors.Add(exception); }
                }
                values.Clear();
                if (errors.Count != 0) throw new AggregateException("Owned Unity cleanup failed", errors);
            }
        }

        private static string Canonical(Type type)
        {
            if (type.IsByRef) return Canonical(type.GetElementType()) + "&";
            if (type.IsGenericParameter) return type.Name;
            return type.FullName;
        }

        private static string MethodRow(MethodInfo method) => method.Name + "|" + Canonical(method.ReturnType) + "|" + string.Join(",", method.GetParameters().Select(p => Canonical(p.ParameterType) + " " + p.Name));

        private static void RequireOption(CustomAttributeData attribute, Option option)
        {
            Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
            Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
            Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo((int)option));
            Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attribute.NamedArguments, Is.Empty);
        }

        private static readonly string[] ExpectedMethods =
        {
            "ProjectToFrontPlaneish|UnityEngine.Vector3|UnityEngine.Vector3 point,UnityEngine.Camera camera,System.Boolean& isBehind",
            "FitTrimmedImageToBoundsHeight|System.Void|UnityEngine.UI.Image image,UnityEngine.RectTransform bounds",
            "ApplySpriteCustomPivotToTransform|System.Void|UnityEngine.UI.Image image,UnityEngine.RectTransform boundingBox,System.Boolean useSpritesCustomPivot,System.Boolean useNativeSize",
            "SetText|System.Void|TMPro.TMP_Text target,System.String text",
            "SetText|System.Void|TMPro.TMP_Text target,System.String text,System.Boolean active",
            "SetText|System.Void|TMPro.TMP_Text target,System.String text,UnityEngine.Color color",
            "SetSprite|System.Void|UnityEngine.UI.Image target,UnityEngine.Sprite sprite,System.Boolean enabled",
            "SetColor|System.Void|UnityEngine.UI.Graphic target,UnityEngine.Color color",
            "SetAlpha|System.Void|UnityEngine.CanvasGroup target,System.Single alpha",
            "SetAlpha|System.Void|UnityEngine.UI.Graphic target,System.Single alpha",
            "SetMaterial|System.Void|UnityEngine.UI.Graphic target,UnityEngine.Material mat",
            "SetFill|System.Void|UnityEngine.UI.Image target,System.Single fillAmount",
            "SetGradient|System.Void|Hardlight.Gradient target,UnityEngine.Color startColor,UnityEngine.Color endColor",
            "SetActive|System.Void|UnityEngine.GameObject target,System.Boolean active",
            "SetActive|System.Void|UnityEngine.MonoBehaviour target,System.Boolean active",
            "FormatInt|System.String|System.String formatString,System.Int32 value,System.IFormatProvider numberFormat",
            "FormatFloat|System.String|System.String formatString,System.Single value,System.IFormatProvider numberFormat",
            "SetTrigger|System.Void|UnityEngine.Animator target,System.String value",
            "SetInteractable|System.Void|Hardlight.HLSelectable target,System.Boolean interactable",
            "SetInteractable|System.Void|UnityEngine.UI.Selectable target,System.Boolean interactable",
            "SetColor|System.Boolean|UnityEngine.Color& currentValue,UnityEngine.Color newValue",
            "SetStruct|System.Boolean|T& currentValue,T newValue",
            "SetClass|System.Boolean|T& currentValue,T newValue",
        };
    }
}
