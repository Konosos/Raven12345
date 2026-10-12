using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Raven12345
{
    /// <summary>Supplies UI input only when no scene-owned EventSystem is active.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ConsoleEventSystemFallback : MonoBehaviour
    {
        [SerializeField] private EventSystem fallback;
        private BaseInputModule _module;

        private void Awake()
        {
            if (fallback == null) return;
            fallback.enabled = false;
#if ENABLE_INPUT_SYSTEM
            var module = fallback.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = fallback.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            _module = module;
#elif ENABLE_LEGACY_INPUT_MANAGER
            _module = fallback.GetComponent<StandaloneInputModule>();
            if (_module == null) _module = fallback.gameObject.AddComponent<StandaloneInputModule>();
#endif
            foreach (var inputModule in fallback.GetComponents<BaseInputModule>())
                inputModule.enabled = false;
        }

        private void Update()
        {
            if (fallback == null) return;
            bool useFallback = _module != null;
            foreach (var system in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (system == fallback || !system.isActiveAndEnabled) continue;
                var owner = system.GetComponentInParent<ConsoleEventSystemFallback>();
                // Multiple console prefabs elect one fallback; scene-owned systems always win.
                if (owner == null || owner.GetInstanceID() < GetInstanceID())
                {
                    useFallback = false;
                    break;
                }
            }
            if (_module != null) _module.enabled = useFallback;
            fallback.enabled = useFallback;
            if (useFallback && EventSystem.current == null) EventSystem.current = fallback;
        }

        private void OnDisable()
        {
            if (_module != null) _module.enabled = false;
            if (fallback != null) fallback.enabled = false;
        }
    }
}
