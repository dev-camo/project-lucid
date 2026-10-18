using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000039; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MeshPlacementSetup", menuName = "Hardlight/Procedural/Mesh Placement Setup", order = 0)]
	public class ProceduralMesh_PlacementSetup : ScriptableObject
	{
		// Original HLSplines.Runtime 0200003a.
		[Serializable]
		public class PerformanceSetup : IComparable
		{
			[SerializeField]
			private PerformanceProfile.QualityLevel m_qualityLevel;

			[SerializeField]
			private float m_spawnDistance;

			[SerializeField]
			private float m_despawnExtraDistance;

			[SerializeField]
			private float m_fadeDistance;

			[SerializeField]
			private bool m_populatePerKnot;

			[SerializeField]
			private float m_numMeshesMultiplier;

			[SerializeField]
			private bool m_useRuntimeCallback;

			// 0600010a..06000110 (1.10.1 RVA 0x1A61340..0x1A613A0)
			public PerformanceProfile.QualityLevel QualityLevel => m_qualityLevel;

			public float SpawnDistance => m_spawnDistance;

			public float DespawnExtraDistance => m_despawnExtraDistance;

			public float FadeDistance => m_fadeDistance;

			public float NumMeshesMultiplier => m_numMeshesMultiplier;

			public bool PopulatePerKnot => m_populatePerKnot;

			public bool UseRuntimeCallback => m_useRuntimeCallback;

			// 06000111 (1.10.1 RVA 0x1A60E70). Declared explicitly: the defaults are
			// stored after the base constructor call, in this order.
			public PerformanceSetup()
			{
				m_qualityLevel = PerformanceProfile.QualityLevel.Medium;
				m_spawnDistance = 40f;
				m_despawnExtraDistance = 1f;
				m_fadeDistance = 5f;
				m_numMeshesMultiplier = 1f;
				m_populatePerKnot = true;
				m_useRuntimeCallback = false;
			}

			// 06000112 (1.10.1 RVA 0x1A613B0). Orders by quality level alone.
			public int CompareTo(object compare)
			{
				PerformanceSetup other = (PerformanceSetup)compare;
				return m_qualityLevel.CompareTo(other.m_qualityLevel);
			}
		}

		[SerializeField]
		private List<PerformanceSetup> m_profiles = new List<PerformanceSetup>(1);

		public event Action<ProceduralMesh_PlacementSetup> OnUpdated;

		private PerformanceSetup m_selectedProfile;

		private SystemRef<ScalablePerformance> m_scalablePerformanceRef;

		// 06000101 (1.10.1 RVA 0x1A60D80)
		private void Reset()
		{
			m_profiles.Add(new PerformanceSetup());
		}

		// 06000102 (1.10.1 RVA 0x1A59B40). The lowest profile is the fallback; with a
		// live ScalablePerformance the highest profile not above the active
		// "ProceduralMeshPlacement" attribute level is selected instead.
		public PerformanceSetup GetSetup()
		{
			if (m_selectedProfile == null)
			{
				if (m_profiles.Count == 0)
				{
					m_selectedProfile = new PerformanceSetup();
				}
				else
				{
					AttemptScalablePerformanceSetup();
					List<PerformanceSetup> sortedProfiles = m_profiles.GetRange(0, m_profiles.Count);
					sortedProfiles.Sort();
					m_selectedProfile = sortedProfiles[0];
					if (m_scalablePerformanceRef.IsValid())
					{
						PerformanceProfile.QualityLevel selectedLevel = m_selectedProfile.QualityLevel;
						PerformanceAttribute attribute = m_scalablePerformanceRef.Get().ActiveProfile.GetAttribute("ProceduralMeshPlacement");
						if (attribute != null)
						{
							foreach (PerformanceSetup profile in sortedProfiles)
							{
								if (profile.QualityLevel > selectedLevel && profile.QualityLevel <= attribute.Level)
								{
									m_selectedProfile = profile;
									selectedLevel = profile.QualityLevel;
								}
							}
						}
					}
				}
			}
			return m_selectedProfile;
		}

		// 06000103 (1.10.1 RVA 0x1A61060)
		private void OnPerformanceProfileUpdated(PerformanceProfile performanceProfile)
		{
			m_selectedProfile = null;
			OnUpdated?.Invoke(this);
		}

		// 06000104 (1.10.1 RVA 0x1A610B0)
		private void Awake()
		{
			AttemptScalablePerformanceSetup();
		}

		// 06000105 (1.10.1 RVA 0x1A60EC0)
		private void AttemptScalablePerformanceSetup()
		{
			if (m_scalablePerformanceRef != null)
				return;

			m_scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>();
			m_scalablePerformanceRef.OnSystemStartup += OnScalablePerformanceStartup;
			m_scalablePerformanceRef.OnSystemShutdown += OnScalablePerformanceShutdown;
			if (m_scalablePerformanceRef.IsValid())
				OnScalablePerformanceStartup(m_scalablePerformanceRef.Get());
		}

		// 06000106 (1.10.1 RVA 0x1A61170)
		private void OnScalablePerformanceShutdown(ScalablePerformance scalablePerformance)
		{
			scalablePerformance.OnProfileUpdated -= OnPerformanceProfileUpdated;
		}

		// 06000107 (1.10.1 RVA 0x1A610C0)
		private void OnScalablePerformanceStartup(ScalablePerformance scalablePerformance)
		{
			scalablePerformance.OnProfileUpdated += OnPerformanceProfileUpdated;
			m_selectedProfile = null;
			OnUpdated?.Invoke(this);
		}

		// 06000108 (1.10.1 RVA 0x1A611F0)
		private void OnDestroy()
		{
			if (m_scalablePerformanceRef.IsValid())
				m_scalablePerformanceRef.Get().OnProfileUpdated -= OnPerformanceProfileUpdated;
		}
	}
}
