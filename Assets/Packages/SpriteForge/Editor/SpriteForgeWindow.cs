using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace SpriteForge
{
    /// <summary>A single workspace for SpriteForge batch operations.</summary>
    public sealed class SpriteForgeWindow : EditorWindow
    {
        private enum Tool { Dashboard, Optimize, Preview, Backup, Atlas, Animation, Slicer, Palette, Importer, Analyzer, Duplicates, References, Unused, Rename }

        [SerializeField] private Tool activeTool;
        [SerializeField] private DefaultAsset sourceFolder;
        [SerializeField] private List<Texture2D> sprites = new List<Texture2D>();
        [SerializeField] private Vector2 sidebarScroll;
        [SerializeField] private Vector2 contentScroll;
        [SerializeField] private bool includeSubfolders = true;
        [SerializeField] private bool useFolderInput = true;
        [SerializeField] private SpriteMasterPreset preset;
        [SerializeField] private int multipleOf = 4;
        [SerializeField] private bool usePowerOfTwo;
        [SerializeField] private bool trimTransparent = true;
        [SerializeField] private int padding = 2;
        [SerializeField] private int atlasMaxSize = 2048;
        [SerializeField] private bool appendToAtlas;
        [SerializeField] private float animationFps = 12f;
        [SerializeField] private bool autoReverse;
        [SerializeField] private string renamePrefix = "sprite_";
        [SerializeField] private string renameSuffix = "";
        [SerializeField] private int renameStart = 1;
        [SerializeField] private bool md5Compare = true;
        [SerializeField] private SpriteAtlas targetAtlas;
        [SerializeField] private int textureType;
        [SerializeField] private int compression = 1;
        [SerializeField] private bool generateMipMaps;
        [SerializeField] private bool readable;
        [SerializeField] private int spriteMode = 1;
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private int filterMode = 1;
        [SerializeField] private int wrapMode = 1;
        [SerializeField] private int maxTextureSize = 2048;
        [SerializeField] private int compressionQuality = 50;
        [SerializeField] private bool alphaIsTransparency = true;
        [SerializeField] private bool crunchCompression;
        [SerializeField] private int analyzerThreshold = 512;
        [SerializeField] private int numberPadding = 2;
        [SerializeField] private int sliceColumns = 4;
        [SerializeField] private int sliceRows = 4;
        [SerializeField] private int paletteLimit = 16;

        private static readonly string[] ToolNames =
        {
            "Overview", "Optimize", "Preview / Dry Run", "Backup & Undo", "Atlas", "Animation", "Sheet Slicer", "Palette", "Importer", "Analyzer", "Duplicates", "References", "Unused Assets", "Rename"
        };

        [MenuItem("Tools/Raven/SpriteForge")]
        public static void Open()
        {
            var window = GetWindow<SpriteForgeWindow>();
            window.titleContent = new GUIContent("SpriteForge");
            window.minSize = new Vector2(850, 540);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            EditorGUILayout.BeginVertical();
            DrawInputBar();
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
            DrawContent();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            var header = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 48, padding = new RectOffset(16, 12, 10, 8) };
            EditorGUILayout.BeginHorizontal(header);
            EditorGUILayout.LabelField("SPRITEFORGE", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.35f, 0.85f, 1f) } }, GUILayout.Width(145));
            GUILayout.FlexibleSpace();
            preset = (SpriteMasterPreset)EditorGUILayout.ObjectField(preset, typeof(SpriteMasterPreset), false, GUILayout.Width(190));
            if (GUILayout.Button("Save New", EditorStyles.toolbarButton, GUILayout.Width(70))) SavePreset();
            using (new EditorGUI.DisabledScope(preset == null)) if (GUILayout.Button("Update", EditorStyles.toolbarButton, GUILayout.Width(55))) { CopyTo(preset); AssetDatabase.SaveAssets(); }
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(45))) LoadPreset();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(170), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("WORKSPACE", EditorStyles.centeredGreyMiniLabel);
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
            for (int i = 0; i < ToolNames.Length; i++)
            {
                var selected = (int)activeTool == i;
                var style = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 34, alignment = TextAnchor.MiddleLeft, fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
                if (GUILayout.Toggle(selected, ToolNames[i], style) && !selected) activeTool = (Tool)i;
            }
            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("v2.0  •  2D workflow suite", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(26));
            EditorGUILayout.EndVertical();
        }

        private void DrawInputBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("INPUT", EditorStyles.boldLabel, GUILayout.Width(46));
            useFolderInput = GUILayout.Toggle(useFolderInput, "Folder", EditorStyles.miniButtonLeft, GUILayout.Width(65));
            useFolderInput = !GUILayout.Toggle(!useFolderInput, "Selection", EditorStyles.miniButtonRight, GUILayout.Width(75));
            if (useFolderInput)
            {
                sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(sourceFolder, typeof(DefaultAsset), false);
                includeSubfolders = GUILayout.Toggle(includeSubfolders, "Include subfolders", EditorStyles.miniButton, GUILayout.Width(125));
            }
            else
            {
                EditorGUILayout.LabelField($"{sprites.Count} texture{(sprites.Count == 1 ? string.Empty : "s")} selected", EditorStyles.miniLabel);
                if (GUILayout.Button("Use Project Selection", GUILayout.Width(145))) CaptureProjectSelection();
            }
            EditorGUILayout.EndHorizontal();
            if (!useFolderInput) DrawDropArea();
            EditorGUILayout.EndVertical();
        }

        private void DrawContent()
        {
            switch (activeTool)
            {
                case Tool.Dashboard: DrawDashboard(); break;
                case Tool.Optimize: DrawOptimize(); break;
                case Tool.Preview: DrawPreview(); break;
                case Tool.Backup: DrawBackup(); break;
                case Tool.Atlas: DrawAtlas(); break;
                case Tool.Animation: DrawAnimation(); break;
                case Tool.Slicer: DrawSlicer(); break;
                case Tool.Palette: DrawPalette(); break;
                case Tool.Importer: DrawImporter(); break;
                case Tool.Analyzer: DrawAnalyzer(); break;
                case Tool.Duplicates: DrawDuplicates(); break;
                case Tool.References: DrawReferences(); break;
                case Tool.Unused: DrawUnused(); break;
                case Tool.Rename: DrawRename(); break;
            }
        }

        private void DrawDashboard()
        {
            Title("Your 2D workflow, in one place", "Choose a tool from the workspace navigation to configure a batch operation.");
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            Stat("TARGET", useFolderInput ? (sourceFolder ? sourceFolder.name : "No folder") : sprites.Count + " sprites");
            Stat("MODE", useFolderInput ? "Folder scan" : "Manual list");
            Stat("PRESET", preset ? preset.name : "Unsaved settings");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(16);
            EditorGUILayout.HelpBox("Quick start: choose a source, select a tool, review its settings, then run the operation. Each tool is designed for safe batch processing.", MessageType.Info);
        }

        private void DrawOptimize()
        {
            Title("Optimization Pipeline", "Fix dimensions, trim empty pixels, and add compression-friendly padding.");
            Section("Resolution Fixer");
            multipleOf = EditorGUILayout.IntPopup("Snap dimensions to", multipleOf, new[] { "4 pixels", "8 pixels", "16 pixels" }, new[] { 4, 8, 16 });
            usePowerOfTwo = EditorGUILayout.Toggle("Power-of-two dimensions", usePowerOfTwo);
            Section("Sprite Trimmer & Padding");
            trimTransparent = EditorGUILayout.Toggle("Trim transparent borders", trimTransparent);
            padding = EditorGUILayout.IntSlider("Padding (pixels)", padding, 0, 16);
            PrimaryButton("Optimize Selected Sprites", OptimizeTextures);
        }

        private void DrawPreview()
        {
            Title("Fix Preview / Dry Run", "Preview the impact of the current optimization settings without changing files.");
            if (!TryGetTargetsSilently(out var targets)) { EditorGUILayout.HelpBox("Choose input textures to preview changes.", MessageType.Info); return; }
            foreach (var texture in targets.Take(50))
            {
                var width = RoundDimension(texture.width + padding * 2);
                var height = RoundDimension(texture.height + padding * 2);
                EditorGUILayout.LabelField(texture.name, $"{texture.width} × {texture.height}  →  {width} × {height}");
            }
            if (targets.Count > 50) EditorGUILayout.LabelField($"…and {targets.Count - 50} more textures.", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawBackup()
        {
            Title("Backup & Undo", "Optimization snapshots are stored outside Unity's import pipeline in Assets/SpriteForge/Backups~.");
            var root = BackupRoot;
            var snapshots = Directory.Exists(root) ? Directory.GetDirectories(root).OrderByDescending(path => path).ToArray() : Array.Empty<string>();
            EditorGUILayout.LabelField($"{snapshots.Length} snapshot(s) available", EditorStyles.boldLabel);
            if (snapshots.Length > 0)
            {
                EditorGUILayout.LabelField("Latest: " + Path.GetFileName(snapshots[0]), EditorStyles.miniLabel);
                PrimaryButton("Restore Latest Backup", RestoreLatestBackup);
            }
            else EditorGUILayout.HelpBox("A snapshot is made automatically before Optimize changes PNG files.", MessageType.Info);
        }

        private void DrawSlicer()
        {
            Title("Sprite Sheet Slicer", "Split each selected texture into a uniform grid.");
            sliceColumns = EditorGUILayout.IntSlider("Columns", sliceColumns, 1, 64);
            sliceRows = EditorGUILayout.IntSlider("Rows", sliceRows, 1, 64);
            EditorGUILayout.HelpBox("Textures must divide evenly by the selected grid. Existing multiple-sprite slicing is replaced.", MessageType.Warning);
            PrimaryButton("Slice Selected Sheets", SliceSheets);
        }

        private void DrawPalette()
        {
            Title("Palette Analyzer", "Inspect the most-used visible colours in selected sprites.");
            paletteLimit = EditorGUILayout.IntSlider("Colours to report", paletteLimit, 4, 64);
            EditorGUILayout.HelpBox("The report ignores fully transparent pixels and is written to the Unity Console.", MessageType.None);
            PrimaryButton("Analyze Palette", AnalyzePalette);
        }

        private void DrawAtlas()
        {
            Title("Smart Atlas Generator", "Pack sprites efficiently to lower draw calls and retain room for future assets.");
            Section("Atlas Settings");
            atlasMaxSize = EditorGUILayout.IntPopup("Maximum texture size", atlasMaxSize, new[] { "1024", "2048", "4096", "8192" }, new[] { 1024, 2048, 4096, 8192 });
            appendToAtlas = EditorGUILayout.Toggle("Dynamically append to existing atlas", appendToAtlas);
            targetAtlas = (SpriteAtlas)EditorGUILayout.ObjectField("Target Sprite Atlas", targetAtlas, typeof(SpriteAtlas), false);
            if (targetAtlas != null && GUILayout.Button("Fix Atlas Settings & Repack", EditorStyles.miniButton)) ConfigureAndPackAtlas();
            PrimaryButton(appendToAtlas ? "Append to Atlas" : "Generate Optimized Atlas", GenerateAtlas);
        }

        private void DrawAnimation()
        {
            Title("Pro Animation Tools", "Natural-sort frames, preview the sequence, and create an animation clip.");
            Section("Sequence");
            animationFps = EditorGUILayout.Slider("Frames per second", animationFps, 1f, 60f);
            autoReverse = EditorGUILayout.Toggle("Create auto-reverse sequence", autoReverse);
            EditorGUILayout.Toggle("Natural sorting (1, 2, 10)", true);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(125));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("ANIMATION PREVIEW", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Slider(0f, 0f, 1f);
            EditorGUILayout.LabelField("Add sprites to preview the sequence", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            PrimaryButton("Create Animation Clip", CreateAnimationClip);
        }

        private void DrawImporter()
        {
            Title("Advanced Batch Importer", "Apply a consistent import profile to hundreds of textures at once.");
            Section("Asset Type & Sprite Settings");
            textureType = EditorGUILayout.Popup("Texture type", textureType, new[] { "Sprite (2D and UI)", "Default", "Normal map" });
            using (new EditorGUI.DisabledScope(textureType != 0))
            {
                spriteMode = EditorGUILayout.Popup("Sprite mode", spriteMode, new[] { "Single", "Multiple", "Polygon" });
                pixelsPerUnit = EditorGUILayout.FloatField("Pixels per unit", Mathf.Max(0.01f, pixelsPerUnit));
                alphaIsTransparency = EditorGUILayout.Toggle("Alpha is transparency", alphaIsTransparency);
            }
            Section("Sampling & Size");
            filterMode = EditorGUILayout.Popup("Filter mode", filterMode, new[] { "Point (no filter)", "Bilinear", "Trilinear" });
            wrapMode = EditorGUILayout.Popup("Wrap mode", wrapMode, new[] { "Repeat", "Clamp", "Mirror" });
            maxTextureSize = EditorGUILayout.IntPopup("Maximum texture size", maxTextureSize, new[] { "256", "512", "1024", "2048", "4096", "8192" }, new[] { 256, 512, 1024, 2048, 4096, 8192 });
            Section("Memory & Compression");
            compression = EditorGUILayout.Popup("Compression", compression, new[] { "None", "Normal Quality", "High Quality" });
            compressionQuality = EditorGUILayout.IntSlider("Compression quality", compressionQuality, 0, 100);
            using (new EditorGUI.DisabledScope(compression == 0)) crunchCompression = EditorGUILayout.Toggle("Crunch compression", crunchCompression);
            Section("Advanced");
            generateMipMaps = EditorGUILayout.Toggle("Generate mip maps", generateMipMaps);
            readable = EditorGUILayout.Toggle("Read / Write enabled", readable);
            PrimaryButton("Apply Import Settings", ApplyImportSettings);
        }

        private void DrawAnalyzer()
        {
            Title("Performance Analyzer", "Identify costly textures before they impact memory budgets and rendering.");
            Section("Analysis Scope");
            analyzerThreshold = EditorGUILayout.IntSlider("Flag textures above (KB)", analyzerThreshold, 64, 8192);
            EditorGUILayout.Toggle("Include estimated runtime memory", true);
            EditorGUILayout.HelpBox("Run an analysis to see texture dimensions, import format, estimated memory, and rendering-risk flags.", MessageType.None);
            PrimaryButton("Analyze Sprite Performance", AnalyzeTextures);
        }

        private void DrawDuplicates()
        {
            Title("Smart Duplicate Finder", "Find byte-for-byte identical assets and clean up redundant files.");
            Section("Comparison");
            md5Compare = EditorGUILayout.Toggle("Use MD5 content hashing", md5Compare);
            EditorGUILayout.Toggle("Group duplicates by folder", true);
            EditorGUILayout.HelpBox("No asset files are changed during a scan. Review duplicate groups before replacing or deleting anything.", MessageType.Info);
            PrimaryButton("Scan for Duplicates", ScanDuplicates);
        }

        private void DrawReferences()
        {
            Title("Reference Finder", "Find project assets that depend on the selected textures.");
            EditorGUILayout.HelpBox("Results are written to the Unity Console. This scan is read-only.", MessageType.Info);
            PrimaryButton("Find References", FindReferences);
        }

        private void DrawUnused()
        {
            Title("Unused Asset Finder", "List texture assets with no detected project dependencies.");
            EditorGUILayout.HelpBox("This is conservative: dynamically loaded assets and Addressables may appear unused. No files are deleted.", MessageType.Warning);
            PrimaryButton("Scan Unused Textures", ScanUnusedTextures);
        }

        private void DrawRename()
        {
            Title("Pattern Renamer", "Build clean, predictable names for a sprite sequence or a whole folder.");
            Section("Naming Pattern");
            renamePrefix = EditorGUILayout.TextField("Prefix", renamePrefix);
            renameSuffix = EditorGUILayout.TextField("Suffix", renameSuffix);
            renameStart = EditorGUILayout.IntField("Start number", renameStart);
            numberPadding = EditorGUILayout.Popup("Number padding", numberPadding, new[] { "None", "2 digits", "3 digits", "4 digits" });
            EditorGUILayout.HelpBox($"Example: {renamePrefix}{renameStart.ToString("D" + numberPadding)}{renameSuffix}", MessageType.None);
            PrimaryButton("Preview & Rename Assets", RenameTextures);
        }

        private void DrawDropArea()
        {
            var rect = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drag Texture2D assets here", EditorStyles.helpBox);
            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return;
            if (evt.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; evt.Use(); }
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var item in DragAndDrop.objectReferences) if (item is Texture2D texture && !sprites.Contains(texture)) sprites.Add(texture);
                evt.Use();
            }
        }

        private void CaptureProjectSelection()
        {
            sprites.Clear();
            foreach (var item in Selection.objects) if (item is Texture2D texture) sprites.Add(texture);
        }

        private List<Texture2D> GetTargets()
        {
            if (!useFolderInput) return sprites.Where(s => s != null).Distinct().ToList();
            if (sourceFolder == null) return new List<Texture2D>();
            var folderPath = AssetDatabase.GetAssetPath(sourceFolder).Replace("\\", "/").TrimEnd('/');
            return AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => includeSubfolders || Path.GetDirectoryName(path).Replace("\\", "/") == folderPath)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null).ToList();
        }

        private bool TryGetTargets(out List<Texture2D> targets)
        {
            targets = GetTargets();
            if (targets.Count != 0) return true;
            EditorUtility.DisplayDialog("SpriteForge", "Choose a valid asset folder or add one or more texture assets.", "OK");
            return false;
        }

        private bool TryGetTargetsSilently(out List<Texture2D> targets)
        {
            targets = GetTargets();
            return targets.Count != 0;
        }

        private static string BackupRoot => Path.Combine(Application.dataPath, "SpriteForge", "Backups~");

        private static void CreateBackupSnapshot(IEnumerable<Texture2D> targets)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var snapshotRoot = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            foreach (var texture in targets)
            {
                var assetPath = AssetDatabase.GetAssetPath(texture);
                var source = Path.Combine(projectRoot, assetPath);
                var destination = Path.Combine(snapshotRoot, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }
        }

        private void RestoreLatestBackup()
        {
            var snapshot = Directory.GetDirectories(BackupRoot).OrderByDescending(path => path).FirstOrDefault();
            if (snapshot == null || !EditorUtility.DisplayDialog("Restore backup", "Restore all PNG files in the latest snapshot? Current files will be overwritten.", "Restore", "Cancel")) return;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var file in Directory.GetFiles(snapshot, "*.png", SearchOption.AllDirectories))
            {
                var relative = file.Substring(snapshot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destination = Path.Combine(projectRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
            AssetDatabase.Refresh();
        }

        private void OptimizeTextures()
        {
            if (!TryGetTargets(out var targets)) return;
            int changed = 0, skipped = 0;
            CreateBackupSnapshot(targets.Where(texture => Path.GetExtension(AssetDatabase.GetAssetPath(texture)).Equals(".png", StringComparison.OrdinalIgnoreCase)));
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var texture = targets[i];
                    EditorUtility.DisplayProgressBar("Optimizing sprites", texture.name, (float)i / targets.Count);
                    var path = AssetDatabase.GetAssetPath(texture);
                    if (Path.GetExtension(path).ToLowerInvariant() != ".png") { skipped++; continue; }
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { skipped++; continue; }
                    var oldReadable = importer.isReadable;
                    if (!oldReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    var pixels = texture.GetPixels32();
                    var sourceRect = GetOpaqueBounds(pixels, texture.width, texture.height);
                    if (!trimTransparent || sourceRect.width == 0) sourceRect = new RectInt(0, 0, texture.width, texture.height);
                    var desiredWidth = RoundDimension(sourceRect.width + padding * 2);
                    var desiredHeight = RoundDimension(sourceRect.height + padding * 2);
                    var output = new Texture2D(desiredWidth, desiredHeight, TextureFormat.RGBA32, false);
                    // Texture2D pixel memory is not guaranteed to start cleared. Explicitly initialize
                    // the whole canvas so added padding cannot retain random non-zero alpha values.
                    output.SetPixels32(new Color32[desiredWidth * desiredHeight]);
                    // Centre the trimmed sprite. POT expansion can add a large amount of extra
                    // space; anchoring at the lower-left would visibly move the artwork to a corner.
                    int destinationX = (desiredWidth - sourceRect.width) / 2;
                    int destinationY = (desiredHeight - sourceRect.height) / 2;
                    for (int y = 0; y < sourceRect.height; y++)
                        for (int x = 0; x < sourceRect.width; x++)
                            output.SetPixel(x + destinationX, y + destinationY, texture.GetPixel(sourceRect.x + x, sourceRect.y + y));
                    // Transparent source pixels can contain white RGB values. Bilinear filtering then
                    // interpolates that colour at the sprite edge, producing a visible white halo.
                    var outputPixels = output.GetPixels32();
                    DilateAlphaColours(outputPixels, desiredWidth, desiredHeight, 2);
                    output.SetPixels32(outputPixels);
                    output.Apply(false, false);
                    File.WriteAllBytes(Path.GetFullPath(path), output.EncodeToPNG());
                    DestroyImmediate(output);
                    importer.isReadable = oldReadable;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorUtility.DisplayDialog("Optimization complete", $"Optimized {changed} PNG texture(s). Skipped {skipped} non-PNG or unsupported texture(s).", "OK");
        }

        private int RoundDimension(int value)
        {
            value = Mathf.Max(1, value);
            if (usePowerOfTwo) return Mathf.NextPowerOfTwo(value);
            return Mathf.CeilToInt(value / (float)multipleOf) * multipleOf;
        }

        private static RectInt GetOpaqueBounds(Color32[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a == 0) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>Clears hidden transparent RGB, then extrudes edge colour into it for filtered sampling.</summary>
        private static void DilateAlphaColours(Color32[] pixels, int width, int height, int iterations)
        {
            var working = (Color32[])pixels.Clone();
            var filled = new bool[working.Length];
            for (int i = 0; i < working.Length; i++)
            {
                filled[i] = working[i].a != 0;
                if (!filled[i]) working[i] = new Color32(0, 0, 0, 0);
            }
            for (int pass = 0; pass < iterations; pass++)
            {
                var nextPixels = (Color32[])working.Clone();
                var nextFilled = (bool[])filled.Clone();
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (filled[index]) continue;
                    bool copied = false;
                    for (int offsetY = -1; offsetY <= 1; offsetY++) for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighbourX = x + offsetX, neighbourY = y + offsetY;
                        if (offsetX == 0 && offsetY == 0 || neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height) continue;
                        int neighbour = neighbourY * width + neighbourX;
                        if (!filled[neighbour]) continue;
                        var colour = working[neighbour];
                        nextPixels[index] = new Color32(colour.r, colour.g, colour.b, 0);
                        nextFilled[index] = true;
                        copied = true;
                        break;
                    }
                    if (copied) continue;
                }
                working = nextPixels;
                filled = nextFilled;
            }
            Array.Copy(working, pixels, pixels.Length);
        }

        private void GenerateAtlas()
        {
            if (!TryGetTargets(out var targets)) return;
            if (targetAtlas == null)
            {
                var path = EditorUtility.SaveFilePanelInProject("Create Sprite Atlas", "SpriteMasterAtlas", "spriteatlas", "Choose a location for the atlas.");
                if (string.IsNullOrEmpty(path)) return;
                targetAtlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(targetAtlas, path);
            }
            SpriteAtlasExtensions.Add(targetAtlas, targets.Cast<UnityEngine.Object>().ToArray());
            ConfigureAndPackAtlas();
            Selection.activeObject = targetAtlas;
        }

        private void ConfigureAndPackAtlas()
        {
            if (targetAtlas == null) return;
            // Alpha dilation writes edge colour into transparent atlas padding. Together with a
            // generous border it prevents neighbouring/transparent texels bleeding into sprites.
            // Tight packing is deliberately disabled: it can expose polygon-edge artifacts in UI sprites.
            var settings = targetAtlas.GetPackingSettings();
            settings.enableRotation = false;
            settings.enableTightPacking = false;
            settings.enableAlphaDilation = true;
            settings.padding = Mathf.Max(8, padding);
            targetAtlas.SetPackingSettings(settings);
            var textureSettings = targetAtlas.GetTextureSettings(); textureSettings.generateMipMaps = false; textureSettings.filterMode = FilterMode.Bilinear; targetAtlas.SetTextureSettings(textureSettings);
            EditorUtility.SetDirty(targetAtlas); AssetDatabase.SaveAssets(); SpriteAtlasUtility.PackAtlases(new[] { targetAtlas }, EditorUserBuildSettings.activeBuildTarget);
        }

        private void CreateAnimationClip()
        {
            if (!TryGetTargets(out var targets)) return;
            var frames = targets.SelectMany(t => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)).OfType<Sprite>())
                .OrderBy(s => s.name, new NaturalNameComparer()).ToList();
            if (frames.Count == 0) { EditorUtility.DisplayDialog("Animation", "The selected textures do not contain Sprite sub-assets. Set their Texture Type to Sprite first.", "OK"); return; }
            if (autoReverse && frames.Count > 2) frames.AddRange(frames.Skip(1).Take(frames.Count - 2).Reverse());
            var path = EditorUtility.SaveFilePanelInProject("Create Animation Clip", "SpriteAnimation", "anim", "Choose where to save the animation clip.");
            if (string.IsNullOrEmpty(path)) return;
            var clip = new AnimationClip { frameRate = animationFps };
            var keys = frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / animationFps, value = sprite }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AssetDatabase.CreateAsset(clip, path); AssetDatabase.SaveAssets(); Selection.activeObject = clip;
        }

        private void ApplyImportSettings()
        {
            if (!TryGetTargets(out var targets)) return;
            foreach (var texture in targets)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = textureType == 0 ? TextureImporterType.Sprite : textureType == 2 ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.textureCompression = compression == 0 ? TextureImporterCompression.Uncompressed : compression == 2 ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                importer.spriteImportMode = spriteMode == 1 ? SpriteImportMode.Multiple : spriteMode == 2 ? SpriteImportMode.Polygon : SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.alphaIsTransparency = alphaIsTransparency;
                importer.filterMode = (FilterMode)filterMode;
                importer.wrapMode = (TextureWrapMode)wrapMode;
                importer.maxTextureSize = maxTextureSize;
                importer.compressionQuality = compressionQuality;
                importer.crunchedCompression = compression != 0 && crunchCompression;
                importer.mipmapEnabled = generateMipMaps;
                importer.isReadable = readable;
                importer.SaveAndReimport();
            }
            EditorUtility.DisplayDialog("Import settings applied", $"Updated {targets.Count} texture importer(s).", "OK");
        }

        private void AnalyzeTextures()
        {
            if (!TryGetTargets(out var targets)) return;
            var report = targets.Select(t => new { Texture = t, KB = (long)t.width * t.height * 4 / 1024 })
                .OrderByDescending(item => item.KB).ToList();
            Debug.Log("<b>SpriteForge — Performance Report</b>\n" + string.Join("\n", report.Select(r => $"{(r.KB >= analyzerThreshold ? "⚠ " : "")} {r.Texture.name}: {r.Texture.width}×{r.Texture.height}, estimated {r.KB:N0} KB")));
            EditorUtility.DisplayDialog("Analysis complete", $"Analyzed {report.Count} texture(s). Detailed results were written to the Unity Console.", "OK");
        }

        private void ScanDuplicates()
        {
            if (!TryGetTargets(out var targets)) return;
            var groups = targets.GroupBy(texture => GetFileHash(AssetDatabase.GetAssetPath(texture))).Where(group => group.Count() > 1).ToList();
            Debug.Log("<b>SpriteForge — Duplicate Report</b>\n" + (groups.Count == 0 ? "No duplicate files found." : string.Join("\n\n", groups.Select(g => "Duplicate group:\n" + string.Join("\n", g.Select(t => " • " + AssetDatabase.GetAssetPath(t)))))));
            EditorUtility.DisplayDialog("Duplicate scan complete", groups.Count == 0 ? "No identical files found." : $"Found {groups.Count} duplicate group(s). See the Unity Console for paths.", "OK");
        }

        private static string GetFileHash(string assetPath)
        {
            using (var md5 = MD5.Create()) using (var stream = File.OpenRead(Path.GetFullPath(assetPath))) return BitConverter.ToString(md5.ComputeHash(stream));
        }

        private void RenameTextures()
        {
            if (!TryGetTargets(out var targets)) return;
            targets = targets.OrderBy(t => t.name, new NaturalNameComparer()).ToList();
            if (!EditorUtility.DisplayDialog("Rename textures", $"Rename {targets.Count} texture(s)? This changes asset filenames and references are preserved by Unity.", "Rename", "Cancel")) return;
            int renamed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var name = renamePrefix + (renameStart + i).ToString("D" + numberPadding) + renameSuffix;
                var error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(targets[i]), name);
                if (string.IsNullOrEmpty(error)) renamed++; else Debug.LogWarning($"Could not rename {targets[i].name}: {error}");
            }
            AssetDatabase.SaveAssets(); EditorUtility.DisplayDialog("Rename complete", $"Renamed {renamed} of {targets.Count} texture(s).", "OK");
        }

        private void SliceSheets()
        {
            if (!TryGetTargets(out var targets)) return;
            foreach (var texture in targets)
            {
                if (texture.width % sliceColumns != 0 || texture.height % sliceRows != 0) { Debug.LogWarning($"SpriteForge: {texture.name} does not divide evenly by the selected grid."); continue; }
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if (importer == null) continue;
                int cellWidth = texture.width / sliceColumns, cellHeight = texture.height / sliceRows;
                var spriteRects = new List<SpriteRect>();
                for (int row = 0; row < sliceRows; row++) for (int column = 0; column < sliceColumns; column++)
                    spriteRects.Add(new SpriteRect { name = $"{texture.name}_{row:D2}_{column:D2}", rect = new Rect(column * cellWidth, texture.height - (row + 1) * cellHeight, cellWidth, cellHeight), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f), spriteID = GUID.Generate() });
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var factories = new SpriteDataProviderFactories();
                factories.Init();
                var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
                dataProvider.InitSpriteEditorDataProvider();
                dataProvider.SetSpriteRects(spriteRects.ToArray());
                dataProvider.Apply();
                importer.SaveAndReimport();
            }
        }

        private void AnalyzePalette()
        {
            if (!TryGetTargets(out var targets)) return;
            foreach (var source in targets)
            {
                var path = AssetDatabase.GetAssetPath(source);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool oldReadable = importer != null && importer.isReadable;
                if (importer != null && !oldReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var colours = texture.GetPixels32().Where(colour => colour.a > 0).GroupBy(colour => (int)colour.r << 24 | (int)colour.g << 16 | (int)colour.b << 8 | colour.a)
                    .OrderByDescending(group => group.Count()).Take(paletteLimit)
                    .Select(group => $"#{(group.Key >> 24) & 255:X2}{(group.Key >> 16) & 255:X2}{(group.Key >> 8) & 255:X2}  α{group.Key & 255}  × {group.Count()}");
                Debug.Log($"<b>SpriteForge — Palette: {texture.name}</b>\n" + string.Join("\n", colours));
                if (importer != null && !oldReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            }
        }

        private void FindReferences()
        {
            if (!TryGetTargets(out var targets)) return;
            var allAssets = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && !path.EndsWith(".meta")).ToArray();
            foreach (var target in targets)
            {
                var targetPath = AssetDatabase.GetAssetPath(target);
                var references = allAssets.Where(path => path != targetPath && AssetDatabase.GetDependencies(path, false).Contains(targetPath)).ToArray();
                Debug.Log($"<b>SpriteForge — References: {target.name}</b>\n" + (references.Length == 0 ? "No serialized references found." : string.Join("\n", references)));
            }
        }

        private void ScanUnusedTextures()
        {
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToHashSet();
            var used = new HashSet<string>(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && !textures.Contains(path) && !path.EndsWith(".meta")).SelectMany(path => AssetDatabase.GetDependencies(path, false)));
            var unused = textures.Where(path => !used.Contains(path)).OrderBy(path => path).ToArray();
            Debug.Log($"<b>SpriteForge — Potentially Unused Textures ({unused.Length})</b>\n" + string.Join("\n", unused));
            EditorUtility.DisplayDialog("Unused scan complete", $"Found {unused.Length} potentially unused texture(s). Review the Unity Console before removing anything.", "OK");
        }

        private sealed class NaturalNameComparer : IComparer<string>
        {
            public int Compare(string left, string right) { return EditorUtility.NaturalCompare(left, right); }
        }

        private void PrimaryButton(string label, Action action)
        {
            EditorGUILayout.Space(12);
            var old = GUI.backgroundColor; GUI.backgroundColor = new Color(0.25f, 0.72f, 0.96f);
            if (GUILayout.Button(label, GUILayout.Height(34))) action();
            GUI.backgroundColor = old;
        }

        private static void Title(string title, string subtitle) { EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, fixedHeight = 30 }); EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel); EditorGUILayout.Space(14); }
        private static void Section(string title) { EditorGUILayout.LabelField(title, EditorStyles.boldLabel); EditorGUILayout.Space(4); }
        private static void Stat(string label, string value) { EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(150), GUILayout.Height(62)); EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel); EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true }); EditorGUILayout.EndVertical(); }

        private void SavePreset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save SpriteForge Preset", "SpriteForgePreset", "asset", "Choose where to save the preset.");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<SpriteMasterPreset>();
            CopyTo(asset); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); preset = asset;
        }
        private void LoadPreset() { if (preset != null) CopyFrom(preset); }
        private void CopyTo(SpriteMasterPreset target)
        {
            target.multipleOf = multipleOf; target.powerOfTwo = usePowerOfTwo; target.trimTransparent = trimTransparent; target.padding = padding;
            target.atlasMaxSize = atlasMaxSize; target.animationFps = animationFps; target.autoReverse = autoReverse;
            target.textureType = textureType; target.compression = compression; target.generateMipMaps = generateMipMaps; target.readable = readable;
            target.spriteMode = spriteMode; target.pixelsPerUnit = pixelsPerUnit; target.filterMode = filterMode; target.wrapMode = wrapMode;
            target.maxTextureSize = maxTextureSize; target.compressionQuality = compressionQuality; target.alphaIsTransparency = alphaIsTransparency; target.crunchCompression = crunchCompression;
            target.sliceColumns = sliceColumns; target.sliceRows = sliceRows; target.paletteLimit = paletteLimit;
            EditorUtility.SetDirty(target);
        }

        private void CopyFrom(SpriteMasterPreset source)
        {
            multipleOf = source.multipleOf; usePowerOfTwo = source.powerOfTwo; trimTransparent = source.trimTransparent; padding = source.padding;
            atlasMaxSize = source.atlasMaxSize; animationFps = source.animationFps; autoReverse = source.autoReverse;
            textureType = source.textureType; compression = source.compression; generateMipMaps = source.generateMipMaps; readable = source.readable;
            spriteMode = source.spriteMode; pixelsPerUnit = source.pixelsPerUnit; filterMode = source.filterMode; wrapMode = source.wrapMode;
            maxTextureSize = source.maxTextureSize; compressionQuality = source.compressionQuality; alphaIsTransparency = source.alphaIsTransparency; crunchCompression = source.crunchCompression;
            sliceColumns = source.sliceColumns; sliceRows = source.sliceRows; paletteLimit = source.paletteLimit;
            Repaint();
        }
    }

}
