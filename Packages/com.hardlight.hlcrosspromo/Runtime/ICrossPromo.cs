using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000018; declarations only, no native bodies.
	public interface ICrossPromo : ISystem
	{
		event Action<CrossPromoMetaData> Initialised;

		event Action<CrossPromoMetaData, int> FullScreenViewRequested;

		event Action<CrossPromoAd, int> LaunchButtonShown;

		event Action<CrossPromoAd> FullScreenViewShown;

		event Action<CrossPromoAd> FullScreenButtonClicked;

		event Action FullScreenViewClosed;

		bool IsReady();

		Sprite RequestIconTexture(CrossPromoTexture iconTexture);

		void ReleaseIconTexture(Sprite texture);

		Sprite RequestFullScreenTexture(CrossPromoTexture fullScreenTexture);

		void ReleaseFullScreenTexture(Sprite texture);

		void RequestFullScreenView(int currentAd);

		void RaiseImpressionEvent(CrossPromoAd ad, int impressionCount);

		void RaiseFullScreenClosedEvent();

		void RaiseFullScreenImpression(CrossPromoAd ad);

		void RaiseFullScreenButtonClicked(CrossPromoAd ad);
	}
}
