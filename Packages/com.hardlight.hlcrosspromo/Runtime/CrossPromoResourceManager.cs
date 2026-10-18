using System;
using System.IO;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000010; bodies translated from the 1.10.1 x86-64 binary.
	// Keeps ad images in the destination folder, downloading missing ones and pruning obsolete ones.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromoResourceManager : ICrossPromoResourceManager
	{
		// Original nested struct 02000011; DeleteObsoleteFiles binds its Find once per local file.
		private struct AdFinder
		{
			public string Search;

			// 0600008f (1.10.1 RVA 0x1A14D40)
			public bool Find(CrossPromoAd v)
			{
				return v.IconTexture.LocalFileName == Search || v.FullScreenTexture.LocalFileName == Search;
			}
		}

		// 0600007e (1.10.1 RVA 0x1A13EE0), 0600007f (1.10.1 RVA 0x1A13F90)
		public event Action<bool> DownloadCompleted;

		private readonly string m_localFolder;

		private readonly CrossPromoConfig m_config;

		private readonly ICrossPromoDownloadManager m_downloadManager;

		private readonly ISharedResourcePool<string, Sprite> m_mainImagePool;

		private readonly ISharedResourcePool<string, Sprite> m_iconImagePool;

		// 06000080 (1.10.1 RVA 0x1A14040)
		public bool HasPendingDownloads => m_downloadManager.HasPendingDownloads;

		// 06000081 (1.10.1 RVA 0x1A0F380): pools whose initial capacity comes from the config;
		// each destroys a sprite once its last holder releases it.
		public CrossPromoResourceManager(CrossPromoConfig config, ICrossPromoDownloadManager downloadManager)
			: this(config, downloadManager,
				new SharedResourcePool<string, Sprite>(config.IconImagePoolSize, Object.Destroy),
				new SharedResourcePool<string, Sprite>(config.MainImagePoolSize, Object.Destroy))
		{
		}

		// 06000082 (1.10.1 RVA 0x1A140D0): the destination folder is kept; the incoming folder always starts empty.
		public CrossPromoResourceManager(CrossPromoConfig config, ICrossPromoDownloadManager downloadManager, ISharedResourcePool<string, Sprite> iconImagePool, ISharedResourcePool<string, Sprite> mainImagePool)
		{
			m_config = config;
			m_downloadManager = downloadManager;
			m_iconImagePool = iconImagePool;
			m_mainImagePool = mainImagePool;
			m_localFolder = config.DestinationPath;
			if (!Directory.Exists(m_localFolder))
			{
				Directory.CreateDirectory(m_localFolder);
			}
			string incomingPath = config.IncomingPath;
			if (Directory.Exists(incomingPath))
			{
				Directory.Delete(incomingPath, true);
			}
			Directory.CreateDirectory(incomingPath);
		}

		// 06000083 (1.10.1 RVA 0x1A141E0)
		public void DownloadMetaData(Action<bool> metaDataDownloadCallback)
		{
			m_downloadManager.StartDownload(m_config.MetaDataURL, m_config.MetaFilePath, true, metaDataDownloadCallback);
		}

		// 06000084 (1.10.1 RVA 0x1A142B0)
		public string RequestMetaData()
		{
			if (File.Exists(m_config.MetaFilePath))
			{
				return File.ReadAllText(m_config.MetaFilePath);
			}
			return null;
		}

		// 06000085 (1.10.1 RVA 0x1A142F0)
		public void VerifyAdResources(CrossPromoAd[] ads)
		{
			string[] localFiles = Directory.GetFiles(m_localFolder);
			VerifyAndDownloadRequiredAssets(localFiles, ads);
			DeleteObsoleteFiles(localFiles, ads);
		}

		// 06000086 (1.10.1 RVA 0x1A14400)
		private void VerifyAndDownloadRequiredAssets(string[] localFiles, CrossPromoAd[] ads)
		{
			foreach (CrossPromoAd ad in ads)
			{
				VerifyAndDownloadRequiredAsset(ad.IconTexture, localFiles);
				VerifyAndDownloadRequiredAsset(ad.FullScreenTexture, localFiles);
			}
		}

		// 06000087 (1.10.1 RVA 0x1A14690)
		private void VerifyAndDownloadRequiredAsset(CrossPromoTexture texture, string[] localFiles)
		{
			string localFilePath = Path.Combine(m_localFolder, texture.LocalFileName);
			if (localFiles.Contains(localFilePath))
			{
				return;
			}
			m_downloadManager.StartDownload(texture.URL, localFilePath, false, DownloadCompleted);
		}

		// 06000088 (1.10.1 RVA 0x1A14520): keeps the meta file (exact ordinal path match) and every file an ad still names.
		private void DeleteObsoleteFiles(string[] localFiles, CrossPromoAd[] ads)
		{
			foreach (string localFile in localFiles)
			{
				if (string.Compare(localFile, m_config.MetaFilePath, StringComparison.Ordinal) == 0)
				{
					continue;
				}
				AdFinder finder = new AdFinder { Search = Path.GetFileName(localFile) };
				if (ads.FindIndex(finder.Find) < 0)
				{
					File.Delete(localFile);
				}
			}
		}

		// 06000089 (1.10.1 RVA 0x1A147D0)
		public Sprite RequestIconTexture(CrossPromoTexture texMeta)
		{
			return HandleTextureRequest(m_iconImagePool, texMeta);
		}

		// 0600008a (1.10.1 RVA 0x1A14A60)
		public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta)
		{
			return HandleTextureRequest(m_mainImagePool, texMeta);
		}

		// 0600008b (1.10.1 RVA 0x1A147F0): pool, then device; otherwise starts a download and returns null for now.
		private Sprite HandleTextureRequest(ISharedResourcePool<string, Sprite> pool, CrossPromoTexture texMeta)
		{
			if (pool.TryGet(texMeta.LocalFileName, out Sprite sprite))
			{
				return sprite;
			}
			sprite = LoadTextureFromDevice(texMeta);
			if (sprite != null)
			{
				pool.Add(texMeta.LocalFileName, sprite);
				return sprite;
			}
			m_downloadManager.StartDownload(texMeta.URL, Path.Combine(m_localFolder, texMeta.LocalFileName), false, DownloadCompleted);
			return null;
		}

		// 0600008c (1.10.1 RVA 0x1A14A80)
		private Sprite LoadTextureFromDevice(CrossPromoTexture texMeta)
		{
			string localFilePath = Path.Combine(m_localFolder, texMeta.LocalFileName);
			if (!File.Exists(localFilePath))
			{
				return null;
			}
			byte[] data = File.ReadAllBytes(localFilePath);
			Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
			texture.LoadImage(data);
			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
		}

		// 0600008d (1.10.1 RVA 0x1A14BE0)
		public void ReleaseIconTexture(Sprite texture)
		{
			m_iconImagePool.ReleaseResource(texture);
		}

		// 0600008e (1.10.1 RVA 0x1A14C90)
		public void ReleaseFullScreenTexture(Sprite texture)
		{
			m_mainImagePool.ReleaseResource(texture);
		}
	}
}
