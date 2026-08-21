using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Raven.PackageInstaller
{
    [Serializable]
    public sealed class PersonalPackageDefinition
    {
        public string displayName;
        public string packageId;
        [TextArea] public string gitUrl;
        [TextArea] public string description;
    }

    /// <summary>Project-owned catalogue, kept outside the installed package so teams can customise it.</summary>
    public sealed class PersonalPackageCatalog : ScriptableObject
    {
        public List<PersonalPackageDefinition> packages = new List<PersonalPackageDefinition>();

        [MenuItem("Assets/Create/Raven/Personal Package Catalog")]
        private static void CreateCatalog()
        {
            var catalog = CreateInstance<PersonalPackageCatalog>();
            catalog.packages.AddRange(DefaultPackages());
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/RavenPackageCatalog.asset");
            AssetDatabase.CreateAsset(catalog, path);
            Selection.activeObject = catalog;
        }

        public static IEnumerable<PersonalPackageDefinition> DefaultPackages()
        {
            const string repository = "https://github.com/Konosos/Raven12345.git?path=Assets/Packages/";
            yield return new PersonalPackageDefinition { displayName = "SpriteForge", packageId = "com.raven12345.spriteforge", gitUrl = repository + "SpriteForge", description = "2D sprite production workflows." };
            yield return new PersonalPackageDefinition { displayName = "Service Container", packageId = "com.raven12345.servicecontainer", gitUrl = repository + "ServiceContainer", description = "Lightweight dependency-injection container." };
            yield return new PersonalPackageDefinition { displayName = "Spine Asset Studio", packageId = "com.raven12345.spine-asset-studio", gitUrl = repository + "SpineAssetStudio", description = "Tools for Spine assets and event timelines." };
            yield return new PersonalPackageDefinition { displayName = "UniTask", packageId = "com.cysharp.unitask", gitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask", description = "High-performance async/await integration for Unity." };
            yield return new PersonalPackageDefinition { displayName = "NuGetForUnity", packageId = "com.github-glitchenzo.nugetforunity", gitUrl = "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity", description = "Manage NuGet dependencies in Unity." };
            yield return new PersonalPackageDefinition { displayName = "Mob Sakai UI Effect", packageId = "com.coffee.ui-effect", gitUrl = "https://github.com/mob-sakai/UIEffect.git?path=Packages/src", description = "Inspector-driven visual effects for uGUI." };
            yield return new PersonalPackageDefinition { displayName = "VContainer", packageId = "jp.hadashikick.vcontainer", gitUrl = "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer", description = "Fast dependency-injection container for Unity." };
        }
    }
}
