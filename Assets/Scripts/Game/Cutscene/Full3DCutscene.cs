using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000440, complete four-method owner. The editor
    // camera is disabled in shipping Awake; the authored actor list is retained.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Full3DCutscene : Cutscene
    {
        [SerializeField] private Transform m_cameraContainer;
        [SerializeField] private Camera m_editorMainCamera;
        [SerializeField] private List<GameObject> m_actors;

        public Transform CameraContainer => m_cameraContainer;
        public List<GameObject> Actors => m_actors;

        protected void Awake()
        {
            if (m_editorMainCamera == null)
                return;
            m_editorMainCamera.enabled = false;
        }

        public Full3DCutscene() { }
    }
}
