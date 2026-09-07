using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;

namespace CargoKing.Bootstrap
{
    public class Greeter : MonoBehaviour
    {
        [Inject] private readonly IEnumerable<string> _strings;

        private void Start()
        {
            Debug.Log(string.Join(" ", _strings));
        }
    }
}
