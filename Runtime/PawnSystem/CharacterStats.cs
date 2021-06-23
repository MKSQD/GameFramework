using UnityEngine;

public enum CharacterStat {
    MovementSpeed
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

    void Start() {
        providers = GetComponents<ICharacterStatProvider>();
    }
}
