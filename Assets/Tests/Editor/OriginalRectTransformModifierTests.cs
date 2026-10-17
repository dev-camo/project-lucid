using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityUI.Runtime RectTransformModifier (global
    // namespace, TypeDefIndex 11834, MethodDefs 0x0600002b..0x06000062) from the
    // x86-64 bodies, covering what the OrientationSpecificRectTransformModifier
    // specification does not: lazy transform caching, the getters and the
    // equality-skip guards of the offset setters. Every case owns one inactive
    // HideAndDontSave host and destroys it before returning.
    [TestFixture]
    public sealed class OriginalRectTransformModifierTests
    {
        const float Tolerance = 1e-4f;
        static readonly FieldInfo TransformField =
            typeof(RectTransformModifier).GetField("m_transform", BindingFlags.Instance | BindingFlags.NonPublic);

        static RectTransformModifier Create(out GameObject owner)
        {
            owner = new GameObject("RectTransformModifier fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var rect = (RectTransform)owner.transform;
            rect.anchorMin = new Vector2(0.1f, 0.2f);
            rect.anchorMax = new Vector2(0.6f, 0.7f);
            rect.pivot = new Vector2(0.3f, 0.4f);
            rect.sizeDelta = new Vector2(50f, 60f);
            rect.anchoredPosition = new Vector2(5f, 6f);
            return owner.AddComponent<RectTransformModifier>();
        }

        // GetTransform 0x06000050 stores transform-as-RectTransform only when the
        // Unity-null test (0x1b3b80c) holds; get_RectTransform 0x0600004d and
        // Awake 0x0600004f are tail calls into it.
        [Test]
        public void RectTransformIsCachedLazilyFromTheOwnTransform()
        {
            var modifier = Create(out GameObject owner);
            try
            {
                Assert.That(TransformField.GetValue(modifier), Is.Null, "inactive AddComponent runs no Awake");
                Assert.That(modifier.RectTransform, Is.SameAs(owner.transform));
                Assert.That(TransformField.GetValue(modifier), Is.SameAs(owner.transform));

                var awake = owner.AddComponent<RectTransformModifier>();
                typeof(RectTransformModifier).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(awake, null);
                Assert.That(TransformField.GetValue(awake), Is.SameAs(owner.transform));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // Each getter reads one RectTransform component; only get_RightOffset
        // (0x1b3ae18) negates, get_TopOffset (0x06000041, 0x1b3b0b8 movshdup,
        // no xorps) does not.
        [Test]
        public void GettersReadTheRectTransformComponents()
        {
            var modifier = Create(out GameObject owner);
            try
            {
                var rect = (RectTransform)owner.transform;
                owner.transform.localScale = new Vector3(2f, 3f, 4f);
                Assert.That(modifier.Height, Is.EqualTo(rect.sizeDelta.y).Within(Tolerance));
                Assert.That(modifier.Width, Is.EqualTo(rect.sizeDelta.x).Within(Tolerance));
                Assert.That(modifier.XOffset, Is.EqualTo(rect.anchoredPosition.x).Within(Tolerance));
                Assert.That(modifier.YOffset, Is.EqualTo(rect.anchoredPosition.y).Within(Tolerance));
                Assert.That(modifier.XAnchorMin, Is.EqualTo(0.1f).Within(Tolerance));
                Assert.That(modifier.XAnchorMax, Is.EqualTo(0.6f).Within(Tolerance));
                Assert.That(modifier.YAnchorMin, Is.EqualTo(0.2f).Within(Tolerance));
                Assert.That(modifier.YAnchorMax, Is.EqualTo(0.7f).Within(Tolerance));
                Assert.That(modifier.LeftOffset, Is.EqualTo(rect.offsetMin.x).Within(Tolerance));
                Assert.That(modifier.RightOffset, Is.EqualTo(-rect.offsetMax.x).Within(Tolerance));
                Assert.That(modifier.BotOffset, Is.EqualTo(rect.offsetMin.y).Within(Tolerance));
                Assert.That(modifier.TopOffset, Is.EqualTo(rect.offsetMax.y).Within(Tolerance));
                Assert.That(rect.offsetMax.x, Is.Not.EqualTo(0f).Within(Tolerance), "a non-zero offset distinguishes the sign");
                Assert.That(rect.offsetMax.y, Is.Not.EqualTo(0f).Within(Tolerance), "a non-zero offset distinguishes the sign");
                Assert.That(modifier.PivotX, Is.EqualTo(0.3f).Within(Tolerance));
                Assert.That(modifier.PivotY, Is.EqualTo(0.4f).Within(Tolerance));
                Assert.That(modifier.LocalScaleX, Is.EqualTo(2f));
                Assert.That(modifier.LocalScaleY, Is.EqualTo(3f));
                Assert.That(modifier.LocalScaleZ, Is.EqualTo(4f));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // set_RightOffset 0x1b3ae5a and set_TopOffset 0x1b3b0fe compare the raw
        // offsetMax component with the un-negated value, but store its negation
        // (xorps at 0x1b3ae95 / 0x1b3b139). Assigning the negation of the current
        // component is therefore skipped; any other value is stored negated.
        [Test]
        public void RightAndTopOffsetSkipWhenTheRawComponentEqualsTheValue()
        {
            var modifier = Create(out GameObject owner);
            try
            {
                var rect = (RectTransform)owner.transform;
                rect.offsetMax = new Vector2(-5f, 4f);
                // Unity stores offsetMax through sizeDelta and anchoredPosition with
                // the fractional pivot, so the read-back components drift slightly;
                // the equality guard is exercised with the values actually stored.
                float rawRight = rect.offsetMax.x;
                float rawTop = rect.offsetMax.y;
                Assert.That(rawRight, Is.EqualTo(-5f).Within(Tolerance));
                Assert.That(rawTop, Is.EqualTo(4f).Within(Tolerance));

                modifier.RightOffset = rawRight;
                Assert.That(rect.offsetMax.x, Is.EqualTo(rawRight), "raw component equals the value: skipped");
                Assert.That(modifier.RightOffset, Is.EqualTo(-rawRight));
                modifier.RightOffset = 3f;
                Assert.That(rect.offsetMax.x, Is.EqualTo(-3f).Within(Tolerance), "other values are stored negated");

                modifier.TopOffset = rawTop;
                Assert.That(rect.offsetMax.y, Is.EqualTo(rawTop), "raw component equals the value: skipped");
                modifier.TopOffset = 6f;
                Assert.That(rect.offsetMax.y, Is.EqualTo(-6f).Within(Tolerance), "other values are stored negated");
                Assert.That(modifier.TopOffset, Is.EqualTo(-6f).Within(Tolerance), "the getter does not negate back");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // SetScaleX/Y/Z (0x0600005f..61) replace one localScale component and keep
        // the other two; set_LocalScaleZ tail-calls SetScaleZ.
        [Test]
        public void LocalScaleSettersReplaceOneComponent()
        {
            var modifier = Create(out GameObject owner);
            try
            {
                owner.transform.localScale = new Vector3(2f, 3f, 4f);
                modifier.LocalScaleX = 5f;
                Assert.That(owner.transform.localScale, Is.EqualTo(new Vector3(5f, 3f, 4f)));
                modifier.LocalScaleY = 6f;
                Assert.That(owner.transform.localScale, Is.EqualTo(new Vector3(5f, 6f, 4f)));
                modifier.LocalScaleZ = 7f;
                Assert.That(owner.transform.localScale, Is.EqualTo(new Vector3(5f, 6f, 7f)));
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
