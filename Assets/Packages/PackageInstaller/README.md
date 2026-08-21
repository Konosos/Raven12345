# Raven Package Installer

Open **Tools > Raven > Package Installer**. On first opening, the tool creates
`Assets/RavenPackageCatalog.asset`. Edit that asset in the Inspector, or edit the
entries directly in the window, to maintain the project's personal package list.

To install this package from this repository with the Unity Package Manager, use:

```
https://github.com/Konosos/Raven12345.git?path=Assets/Packages/PackageInstaller
```

Each catalogue entry uses a Git URL accepted by Unity's Package Manager, including
an optional `?path=` for packages stored in a subfolder of a repository.
