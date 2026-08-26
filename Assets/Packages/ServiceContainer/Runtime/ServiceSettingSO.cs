using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raven12345
{
    public abstract class ServiceSettingSO : ScriptableObject
    {
        public abstract void Register(ServiceContainer container);
    }

}
