using System;
using UnityEngine;

namespace Raven12345.Servicer
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SceneInstaller : MonoBehaviour
    {
        [SerializeField] private ServiceRegistration[] registrations;
        [SerializeField] private ServiceSettingSO[] settings;

        private void Awake()
        {
            ServiceContainer container = ServiceContainer.Scene;

            foreach (ServiceSettingSO setting in settings)
            {
                if (setting == null)
                {
                    Debug.LogError("SceneInstaller contains an empty service setting.", this);
                    continue;
                }

                setting.Register(container);
            }

            foreach (ServiceRegistration registration in registrations)
            {
                Register(container, registration);
            }
        }

        private static void Register(ServiceContainer container, ServiceRegistration registration)
        {
            if (registration == null || registration.Component == null)
            {
                Debug.LogError("SceneInstaller contains an empty service registration.");
                return;
            }

            Component component = registration.Component;
            Type componentType = component.GetType();

            if (registration.Mode is RegistrationMode.Self or RegistrationMode.SelfAndInterfaces)
                container.Register(componentType, component);

            if (registration.Mode is RegistrationMode.Interfaces or RegistrationMode.SelfAndInterfaces)
            {
                foreach (Type interfaceType in componentType.GetInterfaces())
                    container.Register(interfaceType, component);
            }
        }

        [Serializable]
        public sealed class ServiceRegistration
        {
            [SerializeField] private Component component;
            [SerializeField] private RegistrationMode mode = RegistrationMode.Self;

            public Component Component => component;
            public RegistrationMode Mode => mode;
        }

        public enum RegistrationMode
        {
            Self,
            Interfaces,
            SelfAndInterfaces,
        }
    }

}
