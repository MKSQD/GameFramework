using System;
using UnityEngine;

namespace GameFramework.FeelsSystem {
    public interface IFeel {
        void Exec() => throw new Exception("Not implemented");
        void ExecAtPosition(Vector3 position) => Exec();
    }
}