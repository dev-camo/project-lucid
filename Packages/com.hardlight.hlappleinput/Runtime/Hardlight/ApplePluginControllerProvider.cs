using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000009. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public sealed class ApplePluginControllerProvider : Hardlight.BaseControllerProvider, Hardlight.IControllerProvider<Apple.GameController.Controller.GCController>, Hardlight.IBaseControllerProvider
    {
        private readonly System.Collections.Generic.List<Apple.GameController.Controller.GCController> m_applePluginControllers = new List<GCController>();
        private readonly System.Collections.Generic.List<System.String> m_applePluginControllerNames = new List<string>();
        private readonly System.Collections.Generic.HashSet<System.String> m_applePluginControllerUniqueIds = new HashSet<string>();

        // Original 0x06000013; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerProvider(System.Single controllerConnectionPollingRateInSeconds) : base(controllerConnectionPollingRateInSeconds)
        {
        }

        // Original 0x06000014; complete ARM64 and x86-64 bodies retained.
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

        // Original 0x06000015; complete ARM64 and x86-64 bodies retained.
        public override void Shutdown()
        {
            base.Shutdown();
            GCControllerService.ControllerConnected -= OnControllerConnected;
            GCControllerService.ControllerDisconnected -= OnControllerDisconnected;
            m_applePluginControllers.Clear();
            m_applePluginControllerNames.Clear();
            m_applePluginControllerUniqueIds.Clear();
        }

        // Original 0x06000016; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IReadOnlyList<Apple.GameController.Controller.GCController> GetControllers()
        {
            return m_applePluginControllers;
        }

        // Original 0x06000017; complete ARM64 and x86-64 bodies retained.
        public override System.Collections.Generic.IReadOnlyList<System.String> GetControllerNames()
        {
            return m_applePluginControllerNames;
        }

        // Original 0x06000018; complete ARM64 and x86-64 bodies retained.
        private void OnControllerConnected(System.Object sender, Apple.GameController.Controller.ControllerConnectedEventArgs args)
        {
            GCController controller = args.Controller;
            string uniqueId = controller.Handle.UniqueId;
            string vendorName = controller.Handle.VendorName;
            if (m_applePluginControllerUniqueIds.Contains(uniqueId))
            {
                HLOutput.LogError(string.Concat(new string[] { "Unique Id '", uniqueId, "' and vendor name '", vendorName, "' for controller already exists." }), null);
                return;
            }
            m_applePluginControllers.Add(controller);
            m_applePluginControllerNames.Add(vendorName);
            m_applePluginControllerUniqueIds.Add(uniqueId);
        }

        // Original 0x06000019; complete ARM64 and x86-64 bodies retained.
        private void OnControllerDisconnected(System.Object sender, Apple.GameController.Controller.ControllerConnectedEventArgs args)
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
            HLOutput.LogError(string.Concat(new string[] { "Unique Id '", uniqueId, "' and vendor name '", vendorName, "' for controller does not exist." }), null);
        }

    }
}
