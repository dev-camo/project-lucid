using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200001b; declarations only, no native bodies.
	public interface ICrossPromoResourceManager
	{
		event Action<bool> DownloadCompleted;

		bool HasPendingDownloads
		{
			get;
		}

		void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted);

		void VerifyAdResources(CrossPromoAd[] metaDataAds);

		Sprite RequestFullScreenTexture(CrossPromoTexture texMeta);

		void ReleaseFullScreenTexture(Sprite texture);

		Sprite RequestIconTexture(CrossPromoTexture texMeta);

		void ReleaseIconTexture(Sprite texture);

		string RequestMetaData();
	}
}
