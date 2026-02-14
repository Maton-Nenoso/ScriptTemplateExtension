using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// Intercepts newly created script assets and replaces template tokens
    /// (#NAMESPACE#, #SCRIPTNAME#, etc.) with their resolved values.
    /// </summary>
    internal class ScriptTemplateProcessor : AssetModificationProcessor
    {
        /// <summary>
        /// Called by Unity whenever a new asset is about to be written to disk.
        /// At this point the file already exists with the raw template content
        /// and the user-chosen filename.
        /// </summary>
        private static void OnWillCreateAsset(string assetPath)
        {
            // Unity sometimes passes ".meta" paths; strip the .meta suffix.
            assetPath = assetPath.Replace(".meta", "");

            if (!assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return;

            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return;

            string content = File.ReadAllText(fullPath);

            // Only process files that still contain our custom tokens.
            if (!content.Contains("#NAMESPACE#") && !content.Contains("#SCRIPTNAME#"))
                return;

            string scriptName = Path.GetFileNameWithoutExtension(assetPath);
            string folderPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";

            // --- Strip template metadata comments ---
            content = StripMetadataComments(content);

            // --- Replace tokens ---
            content = content.Replace("#SCRIPTNAME#", scriptName);

            if (ScriptTemplateSettings.UseNamespace)
            {
                string ns = ResolveNamespace(folderPath);
                content = content.Replace("#NAMESPACE#", ns);
            }
            else
            {
                // Strip the namespace wrapper entirely if the user doesn't want it.
                content = RemoveNamespaceWrapper(content);
            }

            // Optional header comment.
            if (ScriptTemplateSettings.AddHeaderComment)
            {
                string header = BuildHeader(scriptName);
                content = header + content;
            }

            File.WriteAllText(fullPath, content, new UTF8Encoding(true));
            AssetDatabase.Refresh();
        }

        // -----------------------------------------------------------------
        // Metadata stripping
        // -----------------------------------------------------------------

        /// <summary>
        /// Removes template metadata comment lines (MenuLabel, DefaultName, Priority)
        /// from the top of the file so they don't end up in the generated script.
        /// </summary>
        private static string StripMetadataComments(string content)
        {
            var sb = new StringBuilder();
            bool headerSection = true;

            using (var reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (headerSection)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("//"))
                        {
                            string comment = trimmed.Substring(2).Trim();
                            if (comment.StartsWith("MenuLabel:", StringComparison.OrdinalIgnoreCase) ||
                                comment.StartsWith("DefaultName:", StringComparison.OrdinalIgnoreCase) ||
                                comment.StartsWith("Priority:", StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // Skip this metadata line.
                            }
                        }

                        // Any non-metadata line ends the header section.
                        if (!string.IsNullOrWhiteSpace(trimmed))
                            headerSection = false;
                        else
                            continue; // Skip blank lines between metadata and content.
                    }

                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        // -----------------------------------------------------------------
        // Namespace resolution
        // -----------------------------------------------------------------

        private static string ResolveNamespace(string assetFolderPath)
        {
            // 1) Try to find the closest .asmdef with a rootNamespace.
            if (ScriptTemplateSettings.DetectFromAsmdef)
            {
                string asmdefNs = FindAsmdefNamespace(assetFolderPath);
                if (!string.IsNullOrEmpty(asmdefNs))
                    return asmdefNs;
            }

            // 2) Derive from folder path.
            if (ScriptTemplateSettings.DetectFromFolder)
            {
                string folderNs = DeriveNamespaceFromPath(assetFolderPath);
                if (!string.IsNullOrEmpty(folderNs))
                    return folderNs;
            }

            // 3) Fallback to the default.
            return ScriptTemplateSettings.DefaultNamespace;
        }

        /// <summary>
        /// Walks up from <paramref name="assetFolderPath"/> looking for the
        /// nearest .asmdef file with a non-empty "rootNamespace" field.
        /// If found, appends sub-folder segments after the asmdef location.
        /// </summary>
        private static string FindAsmdefNamespace(string assetFolderPath)
        {
            string current = assetFolderPath;

            while (!string.IsNullOrEmpty(current) && current.StartsWith("Assets"))
            {
                string[] asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { current });

                foreach (string guid in asmdefGuids)
                {
                    string asmdefPath = AssetDatabase.GUIDToAssetPath(guid);

                    // Only consider asmdef files directly in 'current', not in sub-folders.
                    if (Path.GetDirectoryName(asmdefPath)?.Replace('\\', '/') != current)
                        continue;

                    string json = File.ReadAllText(asmdefPath);
                    string rootNs = ExtractJsonStringField(json, "rootNamespace");

                    if (!string.IsNullOrEmpty(rootNs))
                    {
                        // Append the relative sub-path as namespace segments.
                        string relative = assetFolderPath.Length > current.Length
                            ? assetFolderPath.Substring(current.Length + 1)
                            : "";

                        if (string.IsNullOrEmpty(relative))
                            return rootNs;

                        string suffix = relative.Replace('/', '.').Replace(' ', '_');
                        return $"{rootNs}.{suffix}";
                    }
                }

                // Move one directory up.
                int lastSlash = current.LastIndexOf('/');
                current = lastSlash > 0 ? current.Substring(0, lastSlash) : "";
            }

            return null;
        }

        /// <summary>
        /// Derives a namespace from the folder path by stripping the configured
        /// Root Folder prefix and prepending the Root Namespace.
        /// e.g. Root Folder = "Assets/_Project/Scripts", Root Namespace = "SpaceWar"
        ///      "Assets/_Project/Scripts/Systems/Ship" → "SpaceWar.Systems.Ship"
        /// </summary>
        private static string DeriveNamespaceFromPath(string assetFolderPath)
        {
            string rootFolder = ScriptTemplateSettings.RootFolder.TrimEnd('/');
            string rootNs     = ScriptTemplateSettings.DefaultNamespace;

            string relative;

            // Strip the configured root folder prefix.
            if (!string.IsNullOrEmpty(rootFolder) &&
                assetFolderPath.StartsWith(rootFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                relative = assetFolderPath.Length > rootFolder.Length
                    ? assetFolderPath.Substring(rootFolder.Length + 1)
                    : "";
            }
            else if (assetFolderPath.StartsWith("Assets/"))
            {
                // Fallback: strip just "Assets/" if root folder doesn't match.
                relative = assetFolderPath.Substring("Assets/".Length);
            }
            else if (assetFolderPath == "Assets")
            {
                relative = "";
            }
            else
            {
                relative = assetFolderPath;
            }

            // Sanitize folder segments for C# identifiers.
            string suffix = string.IsNullOrEmpty(relative)
                ? ""
                : relative.Replace('/', '.').Replace('\\', '.').Replace(' ', '_').Replace('-', '_');

            // Combine root namespace + folder suffix.
            if (!string.IsNullOrEmpty(rootNs) && !string.IsNullOrEmpty(suffix))
                return $"{rootNs}.{suffix}";

            if (!string.IsNullOrEmpty(rootNs))
                return rootNs;

            if (!string.IsNullOrEmpty(suffix))
                return suffix;

            return null;
        }

        // -----------------------------------------------------------------
        // Namespace stripping
        // -----------------------------------------------------------------

        /// <summary>
        /// Removes the <c>namespace #NAMESPACE# { ... }</c> wrapper while
        /// preserving the inner content and reducing indentation by one level.
        /// </summary>
        private static string RemoveNamespaceWrapper(string content)
        {
            // Simple approach: remove the namespace line, opening brace, closing
            // brace, and un-indent by one level (4 spaces / 1 tab).
            var sb = new StringBuilder();
            bool insideNamespace = false;
            int braceDepth = 0;

            using (var reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.TrimStart();

                    // Skip the namespace declaration line.
                    if (!insideNamespace && trimmed.StartsWith("namespace "))
                    {
                        insideNamespace = true;
                        // If the opening brace is on the same line, just skip it.
                        if (trimmed.EndsWith("{"))
                            braceDepth++;
                        continue;
                    }

                    // Skip standalone opening brace right after namespace.
                    if (insideNamespace && braceDepth == 0 && trimmed == "{")
                    {
                        braceDepth++;
                        continue;
                    }

                    if (insideNamespace)
                    {
                        // Track braces to find the closing one.
                        foreach (char c in line)
                        {
                            if (c == '{') braceDepth++;
                            else if (c == '}') braceDepth--;
                        }

                        // If we just closed the namespace brace, skip this line.
                        if (braceDepth <= 0)
                        {
                            insideNamespace = false;
                            continue;
                        }

                        // Un-indent one level.
                        if (line.StartsWith("    "))
                            line = line.Substring(4);
                        else if (line.StartsWith("\t"))
                            line = line.Substring(1);
                    }

                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        // -----------------------------------------------------------------
        // Header comment
        // -----------------------------------------------------------------

        private static string BuildHeader(string scriptName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// -------------------------------------------------");
            sb.AppendLine($"// {scriptName}.cs");
            if (!string.IsNullOrEmpty(ScriptTemplateSettings.Author))
                sb.AppendLine($"// Author : {ScriptTemplateSettings.Author}");
            sb.AppendLine($"// Created: {DateTime.Now:yyyy-MM-dd}");
            sb.AppendLine("// -------------------------------------------------");
            return sb.ToString();
        }

        // -----------------------------------------------------------------
        // Minimal JSON helper (avoids dependency on JsonUtility for plain text)
        // -----------------------------------------------------------------

        /// <summary>
        /// Extracts a simple string value from a JSON blob by key.
        /// Handles: <c>"key": "value"</c> — no nested objects.
        /// </summary>
        private static string ExtractJsonStringField(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;

            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;

            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0) return null;

            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return null;

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }
    }
}
