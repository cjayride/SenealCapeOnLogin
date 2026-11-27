# SeneaL Cape On Login — changelog (1.0.6)

Use this for Thunderstore “Changelog” or release notes.

## 1.0.6

- Longer cape re-apply after login, spawn, and respawn (including Rune of Recall): about 6s on listen-host, about 15s as a dedicated-server client.
- Retry on inventory changes while that load window is open so a late backpack/cape settle still restores the SeneaL cape slot.

## 1.0.5

- **Fixed:** Unequipping an Adventure Backpack relocates the cape into the SeneaL cape cell so the slot matches equipped stats.
- **Fixed:** Cape no longer snaps back or stays equipped when you unequip; backpack-off then cape-off clears stats on the first click.
- **Fixed:** Extra cape unequip from the SeneaL cell respects the equip duration bar; first-click unequip works with a backpack on the shoulder.

## 1.0.4

- **Verified:** Valheim 1.0.21 APIs (`Player.Load`, `OnSpawned(bool)`, `OnRespawn`, `m_shoulderItem`).
- **Faster:** cached SeneaL delegates/field refs; one retry coroutine instead of three overlapping ones; lighter inventory scan.

## 1.0.3

- **Verified:** Valheim 1.0.21 (Release build).
- **Updated:** BepInEx dependency `denikson-BepInExPack_Valheim-5.4.2360`.

## 1.0.2

- **Fixed:** After login, the cape now appears in SeneaL’s **equipment cape slot**, not left in the main inventory grid (calls `Slots.Relocate` after `CapeSlot.Set`).
- **Fixed:** Finds your cape in inventory when SeneaL’s internal slot was cleared on load; retries until Adventure Backpacks is ready (~3s max, exits immediately when done).
- Hooks: `Player.Load`, `OnSpawned`, `OnRespawn` (local player only).

## 1.0.1

- Login fix: resolve cape from inventory when `CapeSlot.Item` is empty; retry after backpack shoulder equip.

## 1.0.0

- Initial release.

**Requirements:** BepInEx, SeneaL UI, Adventure Backpacks. Client-side only.