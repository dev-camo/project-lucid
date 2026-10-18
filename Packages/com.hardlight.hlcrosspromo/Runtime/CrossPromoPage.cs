using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000021; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromoPage : CrossPromoPageBase
	{
		[SerializeField]
		public Image Image;

		[SerializeField]
		public Text ButtonTextField;

		private bool m_isReady;

		// 06000103 (1.10.1 RVA 0x1A18950)
		public override void SetSprite(Sprite sprite)
		{
			Image.sprite = sprite;
		}

		// 06000104 (1.10.1 RVA 0x1A18960)
		public override Sprite GetSprite()
		{
			return Image.sprite;
		}

		// 06000105 (1.10.1 RVA 0x1A18980)
		public override void SetText(string text)
		{
			ButtonTextField.text = text;
		}

		// 06000106 (1.10.1 RVA 0x1A189A0)
		public override void SetReadyState(bool isReady)
		{
			m_isReady = isReady;
		}

		// 06000107 (1.10.1 RVA 0x1A189B0)
		public override bool GetReadyState()
		{
			return m_isReady;
		}

		// 06000108 (1.10.1 RVA 0x1A189C0)
		public CrossPromoPage()
		{
		}
	}
}
