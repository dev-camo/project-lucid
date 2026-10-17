using System.Collections.Generic;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 8809, methods 0x06000185..0x06000189.
	// Subclasses override the three defaults read before configuration loads.
	public class ConditionalOutputPreInitialiseSettings
	{
		protected virtual bool DefaultLogState => true;

		protected virtual ConditionalOutputSettings.UnhandledLogBehaviour PreInitialiseLogBehaviour => ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog;

		// 0x1aa2520: a new list on every read.
		protected virtual List<ConditionalOutputSettings.LogStateOverride> DefaultLogStateOverrides => new List<ConditionalOutputSettings.LogStateOverride>();

		// 0x1aa15b0: the three virtual reads happen in declaration order.
		public ConditionalOutputSettings CreateConditionalOutputSettings()
		{
			return new ConditionalOutputSettings(DefaultLogState, PreInitialiseLogBehaviour, DefaultLogStateOverrides);
		}
	}
}
