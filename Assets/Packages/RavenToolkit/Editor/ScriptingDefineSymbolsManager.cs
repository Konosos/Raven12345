using System.Collections.Generic;
using UnityEditor;

namespace Raven12345
{
public static class ScriptingDefineSymbolsManager
{
    public static string AddDefineSymbol(this string defines, string symbol)
    {
        List<string> symbols = ParseDefineSymbols(defines);
        string normalizedSymbol = NormalizeSymbol(symbol);
        if (string.IsNullOrEmpty(normalizedSymbol) || symbols.Contains(normalizedSymbol))
            return string.Join(";", symbols);

        symbols.Add(normalizedSymbol);
        UnityEngine.Debug.Log($"Added define symbol: {normalizedSymbol}");
        return string.Join(";", symbols);
    }

    public static string RemoveDefineSymbol(this string defines, string symbol)
    {
        List<string> symbols = ParseDefineSymbols(defines);
        string normalizedSymbol = NormalizeSymbol(symbol);
        if (string.IsNullOrEmpty(normalizedSymbol) || !symbols.Remove(normalizedSymbol))
            return string.Join(";", symbols);

        UnityEngine.Debug.Log($"Removed define symbol: {normalizedSymbol}");
        return string.Join(";", symbols);
    }

    public static bool ContainsDefineSymbol(this string defines, string symbol)
    {
        string normalizedSymbol = NormalizeSymbol(symbol);
        return !string.IsNullOrEmpty(normalizedSymbol) && ParseDefineSymbols(defines).Contains(normalizedSymbol);
    }

    public static void ApplyDefineSymbol(this string defines)
    {
        PlayerSettings.SetScriptingDefineSymbols(GetActiveBuildTargetGroup(), defines);
    }

    public static void AddDefineSymbol(string symbol)
    {
        //Get current build target
        var currentBuildTarget = GetActiveBuildTargetGroup();

        // Get current define symbols list
        string defines = PlayerSettings.GetScriptingDefineSymbols(currentBuildTarget);

        string updatedDefines = defines.AddDefineSymbol(symbol);
        if (updatedDefines != defines)
            PlayerSettings.SetScriptingDefineSymbols(currentBuildTarget, updatedDefines);
    }

    public static void RemoveDefineSymbol(string symbol)
    {
        //Get current build target
        var currentBuildTarget = GetActiveBuildTargetGroup();

        // Get current define symbols list
        string defines = PlayerSettings.GetScriptingDefineSymbols(currentBuildTarget);

        string updatedDefines = defines.RemoveDefineSymbol(symbol);
        if (updatedDefines != defines)
            PlayerSettings.SetScriptingDefineSymbols(currentBuildTarget, updatedDefines);
    }

    public static void ClearAllDefineSymbols()
    {
        PlayerSettings.SetScriptingDefineSymbols(GetActiveBuildTargetGroup(), "");
        UnityEngine.Debug.Log("Cleared all define symbols.");
    }

    public static UnityEditor.Build.NamedBuildTarget GetActiveBuildTargetGroup()
    {
        BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
        return UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(activeBuildTarget));
        //return BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
    }

    private static List<string> ParseDefineSymbols(string defines)
    {
        var symbols = new List<string>();
        if (string.IsNullOrWhiteSpace(defines))
            return symbols;

        foreach (string symbol in defines.Split(';'))
        {
            string normalizedSymbol = NormalizeSymbol(symbol);
            if (!string.IsNullOrEmpty(normalizedSymbol) && !symbols.Contains(normalizedSymbol))
                symbols.Add(normalizedSymbol);
        }

        return symbols;
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol?.Trim();
    }
}
}
