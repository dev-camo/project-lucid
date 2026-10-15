using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;
using Targets = Hardlight.HLButtonHighlighter.ButtonHighlightChangeTargets;
using ProjectLucid.Tests.SelectionShared;

namespace ProjectLucid.Editor
{
    // Owned inactive components cover synchronous public behavior only.
    // Active Apply/callback/animation paths and the original shrink fault remain separate.
    public sealed class OriginalHLButtonHighlighterTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private sealed class Owned : IDisposable
        {
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            public readonly GameObject Root;
            public readonly HLButtonHighlighter Highlighter;
            public readonly Canvas Canvas;
            public readonly Animation Animator;

            public Owned(bool rectTransform = true, bool nestedCanvas = false)
            {
                Root = rectTransform ? new GameObject("Owned original highlighter", typeof(RectTransform))
                    : new GameObject("Owned original highlighter without RectTransform");
                objects.Add(Root);
                try
                {
                    Root.SetActive(false);
                    if (nestedCanvas)
                    {
                        Assert.That(rectTransform, Is.True);
                        // Only these owned Unity Canvas objects are active. The game host stays inactive.
                        var parent = new GameObject("Owned registered parent Canvas", typeof(RectTransform));
                        objects.Insert(0, parent);
                        Canvas parentCanvas = parent.AddComponent<Canvas>();
                        var child = new GameObject("Owned registered child Canvas", typeof(RectTransform));
                        objects.Insert(1, child);
                        child.transform.SetParent(parent.transform, false);
                        Canvas = child.AddComponent<Canvas>();
                        Assert.That(ReferenceEquals(child.transform.parent, parent.transform), Is.True);
                        Assert.That(ReferenceEquals(Canvas.gameObject, child), Is.True);
                        Assert.That(ReferenceEquals(Canvas.rootCanvas, parentCanvas), Is.True);
                        Assert.That(parent.activeSelf && parent.activeInHierarchy && parentCanvas.isActiveAndEnabled, Is.True);
                        Assert.That(child.activeSelf && child.activeInHierarchy && Canvas.isActiveAndEnabled, Is.True);
                        Assert.That(parent.GetComponents<MonoBehaviour>(), Is.Empty);
                        Assert.That(child.GetComponents<MonoBehaviour>(), Is.Empty);
                        // Reverse-finally cleanup destroys the child before its parent, even after failure.
                    }
                    else
                    {
                        // The plain-Transform case must not acquire a Canvas dependency.
                        Canvas = rectTransform ? Root.AddComponent<Canvas>() : null;
                    }
                    Animator = Root.AddComponent<Animation>();
                    Highlighter = Root.AddComponent<HLButtonHighlighter>();
                    Assert.That(Highlighter, Is.Not.Null);
                    Assert.That(Root.activeSelf || Root.activeInHierarchy, Is.False);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public HLButtonHighlightCustomisations Asset(params Source[] sources)
            {
                var asset = ScriptableObject.CreateInstance<HLButtonHighlightCustomisations>();
                objects.Add(asset);
                Assert.That(asset, Is.Not.Null);
                asset.m_buttonHighlightLayerData = new List<Source>(sources);
                return asset;
            }

            private void DestroyAt(int index)
            {
                if (index < 0) return;
                try
                {
                    if (objects[index] != null) UnityEngine.Object.DestroyImmediate(objects[index]);
                }
                finally
                {
                    DestroyAt(index - 1);
                }
            }

            public void Dispose()
            {
                try { DestroyAt(objects.Count - 1); }
                finally { objects.Clear(); }
            }
        }

        private static T Read<T>(HLButtonHighlighter highlighter, string name)
        {
            FieldInfo field = typeof(HLButtonHighlighter).GetField(name, Declared);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.DeclaringType, Is.EqualTo(typeof(HLButtonHighlighter)));
            Assert.That(field.FieldType, Is.EqualTo(typeof(T)));
            return (T)field.GetValue(highlighter);
        }

        private static SerializedProperty Property(SerializedObject target, string name)
        {
            SerializedProperty property = target.FindProperty(name);
            Assert.That(property, Is.Not.Null);
            return property;
        }

