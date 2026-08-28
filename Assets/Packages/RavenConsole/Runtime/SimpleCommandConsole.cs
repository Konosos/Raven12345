using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace Raven12345
{
    /// <summary>
    /// Display severity used by the console log. Each level has its own color
    /// in the inspector so command input, normal output and failures remain
    /// visually distinct on a mobile screen.
    /// </summary>
    public enum CommandLogLevel
    {
        Info,
        Command,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Minimal TextMeshPro UI bridge for a <see cref="CommandRegistry"/>.
    /// Assign an input field, an output label, and the components that expose
    /// instance commands in the Inspector.
    /// </summary>
    public sealed class SimpleCommandConsole : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private MonoBehaviour[] commandSources;

        [Header("Theme")]
        [SerializeField] private CommandConsoleTheme theme;

        [Header("Unity Logging")]
        [SerializeField] private bool captureUnityLogs = true;
        [SerializeField, Min(1)] private int maxStoredLogs = 200;
        [SerializeField] private bool includeStackTraceForErrors;

        private readonly CommandRegistry _registry = new();
        private readonly List<object> _targets = new();
        private readonly Queue<string> _storedLogEntries = new();
        private readonly Queue<UnityLogEntry> _pendingUnityLogs = new();
        private readonly object _pendingUnityLogsLock = new();
        private CommandExecutor _executor;
        private CommandSuggester _suggester;
        private CommandConsoleTheme _fallbackTheme;
        private volatile bool _canCaptureUnityLogs;
        private bool _hasScannedCommands;

        /// <summary>Raised after output changes so a view can update its scroll position.</summary>
        public event Action OutputChanged;

        /// <summary>The visual and text configuration currently used by this console.</summary>
        public CommandConsoleTheme Theme => ActiveTheme;

        private void Awake()
        {
            _executor = new CommandExecutor(_registry);
            _suggester = new CommandSuggester(_registry);
            _canCaptureUnityLogs = outputText != null;
        }

        private void OnEnable()
        {
            _canCaptureUnityLogs = outputText != null;
            Application.logMessageReceivedThreaded += HandleUnityLog;

            if (inputField != null)
            {
                inputField.onSubmit.AddListener(ExecuteInput);
            }
        }

        private void OnDisable()
        {
            _canCaptureUnityLogs = false;
            Application.logMessageReceivedThreaded -= HandleUnityLog;

            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(ExecuteInput);
            }
        }

        private void Update()
        {
            FlushPendingUnityLogs();
        }

        /// <summary>Rebuilds the registry from this component and Inspector sources.</summary>
        public void ScanCommands()
        {
            _registry.Clear();
            _targets.Clear();
            AddCommandSource(this);

            if (commandSources != null)
            {
                foreach (MonoBehaviour source in commandSources)
                {
                    AddCommandSource(source);
                }
            }

            _hasScannedCommands = true;
            AppendLog(ActiveTheme.FormatScanSummary(_registry.Commands.Count), CommandLogLevel.Info);
        }

        /// <summary>Executes input submitted by the assigned input field.</summary>
        public void ExecuteInput(string input)
        {
            EnsureCommandsScanned();
            if (_executor == null)
            {
                _executor = new CommandExecutor(_registry);
            }

            CommandResult result = _executor.Execute(input, _targets);
            string response = result.Message;
            if (result.Succeeded && string.IsNullOrEmpty(response))
            {
                response = ActiveTheme.EmptyCommandResultText;
            }

            AppendLog(ActiveTheme.FormatCommandInput(input), CommandLogLevel.Command);
            AppendLog(
                ActiveTheme.FormatCommandResult(response),
                result.Succeeded ? CommandLogLevel.Success : CommandLogLevel.Error);

            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.ActivateInputField();
            }
        }

        /// <summary>Clears the assigned output label.</summary>
        public void ClearOutput()
        {
            _storedLogEntries.Clear();
            lock (_pendingUnityLogsLock)
            {
                _pendingUnityLogs.Clear();
            }

            if (outputText != null)
            {
                outputText.text = string.Empty;
                OutputChanged?.Invoke();
            }
        }

        /// <summary>
        /// Adds a message to the console using the configured level color.
        /// Result messages are indented to make the command/result relationship
        /// clear when several commands are visible at once.
        /// </summary>
        public void Log(string message, CommandLogLevel level = CommandLogLevel.Info)
        {
            AppendLog(message, level);
        }

        /// <summary>Writes a normal informational message.</summary>
        public void LogInfo(string message)
        {
            Log(message, CommandLogLevel.Info);
        }

        /// <summary>Writes a successful result message.</summary>
        public void LogSuccess(string message)
        {
            Log(message, CommandLogLevel.Success);
        }

        /// <summary>Writes a warning message.</summary>
        public void LogWarning(string message)
        {
            Log(message, CommandLogLevel.Warning);
        }

        /// <summary>Writes an error message.</summary>
        public void LogError(string message)
        {
            Log(message, CommandLogLevel.Error);
        }

        /// <summary>
        /// Lists every command currently registered by this console, including
        /// overloads and the default <c>help</c> command itself.
        /// </summary>
        [Command("help", "Lists all commands currently registered in this console.")]
        public string Help()
        {
            if (_registry.Commands.Count == 0)
            {
                return ActiveTheme.NoCommandsRegisteredText;
            }

            IEnumerable<string> commandLines = _registry.Commands
                .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(command => command.Parameters.Length)
                .ThenBy(command => command.Method.ToString(), StringComparer.Ordinal)
                .Select(FormatCommandLine);

            return $"{ActiveTheme.FormatRegisteredCommandsHeader(_registry.Commands.Count)}\n{string.Join("\n", commandLines)}";
        }

        /// <summary>Gets command and parameter suggestions for the current input text.</summary>
        public IReadOnlyList<CommandSuggestion> GetSuggestions(string input, int maxResults = 8)
        {
            EnsureCommandsScanned();
            if (_suggester == null)
            {
                _suggester = new CommandSuggester(_registry);
            }

            return _suggester.Suggest(input, maxResults);
        }

        private void EnsureCommandsScanned()
        {
            if (!_hasScannedCommands)
            {
                ScanCommands();
            }
        }

        private void AddCommandSource(MonoBehaviour source)
        {
            if (source == null)
            {
                return;
            }

            _targets.Add(source);
            _registry.Scan(source.GetType());
        }

        private string FormatCommandLine(CommandDefinition command)
        {
            string parameterList = string.Join(
                ActiveTheme.ParameterSeparator,
                command.Parameters.Select(FormatParameter));
            string signature = ActiveTheme.FormatCommandSignature(command.Name, parameterList);
            return ActiveTheme.FormatCommandDescription(signature, command.Description);
        }

        private string FormatParameter(ParameterInfo parameter)
        {
            bool isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
            Type valueType = isParams
                ? parameter.ParameterType.GetElementType()
                : Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            string typeName = GetTypeName(valueType);
            string typeWithSuffix = isParams
                ? $"{typeName}{ActiveTheme.ParamsParameterSuffix}"
                : typeName;

            return parameter.IsOptional
                ? ActiveTheme.FormatOptionalParameter(
                    parameter.Name,
                    typeWithSuffix,
                    FormatDefaultValue(parameter.DefaultValue))
                : ActiveTheme.FormatRequiredParameter(parameter.Name, typeWithSuffix);
        }

        private static string FormatDefaultValue(object value)
        {
            return value == null ? "null" : value.ToString();
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(char)) return "char";
            return type == null ? "object" : type.Name;
        }

        private bool AppendLog(
            string message,
            CommandLogLevel level,
            bool refreshOutput = true)
        {
            message = message ?? string.Empty;

            if (outputText == null)
            {
                _canCaptureUnityLogs = false;
                LogToUnityConsole(message, level);
                return false;
            }

            outputText.richText = true;
            string displayLine = ActiveTheme.UseColoredLogs
                ? FormatColoredLog(message, ActiveTheme.GetLogColor(level))
                : EscapeForDisplay(message);
            _storedLogEntries.Enqueue(displayLine);
            TrimStoredLogs(_storedLogEntries);
            if (refreshOutput)
            {
                RefreshOutputText();
            }

            return true;
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (!captureUnityLogs || !_canCaptureUnityLogs)
            {
                return;
            }

            lock (_pendingUnityLogsLock)
            {
                int logLimit = GetLogLimit();
                while (_pendingUnityLogs.Count >= logLimit)
                {
                    _pendingUnityLogs.Dequeue();
                }

                _pendingUnityLogs.Enqueue(new UnityLogEntry(condition, stackTrace, type));
            }
        }

        private void FlushPendingUnityLogs()
        {
            bool outputChanged = false;
            while (true)
            {
                UnityLogEntry entry;
                lock (_pendingUnityLogsLock)
                {
                    if (_pendingUnityLogs.Count == 0)
                    {
                        break;
                    }

                    entry = _pendingUnityLogs.Dequeue();
                }

                CommandLogLevel level = GetLogLevel(entry.Type);
                string message = entry.Message;
                if (includeStackTraceForErrors && level == CommandLogLevel.Error &&
                    !string.IsNullOrWhiteSpace(entry.StackTrace))
                {
                    message = $"{message}\n{entry.StackTrace}";
                }

                outputChanged |= AppendLog(message, level, refreshOutput: false);
            }

            if (outputChanged)
            {
                RefreshOutputText();
            }
        }

        private void RefreshOutputText()
        {
            if (outputText == null)
            {
                return;
            }

            outputText.text = string.Join("\n", _storedLogEntries);
            OutputChanged?.Invoke();
        }

        private void TrimStoredLogs(Queue<string> entries)
        {
            int logLimit = GetLogLimit();
            while (entries.Count > logLimit)
            {
                entries.Dequeue();
            }
        }

        private int GetLogLimit()
        {
            return maxStoredLogs < 1 ? 1 : maxStoredLogs;
        }

        private static CommandLogLevel GetLogLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return CommandLogLevel.Warning;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return CommandLogLevel.Error;
                default:
                    return CommandLogLevel.Info;
            }
        }

        private static string FormatColoredLog(string message, Color color)
        {
            string colorHex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{colorHex}><noparse>{message}</noparse></color>";
        }

        private static string EscapeForDisplay(string message)
        {
            return $"<noparse>{message}</noparse>";
        }

        private CommandConsoleTheme ActiveTheme
        {
            get
            {
                if (theme != null)
                {
                    return theme;
                }

                if (_fallbackTheme == null)
                {
                    _fallbackTheme = ScriptableObject.CreateInstance<CommandConsoleTheme>();
                    _fallbackTheme.hideFlags = HideFlags.DontSave;
                }

                return _fallbackTheme;
            }
        }

        private void LogToUnityConsole(string line, CommandLogLevel level)
        {
            switch (level)
            {
                case CommandLogLevel.Error:
                    Debug.LogError(line, this);
                    break;
                case CommandLogLevel.Warning:
                    Debug.LogWarning(line, this);
                    break;
                default:
                    Debug.Log(line, this);
                    break;
            }
        }

        private readonly struct UnityLogEntry
        {
            public UnityLogEntry(string message, string stackTrace, LogType type)
            {
                Message = message;
                StackTrace = stackTrace;
                Type = type;
            }

            public string Message { get; }
            public string StackTrace { get; }
            public LogType Type { get; }
        }
    }
}
