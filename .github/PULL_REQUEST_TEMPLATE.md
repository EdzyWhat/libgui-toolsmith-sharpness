<!-- Thanks for contributing! Keep this small mod focused; see CONTRIBUTING.md. -->

## What & why

<!-- A sentence or two on the change and the motivation. Link any related issue. -->

## Testing

<!-- How did you verify it in-game? Please note the versions you tested against. -->

- Vintage Story:
- LibGUI (`gui`):
- Toolsmith:
- HudUI / PlayerInvUI (if relevant):

## Checklist

- [ ] Builds clean (`build/restage.sh`) with no new warnings.
- [ ] Verified in-game on the HudUI hotbar and/or PlayerInvUI grids as relevant.
- [ ] No new HudUI / PlayerInvUI / Toolsmith assembly references, and no bundled third-party DLLs.
- [ ] Render path still avoids Toolsmith's side-effecting `Get*Sharpness()` / `Get*Durability()` helpers.
- [ ] Kept the `Compat` → `Toolsmith` one-directional dependency (see README / CLAUDE.md).
- [ ] Updated `CHANGELOG.md` if the change is player-facing.
