using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Original Game.Runtime 02000006 (x86-64 0x506870..0x50698F): the clip asset of a TimelineEventTrack.
[Serializable]
public class TimelineEventClip : PlayableAsset, ITimelineClipAsset
{
	[SerializeField] private TimelineEventBehaviour m_eventPlayableAsset = new TimelineEventBehaviour();

	public TimelineEventBehaviour Behaviour { get { return m_eventPlayableAsset; } } // 06000010

	public ClipCaps clipCaps { get { return ClipCaps.None; } } // 06000011

	// 06000012
	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		return ScriptPlayable<TimelineEventBehaviour>.Create(graph, m_eventPlayableAsset, 0);
	}

	public TimelineEventClip() { } // 06000013: the behaviour is allocated before the PlayableAsset constructor.
}
