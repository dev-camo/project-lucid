using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000009. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ApplePluginControllerProvider
        : BaseControllerProvider, IControllerProvider<GCController>, IBaseControllerProvider
    {
        private readonly List<GCController> m_applePluginControllers = new List<GCController>();
        private readonly List<string> m_applePluginControllerNames = new List<string>();
        private readonly HashSet<string> m_applePluginControllerUniqueIds = new HashSet<string>();

        // Original 0x06000013
        public ApplePluginControllerProvider(float controllerConnectionPollingRateInSeconds)
            : base(controllerConnectionPollingRateInSeconds)
        {
        }

        // Original 0x06000014
        public override void Initialise()
        {
            base.Initialise();
            GCControllerService.Initialize();
            foreach (GCController controller in GCControllerService.GetConnectedControllers())
            {
                m_applePluginControllers.Add(controller);
                m_applePluginControllerNames.Add(controller.Handle.VendorName);
                m_applePluginControllerUniqueIds.Add(controller.Handle.UniqueId);
            }
            GCControllerService.ControllerConnected += OnControllerConnected;
            GCControllerService.ControllerDisconnected += OnControllerDisconnected;
        }

        // Original 0x06000015
        public override void Shutdown()
        {
            base.Shutdown();
            GCControllerService.ControllerConnected -= OnControllerConnected;
            GCControllerService.ControllerDisconnected -= OnControllerDisconnected;
            m_applePluginControllers.Clear();
            m_applePluginControllerNames.Clear();
            m_applePluginControllerUniqueIds.Clear();
        }

        // Original 0x06000016
        public IReadOnlyList<GCController> GetControllers()
        {
            return m_applePluginControllers;
        }

        // Original 0x06000017
        public override IReadOnlyList<string> GetControllerNames()
        {
            return m_applePluginControllerNames;
        }

        // Original 0x06000018
        private void OnControllerConnected(object sender, ControllerConnectedEventArgs args)
        {
            GCController controller = args.Controller;
            string uniqueId = controller.Handle.UniqueId;
            string vendorName = controller.Handle.VendorName;
            if (m_applePluginControllerUniqueIds.Contains(uniqueId))
            {
                HLOutput.LogError(string.Concat(new string[]
                {
                    "Unique Id '", uniqueId, "' and vendor name '", vendorName, "' for controller already exists."
                }), null);
                return;
            }
            m_applePluginControllers.Add(controller);
            m_applePluginControllerNames.Add(vendorName);
            m_applePluginControllerUniqueIds.Add(uniqueId);
        }

        // Original 0x06000019
        private void OnControllerDisconnected(object sender, ControllerConnectedEventArgs args)
        {
            GCController controller = args.Controller;
            string uniqueId = controller.Handle.UniqueId;
            string vendorName = controller.Handle.VendorName;
            if (m_applePluginControllerUniqueIds.Contains(uniqueId))
            {
                m_applePluginControllers.Remove(controller);
                m_applePluginControllerNames.Remove(vendorName);
                m_applePluginControllerUniqueIds.Remove(uniqueId);
                return;
            }
            HLOutput.LogError(string.Concat(new string[]
            {
                "Unique Id '", uniqueId, "' and vendor name '", vendorName, "' for controller does not exist."
            }), null);
        }
    }
}
