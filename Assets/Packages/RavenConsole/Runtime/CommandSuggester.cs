using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Raven12345
{
    /// <summary>Produces command and parameter suggestions from a registry.</summary>
    public sealed class CommandSuggester
    {
        private readonly CommandRegistry _registry;

        public CommandSuggester(CommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Returns command-name completions until the command is identified,
        /// then shows the expected next argument. Boolean and enum arguments
        /// expose selectable value completions; other types are shown as hints.
        /// </summary>
        public IReadOnlyList<CommandSuggestion> Suggest(string input, int maxResults = 8)
        {
            if (maxResults <= 0 || string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<CommandSuggestion>();
            }

            string trimmedStart = input.TrimStart();
            int firstWhitespace = FindFirstWhitespace(trimmedStart);
            if (firstWhitespace < 0)
            {
                return SuggestCommands(trimmedStart, maxResults);
            }

            return SuggestParameter(input, trimmedStart, maxResults);
        }

        private IReadOnlyList<CommandSuggestion> SuggestCommands(string query, int maxResults)
        {
            return _registry.Commands
                .GroupBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Where(command => command.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(command => command.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(command => new CommandSuggestion(
                    command.Name,
                    command.Description,
                    $"{command.Name} ",
                    GetRemainingParameterSignature(command)))
                .ToArray();
        }

        private IReadOnlyList<CommandSuggestion> SuggestParameter(
            string input,
            string trimmedStart,
            int maxResults)
        {
            if (!CommandTokenizer.TryTokenize(trimmedStart, out IReadOnlyList<string> tokens, out _)
                || tokens.Count == 0)
            {
                return Array.Empty<CommandSuggestion>();
            }

            IReadOnlyList<CommandDefinition> commands = _registry.Find(tokens[0]);
            if (commands.Count == 0)
            {
                return Array.Empty<CommandSuggestion>();
            }

            bool startsNewArgument = char.IsWhiteSpace(trimmedStart[trimmedStart.Length - 1]);
            int argumentIndex = startsNewArgument ? tokens.Count - 1 : tokens.Count - 2;
            if (argumentIndex < 0)
            {
                argumentIndex = 0;
            }

            string valueQuery = startsNewArgument ? string.Empty : tokens[tokens.Count - 1];
            return commands
                .SelectMany(command => CreateParameterSuggestions(
                    command,
                    argumentIndex,
                    valueQuery,
                    input))
                .GroupBy(suggestion => suggestion.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(maxResults)
                .ToArray();
        }

        private static IEnumerable<CommandSuggestion> CreateParameterSuggestions(
            CommandDefinition command,
            int argumentIndex,
            string valueQuery,
            string input)
        {
            ParameterInfo parameter = GetParameterAt(command, argumentIndex);
            if (parameter == null)
            {
                return Array.Empty<CommandSuggestion>();
            }

            Type type = GetValueType(parameter);
            string typeName = GetTypeName(type);
            string parameterLabel = FormatParameterLabel(parameter, typeName);
            string remainingSignature = GetRemainingParameterSignature(command, argumentIndex + 1);

            if (type == typeof(bool))
            {
                return new[] { "true", "false" }
                    .Where(value => value.StartsWith(valueQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(value => new CommandSuggestion(
                        value,
                        string.Empty,
                        ReplaceCurrentValue(input, value),
                        remainingSignature));
            }

            if (type.IsEnum)
            {
                return Enum.GetNames(type)
                    .Where(value => value.StartsWith(valueQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(value => new CommandSuggestion(
                        value,
                        string.Empty,
                        ReplaceCurrentValue(input, value),
                        remainingSignature));
            }

            if (Nullable.GetUnderlyingType(parameter.ParameterType) != null &&
                "null".StartsWith(valueQuery, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new CommandSuggestion(
                        "null",
                        string.Empty,
                        ReplaceCurrentValue(input, "null"),
                        remainingSignature)
                };
            }

            return new[]
            {
                new CommandSuggestion(
                    parameterLabel,
                    DescribeParameter(parameter, typeName),
                    ghostHint: remainingSignature)
            };
        }

        private static ParameterInfo GetParameterAt(CommandDefinition command, int argumentIndex)
        {
            ParameterInfo[] parameters = command.Parameters;
            if (argumentIndex < 0 || parameters.Length == 0)
            {
                return null;
            }

            int lastIndex = parameters.Length - 1;
            if (argumentIndex < parameters.Length)
            {
                return parameters[argumentIndex];
            }

            return parameters[lastIndex].GetCustomAttribute<ParamArrayAttribute>() != null
                ? parameters[lastIndex]
                : null;
        }

        private static Type GetValueType(ParameterInfo parameter)
        {
            return parameter.GetCustomAttribute<ParamArrayAttribute>() != null
                ? parameter.ParameterType.GetElementType()
                : Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        }

        private static string FormatParameterLabel(ParameterInfo parameter, string typeName)
        {
            bool isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
            string suffix = isParams ? "..." : string.Empty;
            string content = $"{parameter.Name}: {typeName}{suffix}";
            return parameter.IsOptional
                ? $"[{content} = {parameter.DefaultValue}]"
                : $"<{content}>";
        }

        private static string DescribeParameter(ParameterInfo parameter, string typeName)
        {
            if (parameter.GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                return $"One or more {typeName} values";
            }

            return parameter.IsOptional
                ? $"Optional {typeName} parameter"
                : $"Required {typeName} parameter";
        }

        private static string GetRemainingParameterSignature(CommandDefinition command)
        {
            return GetRemainingParameterSignature(command, 0);
        }

        private static string GetRemainingParameterSignature(
            CommandDefinition command,
            int firstParameterIndex)
        {
            return string.Join(
                " ",
                command.Parameters
                    .Skip(firstParameterIndex)
                    .Select(parameter =>
                    FormatParameterLabel(parameter, GetTypeName(GetValueType(parameter)))));
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
            return type.Name;
        }

        private static string ReplaceCurrentValue(string input, string value)
        {
            if (string.IsNullOrEmpty(input) || char.IsWhiteSpace(input[input.Length - 1]))
            {
                return $"{input}{value} ";
            }

            int index = input.Length - 1;
            while (index >= 0 && !char.IsWhiteSpace(input[index]))
            {
                index--;
            }

            return $"{input.Substring(0, index + 1)}{value} ";
        }

        private static int FindFirstWhitespace(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsWhiteSpace(value[index]))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
