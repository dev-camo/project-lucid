using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class UIContainerPromptDisplayInput : UIContainer
	{
		public enum DecoratorType
		{
			None = 0,
			Left = 1,
			Right = 2,
			Up = 3,
			Down = 4
		}

		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		private class DecoratorTypeEqualityComparer : IEqualityComparer<DecoratorType>
		{
			public bool Equals(DecoratorType a, DecoratorType b)
			{
				return a == b;
			}

			public int GetHashCode(DecoratorType a)
			{
				return (int)a;
			}
		}

		private static readonly DecoratorTypeEqualityComparer s_decoratorTypeComparer = new DecoratorTypeEqualityComparer();

		[SerializeField]
		private GameInputGlyphView m_glyphView;

		[SerializeField]
		private RawImage m_decoratorIconImage;

		[SerializeField]
		private InputType[] m_decoratorInputTypes;

		[SerializeField]
		private SerializableDictionary<DecoratorType, Texture2D> m_decoratorLookup = new SerializableDictionary<DecoratorType, Texture2D>(s_decoratorTypeComparer);

		private DecoratorType m_decorator;

		public override void Setup(IUIContainerParameters parameters)
		{
			base.Setup(parameters);
			Setup(parameters.GetAs<UIContainerPromptDisplayInputParameters>());
		}

		public void Setup(UIContainerPromptDisplayInputParameters parameters)
		{
			m_decorator = parameters.Decorator;
			if (m_glyphView != null)
				m_glyphView.SetInput(parameters.GameInput, parameters.GameAction, parameters.GameActionCombo);
			if (m_decoratorIconImage != null)
			{
				ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
				ControlMapping.OnUpdateLastInputType += OnInputTypeChanged;
				OnInputTypeChanged(ControlMapping.LastInputType);
			}
		}

		private void OnInputTypeChanged(InputType inputType)
		{
			Texture2D texture;
			if (m_decorator != DecoratorType.None && m_decoratorInputTypes.Contains(inputType) && m_decoratorLookup.TryGetValue(m_decorator, out texture))
			{
				m_decoratorIconImage.enabled = true;
				m_decoratorIconImage.texture = texture;
			}
			else
				m_decoratorIconImage.enabled = false;
		}

		private void OnDestroy()
		{
			ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
		}
	}
}
