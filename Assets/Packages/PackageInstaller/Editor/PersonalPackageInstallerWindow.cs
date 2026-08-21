using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Raven.PackageInstaller
{
    public sealed class PersonalPackageInstallerWindow : EditorWindow
    {
        private const string DefaultCatalogPath = "Assets/RavenPackageCatalog.asset";
        private readonly HashSet<string> installedPackageIds = new HashSet<string>();
        private readonly Queue<PersonalPackageDefinition> installQueue = new Queue<PersonalPackageDefinition>();
        private PersonalPackageCatalog catalog;
        private ListRequest listRequest;
        private AddRequest addRequest;
        private PersonalPackageDefinition installingPackage;
        private Vector2 scroll;
        private string message;

        [MenuItem("Tools/Raven/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<PersonalPackageInstallerWindow>();
            window.titleContent = new GUIContent("Package Installer");
            window.minSize = new Vector2(620, 400);
            window.Show();
        }

        private void OnEnable()
        {
            FindOrCreateCatalog();
            RefreshInstalledPackages();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, addRequest != null ? MessageType.Info : MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Create or select a Personal Package Catalog to get started.", MessageType.Info);
            }
            else
            {
                for (var i = 0; i < catalog.packages.Count; i++) DrawPackage(catalog.packages[i], i);
                if (GUILayout.Button("+ Add personal package"))
                {
                    catalog.packages.Add(new PersonalPackageDefinition { displayName = "New Package" });
                    SaveCatalog();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("PERSONAL GIT PACKAGES", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(listRequest != null || addRequest != null))
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) RefreshInstalledPackages();
                if (GUILayout.Button("Select Catalog", EditorStyles.toolbarButton, GUILayout.Width(85))) Selection.activeObject = catalog;
            }
        }

        private void DrawPackage(PersonalPackageDefinition package, int index)
        {
            var installed = !string.IsNullOrWhiteSpace(package.packageId) && installedPackageIds.Contains(package.packageId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    package.displayName = EditorGUILayout.TextField(package.displayName, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (installed) GUILayout.Label("Installed", EditorStyles.miniButton, GUILayout.Width(70));
                    else using (new EditorGUI.DisabledScope(addRequest != null || string.IsNullOrWhiteSpace(package.gitUrl)))
                        if (GUILayout.Button("Install", GUILayout.Width(70))) QueueInstall(package);
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(58)))
                    {
                        catalog.packages.RemoveAt(index);
                        SaveCatalog();
                        GUIUtility.ExitGUI();
                    }
                }
                package.gitUrl = EditorGUILayout.TextField("Git URL", package.gitUrl);
                package.description = EditorGUILayout.TextField("Description", package.description);
                if (GUI.changed) SaveCatalog();
            }
        }

        private void FindOrCreateCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:PersonalPackageCatalog");
            if (guids.Length > 0) catalog = AssetDatabase.LoadAssetAtPath<PersonalPackageCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (catalog != null)
            {
                AddMissingDefaultPackages();
                return;
            }

            catalog = CreateInstance<PersonalPackageCatalog>();
            catalog.packages.AddRange(PersonalPackageCatalog.DefaultPackages());
            AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
            AssetDatabase.SaveAssets();
        }

        private void AddMissingDefaultPackages()
        {
            var changed = false;
            foreach (var defaultPackage in PersonalPackageCatalog.DefaultPackages())
            {
                if (catalog.packages.Any(package => package.packageId == defaultPackage.packageId)) continue;
                catalog.packages.Add(defaultPackage);
                changed = true;
            }
            if (changed) SaveCatalog();
        }

        private void SaveCatalog()
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private void RefreshInstalledPackages()
        {
            listRequest = Client.List(true);
            message = "Checking installed packages...";
            SubscribeToUpdate();
        }

        private void QueueInstall(PersonalPackageDefinition package)
        {
            installQueue.Enqueue(package);
            InstallNext();
        }

        private void InstallNext()
        {
            if (addRequest != null || installQueue.Count == 0) return;
            installingPackage = installQueue.Dequeue();
            addRequest = Client.Add(installingPackage.gitUrl);
            message = "Installing " + installingPackage.displayName + "...";
            SubscribeToUpdate();
        }

        private void SubscribeToUpdate()
        {
            EditorApplication.update -= PollRequests;
            EditorApplication.update += PollRequests;
        }

        private void PollRequests()
        {
            if (listRequest != null && listRequest.IsCompleted)
            {
                if (listRequest.Status == StatusCode.Success)
                {
                    installedPackageIds.Clear();
                    foreach (var package in listRequest.Result) installedPackageIds.Add(package.name);
                    message = "Package list updated.";
                }
                else message = "Could not read installed packages: " + listRequest.Error.message;
                listRequest = null;
            }

            if (addRequest != null && addRequest.IsCompleted)
            {
                if (addRequest.Status == StatusCode.Success)
                {
                    installingPackage.packageId = addRequest.Result.name;
                    installedPackageIds.Add(installingPackage.packageId);
                    SaveCatalog();
                    message = "Installed " + installingPackage.displayName + ".";
                }
                else
                {
                    message = "Could not install " + installingPackage.displayName + ": " + addRequest.Error.message;
                    installQueue.Clear();
                }
                addRequest = null;
                installingPackage = null;
                InstallNext();
            }

            Repaint();
            if (listRequest == null && addRequest == null) EditorApplication.update -= PollRequests;
        }

        private void OnDisable() => EditorApplication.update -= PollRequests;
    }
}
