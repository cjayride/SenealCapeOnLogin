# Changelog

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
