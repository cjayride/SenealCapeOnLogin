using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.SenealCapeOnLogin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("seneaL.valheim.ui", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cjayride.SenealCapeOnLogin";
        public const string PluginName = "SeneaL Cape On Login";
        public const string PluginVersion = "1.0.4";

        private void Awake()
        {
            new Harmony(PluginGuid).PatchAll();
            if (!SenealCapeSlot.Available)
                Logger.LogWarning("SeneaL CapeSlot API not found; cape re-apply will not run.");
            else
                Logger.LogInfo("Re-applies SeneaL UI cape slot after load when a backpack uses the shoulder slot.");
        }
    }

    /// <summary>SeneaL UI inventory grid: equipping via UI calls <see cref="Relocate"/> after <c>EquipItem</c>.</summary>
    internal static class SenealInventorySlots
    {
        private static readonly Func<Inventory, bool> Ready;
        private static readonly Action<Inventory, ItemDrop.ItemData> Relocate;
        private static readonly Action<Inventory> Changed;

        static SenealInventorySlots()
        {
            var slots = AccessTools.TypeByName("SeneaLUI.Inv.Slots");
            if (slots == null)
                return;

            Ready = DelegateFor<Func<Inventory, bool>>(AccessTools.Method(slots, "Ready"));
            Relocate = DelegateFor<Action<Inventory, ItemDrop.ItemData>>(AccessTools.Method(slots, "Relocate"));
            Changed = DelegateFor<Action<Inventory>>(AccessTools.Method(slots, "Changed"));
        }

        internal static bool Available => Relocate != null && Ready != null;

        internal static void PlaceInEquipRow(Inventory inventory, ItemDrop.ItemData item)
        {
            if (!Available || inventory == null || item == null)
                return;

            try
            {
                if (!Ready(inventory))
                    return;

                Relocate(inventory, item);
                Changed?.Invoke(inventory);
            }
            catch
            {
                // Best-effort UI layout; cape logic already applied via CapeSlot.Set.
            }
        }

        private static T DelegateFor<T>(MethodInfo method) where T : Delegate
        {
            return method == null ? null : AccessTools.MethodDelegate<T>(method);
        }
    }

    internal static class SenealBackpacks
    {
        private static readonly Func<ItemDrop.ItemData, bool> IsBackpackFn;
        private static readonly AccessTools.FieldRef<bool> On;

        static SenealBackpacks()
        {
            var backpacks = AccessTools.TypeByName("SeneaLUI.Mods.Backpacks");
            if (backpacks == null)
                return;

            IsBackpackFn = DelegateFor<Func<ItemDrop.ItemData, bool>>(AccessTools.Method(backpacks, "IsBackpack"));
            var onField = AccessTools.Field(backpacks, "On");
            if (onField != null)
                On = AccessTools.StaticFieldRefAccess<bool>(onField);
        }

        internal static bool Ready => On != null && On();

        internal static bool IsBackpack(ItemDrop.ItemData item)
        {
            if (item == null || IsBackpackFn == null)
                return false;

            try
            {
                return IsBackpackFn(item);
            }
            catch
            {
                return false;
            }
        }

        private static T DelegateFor<T>(MethodInfo method) where T : Delegate
        {
            return method == null ? null : AccessTools.MethodDelegate<T>(method);
        }
    }

    /// <summary>
    /// SeneaL stores capes in <c>CapeSlot.Item</c> while Adventure Backpacks wear the vanilla shoulder slot.
    /// After load, <c>Item</c> is often null even though the cape is still in the inventory.
    /// </summary>
    internal static class SenealCapeSlot
    {
        private static readonly Action<Humanoid, ItemDrop.ItemData, bool> Set;
        private static readonly Action<Player> UpdateModifiers;
        private static readonly Action<Humanoid, float> UpdateEquipment;
        private static readonly Func<ItemDrop.ItemData, bool> IsCapeFn;
        private static readonly AccessTools.FieldRef<ItemDrop.ItemData> SlotItem;
        private static readonly AccessTools.FieldRef<Humanoid> Owner;
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ShoulderItem =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_shoulderItem");

        static SenealCapeSlot()
        {
            var capeSlot = AccessTools.TypeByName("SeneaLUI.Mods.CapeSlot");
            if (capeSlot == null)
                return;

            Set = DelegateFor<Action<Humanoid, ItemDrop.ItemData, bool>>(AccessTools.Method(capeSlot, "Set"));
            UpdateModifiers = DelegateFor<Action<Player>>(AccessTools.Method(capeSlot, "UpdateModifiers"));
            UpdateEquipment = DelegateFor<Action<Humanoid, float>>(AccessTools.Method(capeSlot, "UpdateEquipment"));
            IsCapeFn = DelegateFor<Func<ItemDrop.ItemData, bool>>(AccessTools.Method(capeSlot, "IsCape"));

            var itemField = AccessTools.Field(capeSlot, "Item");
            if (itemField != null)
                SlotItem = AccessTools.StaticFieldRefAccess<ItemDrop.ItemData>(itemField);

            var ownerField = AccessTools.Field(capeSlot, "_owner");
            if (ownerField != null)
                Owner = AccessTools.StaticFieldRefAccess<Humanoid>(ownerField);
        }

        internal static bool Available => Set != null && SlotItem != null && UpdateModifiers != null;

        internal static bool TryReapply(Player player)
        {
            if (!Available || player == null || player != Player.m_localPlayer)
                return false;

            if (!SenealBackpacks.Ready)
                return false;

            var inventory = player.GetInventory();
            if (inventory == null)
                return false;

            var shoulder = ShoulderItem(player);
            if (shoulder != null && !SenealBackpacks.IsBackpack(shoulder))
                return false;

            var slotItem = SlotItem();
            var cape = ResolveCape(inventory, slotItem);
            if (cape == null)
                return false;

            var owner = Owner != null ? Owner() : null;
            if (owner != player || slotItem != cape || !cape.m_equipped)
                Set(player, cape, false);

            UpdateModifiers(player);
            UpdateEquipment?.Invoke(player, 0f);
            SenealInventorySlots.PlaceInEquipRow(inventory, cape);

            return SlotItem() == cape;
        }

        private static ItemDrop.ItemData ResolveCape(Inventory inventory, ItemDrop.ItemData slotItem)
        {
            if (slotItem != null && inventory.ContainsItem(slotItem))
                return slotItem;

            ItemDrop.ItemData equipped = null;
            ItemDrop.ItemData fallback = null;
            var items = inventory.GetAllItems();
            var count = items.Count;
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                if (!IsCape(item))
                    continue;

                fallback ??= item;
                if (item.m_equipped)
                {
                    equipped = item;
                    break;
                }
            }

            return equipped ?? fallback;
        }

        private static bool IsCape(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return false;

            if (IsCapeFn != null)
            {
                try
                {
                    return IsCapeFn(item);
                }
                catch
                {
                    // Fall through to local check.
                }
            }

            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder
                   && !SenealBackpacks.IsBackpack(item);
        }

        private static T DelegateFor<T>(MethodInfo method) where T : Delegate
        {
            return method == null ? null : AccessTools.MethodDelegate<T>(method);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    internal static class PlayerLoadReapplyCape
    {
        private static void Postfix(Player __instance) => CapeReapplyScheduler.ScheduleReapply(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned), typeof(bool))]
    internal static class PlayerOnSpawnedReapplyCape
    {
        private static void Postfix(Player __instance) => CapeReapplyScheduler.ScheduleReapply(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnRespawn))]
    internal static class PlayerOnRespawnReapplyCape
    {
        private static void Postfix(Player __instance) => CapeReapplyScheduler.ScheduleReapply(__instance);
    }

    internal static class CapeReapplyScheduler
    {
        private static int _generation;

        internal static void ScheduleReapply(Player player)
        {
            if (player != Player.m_localPlayer || !SenealCapeSlot.Available)
                return;

            int generation = ++_generation;
            player.StartCoroutine(ReapplyAfterLoad(player, generation));
        }

        private static IEnumerator ReapplyAfterLoad(Player player, int generation)
        {
            // Backpack probe, inventory load, and shoulder equip can finish after OnSpawned.
            for (int i = 0; i < 4; i++)
                yield return null;

            const int maxAttempts = 90;
            for (int i = 0; i < maxAttempts; i++)
            {
                if (generation != _generation)
                    yield break;

                if (player == null || player != Player.m_localPlayer)
                    yield break;

                if (SenealCapeSlot.TryReapply(player))
                    yield break;

                yield return null;
                yield return null;
            }
        }
    }
}
