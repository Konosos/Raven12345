using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Raven12345
{
    /// <summary>Renders clickable command suggestions through TextMeshPro links.</summary>
    public sealed class CommandSuggestionView : MonoBehaviour
    {
        [SerializeField] private SimpleCommandConsole console;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text suggestionText;
        [SerializeField] private TMP_Text ghostHintText;
        [SerializeField] private GameObject popup;
        [SerializeField, Min(1)] private int maxResults = 6;

        private IReadOnlyList<CommandSuggestion> _suggestions;

        private void OnEnable()
        {
            ResolveSuggestionText();
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(Refresh);
                Refresh(inputField.text);
            }
        }

        private void OnDisable()
        {
            if (inputField != null)
            {
                inputField.onValueChanged.RemoveListener(Refresh);
            }
        }

        /// <summary>Refreshes the popup from the text currently being entered.</summary>
        public void Refresh(string input)
        {
            ResolveSuggestionText();
            _suggestions = console?.GetSuggestions(input, maxResults);
            bool hasSuggestions = _suggestions != null && _suggestions.Count > 0;

            if (popup != null)
            {
                popup.SetActive(hasSuggestions);
            }

            if (!hasSuggestions || suggestionText == null)
            {
                UpdateGhostHint(input);
                return;
            }

            StringBuilder builder = new StringBuilder();
            CommandConsoleTheme theme = GetTheme();
            for (int index = 0; index < _suggestions.Count; index++)
            {
                CommandSuggestion suggestion = _suggestions[index];
                if (suggestion.CanComplete)
                {
                    builder.Append("<link=");
                    builder.Append(index);
                    builder.Append(">");
                    AppendThemedText(
                        builder,
                        suggestion.Name,
                        theme,
                        theme != null ? theme.SuggestionCompletionColor : default);
                }
                else
                {
                    AppendThemedText(
                        builder,
                        suggestion.Name,
                        theme,
                        theme != null ? theme.SuggestionParameterColor : default);
                }

                if (!string.IsNullOrWhiteSpace(suggestion.GhostHint))
                {
                    AppendThemedText(
                        builder,
                        FormatSuggestionGhostHint(suggestion.GhostHint, theme),
                        theme,
                        theme != null ? theme.SuggestionSecondaryColor : default);
                }

                if (!string.IsNullOrWhiteSpace(suggestion.Description))
                {
                    AppendThemedText(
                        builder,
                        FormatSuggestionDescription(suggestion.Description, theme),
                        theme,
                        theme != null ? theme.SuggestionSecondaryColor : default);
                }

                if (suggestion.CanComplete)
                {
                    builder.AppendLine("</link>");
                }
                else
                {
                    builder.AppendLine();
                }
            }

            suggestionText.text = builder.ToString().TrimEnd();
            UpdateGhostHint(input);
        }

        /// <summary>Completes a suggestion selected from the popup.</summary>
        public void CompleteSuggestion(int index)
        {
            if (_suggestions == null ||
                index < 0 ||
                index >= _suggestions.Count ||
                inputField == null)
            {
                return;
            }

            CommandSuggestion suggestion = _suggestions[index];
            if (!suggestion.CanComplete)
            {
                return;
            }

            inputField.text = suggestion.Completion;
            inputField.caretPosition = inputField.text.Length;
            inputField.ActivateInputField();
            Refresh(inputField.text);
        }

        /// <summary>Completes the command represented by a tapped TMP link.</summary>
        public void HandleLinkClick(TMP_Text text, PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                text,
                eventData.position,
                null);
            if (linkIndex < 0)
            {
                return;
            }

            TMP_LinkInfo link = text.textInfo.linkInfo[linkIndex];
            if (int.TryParse(link.GetLinkID(), out int suggestionIndex))
            {
                CompleteSuggestion(suggestionIndex);
            }
        }

        private void ResolveSuggestionText()
        {
            if (suggestionText == null && popup != null)
            {
                suggestionText = popup.GetComponentInChildren<TMP_Text>(true);
            }

            if (ghostHintText == null && inputField != null)
            {
                Transform ghostTransform = inputField.transform.Find("Ghost Hint");
                ghostHintText = ghostTransform != null
                    ? ghostTransform.GetComponent<TMP_Text>()
                    : null;
            }
        }

        private void UpdateGhostHint(string input)
        {
            if (ghostHintText == null)
            {
                return;
            }

            ghostHintText.text = string.Empty;
            if (_suggestions == null || inputField == null ||
                inputField.caretPosition != input.Length)
            {
                return;
            }

            for (int index = 0; index < _suggestions.Count; index++)
            {
                CommandSuggestion suggestion = _suggestions[index];
                if (!suggestion.CanComplete)
                {
                    ShowParameterGhostHint(input, suggestion);
                    continue;
                }

                if (!suggestion.Completion.StartsWith(input, StringComparison.OrdinalIgnoreCase) ||
                    suggestion.Completion.Length <= input.Length)
                {
                    continue;
                }

                string invisiblePrefix = NoParse(input);
                string completionTail = NoParse(
                    suggestion.Completion.Substring(input.Length) + suggestion.GhostHint);
                ghostHintText.text = FormatGhostHint(invisiblePrefix, completionTail, GetTheme());
                return;
            }
        }

        private void ShowParameterGhostHint(string input, CommandSuggestion suggestion)
        {
            string ghostText;
            if (input.EndsWith(" ", StringComparison.Ordinal))
            {
                ghostText = suggestion.Name + FormatSuggestionGhostHint(suggestion.GhostHint, GetTheme());
            }
            else if (!string.IsNullOrEmpty(suggestion.GhostHint))
            {
                // The current value is already being composed, so only show
                // the parameters that follow it.
                ghostText = FormatSuggestionGhostHint(suggestion.GhostHint, GetTheme());
            }
            else
            {
                return;
            }

            ghostHintText.text = FormatGhostHint(NoParse(input), NoParse(ghostText), GetTheme());
        }

        private CommandConsoleTheme GetTheme()
        {
            return console != null ? console.Theme : null;
        }

        private static void AppendThemedText(
            StringBuilder builder,
            string text,
            CommandConsoleTheme theme,
            Color color)
        {
            string safeText = NoParse(text ?? string.Empty);
            builder.Append(theme == null ? safeText : theme.Colorize(safeText, color));
        }

        private static string FormatSuggestionGhostHint(string hint, CommandConsoleTheme theme)
        {
            if (string.IsNullOrEmpty(hint))
            {
                return string.Empty;
            }

            return theme == null ? $" {hint}" : theme.FormatSuggestionGhostHint(hint);
        }

        private static string FormatSuggestionDescription(string description, CommandConsoleTheme theme)
        {
            return theme == null ? $"  {description}" : theme.FormatSuggestionDescription(description);
        }

        private static string FormatGhostHint(
            string invisiblePrefix,
            string ghostHint,
            CommandConsoleTheme theme)
        {
            return theme == null
                ? $"{invisiblePrefix}{ghostHint}"
                : $"{theme.Colorize(invisiblePrefix, theme.GhostInputMaskColor)}" +
                  theme.Colorize(ghostHint, theme.GhostHintColor);
        }

        private static string NoParse(string value)
        {
            return $"<noparse>{value}</noparse>";
        }
    }
}
