using System;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002d2; bodies translated from the 1.10.1 x86-64 binary.
	public static class SocialUtils
	{
		// 06001251 (1.10.1 RVA 0x1B36D50)
		public static void GetUsername(string id, Action<string> nameCallback)
		{
			GetUserProfile(id, UserProfileCallback);

			// 06001256 (1.10.1 RVA 0x1B37020) (<>c__DisplayClass0_0.<GetUsername>g__UserProfileCallback|0)
			void UserProfileCallback(IUserProfile userProfile)
			{
				nameCallback(userProfile != null ? userProfile.userName : null);
			}
		}

		// 06001252 (1.10.1 RVA 0x1B36F30)
		public static void GetUsernames(string[] ids, Action<string[]> namesCallback)
		{
			Social.LoadUsers(ids, UserProfilesCallback);

			// 06001258 (1.10.1 RVA 0x1B370E0) (<>c__DisplayClass1_0.<GetUsernames>g__UserProfilesCallback|0)
			void UserProfilesCallback(IUserProfile[] userProfiles)
			{
				string[] names = new string[userProfiles.Length];
				for (int i = 0; i < userProfiles.Length; i++)
				{
					names[i] = userProfiles[i].userName;
				}
				namesCallback(names);
			}
		}

		// 06001253 (1.10.1 RVA 0x1B36E20)
		public static void GetUserProfile(string id, Action<IUserProfile> userCallback)
		{
			Social.LoadUsers(new string[] { id }, UserProfileCallback);

			// 0600125a (1.10.1 RVA 0x1B372C0) (<>c__DisplayClass2_0.<GetUserProfile>g__UserProfileCallback|0)
			void UserProfileCallback(IUserProfile[] profiles)
			{
				userCallback(profiles.Length > 0 ? profiles[0] : null);
			}
		}

		// 06001254 (1.10.1 RVA 0x1B37000)
		public static void GetUserProfiles(string[] ids, Action<IUserProfile[]> userCallback)
		{
			Social.LoadUsers(ids, userCallback);
		}
	}
}
