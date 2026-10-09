namespace GK2DonkeyAPI.Api
{
    // The storage chest riding on a cart (docs/portable-chest.md), for Mounts.ChestLoaded / ChestUnloaded and
    // MountHandle.Chest. The chest is the game's own world object the whole time, never copied: Id is its definition
    // ("chest_rough", "chest" or "chest_good"), UniqueId the game's GUID of that object, as text, which stays the same
    // on the ground, on the cart, through saves. Items is what it holds at the moment of the call. (0.24.0)
    public sealed class ChestInfo
    {
        // The cart it rides on (or has just left).
        public MountHandle Cart { get; }

        public string Id { get; }

        public string UniqueId { get; }

        // How many item stacks are inside right now.
        public int Items { get; }

        internal ChestInfo(MountHandle cart, string id, string uniqueId, int items)
        {
            Cart = cart;
            Id = id;
            UniqueId = uniqueId;
            Items = items;
        }

        public override string ToString() => $"{Id} ({Items} item(s))";
    }
}
