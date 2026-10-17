// Original HLUnityUI.Runtime Hardlight.FullScreenTransitionManager 02000023, from the x86-64 bodies
// (0x060000f0..0x060000f7, 0x1B40350..0x1B40918) and metadata.
using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Canvas))]
    public sealed class FullScreenTransitionManager : MonoSingleton<FullScreenTransitionManager>
    {
        [SerializeField]
        private FullScreenTransitionController[] transitionsController;
        [SerializeField]
        private Canvas m_transitionCanvas;
        private Dictionary<FullScreenTransitionType, FullScreenTransitionController> m_transitionControllers = new Dictionary<FullScreenTransitionType, FullScreenTransitionController>();

        // The first controller of each type is kept; later ones of the same type are ignored.
        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < transitionsController.Length; i++)
            {
                FullScreenTransitionController controller = transitionsController[i];
                if (!m_transitionControllers.ContainsKey(controller.Type))
                    m_transitionControllers.Add(controller.Type, controller);
            }
        }

        private void Start()
        {
            if (m_transitionCanvas != null)
                return;

            HLOutput.LogError("FullScreenTransitionManager::Start - transition canvas is not set, please serialize a canvas and save the project.");
            m_transitionCanvas = GetComponent<Canvas>();
        }

        public bool IsTransitionActive(FullScreenTransitionType type)
        {
            FullScreenTransitionController controller;
            return m_transitionControllers.TryGetValue(type, out controller) && controller.CurrentState != FullScreenTransitionController.State.HeldOut;
        }

        // The canvas sorting is applied before the lookup, so it changes even when no controller of
        // that type is registered.
        public bool StartTransition(FullScreenTransitionType type, string sortingLayer = "Default", int sortingOrder = 0, float overrideDuration = 0f, Action midpointCallback = null, Action completionCallback = null)
        {
            SetSortingLayerAndOrder(sortingLayer, sortingOrder);
            FullScreenTransitionController controller;
            if (m_transitionControllers.TryGetValue(type, out controller))
                return controller.StartTransition(overrideDuration, midpointCallback, completionCallback);
            return false;
        }

        public bool ContinueTransition(FullScreenTransitionType type, float overrideDuration = 0f, Action completionCallback = null)
        {
            FullScreenTransitionController controller;
            if (m_transitionControllers.TryGetValue(type, out controller))
                return controller.ContinueTransition(overrideDuration, completionCallback);
            return false;
        }

        public void ResetTransitions()
        {
            foreach (KeyValuePair<FullScreenTransitionType, FullScreenTransitionController> transition in m_transitionControllers)
                transition.Value.ResetTransition();
        }

        // A sorting order of 0 never overrides the canvas's current order.
        private void SetSortingLayerAndOrder(string sortingLayer = "Default", int sortingOrder = 0)
        {
            if (sortingLayer != m_transitionCanvas.sortingLayerName)
            {
                m_transitionCanvas.sortingLayerID = SortingLayer.NameToID(sortingLayer);
                m_transitionCanvas.sortingLayerName = sortingLayer;
            }

            if (m_transitionCanvas.sortingOrder != sortingOrder && sortingOrder != 0)
                m_transitionCanvas.sortingOrder = sortingOrder;
        }
    }
}
