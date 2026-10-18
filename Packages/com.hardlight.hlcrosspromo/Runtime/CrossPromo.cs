using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000004; bodies translated from the 1.10.1 x86-64 binary.
	// Downloads and parses the cross-promotion metadata once at construction, then reports
	// Initialised when no ad image download is still pending.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromo : ICrossPromo, ISystem
	{
		// 06000003 (1.10.1 RVA 0x1A0E550), 06000004 (1.10.1 RVA 0x1A0E600)
		private event Action<CrossPromoMetaData> Initialised;

		// 06000005 (1.10.1 RVA 0x1A0E6B0): a subscriber that arrives once the sync has finished is
		// called at once with the parsed metadata and is not kept.
		// 06000006 (1.10.1 RVA 0x1A0E8E0)
		event Action<CrossPromoMetaData> ICrossPromo.Initialised
		{
			add
			{
				if (IsReady())
				{
					value?.Invoke(m_metaData);
				}
				else
				{
					Initialised += value;
				}
			}
			remove
			{
				Initialised -= value;
			}
		}

		// 06000007 (1.10.1 RVA 0x1A0E990), 06000008 (1.10.1 RVA 0x1A0EA40)
		public event Action<CrossPromoMetaData, int> FullScreenViewRequested;

		// 06000009 (1.10.1 RVA 0x1A0EAF0), 0600000a (1.10.1 RVA 0x1A0EBA0)
		public event Action<CrossPromoAd, int> LaunchButtonShown;

		// 0600000b (1.10.1 RVA 0x1A0EC50), 0600000c (1.10.1 RVA 0x1A0ED00)
		public event Action<CrossPromoAd> FullScreenViewShown;

		// 0600000d (1.10.1 RVA 0x1A0EDB0), 0600000e (1.10.1 RVA 0x1A0EE60)
		public event Action<CrossPromoAd> FullScreenButtonClicked;

		// 0600000f (1.10.1 RVA 0x1A0EF10), 06000010 (1.10.1 RVA 0x1A0EFA0)
		public event Action FullScreenViewClosed;

		private readonly CrossPromoConfig m_config;

		private readonly ICrossPromoResourceManager m_resourceManager;

		private readonly ICrossPromoDataParser m_parser;

		private bool m_syncInProgress;

		private CrossPromoMetaData m_metaData;

		// 06000011 (1.10.1 RVA 0x1A0F030)
		public bool IsEnabled => m_metaData.IsSystemEnabled;

		// 06000012 (1.10.1 RVA 0x1A0E820)
		public bool IsReady()
		{
			return !m_syncInProgress && !m_resourceManager.HasPendingDownloads;
		}

		// 06000013 (1.10.1 RVA 0x1A0F040): the download manager, resource manager and parser are
		// built in that order, then the three-argument constructor runs.
		public CrossPromo(CrossPromoConfig config, ICoroutineHandler coroutineHandler)
			: this(config, new CrossPromoResourceManager(config, new CrossPromoDownloadManager(config, coroutineHandler)), new CrossPromoDataParser(config))
		{
		}

		// 06000014 (1.10.1 RVA 0x1A0F570): subscribes to image downloads before asking for the metadata.
		public CrossPromo(CrossPromoConfig config, ICrossPromoResourceManager resourceManager, ICrossPromoDataParser dataParser)
		{
			m_config = config;
			m_resourceManager = resourceManager;
			m_resourceManager.DownloadCompleted += DownloadCompleted;
			m_parser = dataParser;
			m_syncInProgress = true;
			m_resourceManager.DownloadMetaData(OnMetaDataDownloadCompleted);
		}

		// 06000015 (1.10.1 RVA 0x1A0F7A0): success is never read. Only enabled, parsed metadata starts
		// the ad image check; every other outcome ends the sync without raising Initialised.
		private void OnMetaDataDownloadCompleted(bool success)
		{
			string metaData = m_resourceManager.RequestMetaData();
			if (string.IsNullOrEmpty(metaData))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromo.OnMetaDataDownloadCompleted: Metadata is null or empty.");
			}
			else
			{
				Dictionary<string, Dictionary<string, object>> dataRoot = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(metaData);
				if (dataRoot == null)
				{
					HLOutput.LogError(m_config.Debug, "CrossPromo.OnMetaDataDownloadCompleted: Invalid JSON.");
				}
				else if (m_parser.TryParseMetadata(dataRoot, out m_metaData) && m_metaData.IsSystemEnabled)
				{
					m_resourceManager.VerifyAdResources(m_metaData.Ads);
					TryFinishSync();
					return;
				}
			}
			m_syncInProgress = false;
		}

		// 06000016 (1.10.1 RVA 0x1A0FC00): success is never read.
		private void DownloadCompleted(bool success)
		{
			TryFinishSync();
		}

		// 06000017 (1.10.1 RVA 0x1A0FB20)
		private void TryFinishSync()
		{
			if (!m_resourceManager.HasPendingDownloads)
			{
				m_syncInProgress = false;
				Initialised?.Invoke(m_metaData);
			}
		}

		// 06000018 (1.10.1 RVA 0x1A0FCE0)
		public Sprite RequestIconTexture(CrossPromoTexture texMeta)
		{
			return m_resourceManager.RequestIconTexture(texMeta);
		}

		// 06000019 (1.10.1 RVA 0x1A0FD90)
		public void ReleaseIconTexture(Sprite texture)
		{
			m_resourceManager.ReleaseIconTexture(texture);
		}

		// 0600001a (1.10.1 RVA 0x1A0FE40)
		public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta)
		{
			return m_resourceManager.RequestFullScreenTexture(texMeta);
		}

		// 0600001b (1.10.1 RVA 0x1A0FEF0)
		public void ReleaseFullScreenTexture(Sprite texture)
		{
			m_resourceManager.ReleaseFullScreenTexture(texture);
		}

		// 0600001c (1.10.1 RVA 0x1A0FFA0)
		public void RaiseImpressionEvent(CrossPromoAd ad, int impressionCount)
		{
			LaunchButtonShown?.Invoke(ad, impressionCount);
		}

		// 0600001d (1.10.1 RVA 0x1A0FFC0)
		public void RequestFullScreenView(int adIndex)
		{
			FullScreenViewRequested?.Invoke(m_metaData, adIndex);
		}

		// 0600001e (1.10.1 RVA 0x1A10000)
		public void RaiseFullScreenImpression(CrossPromoAd ad)
		{
			FullScreenViewShown?.Invoke(ad);
		}

		// 0600001f (1.10.1 RVA 0x1A10020)
		public void RaiseFullScreenButtonClicked(CrossPromoAd ad)
		{
			FullScreenButtonClicked?.Invoke(ad);
		}

		// 06000020 (1.10.1 RVA 0x1A10040)
		public void RaiseFullScreenClosedEvent()
		{
			FullScreenViewClosed?.Invoke();
		}

		// 06000021 (1.10.1 RVA 0x1A10060)
		public static bool IsSupported()
		{
			return true;
		}
	}
}
