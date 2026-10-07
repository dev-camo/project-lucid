using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Hardlight;
using Hardlight.Utils;
using HLCloud.Plugin.iOS;
using UnityEngine;

namespace HLCloud.Plugin
{
    /// <summary>
    /// Reconstructed original managed Mac call graph. The external declarations
    /// retain known signatures and PreserveSig, but their original import module
    /// was not preserved by supplied metadata. No invented native binding or
    /// surrogate implementation is provided. Keep this route unselected in the
    /// offline port until its genuine platform binding is restored.
    /// </summary>
    public class Cloud_MacOS : Cloud
    {
        public delegate void UnityCallbackDelegate(IntPtr objectName, IntPtr commandName, IntPtr commandData);

        private readonly HashSet<string> m_allCloudKeys = new HashSet<string>();
        public static bool UseFindGameObject { get; set; }
        private float m_synchronisationCooldownSeconds;
        private bool m_resynchronisationRequested;
        private Coroutine m_synchronisationCoroutine;
        private readonly WaitForSeconds m_waitForCooldown;
        private readonly SystemRef<CoroutineUtils> m_coroutineUtils = ProcessManager.GetSystemRef<CoroutineUtils>(null, true);

        // Original methods carry PinvokeImpl and PreserveSig. The library-name
        // mapping is unresolved; adding DllImport with a guessed module would
        // falsely claim that declaration was recovered. These extern signatures
        // lack a recovered PinvokeImpl module and remain unresolved to the CLR.
        // CS0626 is expected until the real import module can be recovered.
#pragma warning disable 0626
        [PreserveSig] public static extern void ConnectCallback(UnityCallbackDelegate callbackMethod);
        [PreserveSig] private static extern void HLCloud_InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID);
        [PreserveSig] private static extern bool HLCloud_Synchronize();
        [PreserveSig] private static extern string HLCloud_RetrieveAllCloudKeys(string allCloudKeysSeparator);
        [PreserveSig] private static extern string HLCloud_StringForKey(string key);
        [PreserveSig] private static extern void HLCloud_SetStringForKey(string value, string key);
        [PreserveSig] private static extern float HLCloud_FloatForKey(string key);
        [PreserveSig] private static extern void HLCloud_SetFloatForKey(float value, string key);
        [PreserveSig] private static extern int HLCloud_IntForKey(string key);
        [PreserveSig] private static extern void HLCloud_SetIntForKey(int value, string key);
        [PreserveSig] private static extern bool HLCloud_BoolForKey(string key);
        [PreserveSig] private static extern void HLCloud_SetBoolForKey(bool value, string key);
        [PreserveSig] private static extern void HLCloud_RemoveForKey(string key);

#pragma warning restore 0626

        public Cloud_MacOS()
        {
            m_waitForCooldown = new WaitForSeconds(m_synchronisationCooldownSeconds);
        }

        public override void InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID)
        {
            HLCloud_InitializeWithGameObjectName(gameObjectName, useGamePlayerID);
            ConnectCallback(new UnityCallbackDelegate(OnConnectCallback));
        }

        [MonoPInvokeCallback(typeof(UnityCallbackDelegate))]
        private static void OnConnectCallback(IntPtr objectName, IntPtr commandName, IntPtr commandData)
        {
            if (UseFindGameObject)
            {
                string objectNameString = Marshal.PtrToStringAuto(objectName);
                string commandNameString = Marshal.PtrToStringAuto(commandName);
                string commandDataString = Marshal.PtrToStringAuto(commandData);
                GameObject receiver = GameObject.Find(objectNameString);
                if (receiver != null) receiver.SendMessage(commandNameString, commandDataString);
            }
            else
            {
                string message = Marshal.PtrToStringAuto(commandData);
                m_onConnect(message);
            }
        }

        public override bool Synchronize()
        {
            bool useCooldown = m_synchronisationCooldownSeconds > 0f;
            if (useCooldown)
            {
                if (m_synchronisationCoroutine != null || m_resynchronisationRequested)
                {
                    m_resynchronisationRequested = true;
                    return false;
                }
                if (m_coroutineUtils.IsNull())
                {
                    HLOutput.LogError("Cloud_MacOS: Attempt to synchronise before CoroutineUtils available.");
                    return false;
                }
            }

            bool synchronised = HLCloud_Synchronize();
            if (!synchronised) HLOutput.LogError("Cloud_MacOS: Failed to synchronise.");
            if (useCooldown)
            {
                CoroutineUtils.StopUtilCoroutine(ref m_synchronisationCoroutine);
                m_synchronisationCoroutine = CoroutineUtils.Delay(OnSynchronisationCooldownComplete, m_waitForCooldown);
            }
            return synchronised;
        }

        public override HashSet<string> RetrieveAllCloudKeys(string allCloudKeysSeparator)
        {
            m_allCloudKeys.Clear();
            string keys = HLCloud_RetrieveAllCloudKeys(allCloudKeysSeparator);
            if (!string.IsNullOrEmpty(keys))
            {
                string[] splitKeys = keys.Split(allCloudKeysSeparator.ToCharArray());
                for (int i = 0; i < splitKeys.Length; ++i)
                    if (!m_allCloudKeys.Contains(splitKeys[i])) m_allCloudKeys.Add(splitKeys[i]);
            }
            return m_allCloudKeys;
        }

        public override void CloudDidChange(string message)
        {
            UserInfo info = new UserInfo();
            JsonUtility.FromJsonOverwrite(message, info);
            string[] keys = info.NSUbiquitousKeyValueStoreChangedKeysKey;
            ChangeReason reason = (ChangeReason)info.NSUbiquitousKeyValueStoreChangeReasonKey;
            // Native preserves this discarded string-building loop. Null keys
            // fail here before delivering the event to the cloud receiver.
            string keysDescription = string.Empty;
            for (int i = 0; i < keys.Length; ++i)
            {
                keysDescription += keys[i];
                if (i < keys.Length - 1) keysDescription += ", ";
            }
            cloudObject.OnCloudChange(keys, reason);
        }

        public override string StringForKey(string key) => HLCloud_StringForKey(key);
        public override void SetStringForKey(string value, string key) => HLCloud_SetStringForKey(value, key);
        public override float FloatForKey(string key) => HLCloud_FloatForKey(key);
        public override void SetFloatForKey(float value, string key) => HLCloud_SetFloatForKey(value, key);
        public override int IntForKey(string key) => HLCloud_IntForKey(key);
        public override void SetIntForKey(int value, string key) => HLCloud_SetIntForKey(value, key);
        public override bool BoolForKey(string key) => HLCloud_BoolForKey(key);
        public override void SetBoolForKey(bool value, string key) => HLCloud_SetBoolForKey(value, key);
        public override void RemoveForKey(string key) => HLCloud_RemoveForKey(key);

        public override void SetSynchronisationCooldown(float seconds) => m_synchronisationCooldownSeconds = seconds;

        private void OnSynchronisationCooldownComplete()
        {
            m_synchronisationCoroutine = null;
            bool requested = m_resynchronisationRequested;
            m_resynchronisationRequested = false;
            if (requested) Synchronize();
        }
    }
}
