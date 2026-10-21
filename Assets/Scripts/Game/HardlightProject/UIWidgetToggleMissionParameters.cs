using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x020009d5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class UIWidgetToggleMissionParameters : UIWidgetToggleParameters
	{
		public bool Complete { get; }

		public bool Locked { get; }

		public bool UnlockSeen { get; }

		public bool SubRewardsAnchorRight { get; }

		public MissionIsNewState IsNewState { get; }

		public UIShowType RedStarRingsState { get; }

		public UIShowType BlueCoinsState { get; }

		public UIShowType JukeboxState { get; }

		public CollectableType RewardType { get; }

		// 0x060038a0 (0x6A4600): the base constructor takes the string label overload.
		public UIWidgetToggleMissionParameters(bool complete, bool locked, bool unlockSeen, bool subRewardsAnchorRight, string label, CollectableType rewardType, Action<bool> callback, MissionIsNewState isNewState, UIShowType redStarRingsState, UIShowType blueCoinsState, UIShowType jukeboxState)
			: base(label, callback)
		{
			Complete = complete;
			Locked = locked;
			UnlockSeen = unlockSeen;
			SubRewardsAnchorRight = subRewardsAnchorRight;
			IsNewState = isNewState;
			RedStarRingsState = redStarRingsState;
			BlueCoinsState = blueCoinsState;
			JukeboxState = jukeboxState;
			RewardType = rewardType;
		}
	}
}
