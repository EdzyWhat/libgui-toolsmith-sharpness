# Changelog

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Cleanup / hand-off pass — no player-facing behaviour change.

### Changed
- Reorganised the source into two layers — `src/Compat/` (the LibGUI bridge) and `src/Toolsmith/`
  (read-only shims over Toolsmith's data/config) — with a one-directional `Compat` → `Toolsmith`
  dependency that mirrors the fold-into-Toolsmith boundary.

### Removed
- Dropped the experimental liquid-metal ("keen") effect (`SharpnessKeenLiquid`, `LiquidMetalGradient`)
  and its SkSL runtime shader, along with the `SkiaSharp` compile-time reference. The fully-sharp state
  now always uses the lightweight, shader-free `SharpnessKeenSweep` gleam.

### Added
- `LICENSE` (MIT), `CONTRIBUTING.md`, `INTEGRATION.md` (guide to folding this mod into Toolsmith),
  `CHANGELOG.md`, and GitHub issue/PR templates.

### Docs
- Rewrote `README.md` around the one-patch, dependency-agnostic design; corrected stale claims (the bar
  is now always visible with a distinct "keen" state at 100%, not hidden) and the "scaffolded" status.

## [1.0.1]

- Released on the [ModDB](https://mods.vintagestory.at/show/mod/64207).

## [1.0.0]

- Initial release: draws Toolsmith's sharpness as a bar above the durability bar on LibGUI item slots
  (HudUI hotbar + PlayerInvUI grids) via a single Harmony patch on `ItemSlotOverlay.Build`, matches the
  player's Toolsmith sharpness-display mode, adds a fresh-tool "sharpen me" nudge and a legible dull-state
  treatment, and fixes the durability bar for tinkered tools to show the weakest component.

<!--
Detailed per-version history before this cleanup pass was not tracked; the 1.0.0 / 1.0.1 entries
summarise the shipped feature set rather than a precise diff.
-->
