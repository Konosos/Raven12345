using UnityEditor;
using UnityEngine;
using System.IO;

namespace Raven12345
{
public class ScriptEditorWindow : EditorWindow
{
    private string scriptPath;
    private string scriptContent;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Raven/Script Editor")]
    public static void ShowWindow()
    {
        GetWindow<ScriptEditorWindow>("Script Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Script Editor", EditorStyles.boldLabel);

        if (GUILayout.Button("Open Script"))
        {
            string path = EditorUtility.OpenFilePanel("Open Script", Application.dataPath, "cs");
            if (!string.IsNullOrEmpty(path))
            {
                scriptPath = path;
                scriptContent = File.ReadAllText(scriptPath);
            }
        }

        if (!string.IsNullOrEmpty(scriptPath))
        {
            GUILayout.Label("Editing: " + Path.GetFileName(scriptPath));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            scriptContent = EditorGUILayout.TextArea(scriptContent, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            if (GUILayout.Button("Save Script"))
            {
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();
            }
        }
    }
}
}
