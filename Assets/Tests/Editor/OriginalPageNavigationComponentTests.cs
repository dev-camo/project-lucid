using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour specification for the HLUnityUI.Runtime page components that drive the
    // shared page systems: MoveBackMenuPage, OpenMenuPage, OpenDynamicMenuPage,
    // CloseAllMenuPages, OpenDialogPage, CloseDialogPage, OpenCloseOverlayPage,
    // ToggleOverlayPage and the Dialog/Menu system listeners (0x06000513..0x06000533),
    // written from the shipped x86-64 bodies, plus the generated closure identities of
    // PageStackSystem<T> and OverlaySystem from the shipped metadata.
    //
    // MenuSystem.Instance, DialogSystem.Instance, OverlaySystem.Instance and
    // GuiPageRegistry.Instance are process-wide. Each case runs inside scopes that save
    // every mutable field of the systems it touches, install empty state, and put the
    // saved values back on exit. Requests are observed without loading scenes:
    //  - a page stack in the OpeningPage state queues each request as a closure whose
    //    captured fields carry the forwarded arguments;
    //  - a page stack with records but no current page records AddPage requests as
    //    pending records and Enqueue requests as queue insertions;
    //  - an overlay identifier that resolves to a registered page in the Closing state
    //    turns ShowPage into a pending show, and HidePage of that page removes it.
    [TestFixture]
    public sealed class OriginalPageNavigationComponentTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const int Empty = 0, OpeningPage = 2, ClosingPage = 3;

        static object Get(object target, string name) => FieldOf(target.GetType(), name).GetValue(target);
        static void Set(object target, string name, object value) => FieldOf(target.GetType(), name).SetValue(target, value);

        static FieldInfo FieldOf(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        // ---- page stack state -----------------------------------------------------

        sealed class StackScope<T> : IDisposable where T : GuiPage
        {
            static readonly string[] Owned =
            {
                "m_systemFrozen", "m_state", "m_removeFromStack", "m_pending", "m_currentPage", "m_pageQueue",
                "m_pendingPageStackCommands", "OnSystemClosed", "OnSystemOpened", "OnGUIPageClosed", "OnGUIPageOpened",
                "OnGUIPageClosing", "OnGUIPageOpening"
            };

            readonly PageStackSystem<T> system;
            readonly object[] saved;
            readonly List<GameObject> hosts = new List<GameObject>();

            public StackScope(PageStackSystem<T> system)
            {
                this.system = system;
                saved = Owned.Select(name => Get(system, name)).ToArray();
                Set(system, "m_systemFrozen", false);
                SetState(Empty);
                Set(system, "m_removeFromStack", false);
                Set(system, "m_pending", new List<GuiPageRecord<T>>());
                Set(system, "m_currentPage", null);
                Set(system, "m_pageQueue", new LinkedList<GuiPageRecord<T>>());
                Set(system, "m_pendingPageStackCommands", new Queue<Action>());
            }

            public void SetState(int state) =>
                Set(system, "m_state", Enum.ToObject(typeof(PageStackSystem<T>.State), state));

            public void Seed(params int[] identifiers)
            {
                foreach (int identifier in identifiers) Queue.AddLast(new GuiPageRecord<T>(identifier, null, null));
            }

            public void SeedPending(int identifier) => Pending.Add(new GuiPageRecord<T>(identifier, null, null));

            public T CurrentPage(bool canLeave)
            {
                var owner = new GameObject(typeof(T).Name + " fixture") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                hosts.Add(owner);
                T page = owner.AddComponent<T>();
                page.CanLeavePage = canLeave;
                Set(system, "m_currentPage", page);
                return page;
            }

            public LinkedList<GuiPageRecord<T>> Queue => (LinkedList<GuiPageRecord<T>>)Get(system, "m_pageQueue");
            public List<GuiPageRecord<T>> Pending => (List<GuiPageRecord<T>>)Get(system, "m_pending");
            public Queue<Action> Commands => (Queue<Action>)Get(system, "m_pendingPageStackCommands");
            public int[] QueueIds => Queue.Select(record => record.m_identifier).ToArray();
            public int[] PendingIds => Pending.Select(record => record.m_identifier).ToArray();
            public void Raise(string evt) => ((Action)Get(system, evt)).Invoke();

            // The only queued command's closure. Its captured fields identify the queuing
            // method; generated closure names are checked by the dedicated emission tests.
            public object Command()
            {
                Assert.That(Commands.Count, Is.EqualTo(1), "Exactly one deferred command");
                return Commands.Peek().Target;
            }

            public void Dispose()
            {
                try
                {
                    for (int i = 0; i < Owned.Length; i++) Set(system, Owned[i], saved[i]);
                }
                finally
                {
                    foreach (GameObject owner in hosts)
                        if (owner != null) Object.DestroyImmediate(owner);
                }
            }
        }

        static object Captured(object closure, string name)
        {
            FieldInfo field = closure.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, "Deferred command captures " + name);
            return field.GetValue(closure);
        }

        static T Component<T>(List<GameObject> owned) where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " fixture") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            owned.Add(owner);
            return owner.AddComponent<T>();
        }

        static void Destroy(List<GameObject> owned)
        {
            foreach (GameObject owner in owned)
                if (owner != null) Object.DestroyImmediate(owner);
        }

        // ---- menu components ------------------------------------------------------

        [Test]
        public void OpenMenuPageForwardsThePageAndTheReplaceFlag()
        {
            var owned = new List<GameObject>();
            try
            {
                foreach (bool replace in new[] { true, false })
                {
                    using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                    {
                        stack.Seed(7);
                        stack.SetState(OpeningPage);
                        var component = Component<OpenMenuPage>(owned);
                        Set(component, "m_pageToOpen", (MenuPageIdentifier)1234);
                        Set(component, "m_replaceCurrent", replace);
                        component.OpenPage();

                        object closure = stack.Command();
                        Assert.That(Captured(closure, "identifier"), Is.EqualTo(1234));
                        Assert.That(Captured(closure, "replaceCurrent"), Is.EqualTo(replace));
                        Assert.That(Captured(closure, "data"), Is.Null);
                        Assert.That(Captured(closure, "callback"), Is.Null);
                    }
                }

                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(7);
                    stack.SetState(OpeningPage);
                    Component<OpenMenuPage>(owned).OpenPage();
                    Assert.That(stack.Commands, Is.Empty, "MenuPageIdentifier.None opens nothing");
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void OpenDynamicMenuPageSendsTheChosenEntryAsPageData()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(7);
                    stack.SetState(OpeningPage);
                    var component = Component<OpenDynamicMenuPage>(owned);
                    Set(component, "m_pageToOpen", (MenuPageIdentifier)55);
                    Set(component, "m_replaceCurrent", true);
                    Set(component, "m_dynamicPageChoice", (DynamicPageEntry)321);
                    component.OpenPage();

                    object closure = stack.Command();
                    Assert.That(Captured(closure, "identifier"), Is.EqualTo(55));
                    Assert.That(Captured(closure, "replaceCurrent"), Is.True);
                    Assert.That(Captured(closure, "callback"), Is.Null);
                    var data = Captured(closure, "data") as DynamicPageLoadParameters;
                    Assert.That(data, Is.Not.Null, "MoveToPageWithData receives DynamicPageLoadParameters");
                    Assert.That(data.m_pageEntry, Is.EqualTo((DynamicPageEntry)321));
                    Assert.That(data.Selectable, Is.Null);
                }

                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(7);
                    stack.SetState(OpeningPage);
                    var component = Component<OpenDynamicMenuPage>(owned);
                    Set(component, "m_dynamicPageChoice", (DynamicPageEntry)321);
                    component.OpenPage();
                    Assert.That(stack.Commands, Is.Empty, "MenuPageIdentifier.None opens nothing");
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void MoveBackMenuPageMovesBackWhenItCanAndOnlyThenLeavesTheCurrentPage()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(1, 2);
                    stack.SetState(OpeningPage);
                    stack.CurrentPage(canLeave: true);
                    var component = Component<MoveBackMenuPage>(owned);
                    Set(component, "m_defaultPage", (MenuPageIdentifier)99);
                    component.OpenPage();

                    // MoveToPreviousPage -> CloseCurrent(true, StandardClose), deferred by the transition.
                    object closure = stack.Command();
                    Assert.That(Captured(closure, "removeFromStack"), Is.True);
                    Assert.That(Captured(closure, "reasonForClosure"), Is.EqualTo(UISystemClosureReason.StandardClose));
                }

                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(1, 2);
                    stack.SetState(OpeningPage);
                    stack.CurrentPage(canLeave: false);
                    var component = Component<MoveBackMenuPage>(owned);
                    Set(component, "m_defaultPage", (MenuPageIdentifier)99);
                    component.OpenPage();
                    Assert.That(stack.Commands, Is.Empty, "A page that cannot be left blocks the move and the default page");
                    Assert.That(stack.QueueIds, Is.EqualTo(new[] { 1, 2 }));
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void MoveBackMenuPageReplacesWithTheDefaultPageOnlyWhenItCannotMoveBack()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(1);
                    stack.SetState(OpeningPage);
                    var component = Component<MoveBackMenuPage>(owned);
                    Set(component, "m_defaultPage", (MenuPageIdentifier)99);
                    Assert.That(component.GetDefaultPage(), Is.EqualTo((MenuPageIdentifier)99));
                    component.OpenPage();

                    // MoveToPage(m_defaultPage, true) at 0x1b633a4 (edx = 1).
                    object closure = stack.Command();
                    Assert.That(Captured(closure, "identifier"), Is.EqualTo(99));
                    Assert.That(Captured(closure, "replaceCurrent"), Is.True);
                }

                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(1);
                    stack.SetState(OpeningPage);
                    Component<MoveBackMenuPage>(owned).OpenPage();
                    Assert.That(stack.Commands, Is.Empty, "MenuPageIdentifier.None is not opened");
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void CloseAllMenuPagesClosesWithTheStandardReason()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<MenuPage>(MenuSystem.Instance))
                {
                    stack.Seed(1);
                    stack.SeedPending(9);
                    stack.SetState(OpeningPage);
                    Component<CloseAllMenuPages>(owned).CloseAllPages();

                    Assert.That(stack.PendingIds, Is.Empty, "CloseAllPages clears pending records");
                    object closure = stack.Command();
                    Assert.That(Captured(closure, "reasonForClosure"), Is.EqualTo(UISystemClosureReason.StandardClose));
                    Assert.That(Captured(closure, "removeFromStack"), Is.True);
                    Assert.That(stack.QueueIds, Is.EqualTo(new[] { 1 }));
                }
            }
            finally { Destroy(owned); }
        }

        // ---- dialog components ----------------------------------------------------

        [Test]
        public void CloseDialogPageClosesTheCurrentPageOrEveryPage()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<DialogPage>(DialogSystem.Instance))
                {
                    stack.Seed(1);
                    stack.SeedPending(9);
                    stack.SetState(OpeningPage);
                    Component<CloseDialogPage>(owned).ClosePage();

                    // CloseCurrentPage -> CloseCurrent(true, StandardClose); pending records stay.
                    object closure = stack.Command();
                    Assert.That(Captured(closure, "removeFromStack"), Is.True);
                    Assert.That(Captured(closure, "reasonForClosure"), Is.EqualTo(UISystemClosureReason.StandardClose));
                    Assert.That(stack.PendingIds, Is.EqualTo(new[] { 9 }));
                }

                using (var stack = new StackScope<DialogPage>(DialogSystem.Instance))
                {
                    stack.Seed(1);
                    stack.SeedPending(9);
                    stack.SetState(OpeningPage);
                    var component = Component<CloseDialogPage>(owned);
                    Set(component, "m_closeAllDialogs", true);
                    component.ClosePage();

                    Assert.That(stack.PendingIds, Is.Empty, "CloseAllPages clears pending records");
                    object closure = stack.Command();
                    Assert.That(Captured(closure, "reasonForClosure"), Is.EqualTo(UISystemClosureReason.StandardClose));
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void OpenDialogPageFollowsTheOpeningBehaviour()
        {
            var owned = new List<GameObject>();
            try
            {
                // With records [1, 2] and no current page: ShowPage adds a pending record,
                // EnqueuePage inserts at the base and EnqueuePageAfterCurrent before the last record.
                var cases = new (OpenDialogPage.OpeningBehaviour behaviour, int page, int[] queue, int[] pending)[]
                {
                    (OpenDialogPage.OpeningBehaviour.Immediate, 77, new[] { 1, 2 }, new[] { 77 }),
                    (OpenDialogPage.OpeningBehaviour.Enqueue, 77, new[] { 77, 1, 2 }, new int[0]),
                    (OpenDialogPage.OpeningBehaviour.EnqueueAfterCurrent, 77, new[] { 1, 77, 2 }, new int[0]),
                    ((OpenDialogPage.OpeningBehaviour)3, 77, new[] { 1, 2 }, new int[0]),
                    (OpenDialogPage.OpeningBehaviour.Immediate, 0, new[] { 1, 2 }, new int[0]),
                    (OpenDialogPage.OpeningBehaviour.Enqueue, 0, new[] { 1, 2 }, new int[0]),
                };
                foreach (var row in cases)
                {
                    using (var stack = new StackScope<DialogPage>(DialogSystem.Instance))
                    {
                        stack.Seed(1, 2);
                        var component = Component<OpenDialogPage>(owned);
                        Set(component, "m_pageToOpen", (DialogPageIdentifier)row.page);
                        Set(component, "m_openingBehaviour", row.behaviour);
                        component.OpenPage();
                        string label = row.behaviour + " page " + row.page;
                        Assert.That(stack.QueueIds, Is.EqualTo(row.queue), label + " queue");
                        Assert.That(stack.PendingIds, Is.EqualTo(row.pending), label + " pending");
                        Assert.That(stack.Commands, Is.Empty, label + " commands");
                    }
                }
            }
            finally { Destroy(owned); }
        }

        // ---- listeners ------------------------------------------------------------

        static void AssertListener<TListener, TPage>(PageStackSystem<TPage> system, string openedField, string closedField)
            where TListener : MonoBehaviour where TPage : GuiPage
        {
            var owned = new List<GameObject>();
            try
            {
                using (var stack = new StackScope<TPage>(system))
                {
                    TListener listener = Component<TListener>(owned);
                    var opened = (UnityEvent)Get(listener, openedField);
                    var closed = (UnityEvent)Get(listener, closedField);
                    Assert.That(opened, Is.Not.Null, "The constructor allocates " + openedField);
                    Assert.That(closed, Is.Not.Null, "The constructor allocates " + closedField);
                    int openedCalls = 0, closedCalls = 0;
                    opened.AddListener(() => openedCalls++);
                    closed.AddListener(() => closedCalls++);

                    stack.Raise("OnSystemOpened");
                    Assert.That(openedCalls, Is.EqualTo(0), "Not subscribed before OnEnable");

                    typeof(TListener).GetMethod("OnEnable", Any).Invoke(listener, null);
                    stack.Raise("OnSystemOpened");
                    Assert.That((openedCalls, closedCalls), Is.EqualTo((1, 0)));
                    stack.Raise("OnSystemClosed");
                    Assert.That((openedCalls, closedCalls), Is.EqualTo((1, 1)));

                    typeof(TListener).GetMethod("OnDisable", Any).Invoke(listener, null);
                    stack.Raise("OnSystemOpened");
                    stack.Raise("OnSystemClosed");
                    Assert.That((openedCalls, closedCalls), Is.EqualTo((1, 1)), "OnDisable removes both handlers");
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void DialogSystemListenerRelaysTheDialogSystemEvents() =>
            AssertListener<DialogSystemListener, DialogPage>(DialogSystem.Instance, "m_onDialogSystemOpened", "m_onDialogSystemClosed");

        [Test]
        public void MenuSystemListenerRelaysTheMenuSystemEvents() =>
            AssertListener<MenuSystemListener, MenuPage>(MenuSystem.Instance, "m_onMenuSystemOpened", "m_onMenuSystemClosed");

        // ---- overlay components ---------------------------------------------------

        sealed class OverlayScope : IDisposable
        {
            static readonly string[] SystemLists = { "m_openPages", "m_pendingClosures", "m_pendingShows" };
            static readonly string[] SystemValues = { "m_systemFrozen", "OnGUIPageClosed", "OnGUIPageOpened" };
            static readonly string[] RegistryLists = { "s_loadingScenes", "s_unloadingScenes", "m_pendingGuiPageDelegates", "m_pendingSubPageDelegates" };
            static readonly string[] RegistryMaps = { "s_menuPages", "s_subPages" };

            readonly OverlaySystem system = OverlaySystem.Instance;
            readonly GuiPageRegistry registry = GuiPageRegistry.Instance;
            readonly List<(IList list, object[] items)> lists = new List<(IList list, object[] items)>();
            readonly List<(IDictionary map, DictionaryEntry[] entries)> maps = new List<(IDictionary map, DictionaryEntry[] entries)>();
            readonly object[] values;
            readonly List<GameObject> hosts = new List<GameObject>();

            public OverlayScope()
            {
                // The lists are cleared in place: OpenPages and PendingPages wrap these instances.
                foreach (string name in SystemLists) Save((IList)Get(system, name));
                foreach (string name in RegistryLists) Save((IList)Get(registry, name));
                foreach (string name in RegistryMaps)
                {
                    var map = (IDictionary)Get(registry, name);
                    var entries = new DictionaryEntry[map.Count];
                    map.CopyTo(entries, 0);
                    maps.Add((map, entries));
                    map.Clear();
                }
                values = SystemValues.Select(name => Get(system, name)).ToArray();
                Set(system, "m_systemFrozen", false);
            }

            void Save(IList list)
            {
                lists.Add((list, list.Cast<object>().ToArray()));
                list.Clear();
            }

            // A loaded page that is closing: ShowPage records it as a pending show.
            public OverlayPage ClosingPage(OverlayPageIdentifier identifier)
            {
                var owner = new GameObject("OverlayPage fixture") { hideFlags = HideFlags.HideAndDontSave };
                owner.SetActive(false);
                hosts.Add(owner);
                OverlayPage page = owner.AddComponent<OverlayPage>();
                FieldInfo state = FieldOf(typeof(GuiPage), "m_state");
                state.SetValue(page, Enum.ToObject(state.FieldType, 3));
                Assert.That(page.IsClosing, Is.True);
                ((IDictionary)Get(registry, "s_menuPages"))[(int)identifier] = page;
                return page;
            }

            public OverlayPage[] PendingShows => system.PendingPages.Select(entry => entry.Page).ToArray();

            public void AddPendingShow(OverlayPage page) =>
                ((List<OverlaySystem.OverlayPageAndParameters>)Get(system, "m_pendingShows"))
                    .Add(new OverlaySystem.OverlayPageAndParameters { Page = page });

            public void Open(OverlayPage page) => ((List<OverlayPage>)Get(system, "m_openPages")).Add(page);
            public List<OverlayPage> PendingClosures => (List<OverlayPage>)Get(system, "m_pendingClosures");

            public void Dispose()
            {
                try
                {
                    foreach ((IList list, object[] items) in lists)
                    {
                        list.Clear();
                        foreach (object item in items) list.Add(item);
                    }
                    foreach ((IDictionary map, DictionaryEntry[] entries) in maps)
                    {
                        map.Clear();
                        foreach (DictionaryEntry entry in entries) map.Add(entry.Key, entry.Value);
                    }
                    for (int i = 0; i < SystemValues.Length; i++) Set(system, SystemValues[i], values[i]);
                }
                finally
                {
                    foreach (GameObject owner in hosts)
                        if (owner != null) Object.DestroyImmediate(owner);
                }
            }
        }

        [Test]
        public void OpenCloseOverlayPageShowsOnTheFirstOpenAndHidesWhenTheCountReturnsToZero()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var overlay = new OverlayScope())
                {
                    var identifier = (OverlayPageIdentifier)4242;
                    OverlayPage page = overlay.ClosingPage(identifier);
                    var component = Component<OpenCloseOverlayPage>(owned);
                    Set(component, "m_pageToOpenAndClose", identifier);
                    int RefCount() => (int)Get(component, "refCount");

                    component.OpenPage();
                    Assert.That(overlay.PendingShows, Is.EqualTo(new[] { page }), "First open shows the page");
                    Assert.That(OverlaySystem.Instance.PendingPages[0].Parameters, Is.Null);
                    component.OpenPage();
                    Assert.That(overlay.PendingShows.Length, Is.EqualTo(1), "Second open only counts");
                    Assert.That(RefCount(), Is.EqualTo(2));

                    component.ClosePage();
                    Assert.That(overlay.PendingShows.Length, Is.EqualTo(1), "Still referenced");
                    component.ClosePage();
                    Assert.That(RefCount(), Is.EqualTo(0));
                    Assert.That(overlay.PendingShows, Is.Empty, "The last close hides the page");

                    // At zero the count stays at zero and the page is hidden again.
                    overlay.AddPendingShow(page);
                    component.ClosePage();
                    Assert.That(RefCount(), Is.EqualTo(0));
                    Assert.That(overlay.PendingShows, Is.Empty);
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void OpenCloseOverlayPageIgnoresTheNoneIdentifier()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var overlay = new OverlayScope())
                {
                    OverlayPage page = overlay.ClosingPage(OverlayPageIdentifier.None);
                    var component = Component<OpenCloseOverlayPage>(owned);
                    component.OpenPage();
                    Assert.That(overlay.PendingShows, Is.Empty);
                    Assert.That((int)Get(component, "refCount"), Is.EqualTo(0));

                    overlay.AddPendingShow(page);
                    component.ClosePage();
                    Assert.That(overlay.PendingShows, Is.EqualTo(new[] { page }), "ClosePage does not hide None");
                }
            }
            finally { Destroy(owned); }
        }

        [Test]
        public void ToggleOverlayPageShowsAClosedPageAndHidesAnOpenOneWithoutFilteringNone()
        {
            var owned = new List<GameObject>();
            try
            {
                using (var overlay = new OverlayScope())
                {
                    OverlayPage page = overlay.ClosingPage(OverlayPageIdentifier.None);
                    var component = Component<ToggleOverlayPage>(owned);

                    component.Action_TogglePage();
                    Assert.That(overlay.PendingShows, Is.EqualTo(new[] { page }), "Not open: ShowPage");

                    overlay.Open(page);
                    var closed = new List<OverlayPage>();
                    Action<OverlayPage> onClosed = closed.Add;
                    OverlaySystem.Instance.OnGUIPageClosed += onClosed;
                    component.Action_TogglePage();
                    Assert.That(OverlaySystem.Instance.OpenPages, Is.Empty, "Open: HidePage");
                    Assert.That(overlay.PendingClosures, Is.EqualTo(new[] { page }), "A closing page is hidden once its transition ends");
                    Assert.That(closed, Is.EqualTo(new[] { page }));
                }
            }
            finally { Destroy(owned); }
        }

        // ---- generated closure identities (dump.cs TypeDefIndex 11905, 11909..11912) ----

        static string[] FieldNames(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray();

        static Type Nested(Type owner, string name)
        {
            Type nested = owner.GetNestedType(name, BindingFlags.NonPublic);
            Assert.That(nested, Is.Not.Null, owner.Name + "+" + name);
            return nested;
        }

        static string[] MethodNames(Type type) =>
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(method => method.MetadataToken).Select(method => method.Name).ToArray();

        [Test]
        public void PageStackSystemClosuresKeepTheOriginalGeneratedNamesAndCaptures()
        {
            Type owner = typeof(PageStackSystem<>);
            Type addPage = Nested(owner, "<>c__DisplayClass45_0");
            Assert.That(MethodNames(addPage), Is.EqualTo(new[] { "<AddPage>b__0" }));
            Assert.That(FieldNames(addPage), Is.EqualTo(new[] { "<>4__this", "identifier", "data", "callback", "replaceCurrent" }));

            Type closeCurrent = Nested(owner, "<>c__DisplayClass53_0");
            Assert.That(MethodNames(closeCurrent), Is.EqualTo(new[] { "<CloseCurrent>b__0" }));
            Assert.That(FieldNames(closeCurrent), Is.EqualTo(new[] { "<>4__this", "removeFromStack", "reasonForClosure" }));

            Type openNext = Nested(owner, "<>c__DisplayClass55_0");
            Assert.That(MethodNames(openNext), Is.EqualTo(new[] { "<OpenNext>b__0" }));
            Assert.That(FieldNames(openNext), Is.EqualTo(new[] { "guiPageData", "<>4__this" }));

            Assert.That(MethodNames(Nested(owner, "<>c")), Is.EqualTo(new[]
            {
                "<.ctor>b__65_0", "<.ctor>b__65_1", "<.ctor>b__65_2", "<.ctor>b__65_3", "<.ctor>b__65_4", "<.ctor>b__65_5"
            }));
        }

        [Test]
        public void OverlaySystemClosuresKeepTheOriginalGeneratedNamesAndCaptures()
        {
            Type owner = typeof(OverlaySystem);
            Type showPage = Nested(owner, "<>c__DisplayClass25_0");
            Assert.That(MethodNames(showPage), Is.EqualTo(new[] { "<ShowPage>b__0" }));
            Assert.That(FieldNames(showPage), Is.EqualTo(new[] { "parameters", "<>4__this", "callback" }));
            Assert.That(MethodNames(Nested(owner, "<>c")), Is.EqualTo(new[] { "<.ctor>b__1_0", "<.ctor>b__1_1" }));
        }
    }
}
