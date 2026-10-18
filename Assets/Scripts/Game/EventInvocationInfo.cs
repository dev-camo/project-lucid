using System;
using System.Reflection;
using Hardlight;
using UnityEngine;

// Original Game.Runtime 02000002; bodies translated from the 1.10.1 x86-64 binary.
// The type carries no Il2CppSetOption attributes, so its bodies keep IL2CPP null checks.
public class EventInvocationInfo
{
	public Behaviour TargetBehaviour;
	public MethodInfo MethodInfo;
	public static Type[] SupportedTypes = { typeof(string), typeof(float), typeof(int), typeof(bool) };
	public string Key;

	// 06000001 (1.10.1 RVA 0x505580): stores run Key, MethodInfo, TargetBehaviour.
	public EventInvocationInfo(string key, Behaviour targetBehaviour, MethodInfo methodInfo)
	{
		Key = key;
		MethodInfo = methodInfo;
		TargetBehaviour = targetBehaviour;
	}

	// 06000002 (1.10.1 RVA 0x5055E0)
	public void Invoke(object value)
	{
		if (MethodInfo != null)
			MethodInfo.Invoke(TargetBehaviour, new object[] { value });
	}

	// 06000003 (1.10.1 RVA 0x5056C0): the first parameter is read before the null test,
	// so a missing method faults instead of being skipped.
	public void InvokeEnum(int value)
	{
		Invoke(Enum.ToObject(MethodInfo.GetParameters()[0].ParameterType, value));
	}

	// 06000004 (1.10.1 RVA 0x505820)
	public void InvokeNoArgs()
	{
		if (MethodInfo != null)
			MethodInfo.Invoke(TargetBehaviour, null);
	}

	// 06000005 (1.10.1 RVA 0x505870): an enum parameter takes the text as an int. Only the
	// supported types convert, string passing the text itself; anything else is ignored.
	// Every exception, including a missing method or parameter, is swallowed.
	public void Invoke(bool isSingleArg, string value)
	{
		try
		{
			if (isSingleArg)
			{
				Type parameterType = MethodInfo.GetParameters()[0].ParameterType;
				if (parameterType.IsEnum)
				{
					Invoke(value.ConvertToType<int>());
				}
				else if (SupportedTypes.Contains(parameterType))
				{
					if (parameterType == typeof(string))
						Invoke(value);
					else if (parameterType == typeof(int))
						Invoke(value.ConvertToType<int>());
					else if (parameterType == typeof(float))
						Invoke(value.ConvertToType<float>());
					else if (parameterType == typeof(bool))
						Invoke(value.ConvertToType<bool>());
				}
			}
			else
			{
				InvokeNoArgs();
			}
		}
		catch (Exception)
		{
		}
	}
}
