using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AudioForge
{
    /// <summary>Non-destructive batch audio processing that exports processed WAV files alongside Unity assets.</summary>
    public sealed class AudioForgeWindow : EditorWindow
    {
        [SerializeField] private DefaultAsset sourceFolder;
        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
        [SerializeField] private bool useFolder;
        [SerializeField] private bool includeSubfolders = true;
        [SerializeField] private float volumeDb;
        [SerializeField] private bool normalize;
        [SerializeField] private float normalizePeakDb = -1f;
        [SerializeField] private bool autoTrimSilence = true;
        [SerializeField] private float silenceThresholdDb = -45f;
        [SerializeField] private float trimStart;
        [SerializeField] private float trimEnd;
        [SerializeField] private float fadeIn;
        [SerializeField] private float fadeOut;
        [SerializeField] private string outputFolder = "Assets/AudioForge/Processed";
        [SerializeField] private Vector2 scroll;
        [SerializeField] private int previewClipIndex;
        private Stack<ProcessingSettings> undoHistory = new Stack<ProcessingSettings>();
        private readonly Stack<ProcessingSettings> redoHistory = new Stack<ProcessingSettings>();
        private ProcessingSettings lastSettings;
        private ProcessingSettings pendingSettings;
        private bool hasPendingSettingChange;
        private bool applyingHistory;

        private const int HistoryLimit = 40;

        [Serializable]
        private struct ProcessingSettings
        {
            public float volumeDb, normalizePeakDb, silenceThresholdDb, trimStart, trimEnd, fadeIn, fadeOut;
            public bool normalize, autoTrimSilence;
            public string outputFolder;
        }

        [MenuItem("Tools/Raven/AudioForge")]
        public static void Open()
        {
            var window = GetWindow<AudioForgeWindow>();
            window.titleContent = new GUIContent("AudioForge");
            window.minSize = new Vector2(760, 520);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            HandleKeyboardHistory();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSource();
            DrawWaveform();
            DrawProcessing();
            DrawOutput();
            EditorGUILayout.EndScrollView();
            CommitPendingSettingChangeIfFinished();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 48, padding = new RectOffset(16, 12, 10, 8) };
            EditorGUILayout.BeginHorizontal(style);
            EditorGUILayout.LabelField("AUDIOFORGE", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(.35f, .85f, 1f) } }, GUILayout.Width(155));
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(undoHistory.Count == 0)) if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(48))) UndoSettings();
            using (new EditorGUI.DisabledScope(redoHistory.Count == 0)) if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(48))) RedoSettings();
            EditorGUILayout.LabelField("Batch audio polish • non-destructive WAV export", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSource()
        {
            Section("Input clips", "Choose a folder or add AudioClips manually. Project selection can be captured in one click.");
            EditorGUILayout.BeginHorizontal();
            useFolder = GUILayout.Toggle(useFolder, "Folder", EditorStyles.miniButtonLeft, GUILayout.Width(74));
            useFolder = !GUILayout.Toggle(!useFolder, "Manual list", EditorStyles.miniButtonRight, GUILayout.Width(95));
            if (useFolder)
            {
                sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(sourceFolder, typeof(DefaultAsset), false);
                includeSubfolders = GUILayout.Toggle(includeSubfolders, "Include subfolders", EditorStyles.miniButton, GUILayout.Width(125));
            }
            else if (GUILayout.Button("Use Project Selection", GUILayout.Width(155))) CaptureSelection();
            EditorGUILayout.EndHorizontal();
            if (!useFolder)
            {
                for (var i = clips.Count - 1; i >= 0; i--)
                {
                    EditorGUILayout.BeginHorizontal();
                    clips[i] = (AudioClip)EditorGUILayout.ObjectField(clips[i], typeof(AudioClip), false);
                    if (GUILayout.Button("×", GUILayout.Width(25))) clips.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add AudioClip", EditorStyles.miniButton)) clips.Add(null);
            }
        }

        private void DrawWaveform()
        {
            var targets = GetTargets();
            if (targets.Count == 0) return;
            previewClipIndex = Mathf.Clamp(previewClipIndex, 0, targets.Count - 1);
            var names = targets.Select(clip => clip.name).ToArray();
            previewClipIndex = EditorGUILayout.Popup("Waveform preview", previewClipIndex, names);
            var previewClip = targets[previewClipIndex];
            var rect = GUILayoutUtility.GetRect(10, 145, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.08f, .1f, .13f));
            if (TryGetProcessedPreview(previewClip, out var processed, out var error))
            {
                DrawWaveform(rect, processed, previewClip.channels);
                EditorGUILayout.LabelField($"Export preview: {previewClip.name}  •  {processed.Length / (float)(previewClip.channels * previewClip.frequency):0.00}s  •  {previewClip.frequency:N0} Hz  •  {previewClip.channels} ch", EditorStyles.centeredGreyMiniLabel);
            }
            else GUI.Label(rect, error, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawProcessing()
        {
            Section("Processing", "Settings are applied to every selected clip when exporting.");
            EditorGUI.BeginChangeCheck();
            volumeDb = EditorGUILayout.Slider("Volume (dB)", volumeDb, -48f, 24f);
            normalize = EditorGUILayout.Toggle("Normalize peak", normalize);
            using (new EditorGUI.DisabledScope(!normalize)) normalizePeakDb = EditorGUILayout.Slider("Target peak (dB)", normalizePeakDb, -12f, 0f);
            EditorGUILayout.Space(4);
            autoTrimSilence = EditorGUILayout.Toggle("Auto-trim silence", autoTrimSilence);
            using (new EditorGUI.DisabledScope(!autoTrimSilence)) silenceThresholdDb = EditorGUILayout.Slider("Silence threshold (dB)", silenceThresholdDb, -80f, -10f);
            using (new EditorGUI.DisabledScope(autoTrimSilence))
            {
                trimStart = EditorGUILayout.FloatField("Trim start (seconds)", Mathf.Max(0f, trimStart));
                trimEnd = EditorGUILayout.FloatField("Trim end (seconds)", Mathf.Max(0f, trimEnd));
            }
            fadeIn = EditorGUILayout.FloatField("Fade in (seconds)", Mathf.Max(0f, fadeIn));
            fadeOut = EditorGUILayout.FloatField("Fade out (seconds)", Mathf.Max(0f, fadeOut));
            if (EditorGUI.EndChangeCheck()) QueueSettingChange();
        }

        private void DrawOutput()
        {
            Section("Output", "Original imported assets remain untouched. AudioForge creates standard PCM WAV files.");
            EditorGUI.BeginChangeCheck();
            outputFolder = EditorGUILayout.TextField("Folder", outputFolder.Replace('\\', '/'));
            if (EditorGUI.EndChangeCheck()) QueueSettingChange();
            var targets = GetTargets();
            EditorGUILayout.LabelField($"{targets.Count} clip{(targets.Count == 1 ? string.Empty : "s")} ready", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                var old = GUI.backgroundColor; GUI.backgroundColor = new Color(.25f, .72f, .96f);
                if (GUILayout.Button("Export Processed WAV Clips", GUILayout.Height(36))) Export(targets);
                GUI.backgroundColor = old;
            }
            EditorGUILayout.HelpBox("Audio data may need a temporary reimport as Decompress On Load so Unity can read it. AudioForge restores the original importer setting after each export.", MessageType.Info);
        }

        private void CaptureSelection()
        {
            clips = Selection.objects.OfType<AudioClip>().Distinct().ToList();
            previewClipIndex = 0;
        }

        private void OnEnable() { lastSettings = CaptureSettings(); }

        private ProcessingSettings CaptureSettings()
        {
            return new ProcessingSettings
            {
                volumeDb = volumeDb, normalize = normalize, normalizePeakDb = normalizePeakDb, autoTrimSilence = autoTrimSilence,
                silenceThresholdDb = silenceThresholdDb, trimStart = trimStart, trimEnd = trimEnd, fadeIn = fadeIn, fadeOut = fadeOut, outputFolder = outputFolder
            };
        }

        private void QueueSettingChange()
        {
            if (applyingHistory) return;
            if (!hasPendingSettingChange)
            {
                pendingSettings = lastSettings;
                hasPendingSettingChange = true;
            }
            lastSettings = CaptureSettings();
        }

        private void CommitPendingSettingChangeIfFinished()
        {
            if (!hasPendingSettingChange || GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField) return;
            undoHistory.Push(pendingSettings);
            while (undoHistory.Count > HistoryLimit) undoHistory = new Stack<ProcessingSettings>(undoHistory.Take(HistoryLimit).Reverse());
            redoHistory.Clear();
            hasPendingSettingChange = false;
            lastSettings = CaptureSettings();
        }

        private void UndoSettings()
        {
            CommitPendingSettingChangeIfFinished();
            if (undoHistory.Count == 0) return;
            redoHistory.Push(CaptureSettings()); ApplySettings(undoHistory.Pop()); Repaint();
        }

        private void RedoSettings()
        {
            CommitPendingSettingChangeIfFinished();
            if (redoHistory.Count == 0) return;
            undoHistory.Push(CaptureSettings()); ApplySettings(redoHistory.Pop()); Repaint();
        }

        private void ApplySettings(ProcessingSettings settings)
        {
            applyingHistory = true;
            volumeDb = settings.volumeDb; normalize = settings.normalize; normalizePeakDb = settings.normalizePeakDb; autoTrimSilence = settings.autoTrimSilence;
            silenceThresholdDb = settings.silenceThresholdDb; trimStart = settings.trimStart; trimEnd = settings.trimEnd; fadeIn = settings.fadeIn; fadeOut = settings.fadeOut; outputFolder = settings.outputFolder;
            lastSettings = settings; applyingHistory = false;
            hasPendingSettingChange = false;
        }

        private void HandleKeyboardHistory()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || !(current.control || current.command) || current.keyCode != KeyCode.Z) return;
            if (current.shift) RedoSettings(); else UndoSettings();
            current.Use();
        }

        private List<AudioClip> GetTargets()
        {
            if (!useFolder) return clips.Where(clip => clip != null).Distinct().ToList();
            if (sourceFolder == null) return new List<AudioClip>();
            var path = AssetDatabase.GetAssetPath(sourceFolder);
            if (!AssetDatabase.IsValidFolder(path)) return new List<AudioClip>();
            var folders = includeSubfolders ? new[] { path } : null;
            var found = AssetDatabase.FindAssets("t:AudioClip", folders).Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => includeSubfolders || string.Equals(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(clip => clip != null).ToList();
            return found;
        }

        private void Export(List<AudioClip> targets)
        {
            outputFolder = outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!outputFolder.StartsWith("Assets/", StringComparison.Ordinal) && outputFolder != "Assets") { EditorUtility.DisplayDialog("Invalid output folder", "Choose a folder inside Assets, for example Assets/AudioForge/Processed.", "OK"); return; }
            EnsureAssetFolder(outputFolder);
            var exported = 0;
            try
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var clip = targets[i];
                    EditorUtility.DisplayProgressBar("AudioForge", $"Processing {clip.name} ({i + 1}/{targets.Count})", (float)i / targets.Count);
                    if (TryExport(clip, out var message)) exported++; else Debug.LogWarning("AudioForge: " + message);
                }
            }
            finally { EditorUtility.ClearProgressBar(); AssetDatabase.Refresh(); }
            EditorUtility.DisplayDialog("AudioForge export complete", $"Exported {exported} of {targets.Count} clip(s) to {outputFolder}.", "OK");
        }

        private bool TryExport(AudioClip originalClip, out string message)
        {
            message = originalClip.name + " could not be read.";
            var path = AssetDatabase.GetAssetPath(originalClip);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioClipLoadType oldLoadType = AudioClipLoadType.DecompressOnLoad;
            var changedImporter = false;
            try
            {
                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings; oldLoadType = settings.loadType;
                    if (settings.loadType != AudioClipLoadType.DecompressOnLoad) { settings.loadType = AudioClipLoadType.DecompressOnLoad; importer.defaultSampleSettings = settings; importer.SaveAndReimport(); changedImporter = true; }
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) return false;
                var result = Process(samples, clip.channels, clip.frequency);
                if (result.Length == 0) { message = originalClip.name + " became empty after trimming."; return false; }
                var destination = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/" + originalClip.name + "_AudioForge.wav");
                WriteWav(Path.GetFullPath(destination), result, clip.channels, clip.frequency);
                return true;
            }
            catch (Exception exception) { message = originalClip.name + ": " + exception.Message; return false; }
            finally
            {
                if (changedImporter && importer != null)
                {
                    var settings = importer.defaultSampleSettings; settings.loadType = oldLoadType; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                }
            }
        }

        private float[] Process(float[] source, int channels, int frequency)
        {
            var frames = source.Length / channels;
            var start = autoTrimSilence ? FindFirstAudibleFrame(source, channels, DbToLinear(silenceThresholdDb)) : Mathf.Clamp(Mathf.FloorToInt(trimStart * frequency), 0, frames);
            var end = autoTrimSilence ? FindLastAudibleFrame(source, channels, DbToLinear(silenceThresholdDb)) : Mathf.Clamp(frames - Mathf.FloorToInt(trimEnd * frequency), start, frames);
            if (end <= start) return Array.Empty<float>();
            var output = new float[(end - start) * channels]; Array.Copy(source, start * channels, output, 0, output.Length);
            var peak = output.Select(Mathf.Abs).DefaultIfEmpty(0f).Max();
            var gain = DbToLinear(volumeDb) * (normalize && peak > 0f ? DbToLinear(normalizePeakDb) / peak : 1f);
            var fadeInFrames = Mathf.Min(Mathf.FloorToInt(fadeIn * frequency), end - start);
            var fadeOutFrames = Mathf.Min(Mathf.FloorToInt(fadeOut * frequency), end - start);
            for (var frame = 0; frame < end - start; frame++)
            {
                var envelope = 1f;
                if (fadeInFrames > 0 && frame < fadeInFrames) envelope *= (float)frame / fadeInFrames;
                if (fadeOutFrames > 0 && frame >= end - start - fadeOutFrames) envelope *= (float)(end - start - frame - 1) / fadeOutFrames;
                for (var channel = 0; channel < channels; channel++) output[frame * channels + channel] = Mathf.Clamp(output[frame * channels + channel] * gain * envelope, -1f, 1f);
            }
            return output;
        }

        private bool TryGetProcessedPreview(AudioClip clip, out float[] processed, out string error)
        {
            processed = null;
            error = "Waveform unavailable: enable Decompress On Load for this clip.";
            try
            {
                var samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) return false;
                processed = Process(samples, clip.channels, clip.frequency);
                if (processed.Length == 0) { error = "The current trim/silence settings would export an empty clip."; return false; }
                return true;
            }
            catch { return false; }
        }

        private static int FindFirstAudibleFrame(float[] data, int channels, float threshold)
        {
            for (var frame = 0; frame < data.Length / channels; frame++) for (var channel = 0; channel < channels; channel++) if (Mathf.Abs(data[frame * channels + channel]) >= threshold) return frame;
            return data.Length / channels;
        }
        private static int FindLastAudibleFrame(float[] data, int channels, float threshold)
        {
            for (var frame = data.Length / channels - 1; frame >= 0; frame--) for (var channel = 0; channel < channels; channel++) if (Mathf.Abs(data[frame * channels + channel]) >= threshold) return frame + 1;
            return 0;
        }
        private static float DbToLinear(float db) { return Mathf.Pow(10f, db / 20f); }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/'); var current = parts[0];
            for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
        private static void WriteWav(string filePath, float[] samples, int channels, int frequency)
        {
            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                const short bitsPerSample = 16; var dataLength = samples.Length * 2;
                writer.Write("RIFF".ToCharArray()); writer.Write(36 + dataLength); writer.Write("WAVE".ToCharArray()); writer.Write("fmt ".ToCharArray()); writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(frequency); writer.Write(frequency * channels * 2); writer.Write((short)(channels * 2)); writer.Write(bitsPerSample); writer.Write("data".ToCharArray()); writer.Write(dataLength);
                foreach (var sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }
        private static void DrawWaveform(Rect rect, float[] data, int channels)
        {
            Handles.color = new Color(.35f, .85f, 1f); var mid = rect.center.y;
            var frames = data.Length / channels;
            var framesPerPixel = Mathf.Max(1, frames / Mathf.Max(1, (int)rect.width));
            for (var x = 0; x < rect.width; x++)
            {
                var firstFrame = x * framesPerPixel;
                var lastFrame = Mathf.Min(firstFrame + framesPerPixel, frames);
                var amplitude = 0f;
                for (var frame = firstFrame; frame < lastFrame; frame++) for (var channel = 0; channel < channels; channel++) amplitude = Mathf.Max(amplitude, Mathf.Abs(data[frame * channels + channel]));
                amplitude *= rect.height * .45f;
                Handles.DrawLine(new Vector3(rect.x + x, mid - amplitude), new Vector3(rect.x + x, mid + amplitude));
            }
        }
        private static void Section(string title, string subtitle) { EditorGUILayout.Space(12); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel); EditorGUILayout.Space(5); }
    }
}
