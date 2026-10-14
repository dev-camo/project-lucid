using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000056; bodies translated from the 1.10.1 x86-64 binary.
	internal static class HLProfilerUtils
	{
		public const string NotActivePrefix = "(Not active) - ";
		public const int SampleBufferSizeMax = 60;
		private const long BytesPerKibibyte = 1024;
		private const long BytesPerMebibyte = 1048576;
		private const long BytesPerGibibyte = 1073741824;

		// 06000231
		internal static string FormatCount(long value)
		{
			if (value >= 100000000)
				return (value / 1000000f).ToString("0.#M");
			if (value >= 1000000)
				return (value / 1000000f).ToString("0.##M");
			if (value >= 100000)
				return (value / 1000f).ToString("0.#k");
			if (value >= 10000)
				return (value / 1000f).ToString("0.##k");
			return value.ToString("#,0");
		}

		// 06000232
		internal static string FormatBytes(long bytes)
		{
			if (bytes >= BytesPerGibibyte)
				return ((float)bytes / BytesPerGibibyte).ToString("#.0 GiB");
			if (bytes >= BytesPerMebibyte)
				return ((float)bytes / BytesPerMebibyte).ToString("#.0 MiB");
			if (bytes >= BytesPerKibibyte)
				return ((float)bytes / BytesPerKibibyte).ToString("#.0 KiB");
			return bytes.ToString("# B");
		}

		// 06000233
		public static string FormatMilliseconds(float milliseconds)
		{
			return string.Format("{0:0.0} ms", milliseconds);
		}

		// 06000234
		internal static void GUIBeginSliderWithButtonsReskin(float height, float width)
		{
			float sliderHeight = height * 0.025f;
			float buttonWidth = width * 0.1f;
			GUI.skin.horizontalSlider.fixedHeight = sliderHeight;
			GUI.skin.horizontalSliderThumb.fixedWidth = buttonWidth;
			GUI.skin.horizontalSliderThumb.fixedHeight = sliderHeight;
			GUI.skin.button.fixedHeight = sliderHeight;
			GUI.skin.button.fixedWidth = buttonWidth;
		}

		// 06000235. The original restores the button width from the button height entry.
		internal static void GUIEndSliderWithButtonsReskin(IReadOnlyDictionary<HLProfilerConsole.GUISkinModifierTarget, float> guiSkinValues)
		{
			GUI.skin.horizontalSlider.fixedHeight = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderFixedHeight];
			GUI.skin.horizontalSliderThumb.fixedHeight = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderThumbFixedHeight];
			GUI.skin.horizontalSliderThumb.fixedWidth = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderThumbFixedWidth];
			GUI.skin.button.fixedHeight = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.ButtonFixedHeight];
			GUI.skin.button.fixedWidth = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.ButtonFixedHeight];
		}

		// 06000236
		private static void GUIRightAlignedLabel(string text)
		{
			TextAnchor alignment = GUI.skin.label.alignment;
			GUI.skin.label.alignment = TextAnchor.UpperRight;
			GUILayout.Label(text);
			GUI.skin.label.alignment = alignment;
		}

		// 06000237
		internal static float GUILabelSliderWithButton(string labelName, float value, float minValue, float maxValue, float incrementalValue, string valueColor = "white")
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(labelName ?? "");
			GUIRightAlignedLabel("[<color=" + valueColor + ">" + value.ToString("0.#") + "</color>]");
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("-"))
				value -= incrementalValue;
			value = GUILayout.HorizontalSlider(value, minValue, maxValue);
			if (GUILayout.Button("+"))
				value += incrementalValue;
			GUILayout.EndHorizontal();
			return Mathf.Clamp(value, minValue, maxValue);
		}

		// 06000238. Both thumb entries save the slider height, as in the original.
		internal static void GUIBeginGlobalReskin(IDictionary<HLProfilerConsole.GUISkinModifierTarget, float> guiSkinValues, int labelFontSize, GUISkin optionalGuiSkin = null)
		{
			if (optionalGuiSkin)
				GUI.skin = optionalGuiSkin;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.LabelRichText] = GUI.skin.label.richText ? 1f : 0f;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.LabelFontSize] = GUI.skin.label.fontSize;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderFixedHeight] = GUI.skin.horizontalSlider.fixedHeight;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderThumbFixedHeight] = GUI.skin.horizontalSlider.fixedHeight;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.HorizontalSliderThumbFixedWidth] = GUI.skin.horizontalSlider.fixedHeight;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.ButtonFixedHeight] = GUI.skin.button.fixedHeight;
			guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.ButtonFixedWidth] = GUI.skin.button.fixedWidth;
			GUI.skin.label.richText = true;
			GUI.skin.label.fontSize = labelFontSize;
		}

		// 06000239
		internal static void GUIEndGlobalReskin(Dictionary<HLProfilerConsole.GUISkinModifierTarget, float> guiSkinValues)
		{
			GUI.skin.label.richText = guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.LabelRichText] != 0f;
			GUI.skin.label.fontSize = (int)guiSkinValues[HLProfilerConsole.GUISkinModifierTarget.LabelFontSize];
			GUIEndSliderWithButtonsReskin(guiSkinValues);
		}

		// 0600023a
		public static IReadOnlyList<HLProfilerDataReference> GetAllProfilerData(HLProfilerGroup group)
		{
			var dataList = new List<HLProfilerDataReference>();
			for (int i = 0; i < group.LayoutList.Count; i++)
			{
				HLProfilerLayout layout = group.LayoutList[i];
				for (int j = 0; j < layout.DataList.Count; j++)
					dataList.Add(layout.DataList[j]);
			}
			return dataList;
		}
	}
}
