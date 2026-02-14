using System.Linq;
using UnityEditor;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// Watches for changes to .txt files in the ScriptTemplates folder and
    /// automatically regenerates the menu items when templates are added,
    /// removed, or renamed.
    /// </summary>
    internal class ScriptTemplatePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // Check if any of the changed assets are .txt files in a ScriptTemplates folder.
            bool templateChanged =
                importedAssets.Any(IsTemplatePath) ||
                deletedAssets.Any(IsTemplatePath) ||
                movedAssets.Any(IsTemplatePath) ||
                movedFromAssetPaths.Any(IsTemplatePath);

            if (templateChanged)
            {
                // Delay the call to avoid issues during import.
                EditorApplication.delayCall += ScriptTemplateDiscovery.RegenerateMenuItems;
            }
        }

        private static bool IsTemplatePath(string assetPath)
        {
            return assetPath.Contains("/ScriptTemplates/") &&
                   assetPath.EndsWith(".txt");
        }
    }
}
