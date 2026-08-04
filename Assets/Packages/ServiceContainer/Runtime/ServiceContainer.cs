using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raven12345.Servicer
{
    public class ServiceContainer : MonoBehaviour
    {
        private static ServiceContainer globalContainer;

        public static ServiceContainer Global
        {
            get
            {
                if (globalContainer == null)
                {
                    GameObject go = new GameObject("Service Container [Global]");
                    DontDestroyOnLoad(go);
                    globalContainer = go.AddComponent<ServiceContainer>();

                    ServiceGlobalSettingSO globalSetting = Resources.Load<ServiceGlobalSettingSO>("ServiceGlobalSettingSO");

                    if (globalSetting != null)
                        globalSetting.Register(globalContainer);
                }
                return globalContainer;
            }
        }
        private static ServiceContainer sceneContainer;
        public static ServiceContainer Scene
        {
            get
            {
                if (sceneContainer == null)
                {
                    GameObject go = new GameObject("Service Container [Scene]");
                    sceneContainer = go.AddComponent<ServiceContainer>();
                }
                return sceneContainer;
            }
        }

        private readonly Dictionary<Type, object> services = new();

        public IEnumerable<object> RegisteredServices => services.Values;

        private readonly Dictionary<Type, Factory> factories = new();

        public bool TryGet<T>(out T service) where T : class
        {
            Type type = typeof(T);
            if (services.TryGetValue(type, out object obj))
            {
                if (!IsDestroyedUnityObject(obj))
                {
                    service = obj as T;
                    return service != null;
                }

                services.Remove(type);
            }
            if (factories.TryGetValue(type, out Factory factory))
            {
                service = factory.Create<T>();
                if (service != null)
                {
                    Register<T>(service);
                    return true;
                }
            }
            service = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            Type type = typeof(T);
            if (services.TryGetValue(type, out object obj))
            {
                if (!IsDestroyedUnityObject(obj))
                    return obj as T;

                services.Remove(type);
            }
            if (factories.TryGetValue(type, out Factory factory))
            {
                T service = factory.Create<T>();
                if (service != null)
                {
                    Register<T>(service);
                    return service;
                }
            }
            throw new ArgumentException($"Get: Service of type {type.FullName} not registered");
        }

        public T GetOrCreate<T>(bool registerAllInterfaces = false) where T : class
        {
            Type type = typeof(T);
            if (services.TryGetValue(type, out object obj))
            {
                if (!IsDestroyedUnityObject(obj))
                    return obj as T;

                services.Remove(type);
            }

            T instance = CreateTransient<T>();
            if (instance == null)
                throw new InvalidOperationException($"Cannot create service of type {type.FullName}");

            Register(instance);
            if (registerAllInterfaces)
                RegisterAllInterfaces(instance);
            return instance;
        }

        public T CreateTransient<T>() where T : class
        {
            Type type = typeof(T);
            if (type.IsSubclassOf(typeof(Component)))
            {
                GameObject go = new(type.Name, type);
                go.transform.parent = transform;
                return go.GetComponent<T>();
            }
            return Activator.CreateInstance(type) as T;
        }

        public void Register<T>(T service)
        {
            Register(typeof(T), service);
        }

        public void Register(Type type, object service)
        {
            if (IsSceneComponentRegisteredInGlobalContainer(service))
            {
                Component component = service as Component;
                Debug.LogError($"Register: Cannot register scene component '{component.name}' as a global service. " +
                    "Register it in ServiceContainer.Scene or move it under a DontDestroyOnLoad object.", component);
                return;
            }

            if (!type.IsInstanceOfType(service))
            {
                throw new ArgumentException("Type of service does not match type of service interface", nameof(service));
            }

            if (!services.TryAdd(type, service))
            {
                Debug.LogError($"Register: Service of type {type.FullName} already registered");
            }
        }

        private static bool IsDestroyedUnityObject(object service)
        {
            return service is UnityEngine.Object unityObject && unityObject == null;
        }

        private bool IsSceneComponentRegisteredInGlobalContainer(object service)
        {
            if (this != globalContainer || service is not Component component)
                return false;

            return component.gameObject.scene.name != "DontDestroyOnLoad";
        }

        public void RegisterAllInterfaces<T>(T service) where T : class
        {
            Type selfType = service.GetType();
            Type[] allInterfaces = selfType.GetInterfaces();
            foreach (Type iface in allInterfaces)
            {
                Register(iface, service);
            }
        }
        public void RegisterPrefab<T>(T prefab) where T : MonoBehaviour
        {
            T go = Instantiate(prefab, transform);
            go.name = prefab.name;
            Register(go);
        }
        public void RegisterLazySingleton<T>(Factory factory) where T : class
        {
            if (!factories.TryAdd(typeof(T), factory))
            {
                Debug.LogError($"Register: Factory of type {typeof(T).FullName} already registered");
            }
        }
        public void RegisterLazySingleton<T>(GameObject pref, Transform parent = null) where T : class
        {
            Factory factory = new Factory().Prefab(pref).Parent(parent);
            RegisterLazySingleton<T>(factory);
        }
        public void RegisterLazySingleton<T>(string resourceLink, Transform parent = null) where T : class
        {
            Factory factory = new Factory().ResourceLink(resourceLink).Parent(parent);
            RegisterLazySingleton<T>(factory);
        }
    }

}
