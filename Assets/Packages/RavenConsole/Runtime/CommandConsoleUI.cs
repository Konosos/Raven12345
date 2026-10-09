using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
// Match simulated touch coordinates to the simulated display.
using Screen = UnityEngine.Device.Screen;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

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

        [Header("Swipe To Open")]
        [SerializeField] private bool swipeToOpen = true;
        [SerializeField, Range(0.01f, 0.25f)] private float swipeStartArea = 0.05f;
        [SerializeField, Range(0.01f, 0.5f)] private float swipeDistance = 0.12f;
        [SerializeField, Min(0.05f)] private float swipeMaxDuration = 0.5f;
        [SerializeField, Min(1f)] private float swipeVerticalRatio = 2f;

        private bool _trackingSwipe;
        private int _swipeFingerId;
        private Vector2 _swipeStart;
        private float _swipeStartTime;
        private Rect _swipeScreenBounds;
        private Vector2Int _swipeScreenSize;

        private bool _hasOpened;

        /// <summary>Whether the console panel is currently visible and interactive.</summary>
        public bool IsOpen => consolePanel != null && consolePanel.interactable;

        private void Awake()
        {
            SetOpen(openOnStart, false);
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (clearButton != null) clearButton.onClick.AddListener(Clear);
            if (submitButton != null) submitButton.onClick.AddListener(Submit);
            if (console != null) console.OutputChanged += ScrollToBottom;
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Disable();
#endif
            _trackingSwipe = false;
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (clearButton != null) clearButton.onClick.RemoveListener(Clear);
            if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
            if (console != null) console.OutputChanged -= ScrollToBottom;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _trackingSwipe = false;
        }

        private void Update()
        {
            if (!swipeToOpen || IsOpen)
            {
                _trackingSwipe = false;
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            if (touches.Count > 0)
            {
                if (touches.Count != 1)
                {
                    _trackingSwipe = false;
                    return;
                }

                var touch = touches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    BeginSwipe(touch.screenPosition, touch.touchId);
                if (_trackingSwipe && _swipeFingerId == touch.touchId)
                {
                    if (touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                        ContinueSwipe(touch.screenPosition);
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                        touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        _trackingSwipe = false;
                }
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                _trackingSwipe = false;
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame) BeginSwipe(mouse.position.ReadValue(), -1);
            if (_trackingSwipe && _swipeFingerId == -1 &&
                (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                ContinueSwipe(mouse.position.ReadValue());
            if (!mouse.leftButton.isPressed) _trackingSwipe = false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Prefer real touch input over Unity's simulated mouse events.
            if (Input.touchCount > 0)
            {
                if (Input.touchCount != 1)
                {
                    _trackingSwipe = false;
                    return;
                }

                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    BeginSwipe(touch.position, touch.fingerId);
                if (_trackingSwipe && _swipeFingerId == touch.fingerId)
                {
                    if (touch.phase != TouchPhase.Canceled)
                        ContinueSwipe(touch.position);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        _trackingSwipe = false;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0)) BeginSwipe(Input.mousePosition, -1);
            if (_trackingSwipe && _swipeFingerId == -1 &&
                (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
                ContinueSwipe(Input.mousePosition);
            if (!Input.GetMouseButton(0)) _trackingSwipe = false;
#endif
        }

        private void BeginSwipe(Vector2 position, int fingerId)
        {
            _trackingSwipe = false;
            Rect area = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!area.Contains(position) || position.y < area.yMax - area.height * swipeStartArea)
                return;

            _swipeStart = position;
            _swipeFingerId = fingerId;
            _swipeStartTime = Time.unscaledTime;
            _swipeScreenBounds = area;
            _swipeScreenSize = new Vector2Int(Screen.width, Screen.height);
            _trackingSwipe = true;
        }

        private void ContinueSwipe(Vector2 position)
        {
            if (Time.unscaledTime - _swipeStartTime > swipeMaxDuration ||
                Screen.width != _swipeScreenSize.x || Screen.height != _swipeScreenSize.y ||
                !_swipeScreenBounds.Contains(position))
            {
                _trackingSwipe = false;
                return;
            }

            Vector2 delta = position - _swipeStart;
            float downDistance = -delta.y;
            if (downDistance >= _swipeScreenBounds.height * swipeDistance &&
                downDistance >= Mathf.Abs(delta.x) * swipeVerticalRatio)
            {
                _trackingSwipe = false;
                Open();
            }
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
