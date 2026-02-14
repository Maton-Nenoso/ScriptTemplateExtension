using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nenoso.ScriptTemplates.Editor
{
    /// <summary>
    /// Static facade that provides access to the project-level settings.
    /// All values are read from a <see cref="ScriptTemplateProjectSettings"/>
    /// ScriptableObject asset that lives in the project and can be committed
    /// to version control.
    /// </summary>
    internal static class ScriptTemplateSettings
    {
        private const string AssetName = "ScriptTemplateSettings.asset";

        private static ScriptTemplateProjectSettings _instance;

        // --- Instance management ---

        /// <summary>
        /// Returns the settings asset, creating it if it doesn't exist yet.
        /// </summary>
        public static ScriptTemplateProjectSettings GetOrCreateInstance()
        {
            if (_instance != null)
                return _instance;

            // Try to find an existing asset anywhere in the project.
            string[] guids = AssetDatabase.FindAssets("t:ScriptTemplateProjectSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = AssetDatabase.LoadAssetAtPath<ScriptTemplateProjectSettings>(path);
                if (_instance != null)
                    return _instance;
            }

            // Create a new asset next to the editor scripts.
            _instance = ScriptableObject.CreateInstance<ScriptTemplateProjectSettings>();

            string folder = GetSettingsFolder();
            if (folder == null)
            {
                Debug.LogError("[ScriptTemplates] Could not determine settings folder.");
                return _instance;
            }

            string assetPath = Path.Combine(folder, AssetName).Replace('\\', '/');
            AssetDatabase.CreateAsset(_instance, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScriptTemplates] Created project settings at {assetPath}");

            return _instance;
        }

        /// <summary>
        /// Clears the cached instance so it's re-fetched on next access.
        /// Useful after domain reloads.
        /// </summary>
        public static void ClearCache() => _instance = null;

        // --- Properties (facade) ---

        public static bool UseNamespace
        {
            get => GetOrCreateInstance().useNamespace;
            set { GetOrCreateInstance().useNamespace = value; MarkDirty(); }
        }

        public static bool DetectFromAsmdef
        {
            get => GetOrCreateInstance().detectFromAsmdef;
            set { GetOrCreateInstance().detectFromAsmdef = value; MarkDirty(); }
        }

        public static bool DetectFromFolder
        {
            get => GetOrCreateInstance().detectFromFolder;
            set { GetOrCreateInstance().detectFromFolder = value; MarkDirty(); }
        }

        public static string DefaultNamespace
        {
            get => GetOrCreateInstance().rootNamespace;
            set { GetOrCreateInstance().rootNamespace = value; MarkDirty(); }
        }

        public static string RootFolder
        {
            get => GetOrCreateInstance().rootFolder;
            set { GetOrCreateInstance().rootFolder = value; MarkDirty(); }
        }

        public static bool AddHeaderComment
        {
            get => GetOrCreateInstance().addHeaderComment;
            set { GetOrCreateInstance().addHeaderComment = value; MarkDirty(); }
        }

        public static string Author
        {
            get => GetOrCreateInstance().author;
            set { GetOrCreateInstance().author = value; MarkDirty(); }
        }

        // --- Helpers ---

        private static void MarkDirty()
        {
            if (_instance != null)
                EditorUtility.SetDirty(_instance);
        }

        private static string GetSettingsFolder()
        {
            string templateFolder = ScriptTemplateMenuItems.GetTemplateFolder();
            if (templateFolder == null) return null;

            // Go one level up from ScriptTemplates/ to the Editor/ folder.
            return Path.GetDirectoryName(templateFolder)?.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Registers a settings page under Project Settings > Script Templates.
    /// Uses <see cref="SerializedObject"/> for proper undo/redo support.
    /// </summary>
    internal class ScriptTemplateSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Script Templates";

        private SerializedObject _serializedSettings;

        // Serialized properties
        private SerializedProperty _useNamespace;
        private SerializedProperty _detectFromAsmdef;
        private SerializedProperty _detectFromFolder;
        private SerializedProperty _rootNamespace;
        private SerializedProperty _rootFolder;
        private SerializedProperty _addHeaderComment;
        private SerializedProperty _author;

        public ScriptTemplateSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new ScriptTemplateSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Script Templates",
                keywords = new[] { "script", "template", "namespace", "enum", "class" }
            };
        }

        private void EnsureSerializedObject()
        {
            if (_serializedSettings != null && _serializedSettings.targetObject != null)
                return;

            var instance = ScriptTemplateSettings.GetOrCreateInstance();
            _serializedSettings = new SerializedObject(instance);

            _useNamespace      = _serializedSettings.FindProperty("useNamespace");
            _detectFromAsmdef  = _serializedSettings.FindProperty("detectFromAsmdef");
            _detectFromFolder  = _serializedSettings.FindProperty("detectFromFolder");
            _rootNamespace     = _serializedSettings.FindProperty("rootNamespace");
            _rootFolder        = _serializedSettings.FindProperty("rootFolder");
            _addHeaderComment  = _serializedSettings.FindProperty("addHeaderComment");
            _author            = _serializedSettings.FindProperty("author");
        }

        public override void OnGUI(string searchContext)
        {
            EnsureSerializedObject();
            _serializedSettings.Update();

            EditorGUILayout.Space(10);

            // --- Settings asset reference ---
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Settings Asset",
                    _serializedSettings.targetObject,
                    typeof(ScriptTemplateProjectSettings),
                    false);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Namespace", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_useNamespace,
                new GUIContent("Wrap in Namespace",
                    "Whether generated files should be wrapped in a namespace block."));

            using (new EditorGUI.DisabledScope(!_useNamespace.boolValue))
            {
                EditorGUILayout.PropertyField(_detectFromAsmdef,
                    new GUIContent("Detect from .asmdef",
                        "If an Assembly Definition with a Root Namespace is found in a parent folder, use it."));

                EditorGUILayout.PropertyField(_detectFromFolder,
                    new GUIContent("Derive from Folder Path",
                        "Derive namespace from the folder path relative to Root Folder."));

                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(_rootNamespace,
                    new GUIContent("Root Namespace",
                        "The base namespace prepended to the folder-derived segments.\n" +
                        "e.g. 'SpaceWar' → SpaceWar.Systems.Ship"));

                EditorGUILayout.PropertyField(_rootFolder,
                    new GUIContent("Root Folder",
                        "Folder prefix to strip when deriving namespace from path.\n" +
                        "e.g. 'Assets/_Project/Scripts' means folders below this\n" +
                        "become namespace segments after Root Namespace."));

                // Live preview.
                string exampleFolder = _rootFolder.stringValue.TrimEnd('/') + "/Systems/Ship";
                string exampleNs = _rootNamespace.stringValue;
                if (!string.IsNullOrEmpty(exampleNs))
                    exampleNs += ".Systems.Ship";
                else
                    exampleNs = "Systems.Ship";

                EditorGUILayout.HelpBox(
                    $"Example:\n" +
                    $"  File in:    {exampleFolder}/\n" +
                    $"  Namespace:  {exampleNs}",
                    MessageType.None);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Header Comment", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_addHeaderComment,
                new GUIContent("Add File Header",
                    "Adds an auto-generated header comment to new files."));

            using (new EditorGUI.DisabledScope(!_addHeaderComment.boolValue))
            {
                EditorGUILayout.PropertyField(_author,
                    new GUIContent("Author",
                        "Author name used in the file header."));
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Templates are stored in:\n" +
                ScriptTemplateMenuItems.GetTemplateFolder() +
                "\n\nDrop a .txt file there and menu items are generated " +
                "automatically. Use #SCRIPTNAME# and #NAMESPACE# as placeholders." +
                "\n\nOptional metadata header in template files:\n" +
                "  // MenuLabel: My Custom Type\n" +
                "  // DefaultName: NewMyCustomType.cs\n" +
                "  // Priority: 5",
                MessageType.Info);

            if (GUILayout.Button("Regenerate Menu Items"))
            {
                ScriptTemplateDiscovery.RegenerateMenuItems();
            }

            // Apply changes with undo support.
            if (_serializedSettings.ApplyModifiedProperties())
            {
                // Settings changed — no additional action needed since
                // the ScriptableObject is automatically saved with the project.
            }
        }
    }
}
