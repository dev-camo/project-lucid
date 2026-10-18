using System;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000016; bodies translated from the 1.10.1 x86-64 binary.
	public struct CrossPromoMetaData
	{
		public bool IsSystemEnabled;

		public CrossPromoAd[] Ads;

		public string FirstAd;

		public string PressToPlayString;

		// 060000a7 (1.10.1 RVA 0x1A15CC0). A missing ad list or an unmatched FirstAd both select ad 0.
		public int GetFirstAdIndex()
		{
			if (Ads == null)
			{
				return 0;
			}
			int index = Ads.FindIndex(FindFirstAdIndex);
			return index > 0 ? index : 0;
		}

		// 060000a8 (1.10.1 RVA 0x1A15DA0)
		private bool FindFirstAdIndex(CrossPromoAd ad)
		{
			return string.Compare(ad.Name, FirstAd, StringComparison.Ordinal) == 0;
		}
	}
}
