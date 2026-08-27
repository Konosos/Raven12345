using System;
using UnityEngine;

namespace Raven12345
{
    /// <summary>
    /// Visual and textual configuration shared by command console instances.
    /// Create variants by duplicating the supplied theme asset, then assign the
    /// variant to a console prefab or scene instance.
    /// </summary>
    public sealed class CommandConsoleTheme : ScriptableObject
    {
        [Header("Log Colors")]
        [SerializeField] private bool useColoredLogs = true;
        [SerializeField] private Color infoLogColor = new Color(0.82f, 0.88f, 0.93f, 1f);
        [SerializeField] private Color commandLogColor = new Color(0.55f, 0.88f, 1f, 1f);
        [SerializeField] private Color successLogColor = new Color(0.45f, 0.9f, 0.62f, 1f);
        [SerializeField] private Color warningLogColor = new Color(1f, 0.78f, 0.35f, 1f);
        [SerializeField] private Color errorLogColor = new Color(1f, 0.42f, 0.42f, 1f);

        [Header("Suggestion Colors")]
        [SerializeField] private Color suggestionCompletionColor = Color.white;
        [SerializeField] private Color suggestionParameterColor = new Color(0.82f, 0.91f, 0.96f, 1f);
        [SerializeField] private Color suggestionSecondaryColor = new Color(0.61f, 0.71f, 0.78f, 1f);
        [SerializeField] private Color ghostInputMaskColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color ghostHintColor = new Color(0.61f, 0.71f, 0.78f, 1f);

        [Header("Console Text")]
        [SerializeField] private string scanSummaryFormat = "Scanned {0} command(s).";
        [SerializeField] private string emptyCommandResultText = "OK";
        [SerializeField] private string noCommandsRegisteredText = "No commands are registered.";
        [SerializeField] private string registeredCommandsHeaderFormat = "Registered commands ({0}):";

        [Header("Command Formats")]
        [SerializeField] private string commandInputFormat = "> {0}";
        [SerializeField] private string commandResultFormat = "  -> {0}";
        [SerializeField] private string commandSignatureFormat = "{0} {1}";
        [SerializeField] private string commandDescriptionFormat = "{0} - {1}";
        [SerializeField] private string parameterSeparator = " ";
        [SerializeField] private string requiredParameterFormat = "<{0}: {1}>";
        [SerializeField] private string optionalParameterFormat = "[{0}: {1} = {2}]";
        [SerializeField] private string paramsParameterSuffix = "...";
        [SerializeField] private string suggestionGhostHintFormat = " {0}";
        [SerializeField] private string suggestionDescriptionFormat = "  {0}";

        public bool UseColoredLogs => useColoredLogs;
        public string EmptyCommandResultText => emptyCommandResultText;
        public string NoCommandsRegisteredText => noCommandsRegisteredText;
        public string ParameterSeparator => parameterSeparator;
        public string ParamsParameterSuffix => paramsParameterSuffix;
        public Color SuggestionCompletionColor => suggestionCompletionColor;
        public Color SuggestionParameterColor => suggestionParameterColor;
        public Color SuggestionSecondaryColor => suggestionSecondaryColor;
        public Color GhostInputMaskColor => ghostInputMaskColor;
        public Color GhostHintColor => ghostHintColor;

        public Color GetLogColor(CommandLogLevel level)
        {
            switch (level)
            {
                case CommandLogLevel.Command:
                    return commandLogColor;
                case CommandLogLevel.Success:
                    return successLogColor;
                case CommandLogLevel.Warning:
                    return warningLogColor;
                case CommandLogLevel.Error:
                    return errorLogColor;
                default:
                    return infoLogColor;
            }
        }

        public string FormatScanSummary(int commandCount)
        {
            return Format(scanSummaryFormat, "Scanned {0} command(s).", commandCount);
        }

        public string FormatCommandInput(string input)
        {
            return Format(commandInputFormat, "> {0}", input);
        }

        public string FormatCommandResult(string result)
        {
            return Format(commandResultFormat, "  -> {0}", result);
        }

        public string FormatRegisteredCommandsHeader(int commandCount)
        {
            return Format(
                registeredCommandsHeaderFormat,
                "Registered commands ({0}):",
                commandCount);
        }

        public string FormatCommandSignature(string commandName, string parameterList)
        {
            return string.IsNullOrEmpty(parameterList)
                ? commandName
                : Format(commandSignatureFormat, "{0} {1}", commandName, parameterList);
        }

        public string FormatCommandDescription(string signature, string description)
        {
            return string.IsNullOrWhiteSpace(description)
                ? signature
                : Format(commandDescriptionFormat, "{0} - {1}", signature, description);
        }

        public string FormatRequiredParameter(string parameterName, string typeName)
        {
            return Format(requiredParameterFormat, "<{0}: {1}>", parameterName, typeName);
        }

        public string FormatOptionalParameter(
            string parameterName,
            string typeName,
            string defaultValue)
        {
            return Format(
                optionalParameterFormat,
                "[{0}: {1} = {2}]",
                parameterName,
                typeName,
                defaultValue);
        }

        public string FormatSuggestionGhostHint(string ghostHint)
        {
            return Format(suggestionGhostHintFormat, " {0}", ghostHint);
        }

        public string FormatSuggestionDescription(string description)
        {
            return Format(suggestionDescriptionFormat, "  {0}", description);
        }

        public string Colorize(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
        }

        private static string Format(string format, string fallback, params object[] arguments)
        {
            string template = string.IsNullOrEmpty(format) ? fallback : format;
            try
            {
                return string.Format(template, arguments);
            }
            catch (FormatException)
            {
                return string.Format(fallback, arguments);
            }
        }
    }
}
