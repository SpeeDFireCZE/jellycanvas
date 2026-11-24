# Contributing

Thanks for taking an interest in Jellycanvas.

## Bugs and ideas

Open an issue. For a bug, say which Jellyfin version and browser you use,
what you set in the plugin, and what you expected instead - a screenshot
of the preview helps a lot. Themes can be exported as JSON on the plugin
page (*Share / import*); attaching yours makes a report reproducible.

## Changes

- Code, comments and commit messages are in English. The settings page
  itself is in English with translations in `configPage.js` (Czech so
  far) - a new language is a new dictionary there.
- `dotnet test` must pass; the tests cover the pure parts (CSS generation,
  the Branding writer, the script builder). Add a test when you add a
  setting that changes the generated CSS.
- Everything in the theme is CSS written to Branding. JavaScript is only
  for what CSS cannot do, and it must keep working when File
  Transformation is not installed (those sections stay hidden).
- Keep the plugin's GUID; Jellyfin identifies the plugin by it.

The `test/` folder has scripts for a local Jellyfin 12 instance on Windows
(see the README); the preview on the plugin page is the quickest way to
check a change.

## Releases

Maintainers: bump the version in `build.yaml` and the `.csproj`, move the
*Unreleased* notes in `CHANGELOG.md` under the new version, tag `X.Y.Z`
and push the tag. The Release workflow does the rest.

The notes of a version are its GitHub release and its changelog in
Jellyfin's plugin catalog, so they stay short: an opening line or two
(and a `**Before you upgrade:**` line when there is something to do), then
only the sections that have something, in this order - `### ✨ Features`,
`### 🐛 Fixes` (a security fix starts with `Security:`), `### 🔧 Changes`,
`### 📚 Documentation`, `### 🌍 Translations`, `### New Contributors` -
one line per item, short and in the imperative ("Add ...", "Fix ..."),
with no full stop; a pull request as `#12`, with `by @login` for someone
else's work. Last comes
`**Full Changelog**: https://github.com/SpeeDFireCZE/jellycanvas/compare/<previous>...<this>`.
