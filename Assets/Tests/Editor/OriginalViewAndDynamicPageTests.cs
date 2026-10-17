using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for Hardlight.ViewBase (HLUnityUI.Runtime 0x060002b2..b6),
    // Hardlight.ViewParameters (0x060002b7..ba) and Hardlight.DynamicPageLoad
    // (0x06000518..0x0600051b), written from the shipped x86-64 bodies. Every case owns
    // inactive GameObjects; DynamicPageLoad entries are children of an inactive root, so
    // activating them changes activeSelf without running any lifecycle method.
    [TestFixture]
    public sealed class OriginalViewAndDynamicPageTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        sealed class Hosts : IDisposable
        {
            readonly List<GameObject> owned = new List<GameObject>();

            public GameObject Inactive(string name, Transform parent = null)
            {
                var owner = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                if (parent != null) owner.transform.SetParent(parent, false);
                else owned.Add(owner);
                return owner;
            }

            public void Dispose()
            {
                foreach (GameObject owner in owned)
                    if (owner != null) Object.DestroyImmediate(owner);
            }
        }

        // ---- ViewBase -------------------------------------------------------------

        [Test]
        public void SetupStoresTheParametersBeforeOnSetupAndCompletesAfterIt()
        {
            using (var hosts = new Hosts())
            {
                var view = hosts.Inactive("ViewBase fixture").AddComponent<OriginalViewBaseProbe>();
                var parameters = new ViewParameters();
                view.OnSetupComplete += () => view.Calls.Add("OnSetupComplete");

                view.Setup(parameters);

                Assert.That(view.Calls, Is.EqualTo(new[] { "OnSetup", "OnSetupComplete" }));
                Assert.That(view.ReceivedParameters, Is.SameAs(parameters));
                Assert.That(view.FieldDuringSetup, Is.SameAs(parameters), "Stored at 0x1b4d071 before the slot 4 call");
                Assert.That(view.ViewParameters, Is.SameAs(parameters));
            }
        }

        [Test]
        public void SetupWithoutCompletionListenersStillCallsOnSetup()
        {
            using (var hosts = new Hosts())
            {
                var view = hosts.Inactive("ViewBase fixture").AddComponent<OriginalViewBaseProbe>();
                Assert.That(view.OnSetupComplete, Is.Null, "No constructor allocation");
                Assert.DoesNotThrow(() => view.Setup(null));
                Assert.That(view.Calls, Is.EqualTo(new[] { "OnSetup" }));
                Assert.That(view.ViewParameters, Is.Null);
            }
        }

        [Test]
        public void RectTransformFallsBackToTheOwnTransformAndKeepsAnAssignedOne()
        {
            using (var hosts = new Hosts())
            {
                GameObject owner = hosts.Inactive("ViewBase fixture");
                var view = owner.AddComponent<OriginalViewBaseProbe>();
                FieldInfo field = typeof(ViewBase).GetField("m_rectTransform", Any);

                Assert.That(view.RectTransform, Is.SameAs(owner.GetComponent<RectTransform>()));
                Assert.That(field.GetValue(view), Is.SameAs(owner.GetComponent<RectTransform>()), "The lookup is cached");

                var other = (RectTransform)hosts.Inactive("Assigned RectTransform").transform;
                field.SetValue(view, other);
                Assert.That(view.RectTransform, Is.SameAs(other));
            }
        }

        [Test]
        public void RectTransformReplacesADestroyedAssignedTransform()
        {
            using (var hosts = new Hosts())
            {
                GameObject owner = hosts.Inactive("ViewBase fixture");
                var view = owner.AddComponent<OriginalViewBaseProbe>();
                FieldInfo field = typeof(ViewBase).GetField("m_rectTransform", Any);
                GameObject assigned = hosts.Inactive("Destroyed RectTransform");
                field.SetValue(view, assigned.transform);
                Object.DestroyImmediate(assigned);

                // 0x1b4d0fc compares with UnityEngine.Object.op_Equality, so a destroyed
                // transform counts as missing; a reference-null check would return it.
                RectTransform own = owner.GetComponent<RectTransform>();
                Assert.That(view.RectTransform, Is.SameAs(own));
                Assert.That(field.GetValue(view), Is.SameAs(own));
            }
        }

        [Test]
        public void SetActiveSetsTheOwnGameObject()
        {
            using (var hosts = new Hosts())
            {
                GameObject root = hosts.Inactive("Inactive root");
                GameObject owner = hosts.Inactive("ViewBase fixture", root.transform);
                var view = owner.AddComponent<OriginalViewBaseProbe>();
                view.SetActive(true);
                Assert.That(owner.activeSelf, Is.True);
                Assert.That(root.activeSelf, Is.False);
                view.SetActive(false);
                Assert.That(owner.activeSelf, Is.False);
            }
        }

        // ---- ViewParameters -------------------------------------------------------

        sealed class DerivedParameters : ViewParameters
        {
            public object Payload;
        }

        [Test]
        public void ViewParametersAreEqualExactlyWhenTheRuntimeTypesMatch()
        {
            var first = new ViewParameters();
            var second = new ViewParameters();
            var derived = new DerivedParameters();
            Assert.That(first.Equals(second), Is.True);
            Assert.That(derived.Equals(new DerivedParameters()), Is.True);
            Assert.That(first.Equals(derived), Is.False);
            Assert.That(derived.Equals(first), Is.False);
            Assert.That(first.Equals(null), Is.False);
            Assert.That(first.Equals("ViewParameters"), Is.False);

            // 0x1b4d240 tail-calls Object.GetHashCode, so equal instances keep identity hashes.
            Assert.That(first.GetHashCode(), Is.EqualTo(RuntimeHelpers.GetHashCode(first)));
            Assert.That(second.GetHashCode(), Is.EqualTo(RuntimeHelpers.GetHashCode(second)));
        }

        [Test]
        public void CloneIsAShallowCopyOfTheRuntimeType()
        {
            var payload = new object();
            var original = new DerivedParameters { Payload = payload };
            object clone = original.Clone();
            Assert.That(clone, Is.TypeOf<DerivedParameters>());
            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(((DerivedParameters)clone).Payload, Is.SameAs(payload));
        }

        // ---- DynamicPageLoad ------------------------------------------------------

        static readonly Type Pairing = typeof(DynamicPageLoad).GetNestedType("PageEnumPairing", BindingFlags.NonPublic);

        static DynamicPageLoad Loader(Hosts hosts, out GameObject root, params (int id, string name)[] entries)
        {
            root = hosts.Inactive("DynamicPageLoad fixture");
            var loader = root.AddComponent<DynamicPageLoad>();
            // GuiPageContent.OnShow/OnHide read the configuration; an all-false one
            // with the default NoChange background touches nothing else.
            var configuration = hosts.Inactive("UIRuntimeConfiguration fixture").AddComponent<UIRuntimeConfiguration>();
            typeof(GuiPageContent).GetField("m_internalUIRuntimeConfiguration", Any).SetValue(loader, configuration);

            Array pairs = Array.CreateInstance(Pairing, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                object pair = Activator.CreateInstance(Pairing, true);
                var entry = hosts.Inactive(entries[i].name, root.transform).AddComponent<OriginalPageContentProbe>();
                Pairing.GetField("m_dynamicID").SetValue(pair, (DynamicPageEntry)entries[i].id);
                Pairing.GetField("m_page").SetValue(pair, entry);
                pairs.SetValue(pair, i);
            }
            typeof(DynamicPageLoad).GetField("m_pageEntries", Any).SetValue(loader, pairs);
            return loader;
        }

        static OriginalPageContentProbe Entry(GameObject root, string name) =>
            root.transform.Find(name).GetComponent<OriginalPageContentProbe>();

        static GuiPageContent Active(DynamicPageLoad loader) =>
            (GuiPageContent)typeof(DynamicPageLoad).GetField("m_activeContent", Any).GetValue(loader);

        [Test]
        public void OnShowShowsEveryMatchingEntryAndTheLastBecomesActive()
        {
            using (var hosts = new Hosts())
            {
                DynamicPageLoad loader = Loader(hosts, out GameObject root, (11, "A"), (22, "B"), (11, "C"));
                var parameters = new DynamicPageLoadParameters { m_pageEntry = (DynamicPageEntry)11 };
                loader.OnShow(parameters);

                OriginalPageContentProbe a = Entry(root, "A"), b = Entry(root, "B"), c = Entry(root, "C");
                Assert.That(a.gameObject.activeSelf, Is.True);
                Assert.That(b.gameObject.activeSelf, Is.False);
                Assert.That(c.gameObject.activeSelf, Is.True);
                Assert.That(a.Shown, Is.EqualTo(new GuiPageParameters[] { parameters }));
                Assert.That(b.Shown, Is.Empty);
                Assert.That(c.Shown, Is.EqualTo(new GuiPageParameters[] { parameters }));
                Assert.That(Active(loader), Is.SameAs(c), "No break: the last match wins");
            }
        }

        [Test]
        public void OnShowActivatesEachEntryBeforeShowingItAndStoresItAfter()
        {
            using (var hosts = new Hosts())
            {
                DynamicPageLoad loader = Loader(hosts, out GameObject root, (11, "A"), (22, "B"), (11, "C"));
                OriginalPageContentProbe a = Entry(root, "A"), b = Entry(root, "B"), c = Entry(root, "C");
                foreach (OriginalPageContentProbe entry in new[] { a, b, c })
                    entry.ReadActiveContent = () => Active(loader);

                loader.OnShow(new DynamicPageLoadParameters { m_pageEntry = (DynamicPageEntry)11 });

                // Per match: SetActive(true) at 0x1b631ff, the slot 6 OnShow at 0x1b6321e,
                // then the m_activeContent store at 0x1b63231.
                Assert.That(a.ShownState, Is.EqualTo(new[] { (true, (GuiPageContent)null) }));
                Assert.That(b.ShownState, Is.Empty);
                Assert.That(c.ShownState, Is.EqualTo(new[] { (true, (GuiPageContent)a) }));
                Assert.That(Active(loader), Is.SameAs(c));
            }
        }

        [Test]
        public void OnHideForwardsToTheActiveEntryOnly()
        {
            using (var hosts = new Hosts())
            {
                DynamicPageLoad loader = Loader(hosts, out GameObject root, (11, "A"), (11, "C"));
                loader.OnShow(new DynamicPageLoadParameters { m_pageEntry = (DynamicPageEntry)11 });
                var hideParameters = new GuiPageParameters();
                loader.OnHide(hideParameters, UISystemClosureReason.AppRestartClose);

                Assert.That(Entry(root, "A").Hidden, Is.Empty);
                Assert.That(Entry(root, "C").Hidden, Is.EqualTo(new[] { ((GuiPageParameters)hideParameters, UISystemClosureReason.AppRestartClose) }));
            }
        }

        [Test]
        public void OnShowWithOtherParametersFaultsAtTheFirstEntry()
        {
            using (var hosts = new Hosts())
            {
                // The cast result is dereferenced per entry (0x1b631dc) without a null guard.
                DynamicPageLoad loader = Loader(hosts, out GameObject root, (11, "A"));
                Assert.Throws<NullReferenceException>(() => loader.OnShow(new GuiPageParameters()));
                Assert.That(Entry(root, "A").gameObject.activeSelf, Is.False);
                Assert.That(Entry(root, "A").Shown, Is.Empty);

                DynamicPageLoad empty = Loader(hosts, out GameObject _);
                Assert.DoesNotThrow(() => empty.OnShow(new GuiPageParameters()));
                Assert.That(Active(empty), Is.Null);
            }
        }
    }
}
