namespace GK2DonkeyAPI.Api
{
    // One item on a cart, for the cargo events (Mounts.CargoLoaded / CargoUnloaded) and MountHandle.Cargo.
    // Two zombies have the same Id ("body_zombie"); UniqueId tells them apart (the game's item GUID, which for a zombie
    // is its zombie's), and ZombieName is the zombie's own name. (0.22.0)
    public sealed class CargoItem
    {
        // The item id, e.g. "wood", "body_corpse", "body_zombie".
        public string Id { get; }

        // The game's GUID of this item, as text: stays the same while the item is on the cart, through doors and saves.
        public string UniqueId { get; }

        // A zombie (item group "zombie"); its organs and gear travel with it.
        public bool IsZombie { get; }

        // The zombie's name as the game holds it; null for anything else.
        public string ZombieName { get; }

        internal CargoItem(string id, string uniqueId, bool isZombie, string zombieName)
        {
            Id = id;
            UniqueId = uniqueId;
            IsZombie = isZombie;
            ZombieName = zombieName;
        }

        public override string ToString() => IsZombie && !string.IsNullOrEmpty(ZombieName) ? $"{Id} ({ZombieName})" : Id;
    }
}
