using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000a37; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCascade : MonoBehaviour
	{
		[SerializeField]
		private CascadeObjectType m_cascadeObjectType;
		[SerializeField]
		private int m_amount;
		[SerializeField]
		private Transform m_origin;
		[SerializeField]
		private Vector3 m_initialVelocity;

		// 06003a77 (1.10.1 RVA 0x6B82E0): the origin's rotation is passed as both the launch rotation and the object rotation;
		// m_initialVelocity is passed unrotated.
		public void Trigger()
		{
			CascadeObjectPool pool = CascadeObjectPool.GetCascadeObjectPoolByType(m_cascadeObjectType);
			Quaternion rotation = m_origin.rotation;
			pool.TriggerCascade(m_cascadeObjectType, m_amount, m_origin.position, rotation, m_initialVelocity, rotation);
		}
	}
}
