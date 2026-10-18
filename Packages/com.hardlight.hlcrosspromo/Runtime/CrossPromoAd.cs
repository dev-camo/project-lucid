using System;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000015; bodies translated from the 1.10.1 x86-64 binary.
	// Identity is the ad name only; the textures and store URL take no part in equality.
	public struct CrossPromoAd : IEquatable<CrossPromoAd>
	{
		public string Name;

		public CrossPromoTexture IconTexture;

		public CrossPromoTexture FullScreenTexture;

		public string StoreURL;

		// 060000a4 (1.10.1 RVA 0x1A15C00)
		public override bool Equals(object obj)
		{
			return obj is CrossPromoAd other && Equals(other);
		}

		// 060000a5 (1.10.1 RVA 0x1A15C70)
		public bool Equals(CrossPromoAd other)
		{
			return string.Equals(Name, other.Name, StringComparison.Ordinal);
		}

		// 060000a6 (1.10.1 RVA 0x1A15C90)
		public override int GetHashCode()
		{
			return Name != null ? Name.GetHashCode() : 0;
		}
	}
}
