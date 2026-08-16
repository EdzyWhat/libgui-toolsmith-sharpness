# Changelog

All notable changes to this mod are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0]

Maintenance release — no player-facing behaviour change.

### Changed
- The fully-sharp ("keen") bar now consistently uses the lightweight sweep gleam. An experimental
  shader-based effect was removed (it was already dormant — the sweep was always the default).

### Added
- Source code is now public and documented. CC0 license.

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
