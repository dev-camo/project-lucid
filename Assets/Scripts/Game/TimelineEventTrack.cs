using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Original Game.Runtime 02000008 (x86-64 0x5069A0..0x506EDF): hands the track's bound GameObject to every
// event clip so the clip's behaviour can look up its target.
[TrackColor(0.4448276f, 0f, 1f)]
[TrackClipType(typeof(TimelineEventClip))]
[TrackBindingType(typeof(GameObject))]
public class TimelineEventTrack : TrackAsset
{
	// 06000015
	public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
	{
		GameObject boundObject = go.GetComponent<PlayableDirector>().GetGenericBinding(this) as GameObject;
		foreach (TimelineClip clip in GetClips())
		{
			TimelineEventClip eventClip = clip.asset as TimelineEventClip;
			if (eventClip && boundObject) eventClip.Behaviour.TargetObject = boundObject;
		}
		return ScriptPlayable<TimelineEventMixerBehaviour>.Create(graph, inputCount);
	}

	public TimelineEventTrack() { } // 06000016
}
