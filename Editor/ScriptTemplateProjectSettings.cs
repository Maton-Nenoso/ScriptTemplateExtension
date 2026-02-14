using UnityEngine;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// ScriptableObject that stores all Script Template settings.
    /// Lives as an asset in the project so it can be committed to
    /// version control and shared across the team.
    /// </summary>
    // [CreateAssetMenu] is intentionally omitted — the asset is created
    // automatically via ScriptTemplateSettings.GetOrCreateInstance().
    internal class ScriptTemplateProjectSettings : ScriptableObject
    {
        [Header("Namespace")]

        [Tooltip("Whether generated files should be wrapped in a namespace block.")]
        public bool useNamespace = true;

        [Tooltip("If an Assembly Definition with a Root Namespace is found in a parent folder, use it.")]
        public bool detectFromAsmdef = true;

        [Tooltip("Derive namespace from the folder path relative to Root Folder.")]
        public bool detectFromFolder = true;

        [Tooltip("Base namespace prepended to folder-derived segments.\n" +
                 "e.g. 'SpaceWar' → SpaceWar.Systems.Ship")]
        public string rootNamespace = "Game";

        [Tooltip("Folder prefix to strip when deriving namespace from path.\n" +
                 "e.g. 'Assets/_Project/Scripts' means folders below this\n" +
                 "become namespace segments after Root Namespace.")]
        public string rootFolder = "Assets";

        [Header("Header Comment")]

        [Tooltip("Prepend an auto-generated header comment to new files.")]
        public bool addHeaderComment;

        [Tooltip("Author name used in the file header.")]
        public string author = "";
    }
}
