using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.Parsers;
using KotORUnity.KotOR.FileReaders;

namespace KotORUnity.Inventory
{
    // ══════════════════════════════════════════════════════════════════════════
    //  DATA CLASSES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Item slot identifiers matching KotOR's equipment slots.</summary>
    public enum EquipSlot
    {
        None        = -1,
        Head        = 0,
        Body        = 1,
        Hands       = 3,
        ArmL        = 4,
        ArmR        = 5,
        Ring1       = 6,
        Ring2       = 7,
        Belt        = 8,
        Boots       = 9,
        WeaponR     = 10,
        WeaponL     = 11,
        Cloak       = 12,
        Implant     = 13,
    }

    /// <summary>
    /// Parsed representation of a KotOR UTI (item template) GFF.
    /// </summary>
    [Serializable]
    public class ItemData
    {
        public string ResRef        = "";
        public string Tag           = "";
        public string BaseItemName  = "";   // from baseitems.2da row
        public uint   LocalizedName;        // TLK StrRef for name
        public uint   DescStrRef;            // TLK StrRef for description
        public string DisplayName   = "";   // resolved from TLK
        public string DescriptionText = ""; // resolved from TLK (DescIdentified field)

        public int    BaseItemRow   = 0;    // index into baseitems.2da
        public int    ItemType      = 0;    // maps to weapon/armour/misc
        public int    EquipableSlots = 0;   // bitmask of valid slots

        // Combat stats
        public int    AttackBonus   = 0;
        public int    DamageBonus   = 0;
        public int    DamageDie     = 6;    // d4/d6/d8 etc.
        public int    DamageNumDice = 1;

        // Defence stats
        public int    ACBonus       = 0;
        public int    MaxDexBonus   = 99;   // heavy armour limits dex

        // Misc
        public int    Cost          = 0;
        public int    StackSize     = 1;
        public int    Charges       = 0;
        public bool   Identified    = true;
        public bool   Stolen        = false;
        public string Icon          = "";   // texture resref for UI

        // Convenience aliases for UI code
        public string Name        => DisplayName;
        /// <summary>Item description resolved from dialog.tlk (DescStrRef field in UTI).</summary>
        public string Description => string.IsNullOrEmpty(DescriptionText) ? BaseItemName : DescriptionText;
        public string IconResRef  => Icon;

        // Parsed properties (item_property structs in UTI)
        public List<ItemProperty> Properties = new List<ItemProperty>();

        public bool IsWeapon  => (EquipableSlots & ((1 << (int)EquipSlot.WeaponR) |
                                                     (1 << (int)EquipSlot.WeaponL))) != 0;
        public bool IsArmour  => (EquipableSlots & (1 << (int)EquipSlot.Body)) != 0;
        public bool IsUsable  => Charges > 0;
    }

