# SeneaL Cape On Login — changelog (1.0.2)

Use this for Thunderstore “Changelog” or release notes.

## 1.0.2

- **Fixed:** After login, the cape now appears in SeneaL’s **equipment cape slot**, not left in the main inventory grid (calls `Slots.Relocate` after `CapeSlot.Set`).
- **Fixed:** Finds your cape in inventory when SeneaL’s internal slot was cleared on load; retries until Adventure Backpacks is ready (~3s max, exits immediately when done).
- Hooks: `Player.Load`, `OnSpawned`, `OnRespawn` (local player only).

## 1.0.1

- Login fix: resolve cape from inventory when `CapeSlot.Item` is empty; retry after backpack shoulder equip.

## 1.0.0

- Initial release.

**Requirements:** BepInEx, SeneaL UI, Adventure Backpacks. Client-side only.
