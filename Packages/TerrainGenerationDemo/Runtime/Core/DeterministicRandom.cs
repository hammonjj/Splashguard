using UnityEngine;

namespace BitBox.TerrainGeneration.Core
{
    public struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = (uint)seed;
            if (_state == 0u)
            {
                _state = 0x6d2b79f5u;
            }
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x == 0u ? 0x6d2b79f5u : x;
            return _state;
        }

        public float NextFloat()
        {
            return (NextUInt() & 0x00ffffffu) / 16777216f;
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, NextFloat());
        }
    }
}
