using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Raven12345
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
        private string searchText = string.Empty;
        private bool showInstalledOnly;
        private bool editCatalog;

        [MenuItem("Tools/Raven/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<PersonalPackageInstallerWindow>();
            window.titleContent = new GUIContent("Package Installer");
            window.minSize = new Vector2(660, 440);
            window.Show();
        }

        private void OnEnable()
        {
            FindOrCreateCatalog();
            RefreshInstalledPackages();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawFilters();
            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, addRequest != null || listRequest != null ? MessageType.Info : MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Create or select a Personal Package Catalog to get started.", MessageType.Info);
            }
            else
            {
                var visiblePackages = catalog.packages
                    .Select((package, index) => new { package, index })
                    .Where(item => IsVisible(item.package))
                    .ToList();

                if (visiblePackages.Count == 0)
                    EditorGUILayout.HelpBox("No packages match the current filter.", MessageType.Info);

                foreach (var item in visiblePackages) DrawPackage(item.package, item.index);
                if (editCatalog && GUILayout.Button("+  Add personal package", GUILayout.Height(28)))
                {
                    catalog.packages.Add(new PersonalPackageDefinition { displayName = "New Package" });
                    SaveCatalog();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(1, 82, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0.10f, 0.14f, 0.21f));

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, normal = { textColor = new Color(0.38f, 0.78f, 1f) } };
            var subtitleStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.72f, 0.78f, 0.85f) } };
            GUI.Label(new Rect(headerRect.x + 16, headerRect.y + 13, 350, 24), "PACKAGE INSTALLER", titleStyle);
            GUI.Label(new Rect(headerRect.x + 16, headerRect.y + 41, 420, 18), "Your personal Git package catalogue", subtitleStyle);

            var count = catalog == null ? 0 : catalog.packages.Count;
            var installed = catalog == null ? 0 : catalog.packages.Count(package => installedPackageIds.Contains(package.packageId));
            GUI.Label(new Rect(headerRect.xMax - 225, headerRect.y + 17, 94, 18), $"{count} packages", subtitleStyle);
            GUI.Label(new Rect(headerRect.xMax - 225, headerRect.y + 41, 100, 18), $"{installed} installed", subtitleStyle);

            using (new EditorGUI.DisabledScope(listRequest != null || addRequest != null))
            {
                if (GUI.Button(new Rect(headerRect.xMax - 116, headerRect.y + 23, 100, 34), "Refresh")) RefreshInstalledPackages();
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(28)))
            {
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(42));
                searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160));
                if (!string.IsNullOrEmpty(searchText) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22))) searchText = string.Empty;
                GUILayout.Space(8);
                showInstalledOnly = GUILayout.Toggle(showInstalledOnly, "Installed only", EditorStyles.toolbarButton, GUILayout.Width(94));
                GUILayout.FlexibleSpace();
                editCatalog = GUILayout.Toggle(editCatalog, "Edit catalogue", EditorStyles.toolbarButton, GUILayout.Width(95));
                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(80))) Selection.activeObject = catalog;
            }
        }

        private bool IsVisible(PersonalPackageDefinition package)
        {
            if (showInstalledOnly && !installedPackageIds.Contains(package.packageId)) return false;
            if (string.IsNullOrWhiteSpace(searchText)) return true;
            var query = searchText.Trim();
            return (package.displayName ?? string.Empty).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (package.description ?? string.Empty).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawPackage(PersonalPackageDefinition package, int index)
        {
            var installed = !string.IsNullOrWhiteSpace(package.packageId) && installedPackageIds.Contains(package.packageId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
                    if (editCatalog)
                    {
                        EditorGUI.BeginChangeCheck();
                        package.displayName = EditorGUILayout.TextField(package.displayName, nameStyle);
                        if (EditorGUI.EndChangeCheck()) SaveCatalog();
                    }
                    else EditorGUILayout.LabelField(package.displayName, nameStyle);
                    GUILayout.FlexibleSpace();
                    if (ReferenceEquals(package, installingPackage)) GUILayout.Label("Installing…", EditorStyles.miniButton, GUILayout.Width(76));
                    else if (installed) GUILayout.Label("✓ Installed", EditorStyles.miniButton, GUILayout.Width(76));
                    else using (new EditorGUI.DisabledScope(addRequest != null || string.IsNullOrWhiteSpace(package.gitUrl)))
                        if (GUILayout.Button("Install", GUILayout.Width(76), GUILayout.Height(22))) QueueInstall(package);
                    if (editCatalog && GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(58)))
                    {
                        catalog.packages.RemoveAt(index);
                        SaveCatalog();
                        GUIUtility.ExitGUI();
                    }
                }
                if (editCatalog)
                {
                    EditorGUI.BeginChangeCheck();
                    package.description = EditorGUILayout.TextField("Description", package.description);
                    package.gitUrl = EditorGUILayout.TextField("Git URL", package.gitUrl);
                    if (EditorGUI.EndChangeCheck()) SaveCatalog();
                }
                else
                {
                    EditorGUILayout.LabelField(package.description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(package.gitUrl, EditorStyles.miniLabel);
                }
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
