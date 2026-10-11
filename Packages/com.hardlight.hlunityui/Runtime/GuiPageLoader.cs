// Preserved Sonic Dream Team 1.10.1, HLUnityUI.Runtime.dll.
// Original MethodDef identities and native addresses (ARM64, x86_64):
// 0x06000163 0x1b4aa90, 0x1b438b0
// 0x06000166 0x1b4ac00, 0x1b43a20
// 0x06000167 0x1b4ad84, 0x1b43b80
// 0x06000168 0x1b4ad1c, 0x1b43b20
// 0x06000169 0x1b4aeac, 0x1b43c90
// 0x0600016a 0x1b4af20, 0x1b43cf0
// 0x0600016b 0x1b4b124, 0x1b43eb0
// 0x0600016c 0x1b4b1cc, 0x1b43f40
// 0x0600016d 0x1b4b4d0, 0x1b44250
// 0x0600016e 0x1b4b590, 0x1b44300
// 0x0600016f 0x1b4b0a0, 0x1b43e40
// 0x06000170 0x1b4b734, 0x1b44490
// 0x06000171 0x1b4b73c, 0x1b444a0
// Generic MethodDefs 0x06000164 and 0x06000165 have zero open-definition
// pointers; their registered shared native implementations are recorded below.
// Original HLUnityUI.Runtime.dll:Hardlight.GuiPageLoader (02000032).
// The fifteen original method roles are 06000163..06000171. The public
// instance constructor remains real; field initializers retain BeforeFieldInit.
// Initialization is overlay -> menu -> dialog -> subpage, then ready flag.
// Its enum walk is unconstrained, casts each boxed value to Int32, skips zero
// and Dictionary.Add preserves duplicate-key faults and partial additions.
// Missing numeric keys do nothing; string APIs request additive scenes.
// Async requests set priority zero immediately and retain no completion handle.
// Permanent IDs are recorded before loading, survive unload, and repeated
// LoadPermanently calls still request the scene. UnloadAllScenes checks loaded
// status before the helper's own second check; foreach retains disposal/faults.
// Native ARM64 own ordinary ranges 1b4aa90..1b4b83c and shared ranges
// 90f560..90fe8c corroborate the corresponding x86_64 functions. Generic
// definition pointer zeros and their registered shared functions are distinct.
// Unity scene behavior, supplied scene admission and UI readiness need separate
// controlled validation; this reconstruction adds no offline/readiness policy.
using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.SceneManagement;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GuiPageLoader
    {
        private static Dictionary<int, string> m_sceneNameLookup = new Dictionary<int, string>();
        private static bool m_isInitialised = false;
        private static List<int> m_permanentScenes = new List<int>();

        public static void InitialiseGUIPageLoader()
        {
            if (m_isInitialised)
                return;

            InitaliseGUIPageLoaderFromEnum<OverlayPageIdentifier>("s_overlay_");
            InitaliseGUIPageLoaderFromEnum<MenuPageIdentifier>("s_menu_");
            InitaliseGUIPageLoaderFromEnum<DialogPageIdentifier>("s_dialog_");
            InitaliseGUIPageLoaderFromEnum<SubPageIdentifier>("s_subpage_");
            m_isInitialised = true;
        }

        private static void InitaliseGUIPageLoaderFromEnum<T>(string prefix)
        {
            foreach (int value in Enum.GetValues(typeof(T)))
            {
                if (value != 0)
                {
                    string sceneName = string.Format("{0}{1}", prefix, Enum.GetName(typeof(T), value));
                    m_sceneNameLookup.Add(value, sceneName);
                }
            }
        }

        public static void LoadSceneFromEnum<T>(T identifier)
        {
            LoadSceneFromValue(Convert.ToInt32(identifier));
        }

        public static void LoadSceneFromValue(int val)
        {
            if (m_sceneNameLookup.TryGetValue(val, out string name))
                LoadSceneFromString(name);
        }

        public static void LoadSceneFromValueAsync(int val)
        {
            if (m_sceneNameLookup.TryGetValue(val, out string name))
                LoadSceneFromStringAsync(name);
        }

        public static void LoadSceneFromString(string name)
        {
            SceneManager.LoadScene(name, LoadSceneMode.Additive);
        }

        public static void LoadSceneFromStringAsync(string name)
        {
            SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive).priority = 0;
        }

        public static bool UnloadSceneFromValue(int val, bool unloadPermanentScenes = false)
        {
            if (!unloadPermanentScenes && IsPermanentScene(val))
                return false;

            if (m_sceneNameLookup.TryGetValue(val, out string name))
                return UnloadSceneFromString(name);

            return false;
        }

        private static bool UnloadSceneFromString(string name)
        {
            bool loaded = SceneManager.GetSceneByName(name).isLoaded;
            if (loaded)
                SceneManager.UnloadSceneAsync(name);

            return loaded;
        }

        public static void UnloadAllScenes(bool unloadPermanentScenes)
        {
            foreach (KeyValuePair<int, string> scene in m_sceneNameLookup)
            {
                if (unloadPermanentScenes || !IsPermanentScene(scene.Key))
                {
                    if (SceneManager.GetSceneByName(scene.Value).isLoaded)
                        UnloadSceneFromString(scene.Value);
                }
            }
        }

        public static string GetSceneFromID(int id)
        {
            if (m_sceneNameLookup.TryGetValue(id, out string name))
                return name;

            return string.Empty;
        }

        public static void LoadPermanently(int identifier)
        {
            if (!IsPermanentScene(identifier))
                m_permanentScenes.Add(identifier);

            LoadSceneFromValue(identifier);
        }

        public static bool IsPermanentScene(int identifier)
        {
            return m_permanentScenes.Contains(identifier);
        }

        public GuiPageLoader()
        {
        }
    }
}
