using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // A body the game's donkey left on the ground at the crossroads (Mounts.CorpseLeft, docs/corpse-delivery.md):
    // the body item's id, its GUID (the drop's UniqueId, which is the item's), the GD point it was left at and the
    // position.
    public sealed class CorpseInfo
    {
        public string Id { get; }
        public string UniqueId { get; }
        public string Point { get; }
        public Vector3 Position { get; }

        internal CorpseInfo(string id, string uniqueId, string point, Vector3 position)
        {
            Id = id;
            UniqueId = uniqueId;
            Point = point;
            Position = position;
        }

        public override string ToString() => $"{Id} at {Point} {Position:F1}";
    }
}
