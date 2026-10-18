using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000012; bodies translated from the 1.10.1 x86-64 binary.
	// Memory-only resource manager: metadata and sprites live in fields and pools, never on disk.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromoResourceManagerCacheOnly : ICrossPromoResourceManager
	{
		// Original nested enum 02000013. It is declared before the constructor, which the
		// empty handler's name <.ctor>b__13_0 requires: that ordinal counts it.
		public enum SpriteType
		{
			Icon = 0,
			Main = 1
		}

		// 06000090 (1.10.1 RVA 0x1A14D80), 06000091 (1.10.1 RVA 0x1A14E30); the empty handler is <>c.<.ctor>b__13_0.
		public event Action<bool> DownloadCompleted = delegate { };

		// 06000092 (1.10.1 RVA 0x1A14EE0), 06000093 (1.10.1 RVA 0x1A14EF0)
		public bool HasPendingDownloads { get; private set; }

		private readonly Dictionary<SpriteType, ISharedResourcePool<string, Sprite>> m_imagePool = new Dictionary<SpriteType, ISharedResourcePool<string, Sprite>>();

		private readonly CrossPromoDownloadManagerCacheOnly m_downloadManager;

		private readonly CrossPromoConfig m_config;

		private string m_metaData;

		private List<string> m_downloadedFileNames = new List<string>();

		// 06000094 (1.10.1 RVA 0x1A14F00)
		public CrossPromoResourceManagerCacheOnly(CrossPromoConfig config, ICoroutineHandler coroutineHandler)
		{
			m_config = config;
			m_imagePool.Add(SpriteType.Icon, new SharedResourcePool<string, Sprite>(config.IconImagePoolSize, Object.Destroy));
			m_imagePool.Add(SpriteType.Main, new SharedResourcePool<string, Sprite>(config.MainImagePoolSize, Object.Destroy));
			m_downloadManager = new CrossPromoDownloadManagerCacheOnly(config, coroutineHandler);
		}

		// 06000095 (1.10.1 RVA 0x1A15300)
		public void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted)
		{
			m_downloadManager.StartDownloadMetaData(m_config.MetaDataURL, m_config.MetaFilePath, OnMetaDataDownloaded, onMetaDataDownloadCompleted);
		}

		// 06000096 (1.10.1 RVA 0x1A153A0)
		private void OnMetaDataDownloaded(string metaData)
		{
			m_metaData = metaData;
		}

		// 06000097 (1.10.1 RVA 0x1A153C0): only raises the pending flag; sprites download on request instead.
		public void VerifyAdResources(CrossPromoAd[] ads)
		{
			HasPendingDownloads = true;
		}

		// 06000098 (1.10.1 RVA 0x1A153D0): no caller in this assembly.
		private void VerifyAndDownloadRequiredAssets(ICollection<string> localFiles, CrossPromoAd[] ads)
		{
			foreach (CrossPromoAd ad in ads)
			{
				VerifyAndDownloadRequiredAsset(ad.IconTexture, localFiles, SpriteType.Icon);
				VerifyAndDownloadRequiredAsset(ad.FullScreenTexture, localFiles, SpriteType.Main);
			}
		}

		// 06000099 (1.10.1 RVA 0x1A15500)
		private void VerifyAndDownloadRequiredAsset(CrossPromoTexture texture, ICollection<string> localFiles, SpriteType spriteType)
		{
			if (localFiles.Contains(texture.LocalFileName))
			{
				return;
			}
			m_downloadManager.StartDownloadSprite(texture.URL, texture.LocalFileName, spriteType, OnTextureDownloaded);
		}

		// 0600009a (1.10.1 RVA 0x1A15630)
		public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta)
		{
			return HandleTextureRequest(texMeta, SpriteType.Main);
		}

		// 0600009b (1.10.1 RVA 0x1A157A0)
		public void ReleaseFullScreenTexture(Sprite texture)
		{
			m_imagePool[SpriteType.Main].ReleaseResource(texture);
		}

		// 0600009c (1.10.1 RVA 0x1A15870)
		public Sprite RequestIconTexture(CrossPromoTexture texMeta)
		{
			return HandleTextureRequest(texMeta, SpriteType.Icon);
		}

		// 0600009d (1.10.1 RVA 0x1A15880)
		public void ReleaseIconTexture(Sprite texture)
		{
			m_imagePool[SpriteType.Icon].ReleaseResource(texture);
		}

		// 0600009e (1.10.1 RVA 0x1A15950)
		public string RequestMetaData()
		{
			return m_metaData;
		}

		// 0600009f (1.10.1 RVA 0x1A15640): a pool miss starts a download and returns null for now.
		private Sprite HandleTextureRequest(CrossPromoTexture texMeta, SpriteType spriteType)
		{
			if (m_imagePool[spriteType].TryGet(texMeta.LocalFileName, out Sprite sprite))
			{
				return sprite;
			}
			m_downloadManager.StartDownloadSprite(texMeta.URL, texMeta.LocalFileName, spriteType, OnTextureDownloaded);
			return null;
		}

		// 060000a0 (1.10.1 RVA 0x1A15960): DownloadCompleted is invoked without a null check.
		private void OnTextureDownloaded(byte[] data, string localFilePath, SpriteType spriteType)
		{
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
			texture.LoadImage(data);
			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
			m_imagePool[spriteType].Add(localFilePath, sprite);
			m_downloadedFileNames.AddUnique(localFilePath);
			HasPendingDownloads = m_downloadManager.HasPendingDownloads;
			DownloadCompleted(true);
		}
	}
}
