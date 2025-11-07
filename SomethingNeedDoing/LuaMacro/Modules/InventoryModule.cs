using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using NLua;
using SomethingNeedDoing.Core.Interfaces;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SomethingNeedDoing.LuaMacro.Modules;
public unsafe class InventoryModule : LuaModuleBase
{
    public override string ModuleName => "Inventory";
    protected override object? MetaIndex(LuaTable table, object key) => GetInventoryContainer(Enum.Parse<InventoryType>(key.ToString() ?? string.Empty));

    [LuaFunction] public InventoryContainerWrapper GetInventoryContainer(InventoryType container) => new(container);
    [LuaFunction]
    [Changelog("13.53", ChangelogType.Changed, "Renamed from GetInventoryItem to GetInventoryItemBySlot")]
    public InventoryItemWrapper? GetInventoryItemBySlot(InventoryType container, int slot) => new(InventoryManager.Instance()->GetInventoryContainer(container)->GetInventorySlot(slot));

    [LuaFunction]
    [Changelog("12.9")]
    [Changelog("12.10", ChangelogType.Fixed, "Support for Key Items")]
    public int GetItemCount(uint itemId)
    {
        var isHq = itemId < 2_000_000 && itemId % 500_000 != itemId;
        if (itemId < 2_000_000)
            itemId %= 500_000;
        return InventoryManager.Instance()->GetInventoryItemCount(itemId, isHq);
    }

    [LuaFunction]
    [Changelog("12.9")]
    public int GetHqItemCount(uint itemId)
    {
        return InventoryManager.Instance()->GetInventoryItemCount(itemId % 500_000, true);
    }

    [LuaFunction]
    [Changelog("12.17")]
    public int GetCollectableItemCount(uint itemId, int minimumCollectability)
    {
        minimumCollectability = Math.Clamp(minimumCollectability, 1, 1000);
        return InventoryManager.Instance()->GetInventoryItemCount(itemId, false, false, false, (short)minimumCollectability);
    }

    [LuaFunction]
    [Changelog("12.17")]
    public uint GetFreeInventorySlots()
    {
        return InventoryManager.Instance()->GetEmptySlotsInBag();
    }

    [LuaFunction]
    public unsafe InventoryItemWrapper? GetInventoryItem(uint itemId)
    {
        foreach (var type in Enum.GetValues<InventoryType>())
        {
            var container = InventoryManager.Instance()->GetInventoryContainer(type);
            if (container == null) continue;
            for (var i = 0; i < container->Size; i++)
                if (container->Items[i].ItemId == itemId)
                    return new(container, i);
        }
        return null;
    }

