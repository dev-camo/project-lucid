using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;
using UnityEngine.UI;

// Original HLUnityCore.Runtime 02000002; bodies translated from the 1.10.1 x86-64 binary.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public static class CanvasScalerExtensions
{
	// 06000001 (1.10.1 RVA 0x1A98660)
	public static float GetInvertedScaleFactor(this CanvasScaler canvasScaler)
	{
		if (canvasScaler == null)
		{
			return -1f;
		}
		Vector2 referenceResolution = canvasScaler.referenceResolution;
		if (referenceResolution.y <= 0f || referenceResolution.x <= 0f)
		{
			return -1f;
		}
		switch (canvasScaler.screenMatchMode)
		{
		case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
		{
			float width = Mathf.Pow(2f, referenceResolution.x / Screen.width);
			float height = Mathf.Pow(2f, canvasScaler.referenceResolution.y / Screen.height);
			return Mathf.Log(Mathf.Lerp(width, height, canvasScaler.matchWidthOrHeight), 2f);
		}
		case CanvasScaler.ScreenMatchMode.Expand:
			return Mathf.Max(referenceResolution.y / Screen.height, canvasScaler.referenceResolution.x / Screen.width);
		case CanvasScaler.ScreenMatchMode.Shrink:
			return Mathf.Min(referenceResolution.y / Screen.height, canvasScaler.referenceResolution.x / Screen.width);
		default:
			HLOutput.LogError(string.Format("Screen match mode {0} is not supported yet.", canvasScaler.screenMatchMode));
			return -1f;
		}
	}
}
