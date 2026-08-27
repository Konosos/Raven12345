using System;
using System.Reflection;

namespace Raven12345
{
    /// <summary>
    /// Reflection data for one method exposed as a console command.
    /// </summary>
    public sealed class CommandDefinition
    {
        internal CommandDefinition(MethodInfo method, CommandAttribute attribute)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Name = attribute.Name;
            Description = attribute.Description;
            Parameters = method.GetParameters();
        }

        /// <summary>The command text entered by the user.</summary>
        public string Name { get; }

        /// <summary>The optional help text supplied by <see cref="CommandAttribute"/>.</summary>
        public string Description { get; }

        /// <summary>The method that will be invoked by a later executor.</summary>
        public MethodInfo Method { get; }

        /// <summary>The parameters expected by <see cref="Method"/>.</summary>
        public ParameterInfo[] Parameters { get; }

        /// <summary>Whether the command can be invoked without an object instance.</summary>
        public bool IsStatic => Method.IsStatic;
    }
}
