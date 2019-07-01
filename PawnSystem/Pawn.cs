using System;
using UnityEngine;

public abstract class PawnController {
    public Pawn pawn {
        get;
        internal set;
    }

    public abstract void OnPossess(Pawn pawn);
    public abstract void OnUnpossess();
    public abstract void Tick();

    public void Possess(Pawn newPawn) {
        if (newPawn == null)
            throw new ArgumentNullException("newPawn");

        Unpossess();

        if (newPawn.controller != null) {
            newPawn.controller.Unpossess();
        }

        pawn = newPawn;
        newPawn.controller = this;

        OnPossess(newPawn);
        newPawn.OnPossession();
    }
    public void Unpossess() {
        if (pawn == null)
            return;

        pawn.OnUnpossession();
        OnUnpossess();

        pawn.controller = null;
        pawn = null;
    }
}



public class Pawn : MonoBehaviour {
    public PawnController controller;

    public virtual void Tick() {
        if (controller != null) {
            controller.Tick();
        }
    }

    public virtual void OnPossession() { }
    public virtual void OnUnpossession() { }
    public virtual void AddMovementInput(Vector3 worldDirection) { }
    public virtual void AddYawInput(float value) { }
    public virtual void AddPitchInput(float value) { }
}