        private static void Wire(Owned owned, HLButtonHighlightCustomisations defaultAsset)
        {
            using (var serialized = new SerializedObject(owned.Highlighter))
            {
                serialized.Update();
                Property(serialized, "m_canvas").objectReferenceValue = owned.Canvas;
                Property(serialized, "m_defaultCustomisation").objectReferenceValue = defaultAsset;
                Property(serialized, "m_customisableAnimator").objectReferenceValue = owned.Animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Canvas observedCanvas = Read<Canvas>(owned.Highlighter, "m_canvas");
            if (owned.Canvas == null)
                Assert.That(observedCanvas == null, Is.True); // Unity's unassigned serialized reference.
            else
            {
                Assert.That(ReferenceEquals(observedCanvas, owned.Canvas), Is.True);
                Assert.That(observedCanvas == owned.Canvas, Is.True);
            }
            Assert.That(ReferenceEquals(Read<HLButtonHighlightCustomisations>(owned.Highlighter,
                "m_defaultCustomisation"), defaultAsset), Is.True);
            Assert.That(ReferenceEquals(Read<Animation>(owned.Highlighter, "m_customisableAnimator"),
                owned.Animator), Is.True);
        }

        private static void Method(Type type, string name, Type result, int flags, Type[] parameters,
            string[] names)
        {
            MethodInfo method = type.GetMethod(name, Declared, null, parameters, null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.DeclaringType, Is.EqualTo(type));
            Assert.That(method.ReturnType, Is.EqualTo(result));
            Assert.That((int)method.Attributes, Is.EqualTo(flags));
            Assert.That(method.IsGenericMethod, Is.False);
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(names));
        }

