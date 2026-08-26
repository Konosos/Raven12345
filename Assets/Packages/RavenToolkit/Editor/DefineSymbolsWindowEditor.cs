using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Raven12345
{
public class DefineSymbolsWindowEditor : EditorWindow
{
    private readonly List<string> DefaultScriptingDefineSymbols = new List<string>()
    {
        "ENABLE_DEBUG",
        "APPLOVIN",
        "IRONSOURCE",
        "APPSFLYER",
        "ADJUST",
        "FIREBASE",
        "NO_ADS",
        "ADMOB_ID_TEST"
    };

    private readonly string SavePath = "UserSettings/RavenToolkit.DefineSymbols.txt";

    private DropdownField selectedSymbol;
    private Color backgroundColor;

    private string scriptingSymbols;
    private string customScriptingSymbols;
    private bool isStarted;
    private List<string> defineSymbols;

    [MenuItem("Tools/Raven/Define Symbols")]
    public static void ShowExample()
    {
        DefineSymbolsWindowEditor wnd = GetWindow<DefineSymbolsWindowEditor>();
        wnd.titleContent = new GUIContent("Define Symbols");
    }

    protected void OnEnable()
    {
        if (!File.Exists(SavePath))
        {
            File.WriteAllText(SavePath, string.Join(",", DefaultScriptingDefineSymbols));
            defineSymbols = new List<string>(DefaultScriptingDefineSymbols);
        }
        else
        {
            string save = File.ReadAllText(SavePath);
            defineSymbols = save.TrimEnd(',').Split(',').ToList();
        }
        isStarted = false;
        customScriptingSymbols = string.Empty;

        selectedSymbol = new DropdownField("Symbol", defineSymbols, 0);
        selectedSymbol.RegisterValueChangedCallback(evt =>
        {
            CreateGUI();
        });

        selectedSymbol.style.minWidth = StyleKeyword.Auto;
        selectedSymbol.style.height = 30f;
        selectedSymbol.style.flexGrow = .75f;

        selectedSymbol.labelElement.style.minWidth = StyleKeyword.Auto;
        selectedSymbol.labelElement.style.alignSelf = Align.Center;
        selectedSymbol.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;

        ColorUtility.TryParseHtmlString("#272B33", out backgroundColor);
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        root.Clear();
        root.style.justifyContent = Justify.Center;
        if (isStarted)
        {
            var selectSymbolsGroup = CreateRowHolder();

            selectSymbolsGroup.Add(selectedSymbol);

            if (customScriptingSymbols.ContainsDefineSymbol(selectedSymbol.value))
            {
                Button deleteButton = new Button(() =>
                {
                    customScriptingSymbols = customScriptingSymbols.RemoveDefineSymbol(selectedSymbol.value);
                    CreateGUI();
                    //ScriptingDefineSymbolsManager.RemoveDefineSymbol(selectedSymbol.value);
                })
                {
                    text = "Delete",
                };

                deleteButton.style.borderBottomColor = Color.red;
                deleteButton.style.height = 30f;
                deleteButton.style.flexGrow = .25f;
                deleteButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                selectSymbolsGroup.Add(deleteButton);
            }
            else
            {
                Button addButton = new Button(() =>
                {
                    customScriptingSymbols = customScriptingSymbols.AddDefineSymbol(selectedSymbol.value);
                    CreateGUI();
                    //ScriptingDefineSymbolsManager.AddDefineSymbol(selectedSymbol.value);
                })
                {
                    text = "Add",
                };

                addButton.style.borderBottomColor = Color.green;
                addButton.style.height = 30f;
                addButton.style.flexGrow = .25f;
                addButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                selectSymbolsGroup.Add(addButton);
            }

            root.Add(selectSymbolsGroup);

            var buttonHolder = CreateRowHolder();

            Button cancelButton = new Button(() =>
            {
                isStarted = false;
                scriptingSymbols = string.Empty;
                customScriptingSymbols = string.Empty;
                CreateGUI();
            })
            {
                text = "Cancel"
            };

            cancelButton.style.borderBottomColor = Color.red;
            cancelButton.style.height = 30f;
            cancelButton.style.flexGrow = 1f;
            cancelButton.style.unityFontStyleAndWeight = FontStyle.Bold;

            buttonHolder.Add(cancelButton);

            if (customScriptingSymbols != scriptingSymbols)
            {
                Button applyButton = new Button(() =>
                {
                    customScriptingSymbols.ApplyDefineSymbol();
                    isStarted = false;
                    scriptingSymbols = string.Empty;
                    customScriptingSymbols = string.Empty;
                    CreateGUI();
                })
                {
                    text = "Apply"
                };
                applyButton.style.borderBottomColor = Color.green;
                applyButton.style.height = 30f;
                applyButton.style.flexGrow = 1f;
                applyButton.style.unityFontStyleAndWeight = FontStyle.Bold;

                buttonHolder.Add(applyButton);
            }

            root.Add(buttonHolder);
        }
        else
        {
            Button startButton = new Button(() =>
            {
                scriptingSymbols = PlayerSettings.GetScriptingDefineSymbols(ScriptingDefineSymbolsManager.GetActiveBuildTargetGroup());
                customScriptingSymbols = scriptingSymbols;
                isStarted = true;
                CreateGUI();
            })
            {
                text = "Custom Scripting Symbols"
            };

            startButton.style.borderBottomColor = Color.green;
            startButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            startButton.style.height = Length.Percent(25);
            startButton.style.width = Length.Percent(75);
            startButton.style.alignSelf = Align.Center;
            startButton.style.paddingLeft = 10;
            startButton.style.paddingRight = 10;
            startButton.style.marginBottom = 10;

            root.Add(startButton);

            var defineSymbolsHandle = CreateRowHolder();

            TextField stringField = new TextField();
            stringField.style.height = 30;
            stringField.style.flexGrow = .7f;
            //stringField.style.flexDirection = FlexDirection.Column;

            defineSymbolsHandle.Add(stringField);

            Button selectListHandleButton = new Button(() =>
            {
                if (stringField.value != null && stringField.value != string.Empty)
                {
                    if (defineSymbols.Contains(stringField.value))
                    {
                        defineSymbols.Remove(stringField.value);
                    }
                    else
                    {
                        defineSymbols.Add(stringField.value);
                    }

                    File.WriteAllText(SavePath, string.Join(",", defineSymbols));
                }
            })
            {
                text = "Selecter Edit"
            };

            selectListHandleButton.style.borderBottomColor = Color.yellow;
            selectListHandleButton.style.height = 30f;
            selectListHandleButton.style.flexGrow = .2f;
            selectListHandleButton.style.unityFontStyleAndWeight = FontStyle.Bold;

            defineSymbolsHandle.Add(selectListHandleButton);

            Button logButton = new Button(() =>
            {
                Debug.Log(string.Join(",", defineSymbols));
            })
            {
                text = "Log"
            };
            logButton.style.borderBottomColor = Color.gray;
            logButton.style.height = 30f;
            logButton.style.flexGrow = .1f;
            logButton.style.unityFontStyleAndWeight = FontStyle.Bold;

            defineSymbolsHandle.Add(logButton);

            root.Add(defineSymbolsHandle);
        }
    }

    private VisualElement CreateRowHolder()
    {
        var rowHolder = new VisualElement();
        rowHolder.style.flexDirection = FlexDirection.Row;
        rowHolder.style.alignItems = Align.Center;
        rowHolder.style.justifyContent = Justify.SpaceBetween;
        rowHolder.style.backgroundColor = backgroundColor;
        rowHolder.style.paddingLeft = 10;
        rowHolder.style.paddingRight = 10;
        rowHolder.style.height = 50;
        rowHolder.style.marginBottom = 10;
        return rowHolder;
    }
}
}
