# Promenade Custom Boss Orders

## Why

When `PromenadeSpeedrunMode` (/var/home/cmnemoi/code/furi-modding/PromenadeSpeedrunMode) adds the Promenade difficulty to Furi's speedrun menu, selecting Reverse or Random currently does not produce a valid Easy speedrun order. Players should be able to combine Promenade with every custom boss order without changing either mod's native modes.

## Scope

This spec covers interoperability between Furi Custom Boss Order and `PromenadeSpeedrunMode` for Easy/Promenade speedruns, with Classic and Onnamusha characters.

## Rules

### Promenade uses the Furi boss pool

`{#promenade-custom-orders::medium-boss-pool}`

For custom-order purposes, `GameDifficulty.Easy` uses the same ten-boss pool as a Medium/Furi speedrun:

1. `LAW`
2. `NEMESIS`
3. `WISE`
4. `SCALE`
5. `FATHER`
6. `WING`
7. `MAZE`
8. `CHALLENGER`
9. `HORN`
10. `MOTHERSHIP`

The run itself remains Easy: boss gameplay, saved run difficulty, UI, and Promenade-specific behavior must continue to observe `GameDifficulty.Easy`.

### Every custom order is available

`{#promenade-custom-orders::available-modes}`

When both mods are installed and Promenade is selected, Reverse and Random can be started with Classic or Onnamusha.

Reverse contains every boss exactly once in the reverse of the canonical pool. Random contains every boss exactly once in one shuffled order selected for that run. Advancing a run must follow its selected order through the final boss without ending after the first fight or regenerating the order between fights.

### Custom-order records remain isolated

`{#promenade-custom-orders::record-isolation}`

Completed Promenade custom-order runs use Furi Custom Boss Order's local records. Records are isolated by Easy difficulty, character, and Reverse/Random mode.

A Promenade custom-order run must not read, replace, or contribute to `PromenadeSpeedrunMode`'s canonical Promenade personal best. A normal Promenade run must not read or replace custom-order records.

### Custom-order completion semantics take precedence

`{#promenade-custom-orders::completion}`

During a Promenade Reverse or Random run, Furi Custom Boss Order owns level advancement, final-boss detection, local-record display, and completion. The online leaderboard remains disabled. Overlapping Harmony patches from the two mods must produce the same result regardless of patch execution order.

Promenade's restrictions for an active Easy speedrun, including Safe Mode being unavailable, remain in effect.

### Existing behavior is unchanged

`{#promenade-custom-orders::compatibility}`

Medium/Furi and Hard/Furier custom orders keep their existing boss pools and records. Normal-order Promenade speedruns remain owned by `PromenadeSpeedrunMode`. Furi Custom Boss Order continues to load and provide its existing modes when `PromenadeSpeedrunMode` is absent.

## Acceptance criteria

- Given both mods are installed, when Promenade + Classic + Reverse starts, the first boss is `MOTHERSHIP`, the last boss is `LAW`, all ten canonical bosses are fought exactly once, and the run difficulty remains Easy throughout.
- Given both mods are installed, when Promenade + Onnamusha + Random starts, all ten canonical bosses are fought exactly once in the order generated at run start, and that order does not change between fights.
- Completing the first fight of either custom mode advances to its second boss instead of opening the final screen.
- Completing the final fight opens the custom-order final screen, records the run locally, and does not submit an online score.
- Promenade Reverse and Random records are independent from each other, from Classic/Onnamusha records, and from the normal Promenade personal best.
- A completed Promenade custom-order run does not modify `PromenadeSpeedrunMode.records.txt`.
- Safe Mode remains unavailable during the Promenade custom-order run.
- Reversing or randomizing Medium and Hard produces the same eligible boss pools as before this feature.
- With `PromenadeSpeedrunMode` absent, the mod loads without errors and its existing menu and run behavior are unchanged.
- The combined behavior above is unchanged when Harmony patch ordering between the two plugin GUIDs is reversed.

## Design constraints

- Keep `PromenadeSpeedrunMode` an optional integration; do not add a compile-time assembly reference to it.
- Identify Promenade through Furi's `GameDifficulty.Easy` value and active custom-run state.
- Reuse the existing custom-order state and record store rather than adding a second Easy-specific run engine or persistence format.

## Out of scope

- Adding Promenade to the UI when `PromenadeSpeedrunMode` is not installed.
- Changing Promenade combat balance, practice mode, canonical boss pool, or Safe Mode policy.
- Sharing or migrating records between the two mods.
- Online leaderboards for custom orders.
