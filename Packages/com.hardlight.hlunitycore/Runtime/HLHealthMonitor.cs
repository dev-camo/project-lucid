using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9075 (0x02000143), methods 0x060008cf..0x060008d3;
	// bodies from the 1.10.1 x86-64 binary (0x1aeead0..0x1aeec50).
	// The metadata carries the NullChecks/ArrayBoundsChecks pair four times, as when a
	// partial class repeats its attributes in each part; the four pairs are kept on this
	// single declaration. The iOS native monitor (HLHealthMonitor.h/.mm in the iOS module
	// file list) is not part of this player: GetDeviceHealthData returns the "0,0,0"
	// fallback and Start/Stop are empty here, and no other platform branch is visible.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLHealthMonitor
	{
		// TypeDefIndex 9074 (0x02000144).
		public struct HealthData
		{
			public int BatteryLevel;

			public int NetworkSent;

			public int NetworkReceived;
		}

		// 0x060008cf, 0x1aeead0: three comma-separated integers, each parsed
		// independently (a failed field stays 0). Any other count logs an error with
		// the still-zero struct, not the raw text, and returns zeros.
		public static HealthData GetHealthData()
		{
			HealthData healthData = new HealthData();
			string[] values = GetDeviceHealthData().Split(',');
			if (values.Length == 3)
			{
				int.TryParse(values[0], out healthData.BatteryLevel);
				int.TryParse(values[1], out healthData.NetworkSent);
				int.TryParse(values[2], out healthData.NetworkReceived);
			}
			else
			{
				HLOutput.LogError(string.Format("Invalid health data received ({0})", healthData));
			}
			return healthData;
		}

		// 0x060008d0: empty in this player.
		public static void Start()
		{
		}

		// 0x060008d1: empty in this player.
		public static void Stop()
		{
		}

		// 0x060008d2, 0x1aeec00: the constant fallback in this player.
		public static string GetDeviceHealthData()
		{
			return "0,0,0";
		}
	}
}
