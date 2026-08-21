# AudioForge

AudioForge is a Unity Editor tool for non-destructive batch audio polishing. It exports newly processed WAV clips and never overwrites the source AudioClip assets.

## Open the tool

In Unity, select **Tools > Raven > AudioForge**.

## Features

- Batch input from a project folder or a manual AudioClip list.
- Volume adjustment in dB.
- Peak normalization with a configurable target peak.
- Automatic silence trimming or manual start/end trimming.
- Fade in and fade out.
- Export-result waveform preview: the display includes the active trim, gain, normalize, and fade settings.
- Per-window setting Undo/Redo with `Ctrl/Cmd + Z` and `Ctrl/Cmd + Shift + Z`.
- Standard 16-bit PCM WAV export, compatible with Unity's audio importer.

## Workflow

1. Choose **Folder** or **Manual list** under **Input clips**.
2. Pick a clip in the **Waveform preview** dropdown to inspect the predicted exported waveform.
3. Configure volume, normalization, trimming, and fades.
4. Set an output folder inside `Assets` (default: `Assets/AudioForge/Processed`).
5. Select **Export Processed WAV Clips**.

AudioForge uses a unique filename for each exported file, so previous exports and original source files are retained.

## Import compatibility

AudioClip sample data must be accessible for waveform preview and processing. If an imported clip is compressed or streamed, AudioForge temporarily switches its importer to **Decompress On Load** while exporting, then restores the original load type. For a live waveform preview of such a clip, set its **Load Type** to **Decompress On Load** in Unity's Audio Import Settings.

## Notes

- The silence threshold is measured in dB relative to full scale. A lower value keeps more quiet audio; `-45 dB` is the default.
- Normalization is applied after trimming, then the volume adjustment and fade envelope are applied.
- Undo/Redo stores editing settings for the current AudioForge window session. It does not delete or alter already exported files.
