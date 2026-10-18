using System;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200041c; recovered struct declaration from the 1.10.1 metadata (no own methods).
	[Serializable]
	public struct CollectableCount
	{
		public CollectableType Type;

		public int Total;

		[InspectorReadOnly]
		public string[] Guids;
	}
}
