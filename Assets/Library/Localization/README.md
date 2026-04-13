# Localization

Internal notes for reusing this localization stack in future Unity projects.

## Ownership Split

`Assets/Library/Localization` is the reusable layer.

It owns:
- `LocalizationTable`
- `LocalizationEntry`
- `LocalizationLanguageDefinition`
- `GameText`
- runtime lookup caches
- fallback behavior
- missing-key tracking
- `LanguageChanged` event
- the Odin authoring workspace

The calling game owns:
- startup/bootstrap initialization
- persistence policy
- settings/debug UI
- scene hookup
- any automatic UI refresh bindings

Keep the generic plumbing in the library DLL. Keep game policy in `Assets/Scripts`.

## Runtime Contract

`GameText` is the static entry point.

```csharp
GameText.Initialize(localizationTable, initialLanguageId: "en");
GameText.SetLanguage("es");

string currentLanguageId = GameText.CurrentLanguageId;
string title = GameText.Get("ui.title");
string waveLabel = GameText.Get("ui.wave_label", waveIndex);
```

Behavior:
- lookup uses the current language id first
- if that translation is empty, it falls back to the table fallback language
- if nothing resolves, it returns `[MISSING:key]`
- each missing key warns once per session
- `LanguageChanged` fires only when the resolved language actually changes

## Data Model

Languages are data-driven. There is no enum anymore.

Each language is a `LocalizationLanguageDefinition` with:
- `Id`
- `DisplayName`
- `NativeName`

Use stable ids such as:
- `en`
- `es`
- `pt-br`
- `fr`

Do not use display names as identifiers.

## Workspace

Authoring happens in `Tools/Localization/Workspace`.

Pages:
- `Setup`
  Creates or refreshes the shared asset at `Assets/Config/Localization/LocalizationTable.asset`.
- `Languages`
  Adds/removes/reorders languages, and assigns default/fallback.
- `Strings`
  Adds/removes/searches keys and edits translations inline across all configured languages.
- `Validation`
  Shows duplicate keys, empty keys, missing fallback text, missing per-language text, and orphaned translations.

The workspace is generic. It should be reusable across projects without any scene or bootstrap assumptions.

## New Project Setup

For a new game:

1. Include `Assets/Library/Localization`.
2. Open `Tools/Localization/Workspace`.
3. Run `Create Or Refresh Default Asset`.
4. Decide the startup language policy in the game layer.
5. Add a game-specific `LocalizationManager` or equivalent adapter.
6. Attach that adapter in the bootstrap scene.
7. Expose language selection in debug tools and/or settings UI.
8. Make any live-localized UI re-read text on `GameText.LanguageChanged`.

The workspace only guarantees the shared asset exists and is well-formed. It does not wire scenes or create managers for you.

## Adding Languages

Use the `Languages` page in the workspace.

Workflow:

1. Click `Add Language`.
2. Set a stable `Id`.
3. Set `Display Name`.
4. Optionally set `Native Name`.
5. If needed, change `Default Language` and `Fallback Language`.
6. Save.

Notes:
- Removing a language cascades through the table and removes its translations from all entries.
- The active fallback language cannot be removed until another fallback is assigned.
- Reordering languages only affects editor presentation order.

## Adding Strings

Use the `Strings` page in the workspace.

Workflow:

1. Click `Add String`.
2. Enter the key.
3. Fill the fallback language text first.
4. Fill the rest of the languages.
5. Save.
6. Run `Validation` or `Sort Keys` when needed.

Key conventions:
- Use stable dotted keys.
- Group by prefix intentionally: `ui.*`, `dialog.*`, `enemy.*`, `tutorial.*`.
- Do not use English copy as the key.
- Do not casually rename keys once referenced in code/prefabs.

Examples:

```csharp
GameText.Get("ui.start");
GameText.Get("ui.wave_label", waveIndex);
GameText.Get("dialog.intro.line_01");
```

## Validation Rules

Validation currently reports:
- empty language ids
- duplicate language ids
- empty keys
- duplicate keys
- missing fallback translations
- missing translations in any configured language
- orphaned translations whose language id no longer exists on the table

Runtime behavior on duplicate keys is still first-entry-wins. Treat that as a content bug, not a feature.

## What The Calling Game Must Implement

The library does not choose startup language policy.

The game layer should:
- hold a reference to the `LocalizationTable`
- choose the startup language id
- call `GameText.Initialize(...)`
- call `GameText.SetLanguage(...)` when the active language changes
- persist the selected language if desired
- expose a public path for settings/debug UI to change language

Typical startup precedence:
- debug override
- saved preference
- serialized default
- table default

Persistence should stay game-owned. Do not push `PlayerPrefs` policy down into the library.

## UI Refresh

Changing language only affects future `GameText.Get(...)` calls.

Existing UI will not update by itself. If live switching matters, subscribe to:

```csharp
GameText.LanguageChanged += OnLanguageChanged;
GameText.LanguageChanged -= OnLanguageChanged;
```

Then re-apply text in the handler.

If a future project needs a binding component, build it in the game layer first. Only move it into the library if it is clearly generic.

## Asset Expectations

The standard shared asset path is:

`Assets/Config/Localization/LocalizationTable.asset`

The library assumes the asset:
- exists or can be created there
- has at least one valid language id
- has a sensible fallback language

The game is free to load that asset however it wants, but using the standard path keeps the workspace and debug tooling simple.

## Non-Goals

This layer still does not handle:
- pluralization
- locale-specific number/date formatting
- spreadsheet import/export
- automatic locale detection
- smart text rules
- generic UI bindings

If a future project needs those, add them deliberately instead of inflating this base layer ad hoc.