        private static void Fields(Type owner, string[] names, Type[] types, int flags)
        {
            FieldInfo[] fields = owner.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(names));
            Assert.That(fields.Select(f => f.FieldType), Is.EqualTo(types));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.DeclaringType, Is.EqualTo(owner));
                Assert.That((int)field.Attributes, Is.EqualTo(flags));
            }
        }

        private static void Anchors(RectTransform rect)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.pivot, Is.EqualTo(Vector2.zero));
        }

        private static void ChangeAnchors(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.2f, 0.3f);
            rect.anchorMax = new Vector2(0.7f, 0.8f);
            rect.pivot = new Vector2(0.4f, 0.6f);
        }

        [Test]
        public void OriginalDeclarationsAndAbstractModificationContract()
        {
            Type type = typeof(HLButtonHighlighter);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.HLButtonHighlighter"));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetMethods(Declared).Length, Is.EqualTo(8));
            Assert.That(type.GetConstructors(Declared).Length, Is.EqualTo(1));
            Assert.That(type.GetFields(Declared).Length, Is.EqualTo(9));
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Method(type, "Awake", typeof(void), 129, Type.EmptyTypes, new string[0]);
            Method(type, "SetButtonState", typeof(void), 134,
                new[] { typeof(HLButtonHighlightCustomisations), typeof(bool) },
                new[] { "state", "setInstantTransition" });
            Method(type, "SetButtonCanvasLayer", typeof(void), 134,
                new[] { typeof(int), typeof(int) }, new[] { "sortingLayer", "sortingOrder" });
            Method(type, "SetButtonCanvasLayer", typeof(void), 134,
                new[] { typeof(string), typeof(int) }, new[] { "sortingLayer", "sortingOrder" });
            foreach (Type first in new[] { typeof(int), typeof(string) })
            {
                ParameterInfo parameter = type.GetMethod("SetButtonCanvasLayer", Declared, null,
                    new[] { first, typeof(int) }, null).GetParameters()[1];
                Assert.That(parameter.IsOptional && parameter.HasDefaultValue, Is.True);
                Assert.That(parameter.DefaultValue, Is.EqualTo(0));
                Assert.That((int)parameter.Attributes, Is.EqualTo(4112));
            }
            Method(type, "ResetCanvasLayer", typeof(void), 134, Type.EmptyTypes, new string[0]);
            Method(type, "ApplyCurrentCustomisation", typeof(IEnumerator), 129,
                new[] { typeof(bool) }, new[] { "setInstantTransition" });
            Method(type, "ReactivateBehaviours", typeof(void), 134, Type.EmptyTypes, new string[0]);
            Method(type, "ResetAnchorIfNeeded", typeof(void), 129, Type.EmptyTypes, new string[0]);
            Fields(type, new[] { "m_canvas", "m_defaultCustomisation", "m_customisableLayers",
                "m_customisableAnimator", "m_requiredComponents", "m_currentChangeSource",
                "m_cachedChangeSource", "m_cachedCustomisation", "m_coroutine" },
                new[] { typeof(Canvas), typeof(HLButtonHighlightCustomisations), typeof(List<Targets>),
                typeof(Animation), typeof(List<Behaviour>), typeof(List<Source>), typeof(List<Source>),
                typeof(HLButtonHighlightCustomisations), typeof(Coroutine) }, 1);
            string[] serialized = { "m_canvas", "m_defaultCustomisation", "m_customisableLayers",
                "m_customisableAnimator", "m_requiredComponents" };
            foreach (FieldInfo field in type.GetFields(Declared))
            {
                Assert.That((int)field.Attributes, Is.EqualTo(1));
                var attributes = field.GetCustomAttributesData();
                Assert.That(attributes.Count, Is.EqualTo(serialized.Contains(field.Name) ? 1 : 0));
                if (attributes.Count == 1)
                {
                    Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(SerializeField)));
                    Assert.That(attributes[0].ConstructorArguments, Is.Empty);
                    Assert.That(attributes[0].NamedArguments, Is.Empty);
                }
            }

            Type targets = typeof(Targets);
            Assert.That(targets.DeclaringType, Is.EqualTo(type));
            Assert.That((int)targets.Attributes, Is.EqualTo(1056770));
            Assert.That(targets.IsSerializable, Is.True);
            Assert.That(targets.GetFields(Declared).Single().FieldType, Is.EqualTo(typeof(ModifiedOnUISelection[])));
            Assert.That(targets.GetFields(Declared).Single().Name, Is.EqualTo("UISelectionModifications"));
            Assert.That(targets.GetMethods(Declared).Length, Is.EqualTo(5));
            foreach (MethodInfo callback in targets.GetMethods(Declared))
                Assert.That((int)callback.Attributes, Is.EqualTo(134));

            Type asset = typeof(HLButtonHighlightCustomisations);
            Assert.That(asset.FullName, Is.EqualTo("HLButtonHighlightCustomisations"));
            Assert.That(asset.BaseType, Is.EqualTo(typeof(ScriptableObject)));
            Assert.That((int)asset.Attributes, Is.EqualTo(1048833));
            Assert.That(asset.GetFields(Declared).Select(f => f.Name),
                Is.EquivalentTo(new[] { "m_buttonHighlightLayerData", "m_animationClip", "m_animationWrap" }));
            Assert.That(typeof(Source).DeclaringType, Is.EqualTo(asset));
            Assert.That((int)typeof(Source).Attributes, Is.EqualTo(1056770));
            Assert.That(typeof(Source).GetFields(Declared).Select(f => f.Name), Is.EquivalentTo(new[]
                { "Enabled", "Top", "Right", "Bottom", "Left", "Sprite", "Material", "Color", "SkewX", "SkewY" }));
            Fields(asset, new[] { "m_buttonHighlightLayerData", "m_animationClip", "m_animationWrap" },
                new[] { typeof(List<Source>), typeof(AnimationClip), typeof(WrapMode) }, 6);
            Fields(typeof(Source), new[] { "Enabled", "Top", "Right", "Bottom", "Left", "Sprite",
                "Material", "Color", "SkewX", "SkewY" }, new[] { typeof(bool), typeof(float),
                typeof(float), typeof(float), typeof(float), typeof(Sprite), typeof(Material),
                typeof(Color), typeof(float), typeof(float) }, 6);
            var source = new Source();
            Assert.That(source.Enabled, Is.False);
            foreach (float value in new[] { source.Top, source.Right, source.Bottom, source.Left,
                source.SkewX, source.SkewY }) Assert.That(value, Is.EqualTo(0f));
            Assert.That(source.Color, Is.EqualTo(Color.white));
            Assert.That(source.Sprite, Is.Null);
            Assert.That(source.Material, Is.Null);

            Type modification = typeof(ModifiedOnUISelection);
            Assert.That(modification.FullName, Is.EqualTo("Hardlight.ModifiedOnUISelection"));
            Assert.That(modification.Assembly, Is.EqualTo(type.Assembly));
            Assert.That(modification.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)modification.Attributes, Is.EqualTo(1048705));
            Assert.That(modification.GetMethods(Declared).Length, Is.EqualTo(5));
            Assert.That(modification.GetFields(Declared), Is.Empty);
            Method(modification, "OnHighlightBegin", typeof(void), 454,
                new[] { typeof(Source), typeof(Source) }, new[] { "previousSource", "currentSource" });
            Method(modification, "OnHighlightUpdated", typeof(void), 1478,
                new[] { typeof(Source), typeof(Source), typeof(float) },
                new[] { "previousSource", "currentSource", "lerpValue" });
            Method(modification, "OnHighlightEnd", typeof(void), 454,
                new[] { typeof(Source), typeof(Source) }, new[] { "previousSource", "currentSource" });
            Method(modification, "OnActivate", typeof(void), 454, new[] { typeof(Source) }, new[] { "source" });
            Method(modification, "OnDeactivate", typeof(void), 454, new[] { typeof(Source) }, new[] { "source" });
            ConstructorInfo constructor = modification.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6276));
            Assert.That(constructor.GetParameters(), Is.Empty);
            Assert.That(modification.GetMethod("OnHighlightUpdated", Declared).IsAbstract, Is.True);
            // No abstract component or invented callback implementation is constructed.
        }

        [Test]
        public void OrdinaryTargetArraysPreserveEmptyAndNullFaults()
        {
            var targets = new Targets();
            Assert.That(targets.UISelectionModifications, Is.Null);
            Assert.Throws<NullReferenceException>(() => targets.Activate(null));
            targets.UISelectionModifications = new ModifiedOnUISelection[0];
            Assert.DoesNotThrow(() => targets.Activate(null));
            Assert.DoesNotThrow(() => targets.Deactivate(null));
            Assert.DoesNotThrow(() => targets.OnHighlightBegin(null, null));
            Assert.DoesNotThrow(() => targets.OnHighlightUpdated(null, null, float.NaN));
            Assert.DoesNotThrow(() => targets.OnHighlightEnd(null, null));
            targets.UISelectionModifications = new ModifiedOnUISelection[] { null };
            Assert.Throws<NullReferenceException>(() => targets.Activate(null));
            Assert.Throws<NullReferenceException>(() => targets.Deactivate(null));
            Assert.Throws<NullReferenceException>(() => targets.OnHighlightBegin(null, null));
            Assert.Throws<NullReferenceException>(() => targets.OnHighlightUpdated(null, null, 1f));
            Assert.Throws<NullReferenceException>(() => targets.OnHighlightEnd(null, null));
        }

        // ButtonHighlightChangeTargets 0x06000438..0x0600043c (x86-64 0x1b5b5d0,
        // 0x1b5b630, 0x1b5b690, 0x1b5b6f0, 0x1b5b770): each loop rereads the array
        // field every iteration, walks it upward and makes one virtual call with
        // its arguments passed through unchanged. Vtable slots 0x178/0x188/0x198/
        // 0x1a8/0x1b8 are OnHighlightBegin/Updated/End/OnActivate/OnDeactivate, so
        // Activate reaches OnActivate and Deactivate reaches OnDeactivate.
        // Mutations caught: swapped Activate/Deactivate targets, swapped previous
        // and current sources, a substituted lerp value, reverse iteration, and a
        // loop over a cached array (the mid-dispatch replacement is ignored).
        [Test]
        public void OwnedTargetsDispatchEachCallbackToItsSlotInAscendingOrder()
        {
            var roots = new List<GameObject>();
            try
            {
                var calls = new List<OriginalSelectionModificationProbe.Call>();
                OriginalSelectionModificationProbe first = Probe("Owned first selection modification", roots, calls);
                OriginalSelectionModificationProbe second = Probe("Owned second selection modification", roots, calls);
                OriginalSelectionModificationProbe third = Probe("Owned replacement selection modification", roots, calls);
                var targets = new Targets { UISelectionModifications = new ModifiedOnUISelection[] { first, second } };
                var previous = new Source { Top = 1f };
                var current = new Source { Top = 2f };
                const float lerp = 0.375f;

                targets.Activate(current);
                Calls(calls, first, "OnActivate", null, current, float.NaN, second, "OnActivate", null, current, float.NaN);
                targets.Deactivate(previous);
                Calls(calls, first, "OnDeactivate", null, previous, float.NaN, second, "OnDeactivate", null, previous, float.NaN);
                targets.OnHighlightBegin(previous, current);
                Calls(calls, first, "OnHighlightBegin", previous, current, float.NaN, second, "OnHighlightBegin", previous, current, float.NaN);
                targets.OnHighlightUpdated(previous, current, lerp);
                Calls(calls, first, "OnHighlightUpdated", previous, current, lerp, second, "OnHighlightUpdated", previous, current, lerp);
                targets.OnHighlightEnd(previous, current);
                Calls(calls, first, "OnHighlightEnd", previous, current, float.NaN, second, "OnHighlightEnd", previous, current, float.NaN);

                var replacement = new ModifiedOnUISelection[] { first, third };
                first.OnCall = probe => targets.UISelectionModifications = replacement;
                targets.Activate(current);
                first.OnCall = null;
                Calls(calls, first, "OnActivate", null, current, float.NaN, third, "OnActivate", null, current, float.NaN);
                Assert.That(ReferenceEquals(targets.UISelectionModifications, replacement), Is.True);
                foreach (GameObject root in roots) Assert.That(root.activeSelf || root.activeInHierarchy, Is.False);
            }
            finally
            {
                for (int i = roots.Count - 1; i >= 0; --i)
                    if (roots[i] != null) UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }

        private static OriginalSelectionModificationProbe Probe(string name, List<GameObject> roots,
            List<OriginalSelectionModificationProbe.Call> calls)
        {
            var root = new GameObject(name);
            roots.Add(root);
            root.SetActive(false);
            var probe = root.AddComponent<OriginalSelectionModificationProbe>();
            Assert.That(probe, Is.Not.Null);
            probe.Calls = calls;
            return probe;
        }

        // Expects the recorded calls as (receiver, method, previous, current, lerp) groups, then clears.
        private static void Calls(List<OriginalSelectionModificationProbe.Call> calls, params object[] expected)
        {
            Assert.That(calls.Count, Is.EqualTo(expected.Length / 5));
            for (int i = 0; i < calls.Count; ++i)
            {
                OriginalSelectionModificationProbe.Call call = calls[i];
                Assert.That(ReferenceEquals(call.Receiver, expected[i * 5]), Is.True, "receiver " + i);
                Assert.That(call.Method, Is.EqualTo(expected[i * 5 + 1]), "method " + i);
                Assert.That(ReferenceEquals(call.Previous, expected[i * 5 + 2]), Is.True, "previous source " + i);
                Assert.That(ReferenceEquals(call.Current, expected[i * 5 + 3]), Is.True, "current source " + i);
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(call.Lerp), 0),
                    Is.EqualTo(BitConverter.ToInt32(BitConverter.GetBytes((float)expected[i * 5 + 4]), 0)), "lerp " + i);
            }
            calls.Clear();
        }

        // 0x06000432 x86-64 0x1b5b470 (int) and 0x06000431 x86-64 0x1b599c0 (string)
        // set overrideSorting, then the sorting layer, then the order; 0x06000433
        // x86-64 0x1b59a10 only clears overrideSorting. Distinct non-Default layers
        // from ProjectSettings/TagManager.asset (Menu Foreground 13, Menu Background
        // 9) make each layer write observable. Mutations caught: dropping either
        // overload's layer write, or a reset that also restores layer or order.
        [Test]
        public void OwnedInactiveCanvasLayerOverloadsWriteDistinctLayersThatResetKeeps()
        {
            using (var owned = new Owned(nestedCanvas: true))
            {
                Wire(owned, null);
                int foreground = SortingLayer.NameToID("Menu Foreground");
                int background = SortingLayer.NameToID("Menu Background");
                Assert.That(foreground, Is.EqualTo(13));
                Assert.That(background, Is.EqualTo(9));
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Default")));
                owned.Highlighter.SetButtonCanvasLayer(foreground, 17);
                Assert.That(owned.Canvas.overrideSorting, Is.True);
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(foreground));
                Assert.That(owned.Canvas.sortingLayerName, Is.EqualTo("Menu Foreground"));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(17));
                owned.Highlighter.SetButtonCanvasLayer("Menu Background", 5);
                Assert.That(owned.Canvas.overrideSorting, Is.True);
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(background));
                Assert.That(owned.Canvas.sortingLayerName, Is.EqualTo("Menu Background"));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(5));
                owned.Highlighter.ResetCanvasLayer();
                Assert.That(owned.Canvas.overrideSorting, Is.False);
                // Without override this nested canvas reports its parent's sorting; turning the
                // override back on shows the canvas's own values, which the reset left in place.
                owned.Canvas.overrideSorting = true;
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(background));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(5));
                Assert.That(owned.Root.activeSelf || owned.Root.activeInHierarchy, Is.False);
            }
        }

        [Test]
        public void OwnedInactiveCanvasLayerOverloadsAndReset()
        {
            // A nested Canvas exposes overrideSorting; a root Canvas ignores that override.
            using (var owned = new Owned(nestedCanvas: true))
            {
                Wire(owned, null);
                Assert.That(owned.Canvas.overrideSorting, Is.False);
                int defaultLayer = SortingLayer.NameToID("Default");
                owned.Highlighter.SetButtonCanvasLayer(defaultLayer, 17);
                Assert.That(owned.Canvas.overrideSorting, Is.True);
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(defaultLayer));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(17));
                owned.Highlighter.SetButtonCanvasLayer("Default");
                Assert.That(owned.Canvas.overrideSorting, Is.True);
                Assert.That(owned.Canvas.sortingLayerName, Is.EqualTo("Default"));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(0));
                owned.Highlighter.ResetCanvasLayer();
                Assert.That(owned.Canvas.overrideSorting, Is.False);
                Assert.That(owned.Canvas.sortingLayerID, Is.EqualTo(defaultLayer));
                Assert.That(owned.Canvas.sortingOrder, Is.EqualTo(0));
                Assert.That(owned.Root.activeSelf || owned.Root.activeInHierarchy, Is.False);
            }
        }

        [Test]
        public void InactiveStatesCopyListsAndResetAnchorsWithoutCoroutine()
        {
            using (var owned = new Owned())
            {
                var first = new Source { Enabled = true, Top = 2f };
                var second = new Source { Right = 3f };
                var next = new Source { Bottom = 4f };
                var defaultAsset = owned.Asset(first, second);
                var nextAsset = owned.Asset(next);
                Assert.That(defaultAsset.m_animationWrap, Is.EqualTo(WrapMode.Once));
                Assert.That(defaultAsset.m_animationClip, Is.Null);
                Wire(owned, defaultAsset);
                var current = Read<List<Source>>(owned.Highlighter, "m_currentChangeSource");
                var cached = Read<List<Source>>(owned.Highlighter, "m_cachedChangeSource");
                Assert.That(current, Is.Empty);
                Assert.That(cached, Is.Empty);
                Assert.That(ReferenceEquals(current, cached), Is.False);
                var rect = (RectTransform)owned.Root.transform;
                ChangeAnchors(rect);
                owned.Highlighter.SetButtonState(null, true);
                Anchors(rect);
                Assert.That(ReferenceEquals(Read<List<Source>>(owned.Highlighter, "m_currentChangeSource"), current), Is.True);
                Assert.That(ReferenceEquals(Read<List<Source>>(owned.Highlighter, "m_cachedChangeSource"), cached), Is.True);
                Assert.That(ReferenceEquals(Read<HLButtonHighlightCustomisations>(owned.Highlighter,
                    "m_cachedCustomisation"), defaultAsset), Is.True);
                Assert.That(current.Count, Is.EqualTo(2));
                Assert.That(ReferenceEquals(current[0], first) && ReferenceEquals(current[1], second), Is.True);
                Assert.That(cached, Is.Empty);
                Assert.That(ReferenceEquals(current, defaultAsset.m_buttonHighlightLayerData), Is.False);
                defaultAsset.m_buttonHighlightLayerData.Clear();
                Assert.That(current.Count, Is.EqualTo(2));
                ChangeAnchors(rect);
                owned.Highlighter.SetButtonState(nextAsset, false);
                Anchors(rect);
                Assert.That(cached.Count, Is.EqualTo(2));
                Assert.That(ReferenceEquals(cached[0], first) && ReferenceEquals(cached[1], second), Is.True);
                Assert.That(current.Count, Is.EqualTo(1));
                Assert.That(ReferenceEquals(current[0], next), Is.True);
                Assert.That(ReferenceEquals(Read<HLButtonHighlightCustomisations>(owned.Highlighter,
                    "m_cachedCustomisation"), nextAsset), Is.True);
                Assert.That(Read<Coroutine>(owned.Highlighter, "m_coroutine"), Is.Null);
                Assert.That(owned.Root.activeSelf || owned.Root.activeInHierarchy, Is.False);
                // The active shrink path is intentionally not entered or repaired here.
            }
        }

        [Test]
        public void NullDefaultFaultFollowsAnchorAndCacheRotation()
        {
            using (var owned = new Owned())
            {
                var source = new Source();
                var asset = owned.Asset(source);
                Wire(owned, null);
                owned.Highlighter.SetButtonState(asset, false);
                var current = Read<List<Source>>(owned.Highlighter, "m_currentChangeSource");
                var cached = Read<List<Source>>(owned.Highlighter, "m_cachedChangeSource");
                var rect = (RectTransform)owned.Root.transform;
                ChangeAnchors(rect);
                Assert.Throws<NullReferenceException>(() => owned.Highlighter.SetButtonState(null, true));
                Anchors(rect);
                Assert.That(current, Is.Empty);
                Assert.That(cached.Count, Is.EqualTo(1));
                Assert.That(ReferenceEquals(cached[0], source), Is.True);
                Assert.That(ReferenceEquals(Read<List<Source>>(owned.Highlighter, "m_currentChangeSource"), current), Is.True);
                Assert.That(ReferenceEquals(Read<List<Source>>(owned.Highlighter, "m_cachedChangeSource"), cached), Is.True);
                Assert.That(Read<HLButtonHighlightCustomisations>(owned.Highlighter, "m_cachedCustomisation"), Is.Null);
                Assert.That(Read<Coroutine>(owned.Highlighter, "m_coroutine"), Is.Null);
            }
        }

        [Test]
        public void MissingRectTransformFaultPrecedesStateMutation()
        {
            using (var owned = new Owned(false))
            {
                var asset = owned.Asset(new Source());
                Wire(owned, asset);
                Assert.That(owned.Root.transform is RectTransform, Is.False);
                var current = Read<List<Source>>(owned.Highlighter, "m_currentChangeSource");
                var cached = Read<List<Source>>(owned.Highlighter, "m_cachedChangeSource");
                Assert.Throws<NullReferenceException>(() => owned.Highlighter.SetButtonState(asset, false));
                Assert.That(current, Is.Empty);
                Assert.That(cached, Is.Empty);
                Assert.That(Read<HLButtonHighlightCustomisations>(owned.Highlighter, "m_cachedCustomisation"), Is.Null);
                Assert.That(Read<Coroutine>(owned.Highlighter, "m_coroutine"), Is.Null);
            }
        }

        [Test]
        public void GenuineRequiredBehavioursEnableBeforeNullElementFault()
        {
            using (var owned = new Owned())
            {
                Wire(owned, null);
                Assert.That(Read<List<Behaviour>>(owned.Highlighter, "m_requiredComponents"), Is.Empty);
                Assert.DoesNotThrow(() => owned.Highlighter.ReactivateBehaviours());
                using (var serialized = new SerializedObject(owned.Highlighter))
                {
                    serialized.Update();
                    SerializedProperty list = Property(serialized, "m_requiredComponents");
                    list.arraySize = 2;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = owned.Canvas;
                    list.GetArrayElementAtIndex(1).objectReferenceValue = owned.Animator;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var required = Read<List<Behaviour>>(owned.Highlighter, "m_requiredComponents");
                Assert.That(required.Count, Is.EqualTo(2));
                Assert.That(ReferenceEquals(required[0], owned.Canvas) && ReferenceEquals(required[1], owned.Animator), Is.True);
                owned.Canvas.enabled = false;
                owned.Animator.enabled = false;
                owned.Highlighter.enabled = false;
                owned.Highlighter.ReactivateBehaviours();
                Assert.That(owned.Canvas.enabled && owned.Animator.enabled, Is.True);
                Assert.That(owned.Highlighter.enabled, Is.False);
                using (var serialized = new SerializedObject(owned.Highlighter))
                {
                    serialized.Update();
                    SerializedProperty list = Property(serialized, "m_requiredComponents");
                    list.arraySize = 3;
                    list.GetArrayElementAtIndex(2).objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                owned.Canvas.enabled = false;
                owned.Animator.enabled = false;
                // The Editor's serialized unassigned Behaviour throws at its enabled setter.
                // The two earlier genuine entries must still have been enabled.
                Assert.Throws<UnassignedReferenceException>(() => owned.Highlighter.ReactivateBehaviours());
                Assert.That(owned.Canvas.enabled && owned.Animator.enabled, Is.True);
                Assert.That(owned.Highlighter.enabled, Is.False);
                Assert.That(owned.Root.activeSelf || owned.Root.activeInHierarchy, Is.False);
            }
        }
    }
}
