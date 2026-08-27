using System;
using System.Collections.Generic;
using System.Text;

namespace Raven12345
{
    /// <summary>Splits console input into whitespace-delimited tokens.</summary>
    public static class CommandTokenizer
    {
        /// <summary>
        /// Tokenizes input while preserving text enclosed in single or double
        /// quotes. A backslash escapes the next character inside quoted text.
        /// </summary>
        public static bool TryTokenize(
            string input,
            out IReadOnlyList<string> tokens,
            out string error)
        {
            List<string> parsedTokens = new List<string>();
            tokens = parsedTokens;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            StringBuilder currentToken = new StringBuilder();
            char quote = '\0';
            bool escaping = false;
            bool tokenStarted = false;

            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
                if (escaping)
                {
                    currentToken.Append(character);
                    escaping = false;
                    tokenStarted = true;
                    continue;
                }

                if (quote != '\0')
                {
                    if (character == '\\')
                    {
                        escaping = true;
                    }
                    else if (character == quote)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        currentToken.Append(character);
                        tokenStarted = true;
                    }

                    continue;
                }

                if (character == '\'' || character == '"')
                {
                    quote = character;
                    tokenStarted = true;
                }
                else if (char.IsWhiteSpace(character))
                {
                    AddCurrentToken(parsedTokens, currentToken, ref tokenStarted);
                }
                else
                {
                    currentToken.Append(character);
                    tokenStarted = true;
                }
            }

            if (escaping)
            {
                error = "Input ends with an incomplete escape sequence.";
                return false;
            }

            if (quote != '\0')
            {
                error = "Input contains an unterminated quoted string.";
                return false;
            }

            AddCurrentToken(parsedTokens, currentToken, ref tokenStarted);
            return true;
        }

        private static void AddCurrentToken(
            List<string> tokens,
            StringBuilder currentToken,
            ref bool tokenStarted)
        {
            if (!tokenStarted)
            {
                return;
            }

            tokens.Add(currentToken.ToString());
            currentToken.Clear();
            tokenStarted = false;
        }
    }
}
