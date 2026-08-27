using System;
using System.Runtime.CompilerServices;

namespace Raven12345
{
    /// <summary>
    /// Marks a method as a command that can be discovered by Command Console.
    /// </summary>
    /// <example>
    /// [Command("player.heal", "Restore the player's health.")]
    /// private void HealPlayer(int amount) { }
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAttribute : Attribute
    {
        /// <summary>The command text users enter in the console.</summary>
        public string Name { get; }

        /// <summary>A short explanation shown by future help and suggestion features.</summary>
        public string Description { get; }

        /// <param name="name">
        /// The command text. When omitted, the attributed method's name is used.
        /// </param>
        /// <param name="description">An optional explanation of the command.</param>
        public CommandAttribute(
            [CallerMemberName] string name = null,
            string description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A command needs a non-empty name.", nameof(name));
            }

            Name = name;
            Description = description ?? string.Empty;
        }
    }
}
