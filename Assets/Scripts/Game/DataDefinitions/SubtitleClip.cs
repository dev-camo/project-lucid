using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0200044a; native 0x7A5E60.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SubtitleClip : PlayableAsset
    {
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings m_subtitleText;
        [SerializeField]
        [Tooltip("This string allows the subtitles to be previewed in Edit Mode and means that localisations do not need to be done before animation.")]
        private string m_subtitleTextEditor;
        [SerializeField] private Color m_backgroundColour = new Color(0f, 0f, 0f, 0.7f);
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);

        // Original06001953: play mode formats the string through the subtitle character definitions;
        // outside play mode the plain StringTable entry is used when it exists, otherwise string.Empty.
        // The native metadata also registers the "" literal (0x7a5ee3) with no live use; the source shape behind it is unrecovered.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<SubtitleBehaviour> playable = ScriptPlayable<SubtitleBehaviour>.Create(graph, 0);
            SubtitleBehaviour behaviour = playable.GetBehaviour();
            if (Application.isPlaying)
                behaviour.SubtitleText = m_dataManagerRef.Get().SubtitleDefGroup.GetFormattedString(m_subtitleText);
            else
                behaviour.SubtitleText = StringTable.StringExists(m_subtitleText) ? StringTable.GetString(m_subtitleText) : string.Empty;
            behaviour.SubtitleTextEditor = m_subtitleTextEditor;
            behaviour.BackgroundColour = m_backgroundColour;
            return playable;
        }

        // Implicit original06001954.
    }
}
