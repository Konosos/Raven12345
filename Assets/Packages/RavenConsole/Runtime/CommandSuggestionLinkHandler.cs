using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Raven12345
{
    /// <summary>Forwards taps on TMP suggestion links to the parent suggestion view.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CommandSuggestionLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text _text;
        private CommandSuggestionView _view;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _view = GetComponentInParent<CommandSuggestionView>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _view?.HandleLinkClick(_text, eventData);
        }
    }
}
