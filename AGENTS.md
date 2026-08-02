# AGENTS.md

Single-file BepInEx/Harmony mod for the Unity game **Furi** (netstandard2.0).
All mod logic lives in [Plugin.cs](Plugin.cs) — a `BaseUnityPlugin` that runs
`new Harmony(...).PatchAll()` and a set of `[HarmonyPatch]` static classes.

## Critical rules

- Never edit files under `lib/`. They are the game's actual assemblies
  (`Assembly-CSharp.dll`, `BepInEx.dll`, `0Harmony.dll`, `UnityEngine*.dll`)
  copied out of the Furi install and referenced via `HintPath` in the
  `.csproj`. They are not NuGet packages — do not try to restore/update them.
- `furi-source-code/` (if present) is a decompiled reference copy of `Assembly-CSharp`,
  not part of the mod. Read it to look up real field/method names and class
  structure before patching; never edit it, and don't assume it's committed to
  git (it's gitignored/untracked on purpose — it's just a local research aid).
- Harmony patches target private game internals. Look up the exact member
  name in `furi-source-code/Assembly-CSharp/` (or decompile `lib/Assembly-CSharp.dll`
  with ILSpy/dnSpy if `furi-source-code/` isn't there) before writing a patch —
  guessing field names silently fails at runtime, it doesn't throw a compile error.

## Commands

Run from the repository root.

- Build: `dotnet build FuriReverseBossOrder.csproj`
- Build + deploy to local Steam install: `mise run deploy` (builds, then copies
  the DLL to `~/.local/share/Steam/steamapps/common/Furi/BepInEx/plugins/FuriReverseBossOrder/`;
  edit that path in [mise.toml](mise.toml) if the local Furi install differs)
- There is no automated test suite — correctness is verified by launching the
  game with BepInEx and checking `BepInEx/LogOutput.log` plus in-game behavior.

## Harmony patching conventions used in this codebase

- One `internal static class ...Patch` per patched method, named
  `<Type><Method>Patch`, annotated with `[HarmonyPatch(typeof(X), nameof(X.Method))]`
  (or a string literal when the member is private and has no accessible `nameof`).
- Access private game fields via `AccessTools.FieldRefAccess<T, TField>("fieldName")`
  stored in a `static readonly` field (see `DlcBoughtRef`, `CurrentGameModeRef`
  in Plugin.cs) rather than reflection calls scattered inline.
- Prefer `Postfix`/`Prefix` methods that read/mutate the minimum needed state;
  this mod's patches are deliberately narrow (single behavior each) rather than
  replacing whole methods.

## Definition of done

- `dotnet build FuriReverseBossOrder.csproj` succeeds with no warnings-as-new.
- New/changed patches were checked against the actual member names in
  `furi-source-code/Assembly-CSharp/` (or a fresh decompile), not guessed.
- If behavior can be smoke-tested, `mise run deploy` + launching Furi was used
  to confirm the patch fires (check `BepInEx/LogOutput.log`).
