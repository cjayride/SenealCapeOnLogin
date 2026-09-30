# SeneaL Cape On Login

SeneaL UI keeps capes in a separate slot while Adventure Backpacks use the vanilla shoulder slot. After you log in, the saved cape can stay in your inventory without its bonuses or visuals until you touch the cape slot again.

This mod calls SeneaL’s internal `CapeSlot.Set` again after world load, spawn, and respawn when a saved cape is still in your inventory (including when the cape slot was cleared during `Player.Load`).

Install on **clients** (same as SeneaL UI and Adventure Backpacks).

## Changelog

### 1.0.2

- Cape lands in SeneaL’s **equipment cape slot** after login (not stuck in the bag grid).
- Finds cape in inventory when SeneaL’s slot was cleared on load; retries until Adventure Backpacks is ready.

### 1.0.1

- Login/load fix when `CapeSlot.Item` was empty; hook `Player.Load`.

### 1.0.0

- Initial release.
