using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight.Localisation;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class UIContainerPrompt : UIContainer
	{
		[Serializable]
		public sealed class PromptDirectionalElements
		{
			[SerializeField]
			private PromptSlot.PromptSlotDirection m_direction;

			[SerializeField]
			private GameObject[] m_objectsToEnable;

			public PromptSlot.PromptSlotDirection Direction => m_direction;

			public void SetEnabled(bool value)
			{
				foreach (GameObject objectToEnable in m_objectsToEnable)
					objectToEnable.SetActive(value);
		}
		}

		public readonly Bindable<string> Text = new Bindable<string>();

		public readonly Bindable<bool> HasImage = new Bindable<bool>();

		public readonly Bindable<Sprite> Image = new Bindable<Sprite>();

		public readonly Bindable<bool> TapToContinue = new Bindable<bool>();

		public readonly Bindable<bool> HasTarget = new Bindable<bool>();

		public readonly Bindable<Vector3> TargetScreenPosition = new Bindable<Vector3>();

		public readonly Bindable<bool> HasChoices = new Bindable<bool>();

		public readonly Bindable<GameInput> TapToContinueGameInput = new Bindable<GameInput>();

		[SerializeField]
		[Tooltip("Transform to spawn prompt choice prefabs into. If missing, choices will not spawn.")]
		private Transform m_choicesSpawnParent;

		[SerializeField]
		private GameInput m_tapToContinueGameInput = GameInput.PromptContinue;

		[Tooltip("Transform to spawn prompt input display prefabs into. If missing, these will not spawn.")]
		[SerializeField]
		private Transform m_displayGameInputsSpawnParent;

		[SerializeField]
		private PromptDirectionalElements[] m_directionalElements;

		private bool m_complete;

		private Action<UIContainerPrompt, int> m_callbackOnComplete;

		private Transform m_targetTransform;

		private Camera m_camera;

		private float m_timeToContinue;

		private WaitForSeconds m_autoContinueWaitForSeconds;

		private Coroutine m_autoContinueCoroutine;

		private UIContainerPromptParameters m_promptParameters;

		private readonly List<UIContainerPromptChoice> m_promptChoices = new List<UIContainerPromptChoice>();

		private readonly List<GameObject> m_displayGameInputs = new List<GameObject>();

		private AsyncOperationHandle<Sprite> m_assetLoadHandle;

		private readonly SystemRef<UIRuntimeConfiguration> m_uiRuntimeConfigurationRef = ProcessManager.GetSystemRef<UIRuntimeConfiguration>();

		public override void Setup(IUIContainerParameters parameters)
		{
            base.Setup(parameters);
            m_promptParameters = parameters.GetAs<UIContainerPromptParameters>();
            Text.Value = StringTable.GetString(m_promptParameters.Text);
            TapToContinue.Value = m_promptParameters.TapToContinue;
            TapToContinueGameInput.Value = m_tapToContinueGameInput;
            m_callbackOnComplete = m_promptParameters.CallbackOnComplete;
            m_targetTransform = m_promptParameters.Target;
            m_complete = false;
            CinemachineCameraManager cameraManager;
            m_camera = ProcessManager.GetSystemRef<CinemachineCameraManager>().TryGet(out cameraManager) ? cameraManager.MainCamera : null;
            m_timeToContinue = m_promptParameters.MinimumContinueSeconds;
            if (m_targetTransform == null)
            {
                HasTarget.Value = false;
                TargetScreenPosition.Value = Vector3.zero;
            }
            else
            {
                HasTarget.Value = true;
                UpdateTargetScreenPosition();
            }
            CleanUpChoices();
            if (m_promptParameters.PromptChoices != null && m_choicesSpawnParent != null)
            {
                HasChoices.Value = m_promptParameters.PromptChoices.Count > 0;
                int choiceIndex = 0;
                foreach (PromptDefinition.PromptDefinitionChoice definition in m_promptParameters.PromptChoices)
                {
                    UIContainerPromptChoiceParameters choiceParameters = new UIContainerPromptChoiceParameters
                    {
                        Text = definition.Text,
                        ChoiceIndex = choiceIndex,
                        IsDefaultChoice = definition.IsDefaultChoice,
                        GameInput = definition.GameInput,
                        GameAction = definition.GameAction,
                        GameActionCombo = definition.GameActionCombo,
                        MinimumTime = m_timeToContinue
                    };
                    UIContainerPromptChoice choice = Instantiate(definition.PromptChoicePrefab, m_choicesSpawnParent);
                    choice.Setup(choiceParameters);
                    choice.OnSelected += OnPromptChoiceSelected;
                    m_promptChoices.Add(choice);
                    ++choiceIndex;
                }
            }
            else
                HasChoices.Value = false;
            CleanUpDisplayGameInputs();
            if (m_promptParameters.DisplayGameInputs != null)
            {
                int count = m_promptParameters.DisplayGameInputs.Count;
                if (count > 0 && m_displayGameInputsSpawnParent != null)
                {
                UIInputType inputType = m_uiRuntimeConfigurationRef.Get().InputBridge.GetLastInputType();
                for (int i = 0; i < count; ++i)
                {
                    PromptDefinition.PromptDefinitionDisplayInput definition = m_promptParameters.DisplayGameInputs[i];
                    UIContainerPromptDisplayInputParameters displayParameters = new UIContainerPromptDisplayInputParameters
                    {
                        GameInput = definition.GetGameInput(inputType),
                        GameAction = definition.GetGameAction(inputType),
                        GameActionCombo = definition.GetGameActionCombo(),
                        Decorator = definition.Decorator
                    };
                    UIContainerPromptDisplayInput display = Instantiate(definition.PromptDisplayInputPrefab, m_displayGameInputsSpawnParent);
                    display.Setup(displayParameters);
                    m_displayGameInputs.Add(display.gameObject);
                    if (i < count - 1)
                        m_displayGameInputs.Add(Instantiate(m_promptParameters.DisplayGameInputsSeparator, m_displayGameInputsSpawnParent));
                }
            }
            }
            foreach (PromptDirectionalElements directionalElements in m_directionalElements)
                directionalElements.SetEnabled(directionalElements.Direction == m_promptParameters.SlotDirection);
            bool hasImage = m_promptParameters.Image.RuntimeKeyIsValid();
            HasImage.Value = hasImage;
            AddressableManager.ReleaseHandleInManager(m_assetLoadHandle);
            if (hasImage)
            {
                gameObject.SetActive(false);
                m_assetLoadHandle = ProcessManager.GetSystem<AddressableManager>().LoadAsset(m_promptParameters.Image, OnSpriteLoaded);
            }
            else
            {
                Image.Value = null;
                Ready();
            }
		}

		private void Ready()
		{
            if (gameObject == null) return;
            gameObject.SetActive(true);
            if (m_promptParameters.AutomaticContinueSeconds > 0f)
            {
                m_autoContinueWaitForSeconds = new WaitForSeconds(m_promptParameters.AutomaticContinueSeconds);
                m_autoContinueCoroutine = StartCoroutine(AutoContinueCoroutine());
            }
		}

		private void OnSpriteLoaded(Sprite sprite)
		{
            Image.Value = sprite;
            Ready();
		}

		private void OnPromptChoiceSelected(int choiceIndex)
		{
            Finish(choiceIndex);
		}

		private void CleanUpChoices()
		{
            foreach (UIContainerPromptChoice choice in m_promptChoices)
            {
                choice.OnSelected -= OnPromptChoiceSelected;
                Destroy(choice.gameObject);
            }
            m_promptChoices.Clear();
		}

		private void CleanUpDisplayGameInputs()
		{
            foreach (GameObject display in m_displayGameInputs)
                Destroy(display);
            m_displayGameInputs.Clear();
		}

		private void Update()
		{
            if (m_timeToContinue > 0f)
                m_timeToContinue = Mathf.Max(m_timeToContinue - Time.deltaTime, 0f);
            if (HasTarget.Value)
                UpdateTargetScreenPosition();
		}

		private void UpdateTargetScreenPosition()
		{
            if (m_camera == null) return;
            TargetScreenPosition.Value = m_camera.WorldToScreenPoint(m_targetTransform.position);
		}

		public void OnTap()
		{
            if (TapToContinue.Value) TryContinue();
		}

		private bool TryContinue()
		{
            if (m_complete || m_timeToContinue > 0f) return false;
            Finish(-1);
            return false;
		}

		private void Finish(int choiceIndex)
		{
            if (m_complete) return;
            m_complete = true;
            m_callbackOnComplete?.Invoke(this, choiceIndex);
            if (m_autoContinueCoroutine != null)
            {
                StopCoroutine(m_autoContinueCoroutine);
                m_autoContinueCoroutine = null;
            }
            CleanUpChoices();
            CleanUpDisplayGameInputs();
		}

		private IEnumerator AutoContinueCoroutine()
		{
            yield return m_autoContinueWaitForSeconds;
            TryContinue();
		}

		private void OnDisable()
		{
            AddressableManager.ReleaseHandleInManager(m_assetLoadHandle);
		}
	}
}
