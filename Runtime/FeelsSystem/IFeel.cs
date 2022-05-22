using System;
using UnityEngine;

namespace GameFramework.FeelsSystem {
    public interface IFeel {
        void Do() => throw new Exception("Not implemented");
        void Do(Vector3 position, Quaternion rotation) => Do();
    }
}