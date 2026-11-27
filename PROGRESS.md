# SenealCapeOnLogin 1.0.5 (release)

Thunderstore/GitHub release 1.0.5 (successor to published 1.0.4). Includes backpack UI sync, unequip fixes, extra-cape queue, ForceOff for backpack-then-cape unequip.

---

# SenealCapeOnLogin 1.0.7 (ForceOff) — internal

## Goal
Backpack off, then cape off: cape goes to inventory but stats stay until a second click.

## Cause
CapeSlot.UnequipPre Clear()s the extra slot and skips vanilla unequip. After the backpack is gone the cape is still `m_shoulderItem`, so Epic Loot / Skyheim keep bonuses while SeneaL Evicts the item into the bag.

## Fix
- `ForceOff` after cape `UnequipItem`: drop extra-slot ref, `m_equipped=false`, clear `m_shoulderItem` if it is that cape, refresh cape modifiers.
- Still 1.0.7. Login `TryReapply` unchanged.

Build: `dotnet build -c Release` OK.
Gale: `C:\Users\cjrob\AppData\Roaming\com.kesomannen.gale\valheim\profiles\cjayride-beta_test_cjaycraft_ultimate_modpack-4.4.70\BepInEx\plugins\cjayride-SenealCapeOnLogin\Cjayride.SenealCapeOnLogin.dll`

---

# SenealCapeOnLogin 1.0.6

## Goal
Fix cape snapping back on unequip after accidental 1.0.5 cloud ship.

## Cause (1.0.5)
- `EquipCapeMoveToSlot` called `PlaceCapeInUi` after every successful equip.
- `ResolveCape` fell back to unequipped capes; login `TryReapply` could call `CapeSlot.Set` and re-equip.
- `PlaceCapeInUi` keyed off `m_shoulderItem` without checking `CapeSlot.Item`.

## Fix 1.0.6
- Relocate UI only when `CapeSlot.Item` matches (or backpack-unequip sync).
- `ResolveCape` only returns equipped capes.
- Equip postfix only when equipping a cape or backpack.

Build: `dotnet build -c Release` OK.
DLL: `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\bin\Release\net481\Cjayride.SenealCapeOnLogin.dll`

---

# SenealCapeOnLogin 1.0.5 (regression ship)

## Goal
After unequipping a backpack, move the cape into the SeneaL cape cell so the slot matches equipped state (stats +5, cell not empty).

## Cause
SeneaL `CapeSlot.UnequipPost` re-equips the cape onto `m_shoulderItem` when the backpack leaves, but never calls `Slots.Relocate`. Cape stays equipped for eitr, UI cell empty, next click unequips it (0 eitr).

## Files
- `src/Plugin.cs` — `PlaceCapeInUi`; UnequipItem Last postfix; EquipItem Last postfix; `ScheduleUiSync`.

## Build
- `dotnet build -c Release` succeeded.
- DLL: `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\bin\Release\net481\Cjayride.SenealCapeOnLogin.dll` (1.0.5.0)
- Zip: `D:\AI-Agent-Projects\cjaycraft\Cjayride.SenealCapeOnLogin\dist\cjayride-SenealCapeOnLogin-1.0.5.zip` (flat: CHANGELOG.md, DLL, icon.png, manifest.json, README.md)
