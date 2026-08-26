using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Raven12345
{
public class HelpfulButtons : EditorWindow
{
    private const string SceneSearchPath = "Assets";
    private List<string> sceneNames;

    private DropdownField selectedScene;
    private Color backgroundColor;

    [MenuItem("Tools/Raven/Helpful Buttons")]
    private static void OpenWindow()
    {
        GetWindow<HelpfulButtons>().Show();
    }

    
    protected void OnEnable()
    {
        ColorUtility.TryParseHtmlString("#272B33", out backgroundColor);
        LoadSceneData();
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        root.Clear();

        var changeSceneGroup = new VisualElement();
        changeSceneGroup.style.flexDirection = FlexDirection.Row;
        changeSceneGroup.style.alignItems = Align.Center;
        changeSceneGroup.style.justifyContent = Justify.SpaceBetween;
        changeSceneGroup.style.backgroundColor = backgroundColor;
        changeSceneGroup.style.paddingLeft = 10;
        changeSceneGroup.style.paddingRight = 10;
        changeSceneGroup.style.height = 50; 
        changeSceneGroup.style.marginBottom = 10;

        changeSceneGroup.Add(selectedScene);
        Button changeSceneBtn = new Button(() =>
        {
            LoadScene(selectedScene.value);
        })
        {
            text = "Change"
        };
        changeSceneBtn.style.height = 30f;
        changeSceneBtn.style.flexGrow = .25f;
        changeSceneBtn.style.borderBottomLeftRadius = 5f;
        changeSceneBtn.style.borderBottomRightRadius = 5f;
        changeSceneBtn.style.borderTopLeftRadius = 5f;
        changeSceneBtn.style.borderTopRightRadius = 5f;

        changeSceneBtn.RegisterCallback<MouseEnterEvent>(evt =>
        {
            changeSceneBtn.style.color = Color.black;
        });
        changeSceneBtn.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            changeSceneBtn.style.color = Color.white;
        });

        changeSceneGroup.Add(changeSceneBtn);
        root.Add(changeSceneGroup);

        var utilityArea = new VisualElement();
        utilityArea.style.backgroundColor = new StyleColor(Color.gray);
        utilityArea.style.flexDirection = FlexDirection.Row;
        utilityArea.style.paddingLeft = utilityArea.style.paddingRight = 5;
        utilityArea.style.paddingTop = utilityArea.style.paddingBottom = 5;
        utilityArea.style.backgroundColor = backgroundColor;

        Button clearDataBtn = CreateButton(ClearData, "Clear Data", Color.red, 1f);
        utilityArea.Add(clearDataBtn);

        Button removeMissingScript = CreateButton(DeleteFaildScript, "Remove Missing", Color.red, 1f);
        utilityArea.Add(removeMissingScript);

        root.Add(utilityArea);
    }
    private Button CreateButton(System.Action action, string buttonName, Color borderColor, float flexGrow)
    {
        Button button = new Button(action)
        {
            text = buttonName,
        };
        button.style.borderBottomColor = borderColor;
        button.style.flexGrow = flexGrow;

        return button;
    }

    private void LoadSceneData()
    {
        sceneNames = new List<string>();
        var loads = AssetDatabase.FindAssets("t:Scene", new[] { SceneSearchPath });

        foreach (var scene in loads)
        {
            sceneNames.Add(AssetDatabase.GUIDToAssetPath(scene));
        }
        selectedScene = new DropdownField("Scene", sceneNames, 0);
        selectedScene.style.minWidth = StyleKeyword.Auto;

        selectedScene.style.height = 30f;
        selectedScene.style.flexGrow = .75f;

        selectedScene.labelElement.style.minWidth = StyleKeyword.Auto;
        selectedScene.labelElement.style.alignSelf = Align.Center;
        selectedScene.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;

        static string formatValue(string value)
        {
            string[] replaces = value.Split('/');
            return replaces[^1].Replace(".unity", "");
        }

        selectedScene.formatSelectedValueCallback = formatValue;
        selectedScene.formatListItemCallback = formatValue;
    }

    private void LoadScene(string scenePath)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
    }

    private void ClearData() 
    {
        System.IO.DirectoryInfo di = new DirectoryInfo(Application.persistentDataPath);

        foreach (FileInfo file in di.GetFiles())
            file.Delete();
        foreach (DirectoryInfo dir in di.GetDirectories())
            dir.Delete(true);
        PlayerPrefs.DeleteAll();
    }
    private void DeleteFaildScript()
    {
        Object[] objects = UnityEditor.Selection.objects;
        foreach(Object obj in objects)
        {
            GameObject g_Obj = (GameObject)obj;
            if(g_Obj != null)
            {
                RemoveMissingScript(g_Obj);
            }
        }
    }

    private void RemoveMissingScript(GameObject obj)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);

        foreach (Transform t in obj.transform)
        {
            RemoveMissingScript(t.gameObject);
        }
    }

    //[Button]
    private void OpenGooglePlay()
    {
        Application.OpenURL("https://play.google.com/store/apps/details?id=" + Application.identifier);
    }

}
}
