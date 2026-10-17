// Original HLUnityUI OverlaySystem reconstructed from complete shipping metadata and both native slices.
// IsPendingOpen(identifier) deliberately checks the open list; transitions defer requests and callback dispatch precedes removal.
// Freeze only blocks ShowPage. No new null guard, queue flush, listener teardown or scene filtering is added.
// Original source spelling, closure allocation, optimizer fault ordering and rebuilt runtime equivalence remain held.
// The nested types sit between the constructor and ShowPage(identifier) so that its closure keeps the original
// ordinal (<>c__DisplayClass25_0); building the pending entry before reading m_pendingShows keeps the closure's
// field order (parameters, <>4__this, callback) and the cast-before-receiver order seen at 0x1b4ceb9..0x1b4cf2e.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Hardlight
{
    public sealed class OverlaySystem
    {
        public static readonly OverlaySystem Instance = new OverlaySystem();

        private OverlaySystem()
        {
            OpenPages = m_openPages.AsReadOnly();
            PendingPages = m_pendingShows.AsReadOnly();
        }

        public struct OverlayPageAndParameters
        {
            public OverlayPage Page { get; set; }
            public GuiPageParameters Parameters { get; set; }
        }

        public delegate void OnLoaded(OverlayPage page);

        public ReadOnlyCollection<OverlayPage> OpenPages { get; private set; }
        public ReadOnlyCollection<OverlayPageAndParameters> PendingPages { get; private set; }
        private List<OverlayPage> m_openPages = new List<OverlayPage>();
        private List<OverlayPage> m_pendingClosures = new List<OverlayPage>();
        private List<OverlayPageAndParameters> m_pendingShows = new List<OverlayPageAndParameters>();
        public event Action<OverlayPage> OnGUIPageClosed = page => { };
        public event Action<OverlayPage> OnGUIPageOpened = page => { };
        private bool m_systemFrozen;

        public void FreezeSystem()
        {
            m_systemFrozen = true;
        }

        public void UnfreezeSystem()
        {
            m_systemFrozen = false;
        }

        public void ShowPage(OverlayPage page, GuiPageParameters parameters = null)
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot ShowPage while the overlay system is frozen", null);
                return;
            }

            if (!IsOpen(page))
            {
                m_openPages.Add(page);
                page.m_onShowComplete.AddListener(PageOpenedHandler);
            }
            page.Show(parameters);
            OnGUIPageOpened(page);
        }

        public void ShowPage(OverlayPageIdentifier identifier, OnLoaded callback = null, GuiPageParameters parameters = null, bool loadAsync = false)
        {
            if (m_systemFrozen)
            {
                HLOutput.LogError("Cannot ShowPage while the overlay system is frozen", null);
                return;
            }

            GuiPageRegistry.Instance.LoadGuiPage<OverlayPage>((int)identifier, page =>
            {
                if (page.IsClosing)
                {
                    OverlayPageAndParameters pendingShow = new OverlayPageAndParameters { Page = (OverlayPage)page, Parameters = parameters };
                    m_pendingShows.Add(pendingShow);
                }
                if (!page.IsTranstioning)
                    ShowPage((OverlayPage)page, parameters);
                if (callback != null)
                    callback((OverlayPage)page);
            }, loadAsync);
        }

        public void HidePage(OverlayPage page)
        {
            if (IsOpen(page))
            {
                m_openPages.Remove(page);
                if (page.IsTranstioning)
                    m_pendingClosures.Add(page);
                else
                {
                    page.OnPageTransitionDelayElapsed += PageClosedHandler;
                    page.Hide(UISystemClosureReason.StandardClose);
                }
                OnGUIPageClosed(page);
            }
            else
            {
                for (int i = 0; i < m_pendingShows.Count; ++i)
                {
                    if (m_pendingShows[i].Page == page)
                    {
                        m_pendingShows.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        private void PageClosedHandler(GuiPage page)
        {
            page.OnPageTransitionDelayElapsed -= PageClosedHandler;
            int i = 0;
            while (i < m_pendingShows.Count)
            {
                if (m_pendingShows[i].Page != null && !m_pendingShows[i].Page.IsClosing)
                {
                    ShowPage(m_pendingShows[i].Page, m_pendingShows[i].Parameters);
                    m_pendingShows.RemoveAt(i);
                }
                else
                    ++i;
            }
            if (!IsOpen((OverlayPage)page))
                GuiPageLoader.UnloadSceneFromValue(GuiPageRegistry.IdentifierToCRC(page.Identifier), false);
        }

        private void PageOpenedHandler()
        {
            int i = 0;
            while (i < m_pendingClosures.Count)
            {
                if (m_pendingClosures[i] != null && !m_pendingClosures[i].IsTranstioning)
                {
                    m_pendingClosures[i].OnPageTransitionDelayElapsed += PageClosedHandler;
                    m_pendingClosures[i].Hide(UISystemClosureReason.StandardClose);
                    m_pendingClosures.RemoveAt(i);
                }
                else
                    ++i;
            }
        }

        public void HidePage(OverlayPageIdentifier identifier)
        {
            HidePage(GuiPageRegistry.Instance.GetGuiPageIfLoaded<OverlayPage>((int)identifier));
        }

        public bool IsOpen(OverlayPage page)
        {
            return page != null && m_openPages.Contains(page);
        }

        public bool IsPendingOpen(OverlayPage page)
        {
            if (page == null)
                return false;
            for (int i = 0; i < m_pendingShows.Count; ++i)
                if (m_pendingShows[i].Page == page)
                    return true;
            return false;
        }

        public bool IsOpen(OverlayPageIdentifier identifier)
        {
            return IsOpen(GuiPageRegistry.Instance.GetGuiPageIfLoaded<OverlayPage>((int)identifier));
        }

        public bool IsPendingOpen(OverlayPageIdentifier identifier)
        {
            return IsOpen(GuiPageRegistry.Instance.GetGuiPageIfLoaded<OverlayPage>((int)identifier));
        }

        public bool AnyOpen()
        {
            return m_openPages.Count > 0;
        }

        public void CloseAndUnloadAll(UISystemClosureReason reasonForClosure)
        {
            for (int i = 0; i < m_openPages.Count; ++i)
                m_openPages[i].Hide(reasonForClosure);
            m_openPages.Clear();
            m_pendingClosures.Clear();
            m_pendingShows.Clear();
            foreach (int identifier in Enum.GetValues(typeof(OverlayPageIdentifier)))
                GuiPageLoader.UnloadSceneFromValue(identifier, true);
        }
    }
}
