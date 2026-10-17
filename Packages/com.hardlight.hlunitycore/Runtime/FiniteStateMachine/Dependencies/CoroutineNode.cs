using System.Collections;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002af; bodies translated from the 1.10.1 x86-64 binary.
	public class CoroutineNode
	{
		public CoroutineNode m_listPrevious;

		public CoroutineNode m_listNext;

		public IEnumerator m_fiber;

		public bool m_finished;

		public int m_waitForFrame = -1;

		public float m_waitForTime = -1f;

		public CoroutineNode m_waitForCoroutine;

		// 06001167 (1.10.1 RVA 0x1B30DF0). Both wait fields are initializers:
		// one 8-byte store at 0x1b30e07 precedes Object's constructor, and only
		// m_fiber is assigned after it.
		public CoroutineNode(IEnumerator fiber)
		{
			m_fiber = fiber;
		}
	}
}