    [Serializable]
    public class ItemProperty
    {
        public int  PropertyType;   // row index in iprp_*
        public int  SubType;
        public int  CostTable;
        public int  CostValue;
        public int  Param1;
        public int  Param1Value;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UTI READER  —  parses UTI GFF → ItemData
    // ══════════════════════════════════════════════════════════════════════════
    public static class UtiReader
    {
        public static ItemData Parse(byte[] data, string resref = "")
        {
            var root = GffReader.Parse(data);
            if (root == null)
            {
                Debug.LogWarning($"[UtiReader] Failed to parse UTI '{resref}'.");
                return null;
            }

            var item = new ItemData { ResRef = resref };

            item.Tag             = GffReader.GetString(root, "Tag");
            uint nameRef         = (uint)GffReader.GetInt(root, "LocalizedName", 0);
            item.LocalizedName   = nameRef;
            item.DisplayName     = SceneBootstrapper.GetString(nameRef);
            if (string.IsNullOrEmpty(item.DisplayName))
                item.DisplayName = item.Tag;

            item.BaseItemRow     = GffReader.GetInt(root, "BaseItem",   0);
            item.EquipableSlots  = GffReader.GetInt(root, "EquipableSlots", 0);
            item.AttackBonus     = GffReader.GetInt(root, "AttackBonus", 0);
            item.DamageBonus     = GffReader.GetInt(root, "DmgBonus",    0);
            item.ACBonus         = GffReader.GetInt(root, "ArmorBonus",  0);
            item.MaxDexBonus     = GffReader.GetInt(root, "DexBonus",    99);
            item.Cost            = GffReader.GetInt(root, "Cost",        0);
            item.StackSize       = GffReader.GetInt(root, "StackSize",   1);
            item.Charges         = GffReader.GetInt(root, "Charges",     0);
            item.Identified      = GffReader.GetInt(root, "Identified",  1) != 0;
            item.Stolen          = GffReader.GetInt(root, "Stolen",      0) != 0;
            item.Icon            = GffReader.GetString(root, "ModelPart1");

            // Description text from dialog.tlk (DescIdentified StrRef)
            uint descRef = (uint)GffReader.GetInt(root, "DescIdentified", unchecked((int)0xFFFFFFFF));
            item.DescStrRef      = descRef;
            item.DescriptionText = SceneBootstrapper.GetString(descRef);
            if (string.IsNullOrEmpty(item.DescriptionText))
            {
                // Fall back to unidentified description
                uint descUnid = (uint)GffReader.GetInt(root, "Description", unchecked((int)0xFFFFFFFF));
                item.DescriptionText = SceneBootstrapper.GetString(descUnid);
            }

            // Item properties list
            var propList = root.GetField("PropertiesList")?.AsList();
            if (propList != null)
            {
                foreach (var p in propList)
                {
                    item.Properties.Add(new ItemProperty
                    {
                        PropertyType = GffReader.GetInt(p, "PropertyName", 0),
                        SubType      = GffReader.GetInt(p, "Subtype",      0),
                        CostTable    = GffReader.GetInt(p, "CostTable",    0),
                        CostValue    = GffReader.GetInt(p, "CostValue",    0),
                        Param1       = GffReader.GetInt(p, "Param1",       255),
                        Param1Value  = GffReader.GetInt(p, "Param1Value",  0),
                    });
                }
            }

            return item;
        }

        /// <summary>Alias for Parse — used by MerchantUI and other callers.</summary>
        public static ItemData ParseUti(byte[] data, string resref = "") => Parse(data, resref);

        /// <summary>Load an item by resref from the mounted archives.</summary>
        public static ItemData Load(string resref)
        {
            byte[] data = SceneBootstrapper.Resources?.GetResource(resref, ResourceType.UTI);
            if (data == null)
            {
                Debug.LogWarning($"[UtiReader] UTI not found: '{resref}'.");
                return null;
            }
            return Parse(data, resref);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  INVENTORY  —  a collection of items + equipment slots
    // ══════════════════════════════════════════════════════════════════════════
    public class Inventory
    {
        private readonly List<ItemData>                  _items     = new List<ItemData>();
        private readonly Dictionary<EquipSlot, ItemData> _equipped  = new Dictionary<EquipSlot, ItemData>();

        public IReadOnlyList<ItemData>                        Items    => _items;
        public IReadOnlyDictionary<EquipSlot, ItemData>       Equipped => _equipped;

        // ── ADD / REMOVE ──────────────────────────────────────────────────────
        public void AddItem(ItemData item)
        {
            if (item == null) return;

            // Stack identical items
            if (item.StackSize > 1 || item.Charges > 0)
            {
                var existing = _items.Find(x => x.ResRef == item.ResRef);
                if (existing != null)
                {
                    existing.StackSize += item.StackSize;
                    return;
                }
            }
            _items.Add(item);
        }

        public bool RemoveItem(ItemData item, int count = 1)
        {
            var existing = _items.Find(x => x == item);
            if (existing == null) return false;

            existing.StackSize -= count;
            if (existing.StackSize <= 0)
                _items.Remove(existing);
            return true;
        }

        public bool HasItem(string resref) => _items.Exists(x => x.ResRef == resref);

        /// <summary>Return all item resrefs (for save system).</summary>
        public System.Collections.Generic.List<string> GetAllItemResRefs()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var item in _items) list.Add(item.ResRef);
            return list;
        }

        /// <summary>Expose all slots as an IEnumerable of pseudo-InventorySlot.</summary>
        public System.Collections.Generic.IEnumerable<InventorySlot> AllSlots
        {
            get
            {
                foreach (var item in _items)
                    yield return new InventorySlot { Item = item };
            }
        }

