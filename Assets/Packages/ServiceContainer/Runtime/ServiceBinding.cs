using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raven12345.Servicer
{
    [DefaultExecutionOrder(-1000)]
    public class ServiceBinding : MonoBehaviour
    {
        [SerializeField] private Component[] components;
        [SerializeField] private BindTypes bindTypes;
        [SerializeField] private BindFor bindFor;

        private void Awake()
        {
            Binding();
        }

        private void Binding()
        {
            foreach (var component in components)
            {
                switch (bindTypes)
                {
                    case BindTypes.Self:
                        BindingSelf(component);
                        break;
                    case BindTypes.AllInterfaces:
                        BindingAllInterfaces(component);
                        break;
                    case BindTypes.AllInterfacesAndSelf:
                        BindingSelf(component);
                        BindingAllInterfaces(component);
                        break;
                }
            }
        }

        private void BindingSelf(Component component)
        {
            Type selfType = component.GetType();
            ServiceContainer serviceContainer = bindFor == BindFor.Scene ? ServiceContainer.Scene : ServiceContainer.Global;
            serviceContainer.Register(selfType, component);
        }

        private void BindingAllInterfaces(Component component)
        {
            Type selfType = component.GetType();
            Type[] allInterfaces = selfType.GetInterfaces();
            ServiceContainer serviceContainer = bindFor == BindFor.Scene ? ServiceContainer.Scene : ServiceContainer.Global;

            foreach (Type iface in allInterfaces)
            {
                serviceContainer.Register(iface, component);
            }
        }

        public enum BindTypes
        {
            Self,
            AllInterfaces,
            AllInterfacesAndSelf,
        }

        public enum BindFor
        {
            Scene,
            Global,
        }
    }

}
