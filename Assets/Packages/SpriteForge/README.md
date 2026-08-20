# SpriteForge

SpriteForge is a Unity Package Manager (UPM) editor package. It requires Unity 2022.3 or newer.

## Install from Git

In Unity, open **Window > Package Management > Package Manager**, select **+ > Add package from git URL**, then enter:

```text
https://github.com/<owner>/<repository>.git?path=/Assets/Packages/SpriteForge
```

Replace `<owner>/<repository>` with the Git repository that contains this folder. Pin a release tag when needed:

```text
https://github.com/<owner>/<repository>.git?path=/Assets/Packages/SpriteForge#v1.0.0
```

For local development, clone the repository outside the consuming project's `Assets` folder, then use **+ > Add package from disk** and select its `package.json`.

SpriteForge is a Unity Editor workflow suite for preparing, validating, and maintaining 2D sprite assets at scale.

## Open the workspace

In the Unity menu, select **Tools > SpriteForge > Open Workspace**.

Choose an input mode at the top of the window:

- **Folder** scans all textures in a selected folder. Enable **Include subfolders** when required.
- **Selection** accepts project selection or drag-and-drop `Texture2D` assets.

## Main workflows

### Optimize

Trims transparent borders, adds padding, and snaps dimensions to a multiple or Power-of-Two size.

- PNG files only: pixel changes are intentionally limited to PNG sources.
- The output canvas is cleared before writing and sprite content is centred when the canvas grows.
- Alpha edge colours are dilated and the importer is set to **Alpha Is Transparency** and **Clamp** to prevent filtering halos.
- A backup snapshot is created automatically before source files are overwritten.

### Preview / Dry Run

Shows the estimated dimensions after applying the current optimization options without writing any files.

### Backup & Undo

Each Optimize operation creates a timestamped snapshot under `Assets/SpriteForge/Backups~`. This location belongs to the consuming project (not the installed package); Unity ignores folders ending in `~`, so backup images are not included in builds or package commits.

Use **Restore Latest Backup** to replace all PNG files from the most recent snapshot. This overwrites current assets and requires confirmation.

### Atlas

Creates or updates a `SpriteAtlas` with safe settings for UI sprites:

- Alpha dilation enabled
- At least 8 pixels of padding
- Mipmaps disabled
- Rotation and tight packing disabled

Use **Fix Atlas Settings & Repack** for an existing atlas. It changes packing settings and repacks without adding duplicate packables.

### Animation

Creates an Animation Clip from Sprite assets with natural filename sorting and optional auto-reverse frames.

### Sheet Slicer

Splits selected texture sheets into a uniform Columns × Rows grid. The texture dimensions must divide evenly by the selected grid. Existing sprite-sheet slices are replaced.

### Palette

Reports the most common non-transparent pixel colours to the Unity Console. It temporarily enables Read/Write access only when needed, then restores its former value.

### Importer

Applies a reusable import profile in batch, including:

- Sprite type and mode
- Pixels Per Unit and Alpha Is Transparency
- Filter and wrap modes
- Maximum texture size
- Compression, quality, and Crunch compression
- Mipmaps and Read/Write access

### Analyzer and Duplicates

- **Performance Analyzer** estimates raw texture memory and flags assets over a chosen threshold.
- **Duplicate Finder** compares original file bytes using MD5 and reports duplicate groups.

Both reports are written to the Unity Console and do not alter assets.

### References and Unused Assets

- **Reference Finder** lists serialized project assets that depend on selected textures.
- **Unused Asset Finder** reports textures with no detected serialized dependencies.

These scans are read-only. Dynamic loading, Addressables, and code-based references may cause an asset to appear unused, so review results before deleting anything.

### Rename

Batch-renames target textures with prefix, suffix, numbering, and natural sort order. Unity preserves references during an `AssetDatabase` rename. A confirmation dialog appears before the operation.

## Presets

The header supports reusable ScriptableObject presets:

- **Save New** creates a new preset asset.
- **Update** saves current settings into the selected preset.
- **Load** restores settings from the selected preset.

Presets include Optimize, Atlas, Animation, Importer, Sheet Slicer, and Palette configuration.

## Safety notes

- Use **Preview / Dry Run** before large Optimize operations.
- Keep source artwork under version control where possible.
- Use backup restore before attempting manual recovery.
- Review Console reports before renaming, deleting, or replacing assets.
