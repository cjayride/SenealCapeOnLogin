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
        public const string PluginVersion = "1.0.7";

        private void Awake()
        {
            new Harmony(PluginGuid).PatchAll();
            if (!SenealCapeSlot.Available)
                Logger.LogWarning("SeneaL CapeSlot API not found; cape re-apply will not run.");
            else
                Logger.LogInfo("Cape slot re-apply after load; extra cape unequip when backpack occupies the shoulder.");
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

        internal static ItemDrop.ItemData ExtraCape => SlotItem != null ? SlotItem() : null;

        internal static bool IsCapePublic(ItemDrop.ItemData item) => IsCape(item);

        internal static bool IsExtraCape(ItemDrop.ItemData item)
        {
            return item != null && SlotItem != null && SlotItem() == item;
        }

        /// <summary>
        /// CapeSlot.EquipPre no-ops when <c>item == Item</c>. After a failed unequip
        /// that left the static ref set with <c>m_equipped == false</c>, every later
        /// EquipItem is refused. Drop the stale ref so Set() can run.
        /// </summary>
        internal static void ForgetIfNotWorn(ItemDrop.ItemData item)
        {
            if (item == null || SlotItem == null || SlotItem() != item || item.m_equipped)
                return;

            SlotItem() = null;
            if (Owner != null)
                Owner() = null;
        }

        /// <summary>
        /// CapeSlot.UnequipPre Clear()s the extra slot and skips vanilla unequip.
        /// If the cape is also <c>m_shoulderItem</c> (backpack already off), it stays
        /// worn for stats while SeneaL Evicts it into the bag.
        /// </summary>
        internal static void ForceOff(Player player, ItemDrop.ItemData cape)
        {
            if (player == null || cape == null || !IsCape(cape))
                return;

            if (SlotItem != null && SlotItem() == cape)
            {
                SlotItem() = null;
                if (Owner != null)
                    Owner() = null;
            }

            cape.m_equipped = false;
            if (ShoulderItem(player) == cape)
                ShoulderItem(player) = null;

            try
            {
                UpdateModifiers?.Invoke(player);
                UpdateEquipment?.Invoke(player, 0f);
            }
            catch
            {
            }
        }

        /// <summary>
        /// After a backpack leaves the shoulder, SeneaL re-equips the cape on
        /// <c>m_shoulderItem</c> but does not call <c>Slots.Relocate</c>, so the
        /// cape stays in the bag grid (stats on, cape cell empty).
        /// </summary>
        /// <param name="afterBackpackSync">Shoulder-only cape after removing a backpack (SeneaL has not updated the slot ref yet).</param>
        internal static bool PlaceCapeInUi(Player player, bool afterBackpackSync = false)
        {
            if (!SenealInventorySlots.Available || player == null || player != Player.m_localPlayer)
                return false;

            var inventory = player.GetInventory();
            if (inventory == null)
                return false;

            var slotItem = SlotItem();
            var cape = ShoulderItem(player);
            if (!IsCape(cape) || !cape.m_equipped)
                return false;

            if (!afterBackpackSync && slotItem != cape)
                return false;

            SenealInventorySlots.PlaceInEquipRow(inventory, cape);
            return true;
        }

        internal static bool TryReapply(Player player, bool loadWindow, ItemDrop.ItemData loginCape)
        {
            if (!Available || player == null || player != Player.m_localPlayer)
                return false;

            if (!SenealBackpacks.Ready)
                return false;

            var inventory = player.GetInventory();
            if (inventory == null)
                return false;

            var shoulder = ShoulderItem(player);
            var slotItem = SlotItem();
            if (shoulder != null && !SenealBackpacks.IsBackpack(shoulder))
            {
                if (slotItem == null)
                    return false;

                return PlaceCapeInUi(player);
            }

            var cape = ResolveCape(inventory, slotItem, loginCape);
            if (cape == null)
                return false;

            bool worn = cape.m_equipped || (loadWindow && loginCape != null && cape == loginCape);
            if (!worn)
                return false;

            ForgetIfNotWorn(cape);

            var owner = Owner != null ? Owner() : null;
            if (owner != player || slotItem != cape)
                Set(player, cape, false);

            UpdateModifiers(player);
            UpdateEquipment?.Invoke(player, 0f);
            SenealInventorySlots.PlaceInEquipRow(inventory, cape);

            return SlotItem() == cape;
        }

        private static ItemDrop.ItemData ResolveCape(Inventory inventory, ItemDrop.ItemData slotItem, ItemDrop.ItemData loginCape)
        {
            if (slotItem != null && inventory.ContainsItem(slotItem))
                return slotItem;

            if (loginCape != null && inventory.ContainsItem(loginCape))
                return loginCape;

            var items = inventory.GetAllItems();
            var count = items.Count;
            ItemDrop.ItemData best = null;
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                if (!IsCape(item) || !item.m_equipped)
                    continue;

                if (best == null || CapeScore(item) > CapeScore(best))
                    best = item;
            }

            return best;
        }

        internal static bool HasEpicLootMagic(ItemDrop.ItemData item)
        {
            var data = item?.m_customData;
            if (data == null || data.Count == 0)
                return false;

            foreach (var key in data.Keys)
            {
                if (key.IndexOf("epicloot", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static int CapeScore(ItemDrop.ItemData item)
        {
            if (item == null)
                return int.MinValue;

            int score = item.m_equipped ? 1 : 0;
            if (HasEpicLootMagic(item))
                score += 4;

            return score;
        }

        internal static ItemDrop.ItemData FindBestEquippedCape(Inventory inventory)
        {
            if (inventory == null)
                return null;

            ItemDrop.ItemData best = null;
            var items = inventory.GetAllItems();
            var count = items.Count;
            for (int i = 0; i < count; i++)
            {
                var item = items[i];
                if (!IsCape(item) || !item.m_equipped)
                    continue;

                if (best == null || CapeScore(item) > CapeScore(best))
                    best = item;
            }

            return best;
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

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Load), typeof(ZPackage))]
    internal static class InventoryLoadRememberCape
    {
        private static void Postfix(Inventory __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory())
                return;

            CapeReapplyScheduler.RememberCapeFromInventory(__instance);
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

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    [HarmonyPriority(900)]
    internal static class EquipClearStaleExtraCape
    {
        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (!(__instance is Player) || __instance != Player.m_localPlayer)
                return;

            SenealCapeSlot.ForgetIfNotWorn(item);
        }
    }

    /// <summary>
    /// Extra-cape unequip from the SeneaL cell calls <see cref="Humanoid.UnequipItem"/>
    /// immediately. Vanilla capes go through <c>QueueUnequipAction</c> first so the
    /// unequip bar matches equip. Intercept extra-cape unequip and queue it; when
    /// the bar finishes, Valheim calls UnequipItem again and we let CapeSlot.Clear run.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    [HarmonyPriority(900)]
    internal static class QueueExtraCapeUnequip
    {
        private static readonly Action<Player, ItemDrop.ItemData> QueueUnequip =
            AccessTools.MethodDelegate<Action<Player, ItemDrop.ItemData>>(
                AccessTools.Method(typeof(Player), "QueueUnequipAction"));

        private static ItemDrop.ItemData _finishing;

        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (QueueUnequip == null || !(__instance is Player player) || player != Player.m_localPlayer || item == null)
                return true;

            if (!SenealCapeSlot.IsExtraCape(item))
                return true;

            float duration = item.m_shared != null ? item.m_shared.m_equipDuration : 0f;
            if (duration <= 0f)
                return true;

            if (_finishing == item)
            {
                _finishing = null;
                return true;
            }

            if (player.IsEquipActionQueued(item))
            {
                player.RemoveEquipAction(item);
                _finishing = null;
                return false;
            }

            CapeReapplyScheduler.CancelPending();
            _finishing = item;
            QueueUnequip(player, item);
            return false;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    [HarmonyPriority(Priority.Last)]
    internal static class UnequipBackpackMoveCapeToSlot
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (!(__instance is Player player) || player != Player.m_localPlayer || item == null)
                return;

            if (SenealCapeSlot.IsCapePublic(item))
            {
                CapeReapplyScheduler.CancelPending();
                SenealCapeSlot.ForceOff(player, item);
                return;
            }

            if (!SenealBackpacks.IsBackpack(item))
                return;

            CapeReapplyScheduler.ScheduleUiSync(player);
        }
    }

    [HarmonyPatch(typeof(Inventory), "Changed")]
    [HarmonyPriority(Priority.Last)]
    internal static class InventoryChangedRetryCape
    {
        private static void Postfix(Inventory __instance)
        {
            CapeReapplyScheduler.TryDuringLoadWindow(__instance);
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    [HarmonyPriority(Priority.Last)]
    internal static class EquipCapeMoveToSlot
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || !(__instance is Player player) || player != Player.m_localPlayer)
                return;

            if (!SenealCapeSlot.Available)
                return;

            if (SenealBackpacks.IsBackpack(item))
            {
                CapeReapplyScheduler.ScheduleReapply(player);
                return;
            }

            if (SenealCapeSlot.IsCapePublic(item))
                SenealCapeSlot.PlaceCapeInUi(player);
        }
    }

    internal static class CapeReapplyScheduler
    {
        private static int _generation;
        private static bool _loadWindow;
        private static ItemDrop.ItemData _loginCape;

        internal static void CancelPending()
        {
            _generation++;
            _loadWindow = false;
            _loginCape = null;
        }

        internal static void RememberCapeFromInventory(Inventory inventory)
        {
            var cape = SenealCapeSlot.FindBestEquippedCape(inventory);
            if (cape != null)
                _loginCape = cape;
        }

        internal static void ScheduleReapply(Player player)
        {
            if (player != Player.m_localPlayer || !SenealCapeSlot.Available)
                return;

            if (_loginCape == null)
                RememberCapeFromInventory(player.GetInventory());

            int generation = ++_generation;
            _loadWindow = true;
            player.StartCoroutine(ReapplyAfterLoad(player, generation));
        }

        internal static void TryDuringLoadWindow(Inventory inventory)
        {
            if (!_loadWindow || inventory == null)
                return;

            var player = Player.m_localPlayer;
            if (player == null || inventory != player.GetInventory())
                return;

            if (SenealCapeSlot.TryReapply(player, _loadWindow, _loginCape))
                CloseWindow();
        }

        internal static void ScheduleUiSync(Player player)
        {
            if (player != Player.m_localPlayer)
                return;

            player.StartCoroutine(SyncAfterUnequip(player));
        }

        private static void CloseWindow()
        {
            _generation++;
            _loadWindow = false;
            _loginCape = null;
        }

        private static bool IsDedicatedClient()
        {
            var net = ZNet.instance;
            return net != null && !net.IsServer();
        }

        private static IEnumerator SyncAfterUnequip(Player player)
        {
            yield return null;
            if (player == null || player != Player.m_localPlayer)
                yield break;

            if (SenealCapeSlot.PlaceCapeInUi(player, afterBackpackSync: true))
                yield break;

            SenealCapeSlot.TryReapply(player, _loadWindow, _loginCape);
        }

        private static IEnumerator ReapplyAfterLoad(Player player, int generation)
        {
            // Backpack probe, inventory load, and shoulder equip can finish after OnSpawned.
            for (int i = 0; i < 4; i++)
                yield return null;

            // Dedicated join / Rune of Recall can settle gear well after local listen-host spawn.
            int maxAttempts = IsDedicatedClient() ? 450 : 180;
            for (int i = 0; i < maxAttempts; i++)
            {
                if (generation != _generation)
                    yield break;

                if (player == null || player != Player.m_localPlayer)
                    yield break;

                if (SenealCapeSlot.TryReapply(player, _loadWindow, _loginCape))
                {
                    CloseWindow();
                    yield break;
                }

                yield return null;
                yield return null;
            }

            if (generation == _generation)
                _loadWindow = false;
        }
    }
}
