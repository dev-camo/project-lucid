using System;
using System.Collections;
using Hardlight;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Verification-only host for the original GUI style accessors. Run in a genuine
    // Unity Editor coroutine; no synthetic skin/event or product UI is introduced.
    public sealed class RibbonEditorDefinitionGUIFixture : EditorWindow
    {
        private SurfaceEditorDefinition definition;
        private bool completed;
        private Exception failure;
        private int checks;

        private void OnGUI()
        {
            if (completed || definition == null) return;
            try { checks = RibbonEditorDefinitionVerification.RunGUI(definition); }
            catch (Exception error) { failure = error; }
            finally { completed = true; }
        }

        public static IEnumerator VerifyGUI()
        {
            SurfaceEditorDefinition definition = null;
            RibbonEditorDefinitionGUIFixture window = null;
            try
            {
                definition = ScriptableObject.CreateInstance<SurfaceEditorDefinition>();
                if (definition == null) throw new InvalidOperationException("Original SurfaceEditorDefinition creation failed.");
                window = ScriptableObject.CreateInstance<RibbonEditorDefinitionGUIFixture>();
                window.definition = definition;
                window.ShowUtility();
                window.Repaint();
                for (int frame = 0; frame < 120 && !window.completed; ++frame)
                {
                    window.Repaint();
                    yield return null;
                }
                if (!window.completed) throw new InvalidOperationException("No genuine Editor OnGUI event was delivered for the style verification.");
                if (window.failure != null) throw window.failure;
                if (window.checks != 10) throw new InvalidOperationException("Original GUI style verification count differs from ten.");
            }
            finally
            {
                try { if (window != null) window.Close(); }
                finally { if (definition != null) UnityEngine.Object.DestroyImmediate(definition); }
            }
        }
    }
}
