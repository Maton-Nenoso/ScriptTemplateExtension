using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// Scans the ScriptTemplates folder for .txt template files and generates
    /// a C# source file with [MenuItem] entries for each discovered template.
    ///
    /// Template files can optionally include metadata in their first lines:
    /// <code>
    /// // MenuLabel: My Custom Type
    /// // DefaultName: NewMyCustomType.cs
    /// // Priority: 5
    /// </code>
    ///
    /// If no metadata is provided, the filename is used as the menu label
    /// and "New{Filename}.cs" as the default name.
    /// </summary>
    [InitializeOnLoad]
    internal static class ScriptTemplateDiscovery
    {
        /// <summary>
        /// On first domain load, check if the generated file exists.
        /// If not, generate it automatically so menu items appear
        /// without requiring manual intervention after install.
        /// </summary>
        static ScriptTemplateDiscovery()
        {
            string templateFolder = ScriptTemplateMenuItems.GetTemplateFolder();
            if (templateFolder == null) return;

            string editorFolder = Path.GetDirectoryName(templateFolder)?.Replace('\\', '/');
            if (editorFolder == null) return;

            string outputPath = Path.Combine(editorFolder, GeneratedFileName).Replace('\\', '/');

            if (!File.Exists(outputPath))
            {
                EditorApplication.delayCall += RegenerateMenuItems;
            }
        }

        private const string GeneratedFileName = "GeneratedMenuItems.cs";
        private const string MenuBasePath = "Assets/Create/C#/";
        private const int DefaultBasePriority = -220;

        // --- Public API ---

        [MenuItem("Tools/Script Templates/Regenerate Menu Items")]
        public static void RegenerateMenuItems()
        {
            string templateFolder = ScriptTemplateMenuItems.GetTemplateFolder();
            if (templateFolder == null)
            {
                Debug.LogError("[ScriptTemplates] Could not locate the ScriptTemplates folder.");
                return;
            }

            string editorFolder = Path.GetDirectoryName(templateFolder)?.Replace('\\', '/');
            if (editorFolder == null) return;

            string outputPath = Path.Combine(editorFolder, GeneratedFileName).Replace('\\', '/');

            var templates = DiscoverTemplates(templateFolder);
            string code = GenerateMenuItemsCode(templates);

            // Only write if content actually changed to avoid unnecessary recompiles.
            if (File.Exists(outputPath))
            {
                string existing = File.ReadAllText(outputPath);
                if (existing == code)
                {
                    Debug.Log("[ScriptTemplates] Menu items are up to date. No changes needed.");
                    return;
                }
            }

            File.WriteAllText(outputPath, code, new UTF8Encoding(true));
            AssetDatabase.Refresh();
            Debug.Log($"[ScriptTemplates] Generated {templates.Count} menu items → {outputPath}");
        }

        // --- Discovery ---

        /// <summary>
        /// Discovers all .txt template files in the given folder and parses
        /// their optional metadata headers.
        /// </summary>
        public static List<TemplateInfo> DiscoverTemplates(string templateFolder)
        {
            var results = new List<TemplateInfo>();

            if (!Directory.Exists(templateFolder))
                return results;

            string[] files = Directory.GetFiles(templateFolder, "*.txt");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int priorityIndex = 0;

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath); // e.g. "Enum"
                var info = new TemplateInfo
                {
                    TemplateFileName = Path.GetFileName(filePath),  // e.g. "Enum.txt"
                    MenuLabel = SplitPascalCase(fileName),          // e.g. "Enum" or "Abstract Class"
                    DefaultNewFileName = $"New{fileName}.cs",       // e.g. "NewEnum.cs"
                    Priority = priorityIndex
                };

                // Parse optional metadata from the first few lines.
                ParseMetadata(filePath, ref info);

                results.Add(info);
                priorityIndex++;
            }

            return results;
        }

        /// <summary>
        /// Reads the first lines of a template file looking for metadata comments.
        /// Supported keys: MenuLabel, DefaultName, Priority.
        /// </summary>
        private static void ParseMetadata(string filePath, ref TemplateInfo info)
        {
            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    // Only check the first 5 lines for metadata.
                    for (int i = 0; i < 5 && (line = reader.ReadLine()) != null; i++)
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.StartsWith("//"))
                            break; // Stop at first non-comment line.

                        string comment = trimmed.Substring(2).Trim();

                        if (TryParseMetaValue(comment, "MenuLabel", out string label))
                            info.MenuLabel = label;
                        else if (TryParseMetaValue(comment, "DefaultName", out string defaultName))
                            info.DefaultNewFileName = defaultName;
                        else if (TryParseMetaValue(comment, "Priority", out string priorityStr) &&
                                 int.TryParse(priorityStr, out int priority))
                            info.Priority = priority;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScriptTemplates] Could not read metadata from {filePath}: {e.Message}");
            }
        }

        private static bool TryParseMetaValue(string comment, string key, out string value)
        {
            value = null;
            string prefix = key + ":";
            if (!comment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            value = comment.Substring(prefix.Length).Trim();
            return !string.IsNullOrEmpty(value);
        }

        // --- Code Generation ---

        /// <summary>
        /// Generates a complete C# source file containing [MenuItem] entries
        /// for each discovered template.
        /// </summary>
        private static string GenerateMenuItemsCode(List<TemplateInfo> templates)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// -------------------------------------------------");
            sb.AppendLine("// AUTO-GENERATED by ScriptTemplateDiscovery.");
            sb.AppendLine("// Do not edit manually. Regenerate via:");
            sb.AppendLine("//   Tools > Script Templates > Regenerate Menu Items");
            sb.AppendLine("// Or by adding/removing .txt files in the");
            sb.AppendLine("// ScriptTemplates folder.");
            sb.AppendLine("// -------------------------------------------------");
            sb.AppendLine();
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine();
            sb.AppendLine("namespace Nenoso.ScriptTemplates.Editor");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class GeneratedMenuItems");
            sb.AppendLine("    {");

            foreach (var t in templates)
            {
                string methodName = SanitizeIdentifier(t.MenuLabel);
                string menuPath = MenuBasePath + EscapeString(t.MenuLabel);
                int priority = DefaultBasePriority + t.Priority;

                sb.AppendLine();
                sb.AppendLine($"        [MenuItem(\"{menuPath}\", false, {priority})]");
                sb.AppendLine($"        private static void Create_{methodName}()");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            ScriptTemplateMenuItems.CreateFromTemplate(\"{EscapeString(t.TemplateFileName)}\", \"{EscapeString(t.DefaultNewFileName)}\");");
                sb.AppendLine($"        }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // --- Helpers ---

        /// <summary>
        /// Splits PascalCase into separate words.
        /// e.g. "AbstractClass" → "Abstract Class", "Enum" → "Enum"
        /// </summary>
        private static string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, "(?<!^)([A-Z])", " $1");
        }

        /// <summary>
        /// Turns a display label into a valid C# identifier.
        /// </summary>
        private static string SanitizeIdentifier(string label)
        {
            var sb = new StringBuilder();
            foreach (char c in label)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-')
                    sb.Append('_');
            }

            string result = sb.ToString();

            // Ensure it doesn't start with a digit.
            if (result.Length > 0 && char.IsDigit(result[0]))
                result = "_" + result;

            return result;
        }

        /// <summary>
        /// Escapes a string for inclusion in a C# string literal.
        /// </summary>
        private static string EscapeString(string input)
        {
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // --- Data ---

        public struct TemplateInfo
        {
            public string TemplateFileName;   // e.g. "Enum.txt"
            public string MenuLabel;           // e.g. "Enum" or "My Custom Type"
            public string DefaultNewFileName;  // e.g. "NewEnum.cs"
            public int Priority;               // ordering within the menu
        }
    }
}
