using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a39; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public static class UIProgressionUtilities
	{
		// 06003a7a (1.10.1 RVA 0x6B83F0): the first unlock whose id has not been seen yet, which is
		// always the head of the list. The progression type is read and discarded, as in the original.
		public static bool TryGetCurrentUnlock(out IMetaGameUnlock currentUnlock)
		{
			currentUnlock = null;
			List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
			HashSet<string> seenIds = new HashSet<string>();
			foreach (IMetaGameUnlock unlock in unlocks)
			{
				unlock.GetProgressionType();
				if (seenIds.Add(unlock.GetId()))
				{
					currentUnlock = unlock;
					return true;
				}
			}
			return false;
		}

		// 06003a7b (shared generic RVA 0x9DC040): as above, but an unseen unlock that is not a T is skipped.
		public static bool TryGetCurrentUnlock<T>(out T currentUnlock) where T : class, IMetaGameUnlock
		{
			currentUnlock = null;
			List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
			HashSet<string> seenIds = new HashSet<string>();
			foreach (IMetaGameUnlock unlock in unlocks)
			{
				unlock.GetProgressionType();
				if (seenIds.Add(unlock.GetId()) && unlock is T)
				{
					currentUnlock = (T)unlock;
					return true;
				}
			}
			return false;
		}

		// 06003a7c (1.10.1 RVA 0x6B87D0); MoveNext 06003a81 (RVA 0x6B8E40). Samples the animation by
		// accumulated time scaled by the category, then stops it and completes.
		public static IEnumerator WaitForAnimation(Animation uiAnimation, TimeCategoryObject timeCategoryObject, Action onComplete, AnimationClip animationClip = null)
		{
			if (uiAnimation == null)
			{
				onComplete();
				yield break;
			}
			TimeManager timeManager = ProcessManager.GetSystem<TimeManager>();
			string clipName = (animationClip != null ? animationClip : uiAnimation.clip).name;
			uiAnimation.Play(clipName);
			AnimationState animationState = uiAnimation[clipName];
			animationState.weight = 1f;
			float duration = 0f;
			WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
			while (animationState.length > duration)
			{
				yield return waitForEndOfFrame;
				duration += timeManager.GetDeltaTime(timeCategoryObject);
				animationState.time = duration;
				uiAnimation.Sample();
			}
			uiAnimation.Stop();
			onComplete();
		}

		// 06003a7d (1.10.1 RVA 0x6B88B0)
		public static void RemoveUnlock(IMetaGameUnlock metaGameUnlock)
		{
			App app = ProcessManager.GetSystem<App>();
			List<IMetaGameUnlock> unlocks = app.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
			unlocks.Remove(metaGameUnlock);
			app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, unlocks);
		}

		// 06003a7e (1.10.1 RVA 0x6B8AE0): walks the unlocks back to front so removal keeps the indices valid.
		public static void RemoveUnlocksOfType(List<PlayerProgressionTypes> progressionTypes)
		{
			App app = ProcessManager.GetSystem<App>();
			List<IMetaGameUnlock> unlocks = app.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
			for (int i = unlocks.Count - 1; i >= 0; i--)
			{
				if (progressionTypes.Contains(unlocks[i].GetProgressionType()))
				{
					unlocks.RemoveAt(i);
				}
			}
			app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, unlocks);
		}
	}
}
