# SenealCapeOnLogin 1.0.7 (Epic Loot set capes)

## Goal
Fix Ragnar-style **legendary set capes** (e.g. Boneless Wrap on `CapeWolf`) not restoring after login when a spare mundane wolf cape is in the bag, or when Epic Loot load clears `m_equipped` after inventory deserialize.

## Root cause
- Restore required `m_equipped` at retry time; Epic Loot `Inventory.Load` → `InitializeCustomData` can settle after that flag drops.
- First matching equipped cape in inventory scan could be a mundane `CapeWolf`, not the magic set piece.

## Change (1.0.7)
- Snapshot best equipped cape on `Inventory.Load` (prefers Epic Loot magic items).
- During load window, re-apply snapshot cape even if `m_equipped` became false.
- `ResolveCape` prefers Epic Loot magic over mundane capes when multiple are equipped.

## Build
- `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\bin\Release\net481\Cjayride.SenealCapeOnLogin.dll` (1.0.7)

## Test
- Login with backpack + Ragnar set cape equipped; spare Wolf cape in bag → set cape in SeneaL slot, set bonus intact.
- Login with cape intentionally unequipped → must not re-equip.