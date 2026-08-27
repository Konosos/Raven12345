using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Raven12345
{
    /// <summary>
    /// Discovers methods decorated with <see cref="CommandAttribute"/> and keeps
    /// their reflection data ready for command parsing and invocation.
    /// </summary>
    public sealed class CommandRegistry
    {
        private const BindingFlags CommandMethodFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        private readonly List<CommandDefinition> _commands = new List<CommandDefinition>();
        private readonly Dictionary<string, List<CommandDefinition>> _commandsByName =
            new Dictionary<string, List<CommandDefinition>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<MethodInfo> _registeredMethods = new HashSet<MethodInfo>();

        /// <summary>All commands found by this registry.</summary>
        public IReadOnlyList<CommandDefinition> Commands => _commands;

        /// <summary>Removes every command discovered so far.</summary>
        public void Clear()
        {
            _commands.Clear();
            _commandsByName.Clear();
            _registeredMethods.Clear();
        }

        /// <summary>
        /// Finds every overload registered under a command name.
        /// </summary>
        public IReadOnlyList<CommandDefinition> Find(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return Array.Empty<CommandDefinition>();
            }

            return _commandsByName.TryGetValue(commandName, out List<CommandDefinition> commands)
                ? commands
                : Array.Empty<CommandDefinition>();
        }

        /// <summary>
        /// Scans all concrete, non-generic methods declared directly on a type.
        /// Public and non-public methods are both supported.
        /// </summary>
        /// <returns>The number of newly registered commands.</returns>
        public int Scan(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            int discoveredCount = 0;
            foreach (MethodInfo method in type.GetMethods(CommandMethodFlags))
            {
                if (method.IsAbstract || method.ContainsGenericParameters)
                {
                    continue;
                }

                CommandAttribute attribute = (CommandAttribute)Attribute.GetCustomAttribute(
                    method,
                    typeof(CommandAttribute),
                    false);

                if (attribute != null && Register(method, attribute))
                {
                    discoveredCount++;
                }
            }

            return discoveredCount;
        }

        /// <summary>
        /// Scans every loadable type in an assembly. Types that fail to load are
        /// ignored so commands from the rest of the assembly remain available.
        /// </summary>
        /// <returns>The number of newly registered commands.</returns>
        public int Scan(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            return GetLoadableTypes(assembly).Sum(Scan);
        }

        private bool Register(MethodInfo method, CommandAttribute attribute)
        {
            if (!_registeredMethods.Add(method))
            {
                return false;
            }

            CommandDefinition command = new CommandDefinition(method, attribute);
            _commands.Add(command);

            if (!_commandsByName.TryGetValue(command.Name, out List<CommandDefinition> overloads))
            {
                overloads = new List<CommandDefinition>();
                _commandsByName.Add(command.Name, overloads);
            }

            overloads.Add(command);
            return true;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
