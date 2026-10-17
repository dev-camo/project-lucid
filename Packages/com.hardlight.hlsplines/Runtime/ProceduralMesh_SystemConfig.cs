// Original HLSplines.Runtime.dll:Hardlight.ProceduralMesh_SystemConfig (0x0200004a).
// Evidence keys: original-configuration22/<assembly>/<type>/<method-token>; both shipping architectures.
// Original behavior is preserved here; current compilation and runtime qualification are recorded separately.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	[CreateAssetMenu(fileName = "ProceduralMeshSystemConfig", menuName = "Hardlight/Procedural/Mesh System Config", order = 0)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ProceduralMesh_SystemConfig : SystemConfigurationAsset
	{
		[SerializeField]
		// Original constructor 0x06000171 initializes only this flag; authored lock/frustum values are separate.
		private bool m_precomputeMeshLocations = true;

		[SerializeField]
		private bool m_resetMeshStorage;

		[SerializeField]
		private bool m_lockToGameCamera;

		[SerializeField]
		private bool m_useCameraFrustum;

		[SerializeField]
		private bool m_loadBalancePopulate;

		[SerializeField]
		private bool m_loadBalanceUnpopulate;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016a
		public bool PrecomputeMeshLocations => m_precomputeMeshLocations;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016b
		public bool ResetMeshStorage => m_resetMeshStorage;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016c
		public bool LockToGameCamera => m_lockToGameCamera;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016d
		public bool UseCameraFrustum => m_useCameraFrustum;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016e
		public bool LoadBalancePopulate => m_loadBalancePopulate;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x0600016f
		public bool LoadBalanceUnpopulate => m_loadBalanceUnpopulate;

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x06000170
		// Both shipping bodies return without field access or calls; this empty override is intentional.
		public override void Validate()
		{
		}

		// original-configuration22/HLSplines.Runtime.dll/Hardlight.ProceduralMesh_SystemConfig/0x06000171
		// The initializers above reproduce the original writes before the SystemConfigurationAsset base constructor; no additional own body work follows.
		public ProceduralMesh_SystemConfig()
		{
		}
	}
}
