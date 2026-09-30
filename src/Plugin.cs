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
        public const string PluginVersion = "1.0.2";

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
        private static readonly MethodInfo ReadyMethod;
        private static readonly MethodInfo RelocateMethod;
        private static readonly MethodInfo ChangedMethod;

        static SenealInventorySlots()
        {
            var slots = AccessTools.TypeByName("SeneaLUI.Inv.Slots");
            if (slots == null)
                return;

            ReadyMethod = AccessTools.Method(slots, "Ready");
            RelocateMethod = AccessTools.Method(slots, "Relocate");
            ChangedMethod = AccessTools.Method(slots, "Changed");
        }

        internal static bool Available => RelocateMethod != null && ReadyMethod != null;

        internal static void PlaceInEquipRow(Inventory inventory, ItemDrop.ItemData item)
        {
            if (!Available || inventory == null || item == null)
                return;

            try
            {
                if (!(bool)ReadyMethod.Invoke(null, new object[] { inventory }))
                    return;

                RelocateMethod.Invoke(null, new object[] { inventory, item });
                ChangedMethod?.Invoke(null, new object[] { inventory });
            }
            catch
            {
                // Best-effort UI layout; cape logic already applied via CapeSlot.Set.
            }
        }
    }

    internal static class SenealBackpacks
    {
        private static readonly MethodInfo IsBackpackMethod;
        private static readonly FieldInfo OnField;

        static SenealBackpacks()
        {
            var backpacks = AccessTools.TypeByName("SeneaLUI.Mods.Backpacks");
            IsBackpackMethod = backpacks != null ? AccessTools.Method(backpacks, "IsBackpack") : null;
            OnField = backpacks != null ? AccessTools.Field(backpacks, "On") : null;
        }

        internal static bool Ready => OnField != null && (bool)OnField.GetValue(null);

        internal static bool IsBackpack(ItemDrop.ItemData item)
        {
            if (item == null || IsBackpackMethod == null)
                return false;

            try
            {
                return (bool)IsBackpackMethod.Invoke(null, new object[] { item });
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// SeneaL stores capes in <c>CapeSlot.Item</c> while Adventure Backpacks wear the vanilla shoulder slot.
    /// After load, <c>Item</c> is often null even though the cape is still in the inventory.
    /// </summary>
    internal static class SenealCapeSlot
    {
        private static readonly MethodInfo SetMethod;
        private static readonly MethodInfo UpdateModifiersMethod;
        private static readonly MethodInfo UpdateEquipmentMethod;
        private static readonly FieldInfo ItemField;
        private static readonly FieldInfo OwnerField;
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ShoulderItem =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_shoulderItem");

        static SenealCapeSlot()
        {
            var capeSlot = AccessTools.TypeByName("SeneaLUI.Mods.CapeSlot");
            if (capeSlot == null)
                return;

            SetMethod = AccessTools.Method(capeSlot, "Set");
            UpdateModifiersMethod = AccessTools.Method(capeSlot, "UpdateModifiers");
            UpdateEquipmentMethod = AccessTools.Method(capeSlot, "UpdateEquipment");
            ItemField = AccessTools.Field(capeSlot, "Item");
            OwnerField = AccessTools.Field(capeSlot, "_owner");
        }

        internal static bool Available =>
            SetMethod != null && ItemField != null && UpdateModifiersMethod != null;

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

            var slotItem = ItemField.GetValue(null) as ItemDrop.ItemData;
            var cape = ResolveCape(inventory, slotItem);
            if (cape == null)
                return false;

            var owner = OwnerField?.GetValue(null) as Humanoid;
            if (owner != player || slotItem != cape || !cape.m_equipped)
                SetMethod.Invoke(null, new object[] { player, cape, false });

            UpdateModifiersMethod.Invoke(null, new object[] { player });
            if (UpdateEquipmentMethod != null)
                UpdateEquipmentMethod.Invoke(null, new object[] { player, 0f });

            SenealInventorySlots.PlaceInEquipRow(inventory, cape);

            return ItemField.GetValue(null) == cape;
        }

        private static ItemDrop.ItemData ResolveCape(Inventory inventory, ItemDrop.ItemData slotItem)
        {
            if (slotItem != null && inventory.ContainsItem(slotItem))
                return slotItem;

            ItemDrop.ItemData equipped = null;
            ItemDrop.ItemData fallback = null;

            foreach (var item in inventory.GetAllItems())
            {
                if (!IsCape(item))
                    continue;

                fallback ??= item;
                if (item.m_equipped)
                    equipped = item;
            }

            return equipped ?? fallback;
        }

        private static bool IsCape(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return false;

            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder
                   && !SenealBackpacks.IsBackpack(item);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    internal static class PlayerLoadReapplyCape
    {
        private static void Postfix(Player __instance) => CapeReapplyScheduler.ScheduleReapply(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
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
        internal static void ScheduleReapply(Player player)
        {
            if (player != Player.m_localPlayer || !SenealCapeSlot.Available)
                return;

            player.StartCoroutine(ReapplyAfterLoad(player));
        }

        private static IEnumerator ReapplyAfterLoad(Player player)
        {
            // Backpack probe, inventory load, and shoulder equip can finish many frames after OnSpawned.
            const int maxFrames = 180;
            for (int i = 0; i < maxFrames; i++)
            {
                if (player == null || player != Player.m_localPlayer)
                    yield break;

                if (SenealCapeSlot.TryReapply(player))
                    yield break;

                yield return null;
            }
        }
    }
}
