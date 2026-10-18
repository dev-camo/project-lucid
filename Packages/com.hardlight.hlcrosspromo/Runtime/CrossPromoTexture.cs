using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000017; bodies translated from the 1.10.1 x86-64 binary.
	public struct CrossPromoTexture
	{
		public string URL;

		public Vector2Int Resolution;

		public string LocalFileName;

		// 060000a9 (1.10.1 RVA 0x1A11F80). The local file name is derived from the URL alone.
		public CrossPromoTexture(string url, Vector2Int resolution)
		{
			URL = url;
			Resolution = resolution;
			LocalFileName = HashUtils.GetFileSafeSha1(url);
		}
	}
}
