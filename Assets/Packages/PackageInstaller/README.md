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

## Unity Giveaway notification

The package checks Unity's public Publisher Sale page once when the Editor opens.
The result is cached for six hours and a notification is only logged for a newly
detected asset/coupon combination. Turn this off with **Giveaway alerts** in the
Package Installer window, or run **Tools > Raven > Check Unity Giveaway** to
perform a manual, uncached check. The tool never signs in or redeems an asset.
