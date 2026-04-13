# UI Guidelines

## Where UI Goes

- Frontend menus and shared overlays live in `Assets/UI Toolkit/Resources/Frontend` and are built by `FrontendUiController`.
- Per-player gameplay UI lives under a screen-space canvas driven by `CameraViewportCanvasScaler`.
- If you add a standalone player-facing UGUI canvas that is not under the player container flow, add `UiScaleCanvasSync`.

## UI Scale

- UI scale is a global setting.
- Frontend UI scale is applied through `FrontendUiController` and `UiScaleVisualElementUtility`.
- Per-player UGUI scale comes from `CameraViewportCanvasScaler`.
- Do not add random per-widget scale multipliers. Hook into the shared path instead.
- Do not mutate `PanelSettings` at runtime for user scaling.

## Build Pattern

- Put new frontend UXML in `Assets/UI Toolkit/Resources/Frontend`.
- Put shared frontend styles in `FrontendTheme.uss`.
- Query required UI Toolkit elements with `Require<T>()` so missing names fail fast.
- Bind callbacks in one place.
- Refresh visible state from one refresh method after settings or state changes.

## Localization

- Player-facing copy comes from `GameText`.
- Add both English and Spanish entries when you add a new visible string.
- Keep fallback text in UXML or prefabs short and obvious.

## Settings Row Checklist

- Add stable element names in UXML.
- Cache the references once.
- Register callbacks once.
- Refresh values from the current settings snapshot.
- If the row changes something visual, make the preview update live while dragging.

## Avoid

- Hardcoded player-facing strings in code.
- Ad hoc scale hacks on individual controls.
- New player-facing canvases that skip the shared scaling components.
- Hidden one-off binding logic spread across multiple methods.
