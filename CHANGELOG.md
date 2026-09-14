# Changelog

All notable changes to Jellycanvas. The format follows
[Keep a Changelog](https://keepachangelog.com/).

## Unreleased

## 1.0.2 - 2026-09-14

- Card badges: audio and subtitle languages each have their own style -
  audio as flags and subtitles as codes by default, so the flags are not
  repeated for the subtitles.

## 1.0.1 - 2026-09-14

- Plugin logo, shown in the catalog and the plugin list.
- Card badges: video codec and sound (Dolby Digital+ 5.1, DTS-HD 7.1...)
  badges; badges of a corner stacked under each other; a color-per-kind
  style; languages as bare flags (each language its own flag), codes, or
  both.
- Item page: description & info blocks - every block (selectors,
  overview, genres, tags, external links) can sit on a background of its
  own with its own color; a chip color for the whole section.
- Fixed: a glow or neo-brutalist title ribbon made the title, year and
  rating vanish (the surface now sits on a layer behind the content);
  Jellyfin's played / count indicators and the badges keep clear of
  rounded card corners; the search link on library pages no longer moves
  custom toolbar buttons.
- Versions are three-part from now on (1.0.1).

## 1.0.0.0 - 2026-09-14

First public release, for Jellyfin 12.0.

### Designer

- Companion plugins panel at the top: which of File Transformation /
  JavaScript Injector is installed and what each unlocks; the sections
  that need one stay hidden without it.
- Settings page in the Dashboard with a live preview of the real web
  client for the web, TV and mobile layouts; the preview follows the
  setting being changed and can show the player's "Up next" and "Still
  watching?" prompts; Ctrl+click on anything in the preview jumps to its
  setting.
- Save draft (private) / Apply to server / Remove from server; the CSS
  goes to Branding → Custom CSS between marker comments and never touches
  CSS you wrote yourself.
- Presets, Share / import of themes as JSON, editable numbers next to
  every slider, English UI with a Czech translation.

### Theme

- Colors, typography (bundled, system or any Google Fonts family).
- Bar: full-width top bar, top bar split into islands, or a vertical
  sidebar (optionally collapsible); solid / glass / gradient / transparent
  / neo-brutalism / glowmorphism / claymorphism / neumorphism; rounding,
  floating, shadow, height; draggable arrangement of the logo + links,
  icons and user groups; navigation link styles; hide icons; library row
  look next to a sidebar; logo by URL or upload.
- Info bar (announcement strip) at the top or bottom.
- Cards: rounding, hover effects, shadow, border, text placement,
  spacing, watched-item and progress styles (incl. a translucent fill),
  hover buttons off everywhere or only on series / seasons / collections.
- Buttons & inputs, Play button, dialogs & menus (and the player's "Up
  next" prompt).
- Item detail page: title ribbon style, poster, cast & crew as circles /
  squares, selectors / genres / tags / links as chips, overview size,
  section titles, hide sections.
- Background image: random library backdrop rotating every N seconds,
  custom URL, blur, dim, panning.
- Login page: background image or gradient over the whole page (no bar,
  just the logo), form as card / glass, field and button styles, custom
  heading, hide Quick Connect / Forgot password.
- Side menu (mobile drawer), TV focus, mobile tweaks, hide scrollbars,
  free-form extra CSS.

### Script features (with File Transformation or a JS injector)

- Custom toolbar buttons (overlay, new tab or navigate) with a Material
  icon picker; on phones they go into the slide-out menu.
- Home slideshow (hero carousel from a query).
- Card badges: resolution, HDR, audio and subtitle languages in chosen
  corners of movie and episode cards.
- Close button on the info bar.
