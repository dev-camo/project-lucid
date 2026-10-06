using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "LocalisationConfiguration", menuName = "Hardlight/Localisation/Configuration")]
    public class HLLocalisationConfigurationAsset : SystemConfigurationAsset
    {
        [SerializeField]
        [Tooltip("Location inside Assets/StreamingAssets/ where language .bytes data is stored")]
        private string m_localisedDefinitionsDirectory = "Language Strings";

        [Tooltip("Relative path from Assets to data export Javascript file")]
        [SerializeField]
        private string m_nodeDataExportPath = "/../../Server/src/data/full_data_export.js";

        [SerializeField]
        [Tooltip("Relative path from Assets to location where font characters should be stored")]
        private string m_charactersOutputDirectory = "/Game Assets/Fonts/Characters/";

        [SerializeField]
        [Tooltip("If set to true, the editor options to generate data will be visible.")]
        private bool m_enableEditorDataGeneration = true;

        [SerializeField]
        [Tooltip("If set to true, the editor options related to the LocalisedUIString class will be visible.")]
        private bool m_enableLocalisedUIStringFunctionality = true;

        // HLLocalisation.Runtime 0x06000009..0x0600000d: direct original field reads.
        public string LocalisedDefinitionsDirectory => m_localisedDefinitionsDirectory;
        public string NodeDataExportPath => m_nodeDataExportPath;
        public string CharactersOutputDirectory => m_charactersOutputDirectory;
        public bool EnableEditorDataGeneration => m_enableEditorDataGeneration;
        public bool EnableLocalisedUIStringFunctionality => m_enableLocalisedUIStringFunctionality;

        // 0x0600000e. Preserve path-getter/check/log order and independent flag gates.
        public override void Validate()
        {
            if (!ValidateDirectory(m_localisedDefinitionsDirectory, Application.streamingAssetsPath))
                HLOutput.LogError("Localised definitions directory path is not valid: " + m_localisedDefinitionsDirectory);
            if (m_enableEditorDataGeneration && !ValidateFile(m_nodeDataExportPath, Application.dataPath))
                HLOutput.LogError("Node export file is not valid: " + m_nodeDataExportPath);
            if (m_enableLocalisedUIStringFunctionality && !ValidateDirectory(m_charactersOutputDirectory, Application.dataPath))
                HLOutput.LogError("Font characters directory path is not valid: " + m_charactersOutputDirectory);
        }

        // 0x0600000f. All five initializers run in declaration order before the genuine base.
        public HLLocalisationConfigurationAsset() { }
    }
}
