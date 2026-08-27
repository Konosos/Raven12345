namespace Raven12345
{
    /// <summary>A completion or parameter hint presented while the user is typing.</summary>
    public sealed class CommandSuggestion
    {
        internal CommandSuggestion(
            string name,
            string description,
            string completion = null,
            string ghostHint = null)
        {
            Name = name;
            Description = description;
            Completion = completion;
            GhostHint = ghostHint;
        }

        /// <summary>Text displayed in the suggestion popup.</summary>
        public string Name { get; }

        /// <summary>Supplementary help displayed beside <see cref="Name"/>.</summary>
        public string Description { get; }

        /// <summary>
        /// The entire input to use when this suggestion is selected. A null
        /// value denotes an informational parameter hint.
        /// </summary>
        public string Completion { get; }

        /// <summary>
        /// Informational text appended to the ghost completion. Command
        /// suggestions use this for their remaining parameter signature.
        /// </summary>
        public string GhostHint { get; }

        /// <summary>Whether this suggestion can replace the current input.</summary>
        public bool CanComplete => !string.IsNullOrEmpty(Completion);
    }
}
