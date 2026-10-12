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

		public int m_waitForFrame;

		public float m_waitForTime;

		public CoroutineNode m_waitForCoroutine;

		// 06001167 (1.10.1 RVA 0x1B30DF0)
		public CoroutineNode(IEnumerator fiber)
		{
			m_waitForFrame = -1;
			m_waitForTime = -1f;
			m_fiber = fiber;
		}
	}
}