        /// <summary>Remove all items (for save/load).</summary>
        public void Clear() { _items.Clear(); _equipped.Clear(); }

        // ── EQUIP / UNEQUIP ───────────────────────────────────────────────────
        public bool Equip(ItemData item, EquipSlot slot)
        {
            if (item == null) return false;

            // Validate slot compatibility
            if (!IsSlotCompatible(item, slot))
            {
                Debug.LogWarning($"[Inventory] Cannot equip '{item.DisplayName}' in slot {slot}.");
                return false;
            }

            // Unequip current
            if (_equipped.TryGetValue(slot, out var current))
                Unequip(slot);

            _equipped[slot] = item;
            _items.Remove(item);

            Core.EventBus.Publish(Core.EventBus.EventType.ItemEquipped,
                new InventoryEventArgs(item, slot));
            return true;
        }

        public void Unequip(EquipSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return;
            _equipped.Remove(slot);
            _items.Add(item);

            Core.EventBus.Publish(Core.EventBus.EventType.ItemUnequipped,
                new InventoryEventArgs(item, slot));
        }

        public ItemData GetEquipped(EquipSlot slot)
        {
            _equipped.TryGetValue(slot, out var item);
            return item;
        }

        // ── STAT HELPERS ──────────────────────────────────────────────────────
        public int GetTotalACBonus()
        {
            int total = 0;
            foreach (var kvp in _equipped)
                total += kvp.Value.ACBonus;
            return total;
        }

        public int GetMainHandAttackBonus()
            => GetEquipped(EquipSlot.WeaponR)?.AttackBonus ?? 0;

        public int GetOffHandAttackBonus()
            => GetEquipped(EquipSlot.WeaponL)?.AttackBonus ?? 0;

        // ── INTERNAL ─────────────────────────────────────────────────────────
        private static bool IsSlotCompatible(ItemData item, EquipSlot slot)
        {
            if (item.EquipableSlots == 0) return false;
            return (item.EquipableSlots & (1 << (int)slot)) != 0;
        }

        // ── BACKPACK MANIPULATION ─────────────────────────────────────────────

        /// <summary>Swap two items in the backpack by index.</summary>
        public void SwapItems(int indexA, int indexB)
        {
            if (indexA < 0 || indexB < 0) return;
            // Ensure list is large enough
            while (_items.Count <= Mathf.Max(indexA, indexB)) _items.Add(null);
            var tmp = _items[indexA];
            _items[indexA] = _items[indexB];
            _items[indexB] = tmp;
            // Remove nulls introduced
            _items.RemoveAll(x => x == null);
        }

