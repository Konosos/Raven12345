using UnityEngine;

namespace Raven12345
{
    public class Factory
    {
        private Transform parent;
        private GameObject pref;
        private string resourceLink;
        public Factory Parent(Transform parent)
        {
            this.parent = parent;
            return this;
        }
        public Factory Prefab(GameObject pref)
        {
            this.pref = pref;
            return this;
        }
        public Factory ResourceLink(string resourceLink)
        {
            this.resourceLink = resourceLink;
            return this;
        }

        public T Create<T>() where T : class
        {
            if (pref == null)
                pref = Resources.Load<GameObject>(resourceLink);

            if (pref == null)
            {
                Debug.LogError($"Not found pref to create {typeof(T)} in factory");
                return null;
            }

            GameObject obj = GameObject.Instantiate(pref, parent);
            if (obj.TryGetComponent<T>(out var instance))
                return instance;
            Debug.LogError($"Not found class {typeof(T)} in pref");
            return null;
        }
    }

}
