using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
	public struct UIContainerPromptParameters : IUIContainerParameters
	{
		public Strings Text;

		public AssetReferenceAtlasedSprite Image;

		public Transform Target;

		public bool TapToContinue;

		public float MinimumContinueSeconds;

		public float AutomaticContinueSeconds;

		public Action<UIContainerPrompt, int> CallbackOnComplete;

		public IReadOnlyList<PromptDefinition.PromptDefinitionChoice> PromptChoices;

		public IReadOnlyList<PromptDefinition.PromptDefinitionDisplayInput> DisplayGameInputs;

		public GameObject DisplayGameInputsSeparator;

		public PromptSlot.PromptSlotDirection SlotDirection;
	}
}
