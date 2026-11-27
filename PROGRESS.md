# SenealCapeOnLogin 1.0.6 (dedicated timing)

## Goal
Harden cape re-apply after login / Rune of Recall on dedicated clients. Local listen-host often settles in ~3s; remote join can miss that window.

## Change
- Longer retry: ~6s listen-host (`IsServer`), ~15s dedicated client (`!ZNet.IsServer`).
- `Inventory.Changed` retry while load window is open (late backpack/cape flags).
- Still only restores capes with `m_equipped`; unequip still `CancelPending`.

## Install
Gale: `C:\Users\cjrob\AppData\Roaming\com.kesomannen.gale\valheim\profiles\cjayride-beta_test_cjaycraft_ultimate_modpack-4.5.5\BepInEx\plugins\cjayride-SenealCapeOnLogin\`
