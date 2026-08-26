using System.IO;
using System.Linq;
using Unity.Android.Types;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Raven12345
{
public class AndroidBuildWindowEditor : EditorWindow
{
    private readonly string SavePath = "UserSettings/RavenToolkit.AndroidBuildPath.txt";
    private const string VisualTreeAssetPath = "Packages/com.raven12345.raventoolkit/Editor/AndroidBuildUIDocument.uxml";
    string directory = default;

    IntegerField bundleCode;
    TextField version;
    Toggle buildAppBundle;
    Toggle devBuild;
    Button buildButton;
    EnumField symbolsFile;

    [MenuItem("Tools/Raven/Android Build Settings")]
    public static void Open()
    {
        AndroidBuildWindowEditor androidBuildWindowEditor = GetWindow<AndroidBuildWindowEditor>();
        androidBuildWindowEditor.titleContent = new GUIContent("Android Build Setting");
    }
    private void Awake()
    {
        AutoKeystore();
    }
    private void OnEnable()
    {
        if (File.Exists(SavePath))
        {
            directory = File.ReadAllText(SavePath);
        }
    }
    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreeAssetPath);
        if (visualTreeAsset == null)
        {
            Debug.LogError($"Could not load Android build UI from '{VisualTreeAssetPath}'.");
            return;
        }

        visualTreeAsset.CloneTree(root);

        bundleCode = root.Q<IntegerField>("BundleCode");
        version = root.Q<TextField>("Version");
        buildAppBundle = root.Q<Toggle>("BuildAppBundle");
        devBuild = root.Q<Toggle>("DevelopmentBuild");
        symbolsFile = root.Q<EnumField>("SymbolsFile");
        buildButton = root.Q<Button>("BuildButton");

        bundleCode.RegisterValueChangedCallback(value =>
        {
            if (value.newValue < 0)
                bundleCode.value = value.previousValue;
            else
                PlayerSettings.Android.bundleVersionCode = value.newValue;
        });

        version.RegisterValueChangedCallback(value =>
        {
            PlayerSettings.bundleVersion = value.newValue;
        });

        buildAppBundle.RegisterValueChangedCallback(value =>
        {
            EditorUserBuildSettings.buildAppBundle = value.newValue;
        });

        devBuild.RegisterValueChangedCallback(value =>
        {
            EditorUserBuildSettings.development = value.newValue;
        });

        symbolsFile.RegisterValueChangedCallback(value =>
        {
            //EditorUserBuildSettings.androidCreateSymbols = (AndroidCreateSymbols)value.newValue;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = (DebugSymbolLevel)value.newValue;
        });

        buildButton.RegisterCallback<ClickEvent>(BuildButtonClick);

        bundleCode.value = PlayerSettings.Android.bundleVersionCode;
        version.value = PlayerSettings.bundleVersion;
        buildAppBundle.value = EditorUserBuildSettings.buildAppBundle;
        devBuild.value = EditorUserBuildSettings.development;
        symbolsFile.value = UnityEditor.Android.UserBuildSettings.DebugSymbols.level;
    }
    private void AutoKeystore()
    {
        string folderPath = Directory.GetParent(Application.dataPath).FullName;

        // .keystore
        var keystoreFiles = Directory
            .GetFiles(folderPath)
            .Where(f => f.EndsWith(".keystore")/* || f.EndsWith(".jks")*/)
            .ToArray();

        if (keystoreFiles.Length == 0)
        {
            Debug.Log("keystore not exits");
            return;
        }

        // 
        string keystorePath = keystoreFiles[0];

        // 
        PlayerSettings.Android.keystoreName = keystorePath;
        var parts = keystorePath.Split("\\");
        string pass = default;
        if (parts.Length > 0)
            pass = parts[^1].Replace(".keystore", "");

        PlayerSettings.Android.keystorePass = pass;
        PlayerSettings.Android.keyaliasName = pass;
        PlayerSettings.Android.keyaliasPass = pass;
    }
    private void BuildButtonClick(ClickEvent clickEvent)
    {
        string extension = buildAppBundle.value ? "aab" : "apk";
        string defaultFileName = PlayerSettings.productName.Replace(":", "").Replace(" ", "_")
            + $"_v{version.value}"
            + (devBuild.value ? "-devmode" : "")
            + $".{extension}";
        
        string savePath = EditorUtility.SaveFilePanel(
            "Build Android",
            directory,
            defaultFileName,
            extension
        );

        if (string.IsNullOrEmpty(savePath))
        {
            Debug.Log("Build cancelled.");
            return;
        }

        string newDirectory = Path.GetDirectoryName(savePath);
        if (newDirectory != directory)
        {
            File.WriteAllText(SavePath, newDirectory);
            directory = newDirectory;
        }

        PlayerSettings.Android.bundleVersionCode = bundleCode.value;
        PlayerSettings.bundleVersion = version.value;

        BuildOptions buildOptions = BuildOptions.None;

        EditorUserBuildSettings.buildAppBundle = buildAppBundle.value;

        //EditorUserBuildSettings.androidCreateSymbols = (AndroidCreateSymbols)symbolsFile.value;
        UnityEditor.Android.UserBuildSettings.DebugSymbols.level = (DebugSymbolLevel)symbolsFile.value;

        EditorUserBuildSettings.development = devBuild.value;
        if (devBuild.value)
        {
            buildOptions |= BuildOptions.Development;
        }

        string[] scenes = GetActiveSceneList(EditorBuildSettings.scenes);

        BuildReport report = BuildPipeline.BuildPlayer(scenes, savePath, BuildTarget.Android, buildOptions);

        PrintDetailedBuildLog(report);

        if (report.summary.result == BuildResult.Succeeded)
        {
            OpenBuildFolder(savePath);
        }
    }

    private static void PrintDetailedBuildLog(BuildReport report)
    {
        Debug.Log("==== Build Summary ====");
        Debug.Log($"Result: {report.summary.result}");
        Debug.Log($"Platform: {report.summary.platform}");
        Debug.Log($"Output Path: {report.summary.outputPath}");
        Debug.Log($"Total Errors: {report.summary.totalErrors}");
        Debug.Log($"Total Warnings: {report.summary.totalWarnings}");
        Debug.Log($"Total Size: {report.summary.totalSize / 1_000_000} MB");
        Debug.Log($"Build Started: {report.summary.buildStartedAt}");
        Debug.Log($"Build Ended: {report.summary.buildEndedAt}");
        Debug.Log($"Total Time: {report.summary.totalTime}");

        Debug.Log("\n==== Build Steps ====");
        foreach (var step in report.steps)
        {
            Debug.Log($"Step: {step.name}, Duration: {step.duration}");

            foreach (var message in step.messages)
            {
                switch (message.type)
                {
                    case LogType.Error:
                        Debug.LogError($"[Error in {step.name}] {message.content}");
                        break;
                    case LogType.Warning:
                        Debug.LogWarning($"[Warning in {step.name}] {message.content}");
                        break;
                    case LogType.Log:
                        Debug.Log($"[Info in {step.name}] {message.content}");
                        break;
                }
            }
        }

        if (report.summary.result == BuildResult.Failed)
        {
            Debug.LogError("Build failed! Check errors in the steps above.");
        }
    }

    public static string[] GetActiveSceneList(EditorBuildSettingsScene[] scenes)
    {
        System.Collections.Generic.List<string> enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled)
            {
                enabledScenes.Add(scene.path);
            }
        }
        return enabledScenes.ToArray();
    }

    private static void OpenBuildFolder(string buildPath)
    {
        string folderPath = System.IO.Path.GetDirectoryName(buildPath);

        if (!string.IsNullOrEmpty(folderPath))
        {
            System.Diagnostics.Process.Start(folderPath);
        }
        else
        {
            Debug.LogError("Could not determine folder path for build.");
        }
    }
}
}
