using Cube.Transport;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public struct DamageInfo : ISerializable {
        public byte Amount;
        public Vector3 Direction;
        public Vector3 Point;

        public DamageInfo(byte amount) {
            Amount = amount;
            Direction = Vector3.zero;
            Point = Vector3.zero;
        }

        public DamageInfo(byte amount, Vector3 direction, Vector3 point) {
            Amount = amount;
            Direction = direction.normalized;
            Point = point;
        }

        public void Serialize(BitStream bs) {
            bs.Write(Amount);
        }

        public void Deserialize(BitStream bs) {
            Amount = bs.ReadByte();
        }

        public override string ToString() {
            return Amount.ToString();
        }
    }

    public interface IDamageable {
        void ApplyDamage(DamageInfo info);
    }
}