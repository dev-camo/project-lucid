using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using ProjectLucid.Tests.CrossPromoUIShared;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Fixtures for the game's cross promotion launch button, container and start-up component. Nothing here
    // downloads or opens a URL: the cross promotion system is the real class over in-memory collaborators.

    // Metadata comes back at once from memory, so a real CrossPromo built over it finishes its sync inside
    // its constructor.
    internal sealed class ScriptedCrossPromoResources : ICrossPromoResourceManager
    {
        public readonly List<string> Log = new List<string>();

        public event Action<bool> DownloadCompleted
        {
            add { }
            remove { }
        }

        public bool HasPendingDownloads => false;

        public void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted)
        {
            Log.Add("DownloadMetaData");
            onMetaDataDownloadCompleted(true);
        }

        public void VerifyAdResources(CrossPromoAd[] metaDataAds) => Log.Add("VerifyAdResources");

        public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta) => null;

        public void ReleaseFullScreenTexture(Sprite texture) { }

        public Sprite RequestIconTexture(CrossPromoTexture texMeta) => null;

        public void ReleaseIconTexture(Sprite texture) { }

        // Any non-empty object; the parser below decides what it means.
        public string RequestMetaData() => "{\"grid\":{\"lucid\":1}}";
    }

    internal sealed class ScriptedCrossPromoParser : ICrossPromoDataParser
    {
        public CrossPromoMetaData MetaData;

        public bool TryParseMetadata(Dictionary<string, Dictionary<string, object>> dataRoot, out CrossPromoMetaData metaData)
        {
            metaData = MetaData;
            return true;
        }
    }

    // A container manager whose open set is the fixture's, recording each open and close by identifier name.
    internal sealed class RecordingContainerManager : UIContainerManager
    {
        public readonly List<string> Log;
        public readonly HashSet<UIContainerIdentifier> Open = new HashSet<UIContainerIdentifier>();
        // When set, a closed container is still reported open, as a container that refuses to close would be.
        public bool CloseKeepsOpen;

        public RecordingContainerManager(List<string> log)
        {
            Log = log;
        }

        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            Log.Add("open:" + containerPrefab.Identifier.Name);
            return containerPrefab;
        }

        public override void CloseAll() { }

        public override void Close(UIContainerIdentifier identifier)
        {
            Log.Add("close:" + identifier.Name);
            if (!CloseKeepsOpen) Open.Remove(identifier);
        }

        public override bool IsOpen(UIContainerIdentifier identifier) => Open.Contains(identifier);

        public override bool IsAnyOpen() => Open.Count != 0;

        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers) => false;

        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            container = null;
            return false;
        }

        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor) { }
    }

    // The SystemConfiguration row holding one owned CrossPromoConfigurationAsset, so the game's cross promotion
    // code reads the fixture's configuration instead of creating a default one. Leases the row (absent or idle),
    // registers a fresh system into it and unregisters before the lease is released.
    internal sealed class OwnedCrossPromoConfiguration : IDisposable
    {
        private readonly VerificationFixtureOwnership.LifecycleRow m_lease;
        private readonly SystemConfiguration m_system = new SystemConfiguration();
        public readonly CrossPromoConfigurationAsset Asset;

        public OwnedCrossPromoConfiguration(string json)
        {
            m_lease = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<SystemConfiguration>());
            try
            {
                ProcessManager.RegisterSystem(m_system);
                Asset = ScriptableObject.CreateInstance<CrossPromoConfigurationAsset>();
                JsonUtility.FromJsonOverwrite(json, Asset);
                SystemConfiguration.AddConfig<SystemConfigurationAsset>(Asset);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            VerificationFixtureOwnership.RunThenCleanUp(() => ProcessManager.UnregisterSystem(m_system),
                () => { if (Asset != null) Object.DestroyImmediate(Asset); },
                m_lease.Dispose);
        }
    }

    // The registry rows the game's cross promotion classes reach: a real CrossPromo over scripted metadata and a
    // real UIManager whose only container manager is recording. Both rows are acquired absent, before any
    // component whose field initialisers cache references from them is added.
    internal sealed class CrossPromoGameScene
    {
        private readonly Owned m_owned;
        public readonly List<string> Log = new List<string>();
        public readonly CrossPromo Promo;
        public readonly UIManager Manager;
        public readonly RecordingContainerManager Containers;
        public readonly CrossPromoAd[] Ads;

        public CrossPromoGameScene(Owned owned, bool promoEnabled, params string[] adNames)
        {
            m_owned = owned;
            Ads = CrossPromoUiAccess.Ads(adNames);
            var parser = new ScriptedCrossPromoParser
            {
                MetaData = new CrossPromoMetaData { IsSystemEnabled = promoEnabled, Ads = Ads, FirstAd = null },
            };
            Promo = owned.Register(new CrossPromo(default(CrossPromoConfig), new ScriptedCrossPromoResources(), parser));
            Manager = owned.Host("Lucid cross promotion ui manager", false).AddComponent<UIManager>();
            Containers = new RecordingContainerManager(Log);
            Get<Dictionary<UIContainerBehaviour, UIContainerManager>>(Manager, "m_containerManagers")[UIContainerBehaviour.Free] = Containers;
            owned.Register(Manager);
        }

        // A cross promotion container the manager can open: its identifier holds it as the loaded container,
        // and it shows ads through a complete full screen view.
        public UIContainerCrossPromo NewContainer(string name, params UIContainerIdentifier[] hideWhilstOpen)
        {
            var container = m_owned.Host("Lucid container " + name, false).AddComponent<UIContainerCrossPromo>();
            UIContainerIdentifier identifier = NewIdentifier(name);
            identifier.RegisterContainer(container);
            Set(container, "m_identifier", identifier);
            Set(container, "m_behaviour", UIContainerBehaviour.Free);
            Set(container, "m_hideContainersWhilstOpen", hideWhilstOpen);
            Set(container, "m_fullScreenView", NewFullScreenView());
            return container;
        }

        // Identifiers compare, hash and key the manager's open set by their GUID (ScriptableObjectWithGuid), so each one
        // is given a GUID of its own; two with the empty GUID would be the same container.
        public UIContainerIdentifier NewIdentifier(string name) =>
            Create<UIContainerIdentifier>(m_owned, "{\"m_name\":\"" + name + "\",\"m_guid\":\"lucid-" + name + "\"}");

        // A container another container hides: open in the recording manager and loaded in its identifier.
        public UIContainerIdentifier NewHideable(string name)
        {
            UIContainerIdentifier identifier = NewIdentifier(name);
            var loaded = m_owned.Host("Lucid hideable " + name, false).AddComponent<UIContainerCrossPromo>();
            Set(loaded, "m_identifier", identifier);
            Set(loaded, "m_behaviour", UIContainerBehaviour.Free);
            identifier.RegisterContainer(loaded);
            Containers.Open.Add(identifier);
            return identifier;
        }

        // The full screen view of OriginalCrossPromoFullScreenViewTests, reduced to what Show touches: a
        // scroller over a content rectangle, three recording pages, both navigation buttons and a pip group.
        public CrossPromoFullScreenView NewFullScreenView()
        {
            var objects = m_owned.Lease(new CrossPromoUiObjects("lucid-container-view"));
            GameObject view = objects.Child("view");
            ((RectTransform)view.transform).sizeDelta = new Vector2(100f, 100f);
            var component = view.AddComponent<CrossPromoFullScreenView>();
            GameObject scroller = objects.Child("scroller", view.transform);
            ((RectTransform)scroller.transform).sizeDelta = new Vector2(100f, 100f);
            var scrollRect = scroller.AddComponent<ScrollRect>();
            var content = (RectTransform)objects.Child("content", scroller.transform).transform;
            content.sizeDelta = new Vector2(300f, 100f);
            scrollRect.content = content;
            GameObject pips = objects.Child("pips", view.transform, typeof(HorizontalLayoutGroup));
            ((RectTransform)pips.transform).sizeDelta = new Vector2(50f, 8f);
            Set(component, "m_contentScroller", scrollRect);
            Set(component, "m_scrolledContent", content);
            Set(component, "m_pageBasePrev", objects.Child("prev", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>());
            Set(component, "m_pageBaseCurrent", objects.Child("current", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>());
            Set(component, "m_pageBaseNext", objects.Child("next", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>());
            Set(component, "m_previousAdButton", objects.Child("previous-ad", view.transform));
            Set(component, "m_nextAdButton", objects.Child("next-ad", view.transform));
            Set(component, "m_pipHorizontalGroup", (RectTransform)pips.transform);
            Set(component, "m_emptyPip", objects.Sprite("empty-pip"));
            Set(component, "m_fullPip", objects.Sprite("full-pip", 12f));
            return component;
        }

        // The ad the full screen view has current, read from its private field.
        public static int CurrentAd(CrossPromoFullScreenView view) => Get<int>(view, "m_currentAd");

        public static string[] Names(IEnumerable<UIContainerIdentifier> identifiers) => identifiers.Select(identifier => identifier.Name).ToArray();
    }
}