    [LuaFunction]
    [Changelog("12.8")]
    public List<InventoryItemWrapper> GetItemsInNeedOfRepairs(int durability = 0)
    {
        List<InventoryItemWrapper> list = [];
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item is null) continue;
            if (Convert.ToInt32(Convert.ToDouble(item->Condition) / 30000.0 * 100.0) <= durability)
                list.Add(new(item));
        }
        return list;
    }

    [LuaFunction]
    [Changelog("12.8")]
    public List<InventoryItemWrapper> GetSpiritbondedItems()
    {
        List<InventoryItemWrapper> list = [];
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item is null) continue;
            if (item->SpiritbondOrCollectability / 100 == 100)
                list.Add(new(item));
        }
        return list;
    }

    public unsafe class InventoryContainerWrapper(InventoryType container) : IWrapper
    {
        private readonly InventoryContainer* _container = InventoryManager.Instance()->GetInventoryContainer(container);
        [LuaDocs] public int Count => _container->Size;

        [LuaDocs]
        public int FreeSlots
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Count; i++)
                    if (_container->Items[i].ItemId == 0)
                        count++;
                return count;
            }
        }

        [LuaDocs]
        public List<InventoryItemWrapper> Items
        {
            get
            {
                List<InventoryItemWrapper> list = [];
                for (var i = 0; i < Count; i++)
                    if (_container->Items[i].ItemId != 0)
                        list.Add(new(_container, i));
                return list;
            }
        }

        [LuaDocs] public InventoryItemWrapper this[int index] => new(_container, index);
    }

    public unsafe class InventoryItemWrapper : IWrapper
    {
        private readonly InventoryType[] playerInv = [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
            InventoryType.ArmoryMainHand,
            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,
            InventoryType.ArmoryEar,
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings
            ];

        private InventoryItem* Item { get; set; }
        public InventoryItemWrapper(InventoryType container, int slot) => Item = InventoryManager.Instance()->GetInventoryContainer(container)->GetInventorySlot(slot);
        public InventoryItemWrapper(InventoryContainer* container, int slot) => Item = container->GetInventorySlot(slot);
        public InventoryItemWrapper(InventoryItem* item) => Item = item;
        public InventoryItemWrapper(GameInventoryItem item) => Item = (InventoryItem*)item.Address;
        public InventoryItemWrapper(uint itemId)
        {
            foreach (var inv in playerInv)
            {
                var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
                for (var i = 0; i < cont->Size; ++i)
                    if (cont->GetInventorySlot(i)->ItemId == itemId)
                        Item = cont->GetInventorySlot(i);
            }
        }

        [LuaDocs] public uint ItemId => Item->ItemId;
        [LuaDocs] public uint BaseItemId => Item->GetBaseItemId();
        [LuaDocs] public int Count => Item->Quantity;
        [LuaDocs] public ushort SpiritbondOrCollectability => Item->SpiritbondOrCollectability;
        [LuaDocs] public ushort Condition => Item->Condition;
        [LuaDocs] public uint GlamourId => Item->GlamourId;
        [LuaDocs] public bool IsHighQuality => Item->IsHighQuality();
        [LuaDocs][Changelog("13.53")] public bool IsCollectable => Item->IsCollectable();
        [LuaDocs][Changelog("13.53")] public bool IsEmpty => Item->IsEmpty();
        [LuaDocs] public InventoryItemWrapper? LinkedItem => Item->GetLinkedItem() is not null ? new(Item->GetLinkedItem()) : null;

        [LuaDocs] public InventoryType Container => Item->Container;
        [LuaDocs] public int Slot => Item->Slot;

        [LuaDocs]
        [Changelog("13.55")]
        public InventoryType ArmouryContainer => GetRow<Sheets.Item>(ItemId)?.EquipSlotCategory.Value switch
        {
            { MainHand: 1 } => InventoryType.ArmoryMainHand,
            { OffHand: 1 } => InventoryType.ArmoryOffHand,
            { Head: 1 } => InventoryType.ArmoryHead,
            { Body: 1 } => InventoryType.ArmoryBody,
            { Gloves: 1 } => InventoryType.ArmoryHands,
            { Waist: 1 } => InventoryType.ArmoryWaist,
            { Legs: 1 } => InventoryType.ArmoryLegs,
            { Feet: 1 } => InventoryType.ArmoryFeets,
            { Ears: 1 } => InventoryType.ArmoryEar,
            { Neck: 1 } => InventoryType.ArmoryNeck,
            { Wrists: 1 } => InventoryType.ArmoryWrist,
            { FingerL: 1 } => InventoryType.ArmoryRings,
            { FingerR: 1 } => InventoryType.ArmoryRings,
            { SoulCrystal: 1 } => InventoryType.ArmorySoulCrystal,
            _ => InventoryType.Invalid
        };

        [LuaDocs] public void Use() => Game.UseItem(ItemId, IsHighQuality);

        [LuaDocs]
        [Changelog("12.8")]
        public void Desynth()
        {
            if (GetRow<Sheets.Item>(ItemId)?.Desynth == 0)
                return;

            AgentSalvage.Instance()->SalvageItem(Item);
            var retval = new AtkValue();
            Span<AtkValue> param = [
                new AtkValue { Type = ValueType.Int, Int = 0 },
                new AtkValue { Type = ValueType.Bool, Byte = 1 }
            ];
            AgentSalvage.Instance()->AgentInterface.ReceiveEvent(&retval, param.GetPointer(0), 2, 1);
        }

        [LuaDocs]
        [Changelog("12.51")]
        [Changelog("13.46", ChangelogType.Fixed, "Potential fix for fake movement")]
        [Changelog("13.56", ChangelogType.Fixed, "Support for EquippedItems and RetainerEquippedItems containers")]
        public void MoveItemSlot(InventoryType destinationContainer)
            => InventoryManager.Instance()->MoveItemSlot(Container, (ushort)Slot, destinationContainer, GetFirstEmptySlot(destinationContainer, ArmouryContainer), true);
    }

    private static unsafe ushort GetFirstEmptySlot(InventoryType container, InventoryType armouryContainer)
    {
        if (container is InventoryType.EquippedItems or InventoryType.RetainerEquippedItems)
        {
            return armouryContainer switch
            {
                InventoryType.ArmoryMainHand => EquippedSlot.MainHand,
                InventoryType.ArmoryOffHand => EquippedSlot.OffHand,
                InventoryType.ArmoryHead => EquippedSlot.Head,
                InventoryType.ArmoryBody => EquippedSlot.Body,
                InventoryType.ArmoryHands => EquippedSlot.Gloves,
                InventoryType.ArmoryWaist => EquippedSlot.Belt,
                InventoryType.ArmoryLegs => EquippedSlot.Legs,
                InventoryType.ArmoryFeets => EquippedSlot.Feet,
                InventoryType.ArmoryEar => EquippedSlot.Ear,
                InventoryType.ArmoryNeck => EquippedSlot.Neck,
                InventoryType.ArmoryWrist => EquippedSlot.Wrists,
                InventoryType.ArmoryRings => EquippedSlot.FingerL,
                InventoryType.ArmorySoulCrystal => EquippedSlot.SoulCrystal,
                _ => throw new ArgumentOutOfRangeException(nameof(armouryContainer), "Invalid armoury container type"),
            };
        }
        else
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(container);
            for (ushort i = 0; i < cont->Size; i++)
                if (cont->Items[i].ItemId == 0)
                    return i;
            return 0;
        }
    }

    private static class EquippedSlot // imagine enums being useful
    {
        public const ushort
            MainHand = 0,
            OffHand = 1,
            Head = 2,
            Body = 3,
            Gloves = 4,
            Belt = 5,
            Legs = 6,
            Feet = 7,
            Ear = 8,
            Neck = 9,
            Wrists = 10,
            FingerL = 11,
            FingerR = 12,
            SoulCrystal = 13;
    }
}
