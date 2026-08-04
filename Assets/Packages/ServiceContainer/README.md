# Service Container

## Installation

Add the package through Unity's **Package Manager** using the following Git URL:

```text
https://github.com/Konosos/Raven12345.git?path=Assets/Packages/ServiceContainer
```

## Setup

1. Create ScriptableObject classes that inherit from `ServiceSettingSO` and implement `Register`.
2. Create a **Service Global Setting** asset from **Assets > Create > Raven12345 > Service Container > Global Setting**.
3. Place the asset in a `Resources` folder and name it `ServiceGlobalSettingSO`.
4. Add the service-setting assets to its **Settings** list.

The settings in this asset are registered automatically when `ServiceContainer.Global` is first accessed.
