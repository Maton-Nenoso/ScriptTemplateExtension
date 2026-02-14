# Script Template Extension for Unity

A lightweight Unity Editor extension that adds **Create** context menu items for common C# file types beyond MonoBehaviour:

- **C# Enum**
- **C# Class** (plain, no MonoBehaviour)
- **C# Abstract Class**
- **C# Static Class**
- **C# Interface**
- **C# Struct**
- **C# ScriptableObject**

New templates can be added by simply dropping a `.txt` file into the templates folder — no code changes required.

## Features

- **Native UX** — uses Unity's built-in rename-in-Project-window flow, identical to the default "C# Script" creation.
- **Auto-discovery** — drop a `.txt` template file into the templates folder and menu items are generated automatically.
- **Automatic namespace resolution** — detects the namespace from the nearest `.asmdef` root namespace, or derives it from a configurable root folder and namespace.
- **Project-level settings** — all configuration is stored in a `ScriptableObject` asset that gets committed to version control, so the entire team shares the same settings. Supports undo/redo.
- **Optional file header** — auto-generated comment block with author and date.
- **Editable templates** — plain `.txt` files you can customize or extend.

## Installation

### Install via folder copy

Copy the entire `ScriptTemplateExtension` folder into your Unity project's `Assets/` directory (or any subfolder, e.g. `Assets/Plugins/ScriptTemplateExtension`).

### Install via git URL

Requires a version of unity that supports path query parameter for git packages (Unity 2020.3+). You can add `https://github.com/Maton-Nenoso/ScriptTemplateExtension.git` to Package Manager

### After installation

On first compile, the extension auto-generates the menu items and creates the settings asset. If menu items don't appear, use **Tools → Script Templates → Regenerate Menu Items**.

## Usage

1. In the **Project** window, right-click (or use the **Assets** menu).
2. Go to **Create → C#** → pick one of the available file types.
3. Type a name → press Enter.
4. The file is created with the correct template, namespace, and optional header.

## Settings

Open **Edit → Project Settings → Script Templates** to configure. Settings are stored in a `ScriptTemplateSettings.asset` file inside the `Editor/` folder. This asset is auto-created on first use and can be committed to version control so the whole team uses the same configuration.

| Setting                | Description                                                                          |
|------------------------|--------------------------------------------------------------------------------------|
| Wrap in Namespace      | Enable/disable namespace wrapping entirely.                                          |
| Detect from .asmdef    | Use the `rootNamespace` from the nearest Assembly Definition file.                   |
| Derive from Folder Path| Derive namespace from the folder path relative to Root Folder.                       |
| Root Namespace         | Base namespace prepended to folder-derived segments (e.g. `SpaceWar`).               |
| Root Folder            | Folder prefix to strip when deriving namespace (e.g. `Assets/_Project/Scripts`).     |
| Add File Header        | Prepend a comment block with file name, author, and date.                            |
| Author                 | Your name for the file header.                                                       |

**Namespace resolution order:**

1. Nearest `.asmdef` with a `rootNamespace` (if enabled)
2. Folder-path-derived namespace (if enabled)
3. Root Namespace as fallback

**Example configuration:**

| Setting        | Value                      |
|----------------|----------------------------|
| Root Namespace | `SpaceWar`                 |
| Root Folder    | `Assets/_Project/Scripts`  |

A file created in `Assets/_Project/Scripts/Systems/Ship/` gets the namespace `SpaceWar.Systems.Ship`.

## Custom Templates

Templates live in `Editor/ScriptTemplates/` next to the editor scripts. To add a new template, simply create a `.txt` file in that folder. The extension detects it automatically and generates a corresponding menu item — no code changes needed.

### Template tokens

| Token          | Replaced with                          |
|----------------|----------------------------------------|
| `#SCRIPTNAME#` | The filename chosen by the user.       |
| `#NAMESPACE#`  | The resolved namespace.                |

### Optional metadata header

Templates can include metadata comments in the first lines to control menu label, default filename, and ordering:

```
// MenuLabel: ScriptableObject
// DefaultName: NewScriptableObject.cs
// Priority: 10
```

| Key           | Description                                                          | Default                          |
|---------------|----------------------------------------------------------------------|----------------------------------|
| `MenuLabel`   | Display name in the Create menu.                                     | Derived from filename (PascalCase split). |
| `DefaultName` | Default filename when creating.                                      | `New{FileName}.cs`               |
| `Priority`    | Ordering within the C# submenu (lower = higher in menu).            | Auto-incremented by file order.  |

Metadata lines are automatically stripped from the generated `.cs` file.

### Example: adding a custom template

Create `Editor/ScriptTemplates/EditorWindow.txt`:

```csharp
// MenuLabel: Editor Window
// DefaultName: NewEditorWindow.cs
using UnityEditor;
using UnityEngine;

namespace #NAMESPACE#
{
    public class #SCRIPTNAME# : EditorWindow
    {
        [MenuItem("Window/#SCRIPTNAME#")]
        public static void ShowWindow()
        {
            GetWindow<#SCRIPTNAME#>("#SCRIPTNAME#");
        }

        private void OnGUI()
        {
        }
    }
}
```

After Unity reimports, **Create → C# → Editor Window** appears in the context menu.

### Regenerating menu items

Menu items are regenerated automatically when `.txt` files are added, removed, or renamed. You can also trigger it manually:

- **Tools → Script Templates → Regenerate Menu Items**
- **Project Settings → Script Templates → Regenerate Menu Items** button

## File structure

```
ScriptTemplateExtension/
├── Editor/
│   ├── Nenoso.ScriptTemplates.Editor.asmdef
│   ├── ScriptTemplateDiscovery.cs         # Scans templates, generates menu items
│   ├── ScriptTemplateMenuItems.cs         # Core API (template folder, create from template)
│   ├── ScriptTemplatePostprocessor.cs     # Auto-regenerates on template file changes
│   ├── ScriptTemplateProcessor.cs         # Token replacement (#NAMESPACE#, #SCRIPTNAME#)
│   ├── ScriptTemplateProjectSettings.cs   # ScriptableObject for project-level settings
│   ├── ScriptTemplateSettings.cs          # Settings facade + Project Settings UI
│   ├── ScriptTemplateSettings.asset       # Auto-created settings (commit to VCS)
│   ├── GeneratedMenuItems.cs              # Auto-generated (do not edit manually)
│   └── ScriptTemplates/
│       ├── AbstractClass.txt
│       ├── Class.txt
│       ├── Enum.txt
│       ├── Interface.txt
│       ├── ScriptableObject.txt
│       ├── StaticClass.txt
│       └── Struct.txt
└── README.md
```

## Requirements

- Unity 2020.3+ (uses `ProjectWindowUtil.CreateScriptAssetFromTemplateFile`, `SettingsProvider`, and `AssetPostprocessor`)

## License

MIT — use freely in personal and commercial projects.
