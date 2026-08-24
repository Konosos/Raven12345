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
                    if (listRequest.Result != null)
                    {
                        foreach (var package in listRequest.Result)
                        {
                            if (package != null && !string.IsNullOrEmpty(package.name)) installedPackageIds.Add(package.name);
                        }
                    }
                    message = "Package list updated.";
                }
                else message = "Could not read installed packages: " + GetRequestError(listRequest.Error);
                listRequest = null;
            }

            if (addRequest != null && addRequest.IsCompleted)
            {
                // A package installation may cause an assembly/domain reload. In that case,
                // Unity can retain the native request while the window's managed fields are reset.
                // Do not assume the request result, error, or original catalogue entry still exists.
                var completedRequest = addRequest;
                var package = installingPackage;
                addRequest = null;
                installingPackage = null;
                var packageName = package != null && !string.IsNullOrEmpty(package.displayName) ? package.displayName : "package";

                if (completedRequest.Status == StatusCode.Success)
                {
                    if (package != null && completedRequest.Result != null)
                    {
                        package.packageId = completedRequest.Result.name;
                        if (!string.IsNullOrEmpty(package.packageId)) installedPackageIds.Add(package.packageId);
                        SaveCatalog();
                    }
                    message = "Installed " + packageName + ".";
                }
                else
                {
                    message = "Could not install " + packageName + ": " + GetRequestError(completedRequest.Error);
                    installQueue.Clear();
                }
                InstallNext();
            }

            Repaint();
            if (listRequest == null && addRequest == null) EditorApplication.update -= PollRequests;
        }

        private static string GetRequestError(Error error)
        {
            return error != null && !string.IsNullOrEmpty(error.message) ? error.message : "Unity Package Manager did not provide an error message.";
        }

        private void OnDisable() => EditorApplication.update -= PollRequests;
    }
}
