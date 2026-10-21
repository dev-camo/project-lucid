using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008a3, all four methods and five fields, with the
    // <>c__DisplayClass5_0 closure 0x020008a4 (a natural lambda in Setup). Hosts the cross
    // promotion full screen view and keeps other containers hidden while it is open.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerCrossPromo : UIContainer
    {
        [SerializeField] [Tooltip("The Full Screen View visual component.")]
        private CrossPromoFullScreenView m_fullScreenView;

        [Tooltip("Specify containers to hide whilst this container is open.")] [SerializeField]
        private UIContainerIdentifier[] m_hideContainersWhilstOpen = Array.Empty<UIContainerIdentifier>();

        private readonly SystemRef<CrossPromo> m_crossPromoRef = ProcessManager.GetSystemRef<CrossPromo>(null, true);
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>(null, true);
        private readonly List<UIContainerIdentifier> m_hiddenContainers = new List<UIContainerIdentifier>();

        // 0x060031e6: close each listed container that is open, remembering it so Action_Close can
        // bring it back, then show the requested ad as soon as the system is valid.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIManager uiManager = m_uiManagerRef.Get();
            foreach (UIContainerIdentifier identifier in m_hideContainersWhilstOpen)
            {
                if (uiManager.IsOpen(identifier))
                {
                    uiManager.Close(identifier);
                    m_hiddenContainers.AddUnique(identifier);
                }
            }
            UIContainerCrossPromoParameters crossPromoParams = parameters.GetAs<UIContainerCrossPromoParameters>();
            m_crossPromoRef.InvokeOnValid(crossPromo =>
            {
                crossPromo.FullScreenViewRequested += Show;
                crossPromo.RequestFullScreenView(crossPromoParams.ImageIndex);
            });
        }

        // 0x060031e7: the metadata is forwarded unchanged to the full screen view.
        private void Show(CrossPromoMetaData metaData, int adIndex)
        {
            m_fullScreenView.Show(m_crossPromoRef.Get(), metaData, adIndex);
        }

        // 0x060031e8: with the system present, announce the close, stop listening, reopen the
        // containers hidden by Setup and close this one. Without it, nothing happens.
        public void Action_Close()
        {
            if (m_crossPromoRef.IsNull())
                return;
            CrossPromo crossPromo = m_crossPromoRef.Get();
            crossPromo.RaiseFullScreenClosedEvent();
            crossPromo.FullScreenViewRequested -= Show;
            UIManager uiManager = m_uiManagerRef.Get();
            foreach (UIContainerIdentifier identifier in m_hiddenContainers)
                uiManager.GetOrCreate(identifier);
            m_hiddenContainers.Clear();
            uiManager.Close(m_identifier);
        }

        // 0x060031e9: field initialisers above, then the UIContainer constructor.
        public UIContainerCrossPromo() { }
    }
}
