using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000c0 (TypeDefIndex 12019), methods
	// 0x0600053f..0x0600054d; bodies translated from the 1.10.1 x86-64 binary.
	// The nested settings class is declared first: with it as member 0 the
	// iterator's member ordinal is 9, giving the original <WaitForRequiredSystems>d__9.
	// No shipped asset in the reference export references this script
	// (original script GUID d67cf81865b36bf66bff0147c0681a37), so the .meta
	// GUID is fresh and no binding rule is added.
	// Attribute order as in dump.cs TypeDefIndex 12019.
	[Il2CppSetOption(Option.NullChecks, false)]
	[RequireComponent(typeof(RectTransformModifier))]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class OrientationSpecificRectTransformModifier : MonoBehaviour
	{
		[Serializable]
		protected class OrientationSpecificRectSettings
		{
			public enum SettingType
			{
				Height = 0,
				Width = 1,
				PosX = 2,
				PosY = 3,
				XAnchorMin = 4,
				XAnchorMax = 5,
				YAnchorMin = 6,
				YAnchorMax = 7,
				LeftOffset = 8,
				RightOffset = 9,
				BotOffset = 10,
				TopOffset = 11,
				PivotX = 12,
				PivotY = 13
			}

			public SettingType Setting;
			public float SettingValue;
		}

		[SerializeField]
		protected RectTransformModifier m_rectTransformModifier;
		[SerializeField]
		private OrientationSpecificRectSettings[] m_landscapeSettings;
		[SerializeField]
		protected OrientationSpecificRectSettings[] m_portraitSettings;
		protected List<OrientationSpecificRectSettings> m_desiredSetting = new List<OrientationSpecificRectSettings>();
		private UIOrientation m_UIOrientation;

		// 0x0600053f: Unity equality, so a destroyed inspector reference is replaced too.
		public void Awake()
		{
			if (m_rectTransformModifier == null)
			{
				m_rectTransformModifier = GetComponent<RectTransformModifier>();
			}
		}

		// 0x06000540: subscribes the virtual slot-4 refresh with a plain FastAction +=
		// (duplicates allowed); the coroutine starts whether or not the singleton is live.
		private void OnEnable()
		{
			if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange += InitialiseAndSetValues;
			}
			StartCoroutine(WaitForRequiredSystems());
		}

		// 0x06000541
		private void OnDisable()
		{
			if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange -= InitialiseAndSetValues;
			}
		}

		// 0x06000542 / MoveNext 0x0600054a: polls the ProcessManager registry row,
		// not the singleton, before the first refresh.
		private IEnumerator WaitForRequiredSystems()
		{
			while (ProcessManager.IsSystemNull<ScreenManager>())
			{
				yield return null;
			}
			InitialiseAndSetValues();
		}

		// 0x06000543
		protected virtual void InitialiseAndSetValues()
		{
			InitialiseValues();
			SetAllValues();
		}

		// 0x06000544: Instance is read without a null guard. Only Landscape selects the
		// landscape array, and the desired list is appended to, never cleared.
		protected virtual void InitialiseValues()
		{
			m_UIOrientation = MonoSingleton<ScreenManager>.Instance.Orientation;
			m_desiredSetting.AddRange(m_UIOrientation == UIOrientation.Landscape ? m_landscapeSettings : m_portraitSettings);
		}

		// 0x06000545: the list is re-indexed for every read, as in the original.
		protected void SetAllValues()
		{
			for (int i = 0; i < m_desiredSetting.Count; i++)
			{
				switch (m_desiredSetting[i].Setting)
				{
					case OrientationSpecificRectSettings.SettingType.Height:
						m_rectTransformModifier.Height = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.Width:
						m_rectTransformModifier.Width = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.PosX:
						m_rectTransformModifier.XOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.PosY:
						m_rectTransformModifier.YOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.XAnchorMin:
						m_rectTransformModifier.XAnchorMin = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.XAnchorMax:
						m_rectTransformModifier.XAnchorMax = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.YAnchorMin:
						m_rectTransformModifier.YAnchorMin = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.YAnchorMax:
						m_rectTransformModifier.YAnchorMax = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.LeftOffset:
						m_rectTransformModifier.LeftOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.RightOffset:
						m_rectTransformModifier.RightOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.BotOffset:
						m_rectTransformModifier.BotOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.TopOffset:
						m_rectTransformModifier.TopOffset = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.PivotX:
						m_rectTransformModifier.PivotX = m_desiredSetting[i].SettingValue;
						break;
					case OrientationSpecificRectSettings.SettingType.PivotY:
						m_rectTransformModifier.PivotY = m_desiredSetting[i].SettingValue;
						break;
					default:
						HLOutput.LogError("Enum extended, add condition to script");
						break;
				}
			}
		}
	}
}
