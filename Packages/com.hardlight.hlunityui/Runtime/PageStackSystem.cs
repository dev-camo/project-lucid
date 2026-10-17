// Original HLUnityUI.Runtime PageStackSystem<T> (02000053), whole 58-method family.
// Native evidence: ARM64 and x86_64 MethodSpecs/registrations and 116 whole ranges.
// Source reconstruction preserves original authored identities, not an offline adapter.
//
// The six no-op event initializers are genuine callbacks in the original constructor.
// Their field initializers and three collection allocations run before the Object base
// constructor. Ordinary C# emits the delegate/cache/captured closure families.
//
// Transition callbacks intentionally read live fields after invoking user/page callbacks.
// OpenNext calls the record's callback before assigning CurrentPage, then records the
// incoming page's identifier before Show. A null callback skips only its own cast.
// Closing enumerates the old linked-list instance while the live queue may be replaced;
// it does not take a defensive snapshot. CloseAllPagesExcept has no frozen/pending-clear
// guard and preserves the requested-record encounter-order behavior. Each completion
// releases at most one queued command; UnfreezeSystem does not release any command.
//
// Member placement reproduces the original closure ordinals (<>c__DisplayClass45_0 for
// AddPage, 53_0 for CloseCurrent, 55_0 for OpenNext, <>c <.ctor>b__65_*), which the
// closure emission test checks with the capture order: three members (m_systemFrozen,
// FreezeSystem, UnfreezeSystem) sit between the last property and AddPage, and six fields
// follow OnEmpty. Metadata fixes the field order, not which fields sit where; other splits
// may give the same counts (for example the two nested types plus four fields after OnEmpty),
// so the split is inferred. Native layout is not checked.
//
// Source/native spelling and control-flow evidence does not qualify supplier behavior,
// native shared generics as closed bodies, current compiler emission, Unity bindings,
// scene readiness, bootstrap or full-game correctness. No shipping bodies were run.
using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption((Option)2, false)]
    [Il2CppSetOption((Option)1, false)]
    public class PageStackSystem<T> where T : GuiPage
    {
        public enum State
        {
            Empty = 0,
            Open = 1,
            OpeningPage = 2,
            ClosingPage = 3
        }

        public delegate void OnPageLoaded(T success);

        public event Action OnSystemClosed = delegate { };
        public event Action OnSystemOpened = delegate { };
        public event Action<T> OnGUIPageClosed = delegate { };
        public event Action<T> OnGUIPageOpened = delegate { };
        public event Action<T> OnGUIPageClosing = delegate { };
        public event Action<T> OnGUIPageOpening = delegate { };

        public T CurrentPage => m_currentPage;
        public GuiPageRecord<T> CurrentPagePair => m_pageQueue.Count > 0 ? m_pageQueue.Last.Value : null;
        public int CurrentPageID => m_pageQueue.Count > 0 ? m_pageQueue.Last.Value.m_identifier : 0;

        public int NextPageID
        {
            get
            {
                if (m_pending.Count > 0)
                    return m_pending[m_pending.Count - 1].m_identifier;
                return m_pageQueue.Count > 1 ? m_pageQueue.Last.Previous.Value.m_identifier : 0;
            }
        }

        public bool CanMoveBack => m_pageQueue.Count > 1;
        public bool IsTransitioning => m_state == State.OpeningPage || m_state == State.ClosingPage;
        public bool IsOpen => m_state == State.Open;
        public bool IsOpening => m_state == State.OpeningPage;
        public bool Empty => m_pageQueue.Count == 0;
        public int PageCount => m_pageQueue.Count;
        public State CurrentState => m_state;

        private bool m_systemFrozen;

        public void FreezeSystem()
        {
            m_systemFrozen = true;
        }

        public void UnfreezeSystem()
        {
            m_systemFrozen = false;
        }

        protected void AddPage(int identifier, GuiPageParameters data, OnPageLoaded callback, bool replaceCurrent)
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot AddPage while the Page Stack System is frozen", null);
                return;
            }

            if (IsTransitioning)
            {
                m_pendingPageStackCommands.Enqueue(() => AddPage(identifier, data, callback, replaceCurrent));
                return;
            }

            if (CurrentPageID == identifier)
                return;

            if (!Empty)
            {
                m_pending.Add(new GuiPageRecord<T>(identifier, data, callback));
                if (!IsTransitioning)
                    CloseCurrent(replaceCurrent, UISystemClosureReason.StandardClose);
            }
            else
            {
                OnSystemOpened();
                OnNoLongerEmpty();
                m_pageQueue.AddLast(new GuiPageRecord<T>(identifier, data, callback));
                OpenNext();
            }
        }

        protected void AddPageBeforeCurrent(int identifier, GuiPageParameters data, OnPageLoaded callback)
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot AddPage while the Page Stack System is frozen", null);
                return;
            }

            if (m_pageQueue.Count > 0)
                m_pageQueue.AddBefore(m_pageQueue.Last, new GuiPageRecord<T>(identifier, data, callback));
            else
                AddPage(identifier, data, callback, false);
        }

        protected void AddPageAtBase(int identifier, GuiPageParameters data, OnPageLoaded callback)
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot AddPage while the Page Stack System is frozen", null);
                return;
            }

            if (m_pageQueue.Count > 0)
                m_pageQueue.AddFirst(new GuiPageRecord<T>(identifier, data, callback));
            else
                AddPage(identifier, data, callback, false);
        }

        protected void MoveToPreviousPage()
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot Move To Previous Page while the Page Stack System is frozen", null);
                return;
            }

            if (!Empty)
                CloseCurrent(true, UISystemClosureReason.StandardClose);
        }

        protected void CloseAllPages(UISystemClosureReason reasonForClosure)
        {
            if (Empty)
                return;
            if (reasonForClosure != UISystemClosureReason.AppRestartClose && m_systemFrozen)
            {
                HLOutput.LogError("Cannot Close All Pages while the Page Stack System is frozen and this is not a restart", null);
                return;
            }

            m_pending.Clear();
            var current = new[] { CurrentPagePair };
            foreach (var record in m_pageQueue)
            {
                if (record == null)
                    continue;
                if (record.m_identifier != CurrentPageID)
                {
                    GuiPageRegistry.Instance.UnloadPage(record.m_identifier);
                }
                else
                {
                    m_pageQueue = new LinkedList<GuiPageRecord<T>>(current);
                    if (m_state == State.ClosingPage)
                        m_removeFromStack = true;
                    else
                        CloseCurrent(true, reasonForClosure);
                }
            }
        }

        protected void CloseAllPagesExcept(int page)
        {
            if (Empty)
                return;

            var current = CurrentPagePair;
            GuiPageRecord<T> retained = null;
            bool retainedIsDifferent = false;
            foreach (var record in m_pageQueue)
            {
                if (record == null)
                    continue;
                if (record.m_identifier == page)
                {
                    retained = record;
                    retainedIsDifferent = CurrentPageID != page;
                }
                else if (record.m_identifier == CurrentPageID)
                {
                    m_pageQueue = new LinkedList<GuiPageRecord<T>>(
                        retainedIsDifferent ? new[] { retained, current } : new[] { current });
                    if (m_state == State.ClosingPage)
                        m_removeFromStack = true;
                    else
                        CloseCurrent(true, UISystemClosureReason.StandardClose);
                }
                else
                {
                    GuiPageRegistry.Instance.UnloadPage(record.m_identifier);
                }
            }
        }

        protected bool CanLeaveCurrentPage()
        {
            return m_currentPage.CanLeavePage;
        }

        protected bool IsPageInStack(int identifier)
        {
            for (var node = m_pageQueue.First; node != null; node = node.Next)
            {
                if (node.Value.m_identifier == identifier)
                    return true;
            }
            return false;
        }

        protected void CloseCurrent(bool removeFromStack, UISystemClosureReason reasonForClosure)
        {
            if (reasonForClosure != UISystemClosureReason.AppRestartClose && m_systemFrozen)
            {
                HLOutput.LogError("Cannot Close Current Page while the Page Stack System is frozen and this is not a restart", null);
                return;
            }

            if (IsTransitioning)
            {
                m_pendingPageStackCommands.Enqueue(() => CloseCurrent(removeFromStack, reasonForClosure));
                return;
            }

            if (m_currentPage != null)
            {
                m_removeFromStack = removeFromStack;
                m_state = State.ClosingPage;
                m_currentPage.OnPageTransitionDelayElapsed += PageClosedHandler;
                m_currentPage.Hide(reasonForClosure);
                GuiPageRegistry.Instance.PrimePage(NextPageID);
                OnGUIPageClosing(m_currentPage);
            }
        }

        private void PageClosedHandler(GuiPage page)
        {
            m_currentPage.OnPageTransitionDelayElapsed -= PageClosedHandler;
            if (m_removeFromStack)
            {
                GuiPageRegistry.Instance.UnloadPage(GuiPageRegistry.IdentifierToCRC(m_currentPage.Identifier));
                m_pageQueue.RemoveLast();
            }
            OnGUIPageClosed(m_currentPage);
            m_currentPage = null;

            while (m_pending.Count > 0)
            {
                if (!IsPageInStack(m_pending[0].m_identifier))
                {
                    m_pageQueue.AddLast(m_pending[0]);
                    m_pending.RemoveAt(0);
                }
                else
                {
                    while (CurrentPageID != m_pending[0].m_identifier)
                        m_pageQueue.RemoveLast();
                    m_pageQueue.Last.Value = m_pending[0];
                    m_pending.RemoveAt(0);
                }
            }

            if (!Empty)
            {
                OpenNext();
                return;
            }

            m_state = State.Empty;
            OnEmpty();
            OnSystemClosed();
            if (m_pendingPageStackCommands.Count > 0)
                m_pendingPageStackCommands.Dequeue()?.Invoke();
        }

        private void OpenNext()
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot Open Next while the Page Stack System is frozen", null);
                return;
            }

            m_state = State.OpeningPage;
            var guiPageData = m_pageQueue.Last.Value;
            GuiPageRegistry.Instance.LoadGuiPage<GuiPage>(guiPageData.m_identifier, page =>
            {
                guiPageData.m_onLoadedCallback?.Invoke((T)page);
                m_currentPage = (T)page;
                ProcessManager.GetSystem<HLUnityCore>(null, true).Breadcrumb(page.Identifier);
                m_currentPage.m_onShowComplete.AddListener(PageOpenedHandler);
                m_currentPage.Show(guiPageData.m_data);
                OnGUIPageOpening(m_currentPage);
            }, false);
        }

        private void PageOpenedHandler()
        {
            m_currentPage.m_onShowComplete.RemoveListener(PageOpenedHandler);
            m_state = State.Open;
            OnGUIPageOpened(m_currentPage);
            if (m_pendingPageStackCommands.Count > 0)
                m_pendingPageStackCommands.Dequeue()?.Invoke();
        }

        protected virtual void OnNoLongerEmpty()
        {
        }

        protected virtual void OnEmpty()
        {
        }

        private State m_state;
        private bool m_removeFromStack;
        private List<GuiPageRecord<T>> m_pending = new List<GuiPageRecord<T>>();
        private T m_currentPage;
        protected LinkedList<GuiPageRecord<T>> m_pageQueue = new LinkedList<GuiPageRecord<T>>();
        private Queue<Action> m_pendingPageStackCommands = new Queue<Action>();

        public PageStackSystem()
        {
        }
    }
}
