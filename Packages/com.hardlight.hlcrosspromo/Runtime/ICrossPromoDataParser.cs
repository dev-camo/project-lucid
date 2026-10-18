using System.Collections.Generic;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000019; declarations only, no native bodies.
	public interface ICrossPromoDataParser
	{
		bool TryParseMetadata(Dictionary<string, Dictionary<string, object>> dataRoot, out CrossPromoMetaData metaData);
	}
}
