using UnityEngine;

public enum CharacterStat {
    MovementSpeed,
    PerceptionSighted
}

public interface ICharacterStatProvider {
    void ModifyStat(CharacterStat stat, ref float value);
}

public class CharacterStats : MonoBehaviour {
    ICharacterStatProvider[] providers;

    public void ModifyStat(CharacterStat stat, ref float value) {
        foreach (var provider in providers) {
            provider.ModifyStat(stat, ref value);
        }
    }

    void Awake() {
        providers = GetComponents<ICharacterStatProvider>();
    }
}
