using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Raven12345
{
    /// <summary>
    /// Presentation controller for the reusable Quantum-style console widget.
    /// It owns panel visibility, buttons, focus and log auto-scrolling.
    /// </summary>
    public sealed class CommandConsoleUI : MonoBehaviour
    {
        [SerializeField] private SimpleCommandConsole console;
        [SerializeField] private CanvasGroup consolePanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button submitButton;
        [SerializeField] private ScrollRect outputScrollRect;
        [SerializeField] private bool openOnStart = true;

        private bool _hasOpened;

        /// <summary>Whether the console panel is currently visible and interactive.</summary>
        public bool IsOpen => consolePanel != null && consolePanel.interactable;

        private void Awake()
        {
            SetOpen(openOnStart, false);
        }

        private void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (clearButton != null) clearButton.onClick.AddListener(Clear);
            if (submitButton != null) submitButton.onClick.AddListener(Submit);
            if (console != null) console.OutputChanged += ScrollToBottom;
        }

        private void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (clearButton != null) clearButton.onClick.RemoveListener(Clear);
            if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
            if (console != null) console.OutputChanged -= ScrollToBottom;
        }

        /// <summary>Shows the panel and focuses the command field.</summary>
        public void Open()
        {
            SetOpen(true, true);
        }

        /// <summary>Hides the panel and returns selection to no UI control.</summary>
        public void Close()
        {
            SetOpen(false, false);
        }

        /// <summary>Toggles the panel.</summary>
        public void Toggle()
        {
            SetOpen(!IsOpen, !IsOpen);
        }

        /// <summary>Submits the current input text.</summary>
        public void Submit()
        {
            if (console != null && inputField != null)
            {
                console.ExecuteInput(inputField.text);
            }
        }

        /// <summary>Clears the displayed console output.</summary>
        public void Clear()
        {
            console?.ClearOutput();
        }

        private void SetOpen(bool isOpen, bool focusInput)
        {
            bool isFirstOpen = isOpen && !_hasOpened;
            if (isFirstOpen)
            {
                _hasOpened = true;
                console?.ClearOutput();
                console?.ScanCommands();
            }

            if (consolePanel != null)
            {
                consolePanel.alpha = isOpen ? 1f : 0f;
                consolePanel.interactable = isOpen;
                consolePanel.blocksRaycasts = isOpen;
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(!isOpen);
            }

            if (isOpen && focusInput && inputField != null)
            {
                EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
                inputField.ActivateInputField();
            }
        }

        private void ScrollToBottom()
        {
            if (outputScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            outputScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
