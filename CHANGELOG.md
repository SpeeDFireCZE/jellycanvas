# Changelog

All notable changes to Jellycanvas. The format follows
[Keep a Changelog](https://keepachangelog.com/).

## Unreleased

- An update takes effect on its own: the generated CSS lives in Branding and was only written when someone pressed Apply, so after updating the plugin the server kept serving the CSS the previous version had made - fixes in it waited for a visit to the designer. The plugin now rebuilds that block at startup when this version would generate something different (the theme has to be on and the block present; an unchanged theme is not rewritten).
- Buttons: the whole family of looks the bars and the player's control bar have - glass, glow, gradient, neo-brutalism, glowmorphism, claymorphism, neumorphism - on every kind of button the client has, and the same looks on the item page's Play button, the play button on posters, the "skip intro" button and the slideshow button.
- Buttons: a switch to leave the top bar out of it (*Buttons and inputs → Leave the buttons in the top bar out of it*). A frame or a fill around every library link turns the bar into a row of boxes; the links and the row under them keep Jellyfin's plain look while Play all and everything else follow the style.
- Fixed: the rows of the user settings menu (Profile, Display, Home screen...) are buttons, but only their corners followed the Buttons section; they wear the chosen look now.
- The radius sliders that went to 999 now end at 40 with one notch past it for a fully rounded button (the slider was unusable: everything above about 25 px looks the same).
- "Theme the Dashboard" (the Dashboard tab) is the way back to the defaults: ticking it puts the admin pages on the theme as it is set in Defaults and drops what the tab has of its own, and the first change made there unticks it again - the Dashboard keeps that change, and the theme still reaches the admin pages.
- Fixed: on the Dashboard the top bar ignored its own settings - the island layout has nothing to make there and the panel rules were painting over it. The admin bar is a plain bar now (color, radius, shadow apply) and a floating bar stays docked next to the menu.

- The Dashboard carries the theme too (the switch is on the Dashboard tab and needs the client script): Jellyfin 12 renders the branding CSS on the user-facing pages only, so the admin pages came up in the stock colors even though the designer's preview showed them themed.
- Item page: a banner of the item's own picture behind its page (*Item page → Banner*): backdrop, banner or thumb, with dimming and the top of the picture kept in view. It is the look Jellyfin gives users who turned its own "details banner" on - this one comes from the theme, so everyone sees it whatever the background is set to.
- The Dashboard has a tab of its own next to TV and Mobile: the admin pages can be set apart from the rest (the same sections, and what is left alone follows the defaults). Ctrl+click on the Dashboard preview lands there, the tab shows only what the admin pages can use (no posters, player, item page or login form), and panels, tables and bare page there open the colors rather than the background image. The tab also drops rows that belong to the client (an item page's buttons, the library row, the rotating backdrop) and adds a section of its own for the panels the admin pages are built from: opacity, color, corners, border, shadow, glass blur, and hiding the help links under the settings headings. Buttons and inputs on the admin pages (a plugin page has the same ones) follow the Buttons section of that tab. The admin pages come with a MUI theme of their own - tables, grid toolbars, chips, selected rows and the alerts above a form kept Jellyfin's stock greys and blues whatever the palette said; they follow the theme now (lists and cards count as panels too, so the activity feed, the device cards and the admin menu take the panel corners), which also means a panel's rounded corners are no longer squared off by the toolbar above its table. The preview has a second admin page - a settings form - and Ctrl+click there maps to what the admin pages are made of: a panel opens the panel settings, a form row the buttons and inputs, bare page the background color.
- Fixed: the Dashboard's left menu could not be scrolled (the rounded drawer edge was clipping it).
- Lighter on weak clients: badges are placed only for cards near the screen and a few at a time (the rest follow as you scroll), a burst of page changes wakes the script at most every 120 ms instead of every frame, and scrolling wakes the badge pass alone.

- Share / import points at the theme site (jellycanvas.jellyscope.cz): browsing, previewing and downloading are open to everyone, uploading needs an account there.
- The JSON export leaves out the whole Seerr section (address, key and the rows) and empties the preferred audio and subtitle languages - a theme from abroad should not arrive filtering for someone else\'s languages. Nothing in the running theme changes; only what leaves the server.

## 1.2.3 - 2026-09-21

A bug-fix release. Several fixes and the new flat backgrounds live in the generated CSS: open the designer and press Apply once after updating; the client script reloads by itself.

- Fixed: a TV or phone background of its own (per-device settings) changed only the CSS part - the client script kept rotating the defaults' random backdrops there, so on the TV nothing seemed to change. The script now carries each device's own backdrop settings and follows the layout; the backdrop layer rules are scoped to the device too.
- Background: two flat sources - one color, or a gradient between two colors like the login page's (from / to / angle). Jellyfin's own backdrop goes; an item page can still show its own backdrop over it (the "item page" switch), dimmed as set.
- Login page: one flat color as the background (over the gradient; an image URL wins over both).
- Designer: a setting changed while the preview shows an admin page keeps the preview there (it used to jump back to the home page), and the plugin page's own sections follow the theme surface.
- Designer: a device tag on a slider row (a value the TV or the phone overrides) sat over the number box; it sits after the label now.
- Fixed: "hide scrollbars" (Misc) left the page's own scrollbar - the rule reached everything under the root but not the root itself. Apply once after updating.
- Designer: Ctrl+click on a scrollbar (the page's or a panel's) opens Misc at "hide scrollbars".
- Designer: the reset arrow on an overridden checkbox row toggled the checkbox instead (the label lay over the arrow).
- Card badges no longer react to the play button: it lies above them (its own z-index), and an admin who puts it in a badge corner moves one of the two. Before, the hover button counted as a fixed one while the pointer was on the card, and the hover zoom counted as a new card width, so the badges jumped about on hover. Sizes come from the layout width now, not the zoomed one.
- Card badges: a portrait poster is measured against a smaller reference width than a landscape card, so its badges are not smaller than the same card's on the home page (a 195px poster: full size; a phone's 104px poster: about nine tenths - the fitting shrinks only what does not fit).
- Designer: the TV and phone views no longer show rows that only make sense in the defaults - "Still background on TV" and "no play button on phones" (in a device view the setting itself is that device's).
- Fixed: the TV still background stayed over a playing video (its rule outranked the one that clears backdrops during playback). Apply the theme once after updating.
- Card badges: the corners of a card now share one size and are fitted together - nothing lies over another corner, the title strip or a fixed button. Long texts go short first, then all corners shrink together, then a column lies down into rows of two flags, then a corner moves past its neighbour, then badges come off the end; afterwards the size grows back as far as it fits, so badges are never smaller than they must be. A bottom corner no longer sits over the flags of a top one.
- Card badges: commas between the codes of a language pill ("CS, EN" - a small "CSEN" read as one odd word).

## 1.2.2 - 2026-09-20

- Seerr rows: a poster opened through a custom button is addressed the way that button addresses Seerr (its host and base path), so the session the user has in that overlay applies - before, the link used the server-side Seerr address (a container name or a LAN address): a different origin with no session, often not reachable from the browser at all. New setting "Seerr address for links in the browser" for the plain new-tab links; empty = the same as the server address.

## 1.2.1 - 2026-09-20

The play button settings and the phone fixes live in the generated CSS: open the designer and press Apply once after updating. The client script reloads by itself (its address now changes with the settings).

- Card badges on small cards (a phone, a dense grid): the two top corners shrink a little to sit side by side before one is moved past the other, the badges shrink as a whole (their padding follows the font), the inset is smaller, and the shrinking is decisive (two rounds) so a hair of overlap no longer sends a corner under the other; long texts go short before anything shrinks or moves - a sound name loses its layout or its codec (TrueHD Atmos 7.1 -> Atmos 7.1, DTS-HD 7.1 -> DTS-HD) and a language pill folds to two entries plus a count (CS EN FR DE -> CS EN +2). Test media: Oppenheimer and the first Severance episode (a landscape card) carry TrueHD 5.1 next to four flags and four codes. A column at the top - under a played tick, or moved past the other corner - now ends above the title strip and above the play button touch clients keep on every poster (a column too long first shrinks to fit, down to about three fifths - the fixed inset is not scaled, so the shrink really fits; one still too long lies down into rows of two flags, so it still reads as a column; only then does it drop extra flags, then badges); a play button placed in a top corner pushes that corner's badges under it, and bottom badges stay above a bottom one.
- Card badges on phones matched the preview only sometimes: the language pills used the icon font, and before that font had loaded the pill was measured with the icon's name spelled out - three times as wide - so the corners were placed for a pill that never was. The pills' icons are inline SVG now, badges wait for the page fonts (a few seconds at most) and are placed again when a font arrives later. The inset that clears the rounded corner is kept on a box's outer sides only, which leaves narrow cards room for both corners at full size; a column under a played tick shrinks down to half before it lies down into rows (small flags under the tick beat a block of rows); a folded block is never narrower than its pill.
- The client script's address carries a stamp of the settings, so a phone or a PWA that cached the script picks the new one up after any save (before, it could keep running the old script until a hard refresh).
- Designer: the import box empties once its JSON has been taken over.
- Fixed: with "show the text next to the icons" on the item page, the button row ran off a phone's screen; phones now label the play button only and the row wraps if it still has to.
- Seerr rows: a poster's Seerr page can open through a custom toolbar button that points at Seerr (its overlay, or in place) instead of a second Seerr tab - "Open a poster's Seerr page" in the Seerr section; new tab stays the default.
- Cards: the play button on posters has its own settings (Cards section, "Play button on posters"): style (Jellyfin's own, accent, dark, white, glass), a color of its own, fill opacity, a hover color (empty = a little brighter with the icon in the accent color, as Jellyfin does), corner radius, position (Jellyfin's own, middle or a corner - a bottom corner starts above an overlaid title, and on the web above the small mark / favourite / menu group), size, and "no play button on phones". Applies to the web's hover button (which keeps its 1.4x hover zoom at any size) and the fixed one touch clients draw - both the new card's button group and the old home-page card's button; the TV has none, so the rows stay out of the TV view.
- Designer: Ctrl+click on a card badge in the preview opens the badges section (the badges let clicks through to the poster, so they are found by their place, like the info strip).

## 1.2.0 - 2026-09-19

Several fixes live in the generated CSS: open the designer and press Apply once after updating.

- Per-device settings: the designer edits the defaults (web and everything else) or, with the switch at the top, the TV or the phone - the same sections, and a value changed there applies to that device only while everything else keeps following the defaults; changed rows are marked and reset with one click, and in the default view a row a device overrides carries that device's tag (click it to get there). Ctrl+click in a TV or phone preview opens the device's own value when it has one, otherwise the default. The CSS carries a scoped copy of the theme for each device with changes, and the defaults stay off such a device (a full bar on the TV no longer shows the web's islands through it). In the TV and phone views the sidebar layout (a desktop thing), the other device's sections and the info bar and logo (defaults with their own "hide on TV / phone" switches) are out of the way; the TV and phone sections live in their device's view only.
- The preview reminds you of Ctrl+click: a hint follows the mouse until the first click, and with Ctrl held the element under the mouse is framed and the hint names the section a click opens (gone as soon as Ctrl is released). The info strip - a pseudo-element with nothing to click on - is found by its place, so Ctrl+click on it opens the info bar section. The slideshow (its logo, overview and button land on their own switches) and the Seerr rows open their own sections instead of the toolbar buttons.
- On load the designer no longer opens the presets and colors once the theme has been applied, nor the companion plugins while one is installed and working.
- Fixed: the mobile card radius did nothing (it set only the theme variable).
- Fixed: with "hide the hover buttons on series, seasons and collections" on, a series in a Seerr row lost its open button too (the card carries the type); the rows' cards are exempt from it, and from "hide the hover buttons" altogether - their one button is the way to the title. Apply the theme once after updating - the rule lives in the generated CSS.
- Fixed: on TV the split bar read as one long island with only the icons apart - Jellyfin lets the left group grow across the row; it now ends at its content. The default logo also sits in a square box (Jellyfin 12 draws its icon in the old banner's 13.2em box, leaving a gap before a custom button).
- Fixed: the home slideshow crept down the page while scrolling (the gap it keeps under the top bar was re-measured against the viewport on every sync, so each scroll pushed it - and everything under it - further down).

- TV: a still background - no rotation and no panning on TV, one random backdrop; an item page still shows its own backdrop.
- Card badges follow the card width (down to about three quarters on a dense library grid), two corners of one edge that would run into each other stack instead, a pill never runs past the card, and the bottom corners sit right above an overlaid title (they were lifted twice).
- Test media: The Matrix carries the widest badges (DD+ 7.1, two audio flags, four subtitle codes).

- Seerr rows: popular movies / series have no movies-or-series choice (they are one type).
- Home slideshow: more sources - newest releases (by premiere date), top rated (the TMDB / IMDb star rating), not played yet; continue watching is gone (a saved setting falls back to random) - and the button has its own settings: text (empty = the item's title), icon, style (accent, outline, glass, white), corner radius and size.

- Hardening: the poster proxy fetches only posters Seerr has named (the address is open); an SVG logo with script or event handlers is refused and the logo is served with a script-free content security policy and nosniff; a custom button whose target is not a web address or a client path (javascript:, data:) is dropped; button labels are inserted as text; who requested a title leaves the server only for rows set to show it; a changed Seerr address or key takes effect on the next row load (no five-minute cache); a Seerr answer with an odd id or status no longer fails the row; the JSON export strips private values inside the per-device documents too; a custom font name is written as a proper CSS string.

## 1.1.1 - 2026-09-17

- Fixed: the Seerr rows made the home page throw "pause is not a function"
  when leaving it (their scroller carried Jellyfin's own itemsContainer
  class); "coming soon" posters always open Seerr, a series partly in the
  library included.
- README: screenshots of the designer itself.

## 1.1.0 - 2026-09-17

- Seerr rows (Jellyseerr / Overseerr) on the home page: coming soon
  (approved requests not in the library yet, next release first),
  recently requested, waiting for approval, requests now available,
  trending, popular movies, popular series - any number of rows, each
  limited to movies, series or both, with a heading (a default in the
  viewer's language), a place above or below Jellyfin's rows, a limit
  (16 like Jellyfin's rows), scroll arrows when needed, and what the
  posters carry (title, year, who requested it, release date, request
  state, movie / series). The server talks to Seerr with the API key and
  serves the posters itself; the address and key stay out of a shared
  theme. A poster opens the item here when the library has it, otherwise
  its page in Seerr; "Test the connection" also reports how many posters
  each row has.
- Player section: the control bar (style, color, opacity, blur, floating,
  rounding), the progress slider (color, height), button size, and the
  built-in "Skip intro / credits" button (style, color, rounding, position,
  offset, size) - with a mocked player page in the preview.
- Background: an item's page can show that item's own backdrop (random
  and custom modes, client script).
- Card badges: at most 1-4 audio and subtitle languages with preferred
  languages first and the most widely spoken filling up; hide on TV; badges
  keep clear of Jellyfin's own corner indicators and of the title strip
  on TV cards; sound names shortened (DD+ Atmos 5.1).
- Library row: styling under every bar layout, islands (the surface
  around each group - automatic under an islands bar), a thin outline,
  and hiding the library name, count, Play / Shuffle, Filter, Sort, View
  and paging.
- TV: the focused tab (Home, Favorites, libraries) can be filled, ringed,
  glowing or Jellyfin's own grow, without running into its neighbours;
  TV cards are rounded like the others.
- The designer: group headings and the open section stand out.
- Fixed: the split bar's surface ran across the whole library row; a
  panning backdrop was a band across the middle of a phone screen; the
  info bar's close button floated above a custom button's overlay; the
  theme's background (rotating backdrops) covered the video while it
  played; the player's progress slider now follows the accent color; the
  CSS no longer uses the `inset` shorthand (older TV browsers).
- README: screenshots from the demo.
- Release workflow: with a `RELEASE_TOKEN` secret the release and the
  manifest commit are made under its owner's account.

## 1.0.4 - 2026-09-15

- Card badges: the Colorful style gives every value its own color (4K
  pink, 1080p blue, 720p green, SD grey; DV / HDR / HLG; HEVC, H264, AV1...;
  a color per sound family; a color per language) with a choice of range:
  vivid, cool, warm, pastel, neon. Badges in a corner that holds Jellyfin's
  own indicator (played tick, unplayed count, media source) start under it.
- Info bar: *Hide on TV*; the close button's memory is a choice - remember
  the close until the text changes (as before), or show the strip again
  on every page load.
- Fixed: two custom buttons on the same side of the bar kept swapping
  places on every sync and never received a click.
- Fixed: closing the info bar in the designer's preview hid it for the
  admin on the site as well (same browser storage); the preview now keeps
  its closes to itself and *Apply* clears a remembered close in the
  admin's browser.
- The theme import lives in *Share / import* only (it was also in the
  Extra CSS section for a while).

## 1.0.3 - 2026-09-15

A bug-fix release, with the TV layout and theme sharing on top.

- TV layout: the preview really starts the client in its TV layout, and
  the bar settings reach the TV's own top bar (style, islands, floating,
  rounding, shadow, navigation pills / underline, hidden icons, logo);
  bar height and icon size under *TV*; the tabs stay inside the bar at
  any height; the info bar sits under the bar; custom toolbar buttons
  show on TV when allowed (icons before the search, links next to the
  logo) and their overlay opens below the bar.
- Mobile: the bar stays on one row (the user icon used to drop under an
  islands bar); a custom link in the slide-out menu opens again.
- Background: a random backdrop rotation cross-fades smoothly between
  images (script) instead of cutting.
- Info bar: long text wraps onto more lines, on phones too, and the page
  moves down for it.
- Share / import: a theme can be imported from a link (a raw GitHub
  file, for example) - also from the *Extra CSS, export & import*
  section. The export carries only the look: custom buttons and their
  addresses, the info bar text, the login title, the uploaded logo and
  image links on private addresses stay out of it.
- Fixed: the TV info bar overlapped the top bar; a rotation stopped while
  an image was loading threw an error.
- Old theme files: a server upgraded from 10.x can be left with the old
  `web/themes/*/theme.css`, which does not read the `--jf-*` variables -
  the palette and the card rounding were ignored there. The generated CSS
  now carries the theme stylesheet's variable rules itself (a no-op on a
  current install), the card rounding is also written straight onto the
  card elements, and *Misc → Theme files on the server* lists which files
  are old and can patch them (a copy is kept as `theme.css.jellycanvas-bak`).
- Fixed: with *Apply to dark themes only*, every rule starting with `html`
  (bar, cards, TV, mobile - a third of the theme) was generated as
  `html:not(...) html ...` and never applied.
- Fixed: the corner-badge played style sat inset from the corner on
  rounded cards; its icon and the unplayed count now keep clear of the
  rounded corner.
- Development: `test\Make-Media.ps1` regenerates the test clips with
  resolutions, HDR, audio languages and subtitles so the badges have
  something to show; the test scripts run on Jellyfin 12.1.

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
