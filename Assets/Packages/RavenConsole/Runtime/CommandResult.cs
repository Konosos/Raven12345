namespace Raven12345
{
    /// <summary>The outcome of one command execution attempt.</summary>
    public sealed class CommandResult
    {
        private CommandResult(bool succeeded, string message, object value)
        {
            Succeeded = succeeded;
            Message = message;
            Value = value;
        }

        /// <summary>Whether the target command was invoked successfully.</summary>
        public bool Succeeded { get; }

        /// <summary>A display-ready success or error message.</summary>
        public string Message { get; }

        /// <summary>The return value, if the command returned one.</summary>
        public object Value { get; }

        public static CommandResult Success(object value = null)
        {
            return new CommandResult(true, value?.ToString() ?? string.Empty, value);
        }

        public static CommandResult Failure(string message)
        {
            return new CommandResult(false, message, null);
        }
    }
}
