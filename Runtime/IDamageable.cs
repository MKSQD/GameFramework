using Cube.Transport;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public enum DamageType : byte {
        Physical,
        Fire,
        Cold,
    }

    public struct DamageInfo : ISerializable {
        public byte Amount;
        public DamageType Type;
        public Vector3 Direction;
        public Vector3 Point;
        public GameObject Who;

        public DamageInfo(byte amount, DamageType type, GameObject who) {
            Who = who;
            Amount = amount;
            Type = type;
            Direction = Vector3.zero;
            Point = Vector3.zero;
        }

        public DamageInfo(byte amount, DamageType type, GameObject who, Vector3 direction, Vector3 point) {
            Who = who;
            Amount = amount;
            Type = type;
            Direction = direction.normalized;
            Point = point;
        }

        public void Serialize(BitStream bs) {
            bs.Write(Amount);
            bs.WriteIntInRange((byte)Type, 0, 3);
        }

        public void Deserialize(BitStream bs) {
            Amount = bs.ReadByte();
            Type = (DamageType)bs.ReadIntInRange(0, 3);
        }

        public override string ToString() {
            return $"{Amount} {Type}";
        }
    }

    public interface IDamageable {
        void ApplyDamage(DamageInfo info);
    }
}