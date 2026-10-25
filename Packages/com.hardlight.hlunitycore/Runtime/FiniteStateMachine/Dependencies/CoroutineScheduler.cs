using System;
using System.Collections;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 0x020002b0. A manually ticked coroutine
	// list: new coroutines are pushed at the head, and a coroutine yields an
	// int frame delay (null counts as one frame), a float time delay or
	// another CoroutineNode to wait for.
	public class CoroutineScheduler
	{
		private CoroutineNode m_first;
		private int m_currentFrame;
		private float m_currentTime;

		// 0x06001168; 0x1B30E30. A null fiber is ignored; the node is not
		// stepped until the next UpdateAllCoroutines.
		public CoroutineNode StartCoroutine(IEnumerator fiber)
		{
			if (fiber == null) return null;
			CoroutineNode coroutine = new CoroutineNode(fiber);
			AddCoroutine(coroutine);
			return coroutine;
		}

		// 0x06001169; 0x1B30F60. Only the head is dropped; nodes keep their
		// links and are not marked finished.
		public void StopAllCoroutines()
		{
			m_first = null;
		}

		// 0x0600116a; 0x1B30F80.
		public bool HasCoroutines() => m_first != null;

		// 0x0600116b; 0x1B30F90. The next link is read before stepping, and
		// at most one wait condition is resolved per node per call.
		public void UpdateAllCoroutines(int frame, float time)
		{
			m_currentFrame = frame;
			m_currentTime = time;
			CoroutineNode coroutine = m_first;
			while (coroutine != null)
			{
				CoroutineNode listNext = coroutine.m_listNext;
				if (coroutine.m_waitForFrame > 0 && frame >= coroutine.m_waitForFrame)
				{
					coroutine.m_waitForFrame = -1;
					UpdateCoroutine(coroutine);
				}
				else if (coroutine.m_waitForTime > 0f && time >= coroutine.m_waitForTime)
				{
					coroutine.m_waitForTime = -1f;
					UpdateCoroutine(coroutine);
				}
				else if (coroutine.m_waitForCoroutine != null && coroutine.m_waitForCoroutine.m_finished)
				{
					coroutine.m_waitForCoroutine = null;
					UpdateCoroutine(coroutine);
				}
				else if (coroutine.m_waitForFrame == -1 && coroutine.m_waitForTime == -1f && coroutine.m_waitForCoroutine == null)
				{
					UpdateCoroutine(coroutine);
				}
				coroutine = listNext;
			}
		}

		// 0x0600116c; 0x1B31080. Current is read once. The CoroutineNode test
		// is exact-type; anything else throws after the fiber has advanced.
		// The spelling is inferred: an int class test (0x1B3124A) is followed
		// by an UnBoxNullable_internal call (0x1B31372), and the init list
		// holds Nullable<int>.get_HasValue and GetValueOrDefault, which is the
		// `(int?)x ?? 1` lowering. Keep it rather than `is int frames` or a
		// GetType() comparison.
		private void UpdateCoroutine(CoroutineNode coroutine)
		{
			IEnumerator fiber = coroutine.m_fiber;
			if (fiber.MoveNext())
			{
				object yieldCommand = fiber.Current;
				if (yieldCommand == null || yieldCommand is int)
				{
					coroutine.m_waitForFrame = ((int?)yieldCommand ?? 1) + m_currentFrame;
				}
				else if (yieldCommand is float)
				{
					coroutine.m_waitForTime = (float)yieldCommand + m_currentTime;
				}
				else if (yieldCommand.GetType() == typeof(CoroutineNode))
				{
					coroutine.m_waitForCoroutine = (CoroutineNode)yieldCommand;
				}
				else
				{
					throw new ArgumentException("CoroutineScheduler: Unexpected coroutine yield type: " + yieldCommand.GetType());
				}
			}
			else
			{
				coroutine.m_finished = true;
				RemoveCoroutine(coroutine);
			}
		}

		// 0x0600116d; 0x1B30F00.
		private void AddCoroutine(CoroutineNode coroutine)
		{
			if (m_first != null)
			{
				coroutine.m_listNext = m_first;
				m_first.m_listPrevious = coroutine;
			}
			m_first = coroutine;
		}

		// 0x0600116e; 0x1B31480. Null returns before any field access.
		// A non-head node with no links is left in
		// place; one with a next link but no previous link faults before its
		// links are cleared. Otherwise the removed node's links are cleared.
		private void RemoveCoroutine(CoroutineNode coroutine)
		{
			if (coroutine == null) return;
			if (m_first == coroutine)
			{
				m_first = coroutine.m_listNext;
			}
			else if (coroutine.m_listNext != null)
			{
				coroutine.m_listPrevious.m_listNext = coroutine.m_listNext;
				coroutine.m_listNext.m_listPrevious = coroutine.m_listPrevious;
			}
			else if (coroutine.m_listPrevious != null)
			{
				coroutine.m_listPrevious.m_listNext = null;
			}
			coroutine.m_listPrevious = null;
			coroutine.m_listNext = null;
		}
	}
}
