using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Raven12345
{
    /// <summary>Finds, binds, and invokes commands discovered by a registry.</summary>
    public sealed class CommandExecutor
    {
        private readonly CommandRegistry _registry;

        public CommandExecutor(CommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Executes a command line. Supply <paramref name="target"/> for
        /// instance commands declared by that target's type.
        /// </summary>
        public CommandResult Execute(string input, object target = null)
        {
            return Execute(
                input,
                target == null ? Array.Empty<object>() : new[] { target });
        }

        /// <summary>
        /// Executes a command line using the first compatible object for any
        /// instance command.
        /// </summary>
        public CommandResult Execute(string input, IEnumerable<object> targets)
        {
            if (!CommandTokenizer.TryTokenize(input, out IReadOnlyList<string> tokens, out string tokenError))
            {
                return CommandResult.Failure(tokenError);
            }

            if (tokens.Count == 0)
            {
                return CommandResult.Failure("Enter a command.");
            }

            IReadOnlyList<CommandDefinition> candidates = _registry.Find(tokens[0]);
            if (candidates.Count == 0)
            {
                return CommandResult.Failure($"Unknown command '{tokens[0]}'.");
            }

            string[] arguments = tokens.Skip(1).ToArray();
            List<BoundCommand> matches = new List<BoundCommand>();
            object[] availableTargets = targets?.Where(target => target != null).ToArray()
                ?? Array.Empty<object>();

            foreach (CommandDefinition candidate in candidates)
            {
                object invocationTarget = null;
                if (!candidate.IsStatic)
                {
                    invocationTarget = availableTargets.FirstOrDefault(
                        target => candidate.Method.DeclaringType.IsInstanceOfType(target));
                    if (invocationTarget == null)
                    {
                        continue;
                    }
                }

                if (TryBind(
                        candidate,
                        arguments,
                        out object[] values,
                        out int score,
                        out int defaultedParameterCount))
                {
                    matches.Add(new BoundCommand(
                        candidate,
                        invocationTarget,
                        values,
                        score,
                        defaultedParameterCount));
                }
            }

            if (matches.Count == 0)
            {
                return CommandResult.Failure(
                    $"No overload of '{tokens[0]}' accepts the supplied arguments.");
            }

            int highestScore = matches.Max(match => match.Score);
            List<BoundCommand> bestMatches = matches
                .Where(match => match.Score == highestScore)
                .ToList();

            int fewestDefaultedParameters = bestMatches.Min(match => match.DefaultedParameterCount);
            bestMatches = bestMatches
                .Where(match => match.DefaultedParameterCount == fewestDefaultedParameters)
                .ToList();

            if (bestMatches.Count != 1)
            {
                return CommandResult.Failure(
                    $"Arguments are ambiguous for command '{tokens[0]}'.");
            }

            BoundCommand command = bestMatches[0];
            try
            {
                object value = command.Definition.Method.Invoke(
                    command.Target,
                    command.Arguments);
                return CommandResult.Success(value);
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                return CommandResult.Failure(cause.Message);
            }
            catch (Exception exception)
            {
                return CommandResult.Failure(exception.Message);
            }
        }

        private static bool TryBind(
            CommandDefinition command,
            IReadOnlyList<string> tokens,
            out object[] values,
            out int score,
            out int defaultedParameterCount)
        {
            ParameterInfo[] parameters = command.Parameters;
            values = new object[parameters.Length];
            score = 0;
            defaultedParameterCount = 0;
            int tokenIndex = 0;

            for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                ParameterInfo parameter = parameters[parameterIndex];
                bool isParamsArray = parameter.GetCustomAttribute<ParamArrayAttribute>() != null;

                if (isParamsArray)
                {
                    Type elementType = parameter.ParameterType.GetElementType();
                    int remainingTokenCount = tokens.Count - tokenIndex;
                    Array array = Array.CreateInstance(elementType, remainingTokenCount);

                    for (int index = 0; index < remainingTokenCount; index++)
                    {
                        if (!TryConvert(tokens[tokenIndex++], elementType, out object value, out int conversionScore))
                        {
                            return false;
                        }

                        array.SetValue(value, index);
                        score += conversionScore;
                    }

                    values[parameterIndex] = array;
                    continue;
                }

                if (tokenIndex == tokens.Count)
                {
                    if (!parameter.IsOptional)
                    {
                        return false;
                    }

                    values[parameterIndex] = parameter.DefaultValue;
                    defaultedParameterCount++;
                    continue;
                }

                if (!TryConvert(
                        tokens[tokenIndex++],
                        parameter.ParameterType,
                        out object convertedValue,
                        out int convertedScore))
                {
                    return false;
                }

                values[parameterIndex] = convertedValue;
                score += convertedScore;
            }

            return tokenIndex == tokens.Count;
        }

        private static bool TryConvert(
            string text,
            Type type,
            out object value,
            out int score)
        {
            Type nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
                {
                    value = null;
                    score = 1;
                    return true;
                }

                return TryConvert(text, nullableType, out value, out score);
            }

            if (type == typeof(string))
            {
                value = text;
                score = 0;
                return true;
            }

            if (type == typeof(char))
            {
                value = text.Length == 1 ? text[0] : default(char);
                score = 10;
                return text.Length == 1;
            }

            if (type == typeof(bool))
            {
                bool isTrue = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1";
                bool isFalse = string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0";
                value = isTrue;
                score = 10;
                return isTrue || isFalse;
            }

            if (type.IsEnum)
            {
                bool parsed = Enum.TryParse(type, text, true, out object enumValue);
                value = enumValue;
                score = 10;
                return parsed;
            }

            if (type == typeof(byte) && byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte byteValue))
            {
                value = byteValue; score = 10; return true;
            }

            if (type == typeof(sbyte) && sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte sbyteValue))
            {
                value = sbyteValue; score = 10; return true;
            }

            if (type == typeof(short) && short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short shortValue))
            {
                value = shortValue; score = 10; return true;
            }

            if (type == typeof(ushort) && ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort ushortValue))
            {
                value = ushortValue; score = 10; return true;
            }

            if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                value = intValue; score = 10; return true;
            }

            if (type == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint uintValue))
            {
                value = uintValue; score = 10; return true;
            }

            if (type == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                value = longValue; score = 10; return true;
            }

            if (type == typeof(ulong) && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ulongValue))
            {
                value = ulongValue; score = 10; return true;
            }

            if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            {
                value = floatValue; score = 10; return true;
            }

            if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                value = doubleValue; score = 10; return true;
            }

            if (type == typeof(decimal) && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
            {
                value = decimalValue; score = 10; return true;
            }

            if (type == typeof(Guid) && Guid.TryParse(text, out Guid guidValue))
            {
                value = guidValue; score = 10; return true;
            }

            value = null;
            score = 0;
            return false;
        }

        private sealed class BoundCommand
        {
            public BoundCommand(
                CommandDefinition definition,
                object target,
                object[] arguments,
                int score,
                int defaultedParameterCount)
            {
                Definition = definition;
                Target = target;
                Arguments = arguments;
                Score = score;
                DefaultedParameterCount = defaultedParameterCount;
            }

            public CommandDefinition Definition { get; }
            public object Target { get; }
            public object[] Arguments { get; }
            public int Score { get; }
            public int DefaultedParameterCount { get; }
        }
    }
}
