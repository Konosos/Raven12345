using UnityEngine;

namespace Raven12345
{
    [DefaultExecutionOrder(-900)]
    public abstract class SceneContext : MonoBehaviour
    {
        private void Awake()
        {
            RegisterContext();
        }
        public abstract void RegisterContext();
    }

}
