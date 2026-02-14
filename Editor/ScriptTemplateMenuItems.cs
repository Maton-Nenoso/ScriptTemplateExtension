using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// Provides the core template creation API used by both the generated
    /// menu items and the discovery system.
    /// </summary>
    internal static class ScriptTemplateMenuItems
    {
        // --- Public API ---

        /// <summary>
        /// Returns the absolute path to the ScriptTemplates folder that lives
        /// next to this script file. Works regardless of where the user places
        /// the extension inside their project.
        /// </summary>
        public static string GetTemplateFolder()
        {
            // Find the folder by locating this very script asset via its type name.
            string[] guids = AssetDatabase.FindAssets("t:Script ScriptTemplateMenuItems");
            if (guids.Length == 0)
            {
                Debug.LogError("[ScriptTemplates] Could not locate ScriptTemplateMenuItems.cs in the project.");
                return null;
            }

            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string editorDir  = Path.GetDirectoryName(scriptPath);
            return Path.Combine(editorDir, "ScriptTemplates").Replace('\\', '/');
        }

        /// <summary>
        /// Creates a new script file from the given template.
        /// Uses Unity's native rename-in-project-window flow.
        /// </summary>
        public static void CreateFromTemplate(string templateFileName, string defaultNewFileName)
        {
            string templateFolder = GetTemplateFolder();
            if (templateFolder == null) return;

            string templatePath = Path.Combine(templateFolder, templateFileName).Replace('\\', '/');

            if (!File.Exists(templatePath))
            {
                Debug.LogError($"[ScriptTemplates] Template not found: {templatePath}");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, defaultNewFileName);
        }
    }
}
