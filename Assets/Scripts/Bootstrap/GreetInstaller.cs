using Reflex.Core;
using UnityEngine;

namespace CargoKing.Bootstrap
{
    public class GreetInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            builder.RegisterValue("World"); // Note that values are always registered as singletons
        }
    }
}
