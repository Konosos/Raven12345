using UnityEngine;

namespace Raven12345.Servicer
{
    [CreateAssetMenu(fileName = "ServiceGlobalSettingSO", menuName = "Raven12345/Service Container/Global Setting")]
    public sealed class ServiceGlobalSettingSO : ScriptableObject
    {
        [SerializeField] private ServiceSettingSO[] settings;

        public void Register(ServiceContainer container)
        {
            foreach (ServiceSettingSO setting in settings)
            {
                if (setting == null)
                {
                    Debug.LogError("ServiceGlobalSettingSO contains an empty service setting.", this);
                    continue;
                }

                setting.Register(container);
            }
        }
    }
}
