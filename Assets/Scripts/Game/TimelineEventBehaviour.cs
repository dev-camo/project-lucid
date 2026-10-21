using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;

// Original Game.Runtime 02000004 (x86-64 0x5060E0..0x50677F) with its <>c__DisplayClass8_0 closure (02000005):
// calls a named method on a component of the track's bound object when the clip starts playing.
[Serializable]
public class TimelineEventBehaviour : PlayableBehaviour
{
	[SerializeField] private string m_handlerKey;
	[SerializeField] private bool m_expectsSingleParameter;
	[SerializeField] private bool m_invokeEventInEditMode;
	public GameObject TargetObject;
	[SerializeField] private string m_argumentValue;
	private EventInvocationInfo m_invocationInfo;

	// 06000009: a clip start (frame id 0) or any forward step invokes the handler.
	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		if (info.frameId == 0 || info.deltaTime > 0f)
		{
			UpdateDelegates();
			if (m_invocationInfo != null) m_invocationInfo.Invoke(m_expectsSingleParameter, m_argumentValue);
		}
	}

	// 0600000a: handlers resolve in play mode, or in edit mode only when the clip asks for it.
	private void UpdateDelegates()
	{
		bool isEnabled = Application.isPlaying || m_invokeEventInEditMode;
		m_invocationInfo = GetInvocationInfo(isEnabled, m_handlerKey, m_invocationInfo, m_expectsSingleParameter);
	}

	// 0600000b: the cached info is kept while its key is unchanged and not "none"; otherwise the key is resolved
	// to a public instance method with that name, a void return and one parameter only when an argument is expected.
	private EventInvocationInfo GetInvocationInfo(bool isEnabled, string methodKey, EventInvocationInfo currentInfo, bool methodWithArg)
	{
		if (currentInfo != null && currentInfo.Key == methodKey && !string.IsNullOrEmpty(methodKey) && methodKey.ToLower() != "none")
			return currentInfo;
		Behaviour targetBehaviour = null;
		string methodName = null;
		GetBehaviourAndMethod(isEnabled, methodKey, ref targetBehaviour, ref methodName);
		if (targetBehaviour != null)
		{
			MethodInfo method = targetBehaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.FirstOrDefault(m => m.Name == methodName && m.ReturnType == typeof(void) && m.GetParameters().Length == (methodWithArg ? 1 : 0));
			return new EventInvocationInfo(methodKey, targetBehaviour, method);
		}
		return null;
	}

	// 0600000c: the key is "TypeName.MethodName"; an unresolvable key throws instead of being ignored.
	private void GetBehaviourAndMethod(bool isEnabled, string key, ref Behaviour targetBehaviour, ref string methodName)
	{
		if (!isEnabled || string.IsNullOrEmpty(key)) return;
		if (key.ToLower() == "none" || string.IsNullOrEmpty(key)) return;
		int separator = key.LastIndexOf('.');
		string typeName = key.Substring(0, separator);
		methodName = key.Substring(separator + 1, key.Length - (separator + 1));
		if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
			throw new Exception("Unable to parse callback method: " + key);
		targetBehaviour = null;
		if (TargetObject == null) throw new Exception("No target set for key " + key);
		Behaviour[] behaviours = TargetObject.GetComponents<Behaviour>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			Behaviour behaviour = behaviours[i];
			if (behaviour.GetType().ToString() == typeName)
			{
				targetBehaviour = behaviour;
				break;
			}
		}
		if (targetBehaviour == null)
			throw new Exception("Unable to find target behaviour: key " + key + " typename " + typeName);
	}

	public TimelineEventBehaviour() { } // 0600000d
}
