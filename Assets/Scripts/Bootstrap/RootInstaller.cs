using CargoKing.Car;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;

namespace CargoKing.Bootstrap
{
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(CurrentCarProvider), Lifetime.Singleton, Reflex.Enums.Resolution.Lazy);
        }
    }
}
