# Changelog

## 1.0.7

- **Fixed:** Epic Loot legendary **set** shoulder capes (e.g. Ragnar's Boneless Wrap) not returning to the SeneaL cape slot after login when Epic Loot load cleared `m_equipped` before the retry window.
- Snapshot the worn cape when inventory loads from the character save; restore that item during the login retry window even if the equipped flag was cleared later.
- When several capes qualify, prefer Epic Loot magic items over mundane duplicates (e.g. spare Wolf capes).

## 1.0.6

- Longer cape re-apply after login, spawn, and respawn (including Rune of Recall): about 6s on listen-host, about 15s as a dedicated-server client.
- Retry on inventory changes while that load window is open so a late backpack/cape settle still restores the SeneaL cape slot.
- Still only restores capes that are already equipped; taking the cape off still cancels pending retries.

## 1.0.5

- After unequipping an Adventure Backpack, the cape is moved back into the SeneaL cape cell (`Slots.Relocate`). SeneaL was re-equipping the cape on the shoulder for stats but leaving it in the bag grid, so the cape slot looked empty and the next click unequipped it (eitr dropped to 0).
- **Fixed:** Cape stayed equipped or snapped back when you tried to take it off. UI sync now runs only for cape/backpack equips and login recovery, and only on capes that are actually equipped.
- **Fixed:** Extra-cape unequip uses the normal equip duration bar; stale `CapeSlot.Item` refs are cleared so equip/unequip is not blocked.
- **Fixed:** Clicking the cape in the SeneaL cape cell while a backpack is on the shoulder unequips that extra cape on the first click.
- **Fixed:** After backpack off, then cape off, stats now drop on the first click (clears `m_shoulderItem` when SeneaL skips vanilla unequip).

## 1.0.4

- Verified `Player.Load(ZPackage)`, `Player.OnSpawned(bool)`, `Player.OnRespawn()`, and `Humanoid.m_shoulderItem` against Valheim 1.0.21.
- Cache SeneaL API delegates and static field refs instead of `MethodInfo.Invoke` / `FieldInfo.GetValue` on every retry.
- Collapse overlapping load/spawn/respawn retries into one generation-token coroutine; poll every other frame.
- Use SeneaL `CapeSlot.IsCape` when present and stop the inventory scan once an equipped cape is found.

## 1.0.3

- Verified against Valheim 1.0.21 (Release build).
- BepInEx dependency set to `denikson-BepInExPack_Valheim-5.4.2360`.

## 1.0.2

- After `CapeSlot.Set`, call SeneaL’s `Slots.Relocate` so the cape appears in the equipment row (not left in the bag grid). Clicking the cape did this via the normal `EquipItem` postfix.

## 1.0.1

- Fix: after login/load, re-find the cape in inventory when SeneaL’s `CapeSlot.Item` was cleared (1.0.0 only re-applied if that static field was already set).
- Hook `Player.Load` and retry for ~3 seconds so re-apply runs after Adventure Backpacks equips to the shoulder slot.

## 1.0.0

- Initial release: re-apply SeneaL cape slot after spawn/respawn.