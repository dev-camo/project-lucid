using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000492 and nested 02000493; bodies translated from the 1.10.1 x86-64 binary
	// (0x53F240..0x53F614).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_ProjectileSpawnAndShootInRows", menuName = "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShootInRows", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossAbilityDefinition_ProjectileSpawnAndShootInRows : BossAbilityDefinition_ProjectileSpawnAndShoot
	{
		[FormerlySerializedAs("ProjectileGroupLaunchTime")]
		[Tooltip("Time to launch all projectiles in a group.")]
		public float ProjectileGroupLaunchTimeSeconds = 1f;

		public SpawnData[] SpawnOrders;

		[Tooltip("Rows will be ignored from the spawn pool for X number of spawns after they are used.")]
		public int RowPatternIgnoreCount = 3;

		public override Type ScriptType => typeof(BossAbility_ProjectileSpawnAndShootInRows);

		// 0x53F2A0: every authored row rebuilds its lane lists from the CSV text.
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			for (int i = 0; i < SpawnOrders.Length; i++)
				SpawnOrders[i].CalculateCachedValues();
		}

		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		public class SpawnData
		{
			public string LanesInUseCSV;

			public string ReturnableLanesCSV;

			[HideInInspector]
			public List<int> LanesInUseInt = new List<int>();

			[HideInInspector]
			public List<int> ReturnableLaneChoices = new List<int>();

			[NonSerialized]
			public int ReturnableLane;

			// 0x53F2F0: entries that do not parse as integers are skipped; a null CSV faults at Split.
			public void CalculateCachedValues()
			{
				LanesInUseInt.Clear();
				foreach (string lane in LanesInUseCSV.Split(","))
					if (int.TryParse(lane, out int laneIndex))
						LanesInUseInt.Add(laneIndex);
				ReturnableLaneChoices.Clear();
				foreach (string lane in ReturnableLanesCSV.Split(","))
					if (int.TryParse(lane, out int returnableLaneIndex))
						ReturnableLaneChoices.Add(returnableLaneIndex);
			}

			// 0x53F510: an empty choice list faults in the list indexer, as Random.Range(0, 0) yields 0.
			public void ChooseReturnRow()
			{
				ReturnableLane = ReturnableLaneChoices[UnityEngine.Random.Range(0, ReturnableLaneChoices.Count)];
			}
		}
	}
}