        /// <summary>Sort backpack: weapons first, then armour, then misc. Alphabetical within category.</summary>
        public void Sort()
        {
            _items.Sort((a, b) =>
            {
                int catA = a.IsWeapon ? 0 : a.IsArmour ? 1 : 2;
                int catB = b.IsWeapon ? 0 : b.IsArmour ? 1 : 2;
                if (catA != catB) return catA.CompareTo(catB);
                return string.Compare(a.DisplayName, b.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  INVENTORY MANAGER  —  MonoBehaviour for the player's inventory
    // ══════════════════════════════════════════════════════════════════════════
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public Inventory PlayerInventory { get; private set; } = new Inventory();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        public void PickUp(string resref)
        {
            var item = UtiReader.Load(resref);
            if (item == null) return;
            PlayerInventory.AddItem(item);
            Debug.Log($"[Inventory] Picked up: {item.DisplayName}");
        }

        public bool Equip(string resref, EquipSlot slot)
        {
            var item = PlayerInventory.Items
                .FirstOrDefault(x => string.Equals(x.ResRef, resref, StringComparison.OrdinalIgnoreCase));
            if (item == null) item = UtiReader.Load(resref);
            if (item == null) return false;
            return PlayerInventory.Equip(item, slot);
        }

        public void Unequip(EquipSlot slot) => PlayerInventory.Unequip(slot);

        /// <summary>Remove one copy of an item by resref.</summary>
        public void RemoveItem(string resref)
        {
            var item = PlayerInventory.Items
                .FirstOrDefault(x => string.Equals(x.ResRef, resref, StringComparison.OrdinalIgnoreCase));
            if (item != null) PlayerInventory.RemoveItem(item);
        }

        // ── CREDITS ───────────────────────────────────────────────────────────
        // Credits are canonical in PartyManager.Table when available;
        // InventorySystem acts as a delegate to keep them in sync.
        private int _credits;

        public int PlayerCredits
        {
            get
            {
                // Always read from PartyManager if available (single source of truth)
                var pm = Party.PartyManager.Instance;
                if (pm != null) return pm.Table.Credits;
                return _credits;
            }
        }

        public void AddCredits(int amount)
        {
            var pm = Party.PartyManager.Instance;
            if (pm != null) pm.AddCredits(amount);
            else _credits = Mathf.Max(0, _credits + amount);
        }

        public void SpendCredits(int amount)
        {
            var pm = Party.PartyManager.Instance;
            if (pm != null) pm.AddCredits(-amount);
            else _credits = Mathf.Max(0, _credits - amount);
        }

        public void SetCredits(int amount)
        {
            var pm = Party.PartyManager.Instance;
            if (pm != null) pm.Table.Credits = Mathf.Max(0, amount);
            else _credits = Mathf.Max(0, amount);
        }

        /// <summary>
        /// Load an item by ResRef from the resource manager and add it to the player inventory.
        /// Used by EncounterManager loot distribution and NWScriptVM GiveItem.
        /// </summary>
        public void AddItemByResRef(string resRef)
        {
            if (string.IsNullOrEmpty(resRef)) return;
            var res = Bootstrap.SceneBootstrapper.Resources;
            if (res == null) { Debug.LogWarning($"[Inventory] No ResourceManager — cannot load {resRef}"); return; }

            byte[] raw = res.GetResource(resRef, KotOR.FileReaders.ResourceType.UTI);
            if (raw == null) { Debug.LogWarning($"[Inventory] Resource not found: {resRef}.uti"); return; }

            var item = KotOR.Parsers.ItemParser.Parse(raw, resRef);
            if (item != null) PlayerInventory.AddItem(item);
            else Debug.LogWarning($"[Inventory] Failed to parse item: {resRef}");
        }

        // ── SERIALIZATION (hooked into SaveManager) ───────────────────────────
        public List<InventorySaveData> GetSaveData()
        {
            var list = new List<InventorySaveData>();
            foreach (var item in PlayerInventory.Items)
                list.Add(new InventorySaveData(item.ResRef, item.StackSize, false, EquipSlot.None));
            foreach (var kvp in PlayerInventory.Equipped)
                list.Add(new InventorySaveData(kvp.Value.ResRef, 1, true, kvp.Key));
            return list;
        }

        public void LoadSaveData(List<InventorySaveData> data)
        {
            PlayerInventory = new Inventory();
            if (data == null) return;
            foreach (var entry in data)
            {
                var item = UtiReader.Load(entry.ResRef);
                if (item == null) continue;
                item.StackSize = entry.StackSize;
                PlayerInventory.AddItem(item);
                if (entry.IsEquipped) PlayerInventory.Equip(item, entry.Slot);
            }
        }
    }

    // ── SAVE DATA ─────────────────────────────────────────────────────────────
    [Serializable]
    public class InventorySaveData
    {
        public string   ResRef;
        public int      StackSize;
        public bool     IsEquipped;
        public EquipSlot Slot;

        public InventorySaveData(string r, int s, bool eq, EquipSlot sl)
        { ResRef = r; StackSize = s; IsEquipped = eq; Slot = sl; }
    }

    // ── EVENTARGS ─────────────────────────────────────────────────────────────
    public class InventoryEventArgs : Core.EventBus.GameEventArgs
    {
        public ItemData  Item { get; }
        public EquipSlot Slot { get; }
        public InventoryEventArgs(ItemData item, EquipSlot slot) { Item = item; Slot = slot; }
    }

    // ── INVENTORY SLOT  (lightweight view for UI iteration) ──────────────────
    public class InventorySlot
    {
        public ItemData Item;
    }
}
