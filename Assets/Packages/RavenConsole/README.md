# RavenConsole

RavenConsole is a runtime command console for Unity. It provides command discovery through `[Command]`, command parsing and execution, TextMeshPro UI support, Unity log capture, and command suggestions.

## Install from Git

Add the package with Unity Package Manager using the repository URL and this package path:

```
https://github.com/Konosos/Raven12345.git.git?path=/Assets/Packages/RavenConsole
```

Pin a revision when appropriate:

```
https://github.com/Konosos/Raven12345.git.git?path=/Assets/Packages/RavenConsole#v1.0.0
```

## Requirements

- Unity 2022.3 or newer
- TextMeshPro (`com.unity.textmeshpro`)
- Input System (`com.unity.inputsystem`, installed as a package dependency)

## Swipe to open

With the console closed, drag down from the top 5% of the full screen by at least
12% of its height within 0.5 seconds. Configure these thresholds under
`Swipe To Open` on `CommandConsoleUI`. Touch and mouse are supported.
The gesture supports Legacy Input Manager, Input System, and Both; Both uses
Input System exclusively. Device Simulator uses the simulated screen dimensions.

## Quick start

1. Add the `CommandConsoleUI` prefab to a scene.
2. Assign the input and output TextMeshPro UI components to `SimpleCommandConsole`.
3. Add a component containing methods decorated with `[Command]` to the console's command sources.

```csharp
using Raven12345;
using UnityEngine;

public sealed class GameCommands : MonoBehaviour
{
    [Command("player.heal", "Restore the player's health.")]
    private void HealPlayer(int amount)
    {
        Debug.Log($"Healed {amount} health.");
    }
}
```
