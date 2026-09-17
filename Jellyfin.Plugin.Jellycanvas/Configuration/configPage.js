/*
 * Jellycanvas settings page logic.
 *
 * How this file gets here: the Dashboard fetches configPage.html, inserts it
 * into its own page and runs the <script> in it; that script points here
 * (configurationpage?name=JellycanvasJs). Jellyfin runs the page's scripts
 * again every time the page is shown - and keeps the same DOM element - so
 * everything is wrapped in a function and the element is marked once it is
 * wired up (otherwise every click handler would stack up per visit).
 *
 * What happens here:
 *  1. the plugin configuration is loaded (JSON, same shape as PluginConfiguration in C#),
 *  2. the controls are bound to paths in that JSON (data-path="Header.Radius"),
 *  3. every change posts the settings to the server (/Jellycanvas/Preview),
 *     which returns CSS that is injected into the preview - an iframe with
 *     the real web client,
 *  4. "Apply" saves the same settings and writes the CSS to Branding.
 *
 * Only what the Dashboard already provides is used: ApiClient (authenticated
 * calls) and Dashboard (loading spinner, messages). No libraries.
 */
(function () {
    'use strict';

    var PLUGIN_ID = '433e86f7-318f-4bd5-98e9-98fd3eea7d42';

    // The Dashboard can hold up to three pages at once (for the transition
    // animation); when the user comes back, the newest one is last in the DOM.
    var pages = document.querySelectorAll('#JellycanvasPage');
    var page = pages[pages.length - 1];
    if (!page || page.getAttribute('data-jc-bound')) {
        return;
    }
    page.setAttribute('data-jc-bound', '1');

    // ------------------------------------------------------------------
    // Strings. English lives in the HTML and is the default; a translation
    // is chosen with the selector in the toolbar and remembered in this
    // browser. To add a language, add a key to "strings" and an <option>.
    // ------------------------------------------------------------------
    var strings = {
        en: {
            statusOn: 'Theme is enabled and written to Branding.', statusOff: 'Theme is not applied on the server yet - preview only.',
            statusMissing: 'Theme is enabled but missing from Branding (removed by hand?). Click Apply.',
            statusForeign: 'Branding also holds other CSS ({0} chars) - it will be preserved.',
            applied: 'Theme applied. Browsers pick it up on reload - the web client caches the branding for up to a minute.', saved: 'Draft saved.', removed: 'Theme removed from the server.',
            confirmDisable: 'Remove the theme from Branding? Your settings stay saved.', confirmReset: 'Reset everything to Jellyfin defaults?',
            failed: 'Failed: {0}', logoUploaded: 'Logo uploaded.', logoDeleted: 'Uploaded logo deleted.', copied: 'Copied to the clipboard.',
            scriptsFt: 'File Transformation is installed - the script is injected into the web client automatically once you apply.',
            scriptsInjector: 'A JavaScript injector plugin is installed - copy the generated script into it.',
            scriptsNone: 'Toolbar buttons and the home slideshow need JavaScript in the web client. Install the File Transformation plugin (recommended, automatic) or a JavaScript injector plugin to unlock them.',
            chipNav: 'Logo & links', chipIcons: 'Icons', chipUser: 'User', slotTop: 'Top', slotMiddle: 'Middle', slotBottom: 'Bottom', slotLeft: 'Left', slotCenter: 'Center', slotRight: 'Right',
            pluginInstalled: 'Installed', pluginMissing: 'Not installed', pluginFtDesc: 'Injects the client script into the web client automatically. Unlocks: custom toolbar buttons, the home slideshow, card badges (resolution, languages), the close button on the info bar, and the live preview of these.', pluginInjectorDesc: 'An alternative when File Transformation is not wanted: the generated script is copied into it by hand. Unlocks the same features (after pasting).',
            chipResolution: 'Resolution', chipHdr: 'HDR', chipCodec: 'Video codec', chipSound: 'Sound (DD+ Atmos 5.1, DTS-HD 7.1…)', chipAudio: 'Audio languages', chipSubtitles: 'Subtitle languages',
            iconSearch: 'Search icons…', iconNone: 'Nothing found - any Material Icons name can also be typed by hand.', close: 'Close',
            importDone: 'Theme loaded into the designer - check the preview, then Apply.', importBad: 'That is not a Jellycanvas theme (expected a JSON object with the settings).',
            seerrTestOk: 'Connected: {0}', seerrTestFail: 'Failed: {0}',
            themeFilesHeading: 'Theme files on the server', themeFilesHint: 'Jellyfin 12 themes (web/themes/*/theme.css) read the --jf-* variables this theme sets. A server upgraded from 10.x can keep the old files, which do not; Jellycanvas carries the missing rules in its own CSS, so the theme still works - the files are checked here for your information.',
            themeRepair: 'Patch the old files', themeRepairHint: 'Appends the missing rules to each old theme.css (a copy is kept as theme.css.jellycanvas-bak). Needs write access to the web folder - a packaged install usually has none; the proper fix is reinstalling the jellyfin-web package.',
            themeCurrent: 'current', themeOld: 'old (pre-12 format, {0} characters) - compensated by the generated CSS', themePatched: 'old, patched by Jellycanvas', themeNone: 'No theme files found in {0}.', themeRepairDone: 'Theme files patched.', themeRepairFailed: 'Could not write: {0}',
            importFetchFail: 'The link could not be loaded (the site has to allow cross-origin requests; raw GitHub links do).', importPlaceholder2: 'Paste a theme JSON or a link to one',
            mockNext: 'Next episode starts in {0} s', mockSkip: 'Skip Intro', mockEndsAt: 'Ends at 21:35', mockEpisode: 'S1:E2 - Episode title', mockStartNow: 'Start now', mockHide: 'Hide', mockStill: 'Are you still watching?', mockStop: 'Stop watching', mockContinue: 'Continue watching'
        },
        cs: {
            reset: 'Reset', save: 'Uložit rozpracované', apply: 'Použít na serveru', disable: 'Odebrat ze serveru',
            presets: 'Začít ze šablony', applyTo: 'Použít na', applyAll: 'Všechny vzhledy Jellyfinu', applyDark: 'Jen tmavé vzhledy (světlý nechat být)',
            colors: 'Barvy', accent: 'Zvýraznění', background: 'Pozadí', surface: 'Plochy (lišty, dialogy)', text: 'Text', outline: 'Rámeček a tvrdý stín neo-brutalismu (prázdné = černá)', secondaryText: 'Krytí vedlejšího textu',
            header: 'Lišta', style: 'Styl', solid: 'Plná', glass: 'Sklo', gradient: 'Přechod', transparent: 'Průhledná', neoBrutalism: 'Neo-brutalismus', glowmorphism: 'Glowmorfismus', claymorphism: 'Claymorfismus', neumorphism: 'Neumorfismus',
            styleHint: 'Neo-brutalismus vyplní lištu zvýrazňovací barvou (změníš ji barvou lišty), odsadí ji od okrajů okna, aby byl vidět rámeček i stín, a nejlépe vypadá se zaoblením 0; glow, clay a neumorfismus vyniknou na ostrůvcích nebo plovoucí liště se zaoblením; neumorfismus bere barvu pozadí stránky, pokud není nastavená barva lišty.', barColor: 'Barva lišty (prázdné = barva ploch)', opacity: 'Krytí', blur: 'Rozmazání pozadí (vidět při krytí pod 100)', radius: 'Zaoblení rohů',
            floating: 'Plovoucí (odsazená od okrajů)', topBarHeading: 'Horní lišta', sidebarHeading: 'Boční lišta', infobarTextHeading: 'Text a umístění', behaviourHeading: 'Chování', cardsHoverHeading: 'Najetí a text', buttonsAllHeading: 'Všechna tlačítka', buttonsDetailHeading: 'Tlačítka na stránce položky', sourceHeading: 'Zdroj', backdropRotationHeading: 'Střídání', contentHeading: 'Obsah', devicesHeading: 'Zařízení', backgroundHeading: 'Pozadí', loginFormHeading: 'Formulář', loginTextsHeading: 'Texty a další tlačítka', shapeHeading: 'Tvar', logoTextHeading: 'Název serveru', logoImageHeading: 'Obrázek', libraryRowHeading: 'Lišta knihovny (název knihovny, počet, přehrát, filtr, řazení, zobrazení, stránkování)', libraryRowIslands: 'Ostrovy: plocha kolem každé skupiny, ne přes celý řádek (pod lištou z ostrovů s „stejně jako lišta“ vždy)', libraryRowBorder: 'Tenký rámeček', libraryRowHideHint: 'Schovat části řádku:', libraryRowHideTitle: 'Název knihovny', libraryRowHideCount: 'Počet položek', libraryRowHidePlay: 'Přehrát vše / Náhodně', libraryRowHideFilter: 'Filtr', libraryRowHideSort: 'Řazení', libraryRowHideView: 'Nastavení zobrazení', libraryRowHidePaging: 'Stránkování (předchozí / další)', libraryRowSame: 'Stejná jako horní/boční lišta', libraryRowColor: 'Barva řádku (prázdné = barva ploch; u neo-brutalismu zvýraznění)', libraryRowHidden: 'Schovaný', libraryRowHeight: 'Výška řádku (52 = výchozí Jellyfin, 44 = minimum, do kterého se vejde ovládání)', libraryRowRadius: 'Zaoblení řádku (při zaoblení odsazený od okrajů)', shadow: 'Stín', bottomBorder: 'Tenká linka dole', hideLogo: 'Schovat logo i název serveru',
            drawer: 'Boční menu (mobil / úzké okno)', menuColor: 'Barva menu (prázdné = barva pozadí)', itemRadius: 'Zaoblení položek', width: 'Šířka (0 = výchozí)',
            cards: 'Karty (plakáty)', hover: 'Efekt při najetí', none: 'Žádný', lift: 'Nadzvednout', zoom: 'Zvětšit', glow: 'Rozsvítit',
            cardText: 'Název a text', below: 'Pod obrázkem', overlay: 'Přes obrázek', hidden: 'Skrytý', border: 'Tenký rámeček',
            hideOverlay: 'Schovat tlačítka při najetí (přehrát, menu)', spacing: 'Rozestupy',
            buttons: 'Tlačítka a pole', filled: 'Plná', outline: 'Obrys', soft: 'Jemná (tónovaná)',
            typography: 'Písmo', family: 'Rodina', fontDefault: 'Výchozí Jellyfin (Noto Sans)', fontSystem: 'Systémové písmo', fontCustom: 'Vlastní název…',
            fontCustomName: 'Název vlastního písma (přesně jako na Google Fonts)', googleFonts: 'Načíst z Google Fonts',
            googleFontsHint: 'Inter, Roboto, Poppins, Nunito i vlastní název. Vypni, pokud prohlížeče nesmí na internet.', fontScale: 'Velikost',
            backdrop: 'Obrázek na pozadí', dim: 'Ztmavení', loginGradient: 'Pozadí přechodem', gradientFrom: 'Přechod od (prázdné = barva pozadí)', gradientTo: 'Přechod do (prázdné = tmavé zvýraznění)', gradientAngle: 'Směr přechodu', gradientOpacity: 'Krytí přechodu (pod 100 prosvítá backdrop)', loginGradientHint: 'Adresa obrázku na pozadí výše má před přechodem přednost.', loginTransparentBar: 'Na přihlášení bez lišty - jen logo přes pozadí', loginTitle: 'Text nadpisu (prázdné = „Prosíme, přihlaste se“)', loginFieldsHeading: 'Pole a tlačítka', loginFormWidth: 'Šířka formuláře (0 = výchozí)', loginInputs: 'Textová pole', loginButtons: 'Tlačítka', loginInputsDefault: 'Výchozí Jellyfin', loginInputRadius: 'Zaoblení polí a tlačítek (-1 = jako formulář)', loginInputScale: 'Výška polí', loginHideTitle: 'Schovat nadpis', loginHideQuick: 'Schovat tlačítko Rychlé připojení', loginHideForgot: 'Schovat tlačítko Zapomenuté heslo', loginRadius: 'Zaoblení rohů',
            tvBarHeading: 'Horní lišta na TV', tvBarHint: 'TV rozložení má klasickou horní lištu Jellyfinu (logo, ikony, záložky); styl, barvy a zaoblení bere ze sekce Lišta. Výška a velikost se nastavují tady.', tvBarHeight: 'Výška lišty (0 = výchozí Jellyfin)', tvBarScale: 'Velikost ikon a záložek', tvTabFocus: 'Záložka s fokusem (Domů, Oblíbené, knihovny)', tabFocusHighlight: 'Vyplnit barvou fokusu', tabFocusRing: 'Rámeček v barvě fokusu', tabFocusGlow: 'Jemné zvětšení se září', tabFocusScale: 'Jako Jellyfin: zvětší se 1,3×',
            tv: 'Rozložení TV', focusColor: 'Barva zaměření (prázdné = zvýraznění)', focusWidth: 'Tloušťka rámečku zaměření', focusScale: 'Zvětšení zaměřené karty',
            mobile: 'Rozložení mobil', cardRadiusMobile: 'Zaoblení karet (-1 = jako web)', fontScaleMobile: 'Velikost písma (0 = jako web)',
            extra: 'Vlastní CSS a export', importPlaceholder2: 'Sem vlož JSON tématu nebo odkaz na něj', importFetchFail: 'Odkaz se nepodařilo načíst (web musí povolit cross-origin požadavky; raw odkazy z GitHubu to umí).', extraHint: 'Cokoli, co ovládací prvky neumí. Připojí se za vygenerované CSS.',
            exportHint: 'Vygenerované CSS - jen pro čtení. Zkopíruj, pokud ho chceš použít jinde.',
            devWeb: 'Web', devTv: 'TV', devMobile: 'Mobil', pageHome: 'Domů', pageLibrary: 'Knihovna', pageDetail: 'Detail položky', pageLogin: 'Přihlášení', pageUpNext: 'Přehrávač: další díl', pageStillWatching: 'Přehrávač: „Stále se díváte?“', mockNext: 'Další díl začne za {0} s', mockSkip: 'Přeskočit úvod', mockEndsAt: 'Konec ve 21:35', mockEpisode: 'S1:E2 – Název epizody', mockStartNow: 'Spustit hned', mockHide: 'Skrýt', mockStill: 'Stále se díváte?', mockStop: 'Přestat sledovat', mockContinue: 'Pokračovat ve sledování',
            previewHint: 'Náhled je skutečný webový klient s vloženým CSS - dá se v něm klikat. Ctrl+klik na prvek otevře jeho nastavení. Vlastní CSS se projeví v klientech založených na webu (prohlížeč, Jellyfin Media Player, aplikace pro Android); nativní TV aplikace ho ignorují.',
            statusOn: 'Téma je zapnuté a zapsané v Brandingu.', statusOff: 'Téma zatím není na serveru použité - zatím jen náhled.',
            statusMissing: 'Téma je zapnuté, ale v Brandingu chybí (někdo ho smazal ručně). Klikni na Použít.',
            statusForeign: 'V Brandingu je i cizí CSS ({0} znaků) - zůstane zachované.',
            applied: 'Téma použito. Prohlížeče si ho vezmou při obnovení stránky - klient si branding cachuje až minutu.', saved: 'Rozpracované téma uloženo.', removed: 'Téma odebráno ze serveru.',
            confirmDisable: 'Odebrat téma z Brandingu? Nastavení zůstane uložené.', confirmReset: 'Vrátit všechno na výchozí hodnoty Jellyfinu?',
            failed: 'Nepovedlo se: {0}',
            layout: 'Rozvržení', layoutFull: 'Jedna souvislá lišta nahoře', layoutSections: 'Horní lišta rozdělená na sekce (ostrůvky)', layoutSidebar: 'Svislý panel vlevo (jako před verzí 12; mobil a TV mají horní lištu)', sidebarWidth: 'Šířka panelu', sectionRadius: 'Zaoblení sekcí (999 = pilulka)',
            height: 'Výška (0 = výchozí)', nav: 'Odkazy v liště', navText: 'Text (výchozí)', navPill: 'Pilulky, aktivní vyplněný', navUnderline: 'Aktivní podtržený',
            hideSyncPlay: 'Schovat ikonu SyncPlay', hideCast: 'Schovat ikonu Cast', hideSearch: 'Schovat ikonu hledání',
            logo: 'Logo', showServerName: 'Zobrazit vedle loga název serveru', logoImage: 'Obrázek loga', logoDefault: 'Ikona Jellyfinu', logoCustom: 'Vlastní obrázek', logoHidden: 'Bez obrázku',
            logoUrl: 'Adresa vlastního obrázku', logoUpload: 'Nahrát obrázek…', logoDelete: 'Smazat nahraný',
            logoHint: 'PNG, SVG, WebP, JPG nebo GIF do 2 MB. Nahrání vyplní adresu a přepne obrázek na Vlastní.', logoHeight: 'Výška loga', logoWidth: 'Šířka vlastního loga (0 = auto)',
            drawerRadius: 'Zaoblení vnější hrany',
            played: 'Přehrané položky', playedBadge: 'Fajfka (výchozí)', playedCorner: 'Fajfka zastrčená do rohu', playedDimmed: 'Ztlumený plakát, bez fajfky', playedGray: 'Černobílý plakát, bez fajfky', playedHidden: 'Nijak',
            playedColor: 'Barva fajfky (prázdné = zvýraznění)', progress: 'Ukazatel rozkoukání', progressDefault: 'Tenká linka dole (výchozí)', progressFloating: 'Plovoucí zaoblený proužek', progressBold: 'Tlustý proužek dole', progressTop: 'Tenká linka nahoře', progressFill: 'Průsvitný nádech přes plakát až tam, kam je zhlédnuto', progressFillOpacity: 'Krytí nádechu', hideOverlayFolders: 'Schovat tlačítka při najetí jen u celých seriálů, sezón a kolekcí (filmy a díly je mají dál)',
            progressColor: 'Barva ukazatele (prázdné = zvýraznění)',
            accentPlay: 'Tlačítko Přehrát na detailu vyplnit barvou zvýraznění', buttonsHint: 'Ovlivní plochá tlačítka na detailu, dialogy a nastavení a tlačítka Přehrát vše / filtry v knihovnách.',
            player: 'Přehrávač', playerHint: 'Ovládání při přehrávání videa. Náhled to ukazuje jako maketu přes domovskou stránku (stránka „Přehrávač: ovládání a tlačítko přeskočit“).', osdHeading: 'Ovládací lišta', osdDefault: 'Jellyfin – ztmavení dolů', osdColor: 'Barva lišty (prázdné = barva ploch; akcent u neo-brutalismu)', osdFloating: 'Plovoucí (odsazená od okrajů)', progressHeading: 'Posuvník a tlačítka', playerProgressColor: 'Přehraná část a knoflík (prázdné = akcent)', playerProgressHeight: 'Výška dráhy (0 = výchozí Jellyfin)', playerButtonScale: 'Velikost tlačítek', skipHeading: 'Tlačítko „Přeskočit úvod / titulky“', skipHint: 'Vlastní tlačítko Jellyfinu pro segmenty médií (Nastavení → Přehrávání → akce pro segmenty: „Zeptat se“); pluginy, které označují úvody a titulky, ho plní.', skipDefault: 'Jellyfin – tmavý box', skipAccent: 'Výplň akcentem', skipSurface: 'Barva ploch', skipOutline: 'Jen obrys', skipColor: 'Barva tlačítka (prázdné = akcent nebo barva ploch podle stylu)', skipPosition: 'Umístění', skipBottomRight: 'Vpravo dole (výchozí Jellyfin)', skipBottomCenter: 'Dole uprostřed', skipBottomLeft: 'Vlevo dole', skipTopRight: 'Vpravo nahoře', skipOffset: 'Vzdálenost od okraje', skipScale: 'Velikost', pagePlayer: 'Přehrávač: ovládání a tlačítko přeskočit',
            dialogs: 'Dialogy a nabídky', dialogsUpNext: 'Také hláška přehrávače „Další díl“ (odpočet do další epizody)', dialogsHint: 'V náhledu otevři nabídku (tři tečky na kartě nebo uživatelské menu), ať to vidíš; v seznamu stránek náhledu jsou i hlášky přehrávače „Další díl“ a „Stále se díváte?“.', ribbonHeading: 'Titulní pás pod backdropem', ribbonSame: 'Stejný jako lišta', ribbonColor: 'Barva pásu (prázdné = barva lišty)', posterHeading: 'Plakát a logo',
            detail: 'Stránka detailu', transparentRibbon: 'Průhledná stuha s názvem pod backdropem', posterRadius: 'Zaoblení plakátu (-1 = jako karty)', posterShadow: 'Stín plakátu', hideTitleLogo: 'Schovat obrázkové logo titulu', peopleHeading: 'Herci a tvůrci', peopleShape: 'Tvar fotky', peopleDefault: 'Karty na výšku (výchozí)', peopleCircle: 'Kruhy, jméno na střed', peopleSquare: 'Čtverce', peopleRounded: 'Na výšku s velkým zaoblením', peopleScale: 'Velikost karet', peopleRing: 'Kroužek ve zvýrazňovací barvě kolem fotky', peopleGray: 'Černobíle, barevně při najetí', hideCastSection: 'Schovat sekci Herci a tvůrci', detailBlocksHeading: 'Popis a informační bloky', chipColor: 'Barva štítků (prázdné = automaticky)', detailBlockSurfacesHeading: 'Pozadí bloků', detailBlockSurfacesHint: 'Dej kterékoli části stránky vlastní pozadí – kartu, sklo nebo jeden ze stylů – každé s vlastní barvou.', blockNone: 'Žádné (přímo na stránce)', blockColor: 'Barva bloku (prázdné = plochy; u neo-brutalismu zvýraznění)', blockOpacity: 'Krytí bloků', blockRadius: 'Zaoblení bloků', blockSelectors: 'Výběr Verze / Video / Zvuk / Titulky', blockOverview: 'Popis (tagline + text)', blockGenres: 'Žánry', blockTags: 'Štítky', blockLinks: 'Externí odkazy', trackSelections: 'Výběr Verze / Video / Zvuk / Titulky', genresRow: 'Žánry', tagsRow: 'Štítky', externalLinksRow: 'Externí odkazy (IMDb, TMDB…)', blockDefault: 'Výchozí', blockChips: 'Štítky (chips)', blockAccentChips: 'Štítky ve zvýrazňovací barvě', overviewScale: 'Velikost textu popisu', overviewMaxWidth: 'Max. šířka popisu (0 = bez limitu)', hideTagline: 'Schovat tagline', detailSectionsHeading: 'Sekce níže', sectionTitles: 'Nadpisy sekcí', titleUppercase: 'Malé verzálky', titleAccentLine: 'Barevná linka pod nadpisem', titleAccentBar: 'Barevný proužek vlevo', hideSimilar: 'Schovat „Podobné položky“',
            backdropMode: 'Zdroj', backdropDefault: 'Výchozí Jellyfin (nastavení uživatele „Zobrazit pozadí“)', backdropRandom: 'Náhodný backdrop z knihovny, na každé stránce', backdropCustom: 'Vlastní adresa obrázku',
            backdropUrl: 'Adresa vlastního obrázku', animate: 'Pomalé plynutí obrázku', rotate: 'Střídat náhodný backdrop po (0 = jen při načtení)', backdropItemDetail: 'Na stránce položky ukázat její vlastní backdrop (klientský skript)',
            backdropHint: '„Náhodný“ vybere při každém načtení jiný backdrop filmu nebo seriálu - uvidí ho i nepřihlášený na přihlašovací stránce. U „Výchozí Jellyfin“ se obrázek ukazuje jen tam, kde má uživatel pozadí zapnuté (detail, domů podle nastavení zobrazení).',
            share: 'Sdílení / import', shareHint: 'Téma je jeden soubor JSON se vzhledem z této stránky. Nic, co ukazuje na tvůj server, v něm není: vlastní tlačítka a jejich adresy, text informační lišty, nadpis přihlášení, nahrané logo ani odkazy na obrázky na privátních adresách. Zkopíruj ho pro sdílení; vlož cizí – nebo odkaz na něj (třeba raw soubor z GitHubu) – a vyzkoušej ho. Na server se nic nezapíše, dokud nedáš Použít.', exportHeading: 'Export', exportCopy: 'Zkopírovat JSON tématu', exportFile: 'Stáhnout .json', importHeading: 'Import', importPlaceholder: 'Sem vlož JSON tématu', importApply: 'Načíst do editoru', importFile: 'Otevřít soubor .json…', importDone: 'Téma načteno do editoru – zkontroluj náhled a pak Použít.', importBad: 'Tohle není téma Jellycanvas (čekal jsem JSON objekt s nastavením).',
            plugins: 'Spolupracující pluginy', pluginsHint: 'Celé téma je čisté CSS a nic dalšího nepotřebuje. Pár funkcí vyžaduje JavaScript ve webovém klientu; jejich sekce se ukážou, jen když je nainstalovaný některý z těchto pluginů.', pluginInstalled: 'Nainstalovaný', pluginMissing: 'Není nainstalovaný', pluginFtDesc: 'Vloží klientský skript do webového klienta automaticky. Odemyká: vlastní tlačítka v liště, slideshow na Domů, odznaky na kartách (rozlišení, jazyky), křížek na informační liště a jejich živý náhled.', pluginInjectorDesc: 'Alternativa, když nechceš File Transformation: vygenerovaný skript se do něj vloží ručně. Odemyká totéž (po vložení).',
            login: 'Přihlašovací stránka', loginBg: 'Adresa obrázku na pozadí (prázdné = žádný)', loginForm: 'Formulář', loginPlain: 'Prostý (výchozí)', loginCard: 'Karta', loginGlass: 'Skleněná karta',
            misc: 'Různé', hideScrollbars: 'Schovat posuvníky',
            themeFilesHeading: 'Soubory témat na serveru', themeFilesHint: 'Témata Jellyfinu 12 (web/themes/*/theme.css) čtou proměnné --jf-*, které tohle téma nastavuje. Server aktualizovaný z 10.x může mít staré soubory, které je nečtou; Jellycanvas chybějící pravidla nese ve vlastním CSS, takže téma funguje i tak – tady je to jen pro informaci.',
            themeRepair: 'Opravit staré soubory', themeRepairHint: 'Připojí chybějící pravidla na konec každého starého theme.css (kopie zůstane jako theme.css.jellycanvas-bak). Potřebuje právo zápisu do složky webu – balíčková instalace ho obvykle nemá; správná oprava je přeinstalovat balíček jellyfin-web.',
            themeCurrent: 'aktuální', themeOld: 'starý (formát před 12, {0} znaků) – vygenerované CSS to dorovnává', themePatched: 'starý, opravený Jellycanvasem', themeNone: 'V {0} nejsou žádné soubory témat.', themeRepairDone: 'Soubory témat opraveny.', themeRepairFailed: 'Nešlo zapsat: {0}',
            infobar: 'Informační lišta', infobarEnabled: 'Zobrazit oznamovací proužek', infobarText: 'Text', infobarPosition: 'Umístění', infobarTop: 'Nahoře (pod horní lištou / nad obsahem u postranního panelu)', infobarBottom: 'Dolní okraj', infobarColor: 'Pozadí (prázdné = zvýraznění)', infobarTextColor: 'Barva textu (prázdné = automaticky)', infobarHeight: 'Výška', infobarMobile: 'Schovat na mobilu', infobarTv: 'Schovat na TV', infobarClosable: 'Křížek pro zavření (potřebuje klientský skript)', infobarRemember: 'Pamatovat si zavření do změny textu (vypnuto = lišta se ukáže při každém načtení stránky)', infobarHint: 'Jen prostý text – CSS neumí odkazy. Křížek dodává klientský skript, takže potřebuje File Transformation nebo JS injector.',
            logoUploaded: 'Logo nahráno.', logoDeleted: 'Nahrané logo smazáno.', copied: 'Zkopírováno do schránky.',
            scripts: 'Vlastní tlačítka v liště',
            groupStart: 'Začátek', groupBars: 'Lišty a navigace', groupContent: 'Obsah', groupPages: 'Pozadí a stránky', groupDevices: 'Zařízení', groupAdvanced: 'Pokročilé',
            slotsHeading: 'Uspořádání', slotsHint: 'Přetáhni skupiny mezi levou, střední a pravou částí lišty.', slotLeft: 'Vlevo', slotCenter: 'Střed', slotRight: 'Vpravo',
            chipNav: 'Logo a odkazy', chipIcons: 'Ikony', chipUser: 'Uživatel', slotTop: 'Nahoře', slotMiddle: 'Uprostřed', slotBottom: 'Dole',
            sidebarCollapsible: 'Vysouvací panel (jen ikony, rozbalí se při najetí)', sidebarCollapsed: 'Šířka sbaleného panelu', drawerHint: 'Hamburger menu, které se vysouvá zleva v mobilním rozložení (a v úzkém okně). Na desktopu jsou odkazy v horní liště.', lookHeading: 'Vzhled', navHeading: 'Odkazy a ikony', playedHeading: 'Zhlédnuto a rozkoukáno',
            infobarRadius: 'Zaoblení rohů', follow: 'Sledovat nastavení',
            badges: 'Odznaky na kartách (rozlišení, jazyky)', badgesHint: 'Rozlišení, HDR a jazyky zvuku / titulků na kartách filmů a dílů, načtené z mediálních streamů klientským skriptem (jeden dotaz na dávku karet, s cache). Potřebuje zapnuté skriptové funkce. V náhledu se ukáže po Uložit nebo Použít.', badgesEnabled: 'Zobrazit odznaky na kartách', badgesCornersHeading: 'Rohy', badgesCornersHint: 'Přetáhni každý odznak do rohu karty, nebo do „Vypnuto“, když ho nechceš.', cornerTl: 'Vlevo nahoře', cornerTr: 'Vpravo nahoře', cornerBl: 'Vlevo dole', cornerBr: 'Vpravo dole', cornerOff: 'Vypnuto', chipResolution: 'Rozlišení', chipHdr: 'HDR', chipCodec: 'Kodek videa', chipSound: 'Zvuk (DD+ Atmos 5.1, DTS-HD 7.1…)', badgeColorful: 'Barva podle hodnoty (4K, 1080p, HEVC, Atmos, každý jazyk…)', badgePalette: 'Barevný rozsah', paletteVivid: 'Sytá (celé spektrum)', paletteCool: 'Studená (tyrkysová, modrá, fialová)', paletteWarm: 'Teplá (červená, oranžová, žlutá)', palettePastel: 'Pastelová (světlá, tmavý text)', paletteNeon: 'Neonová (zářivá, tmavý text)', badgeStacked: 'Odznaky v rohu skládat pod sebe', chipAudio: 'Jazyky zvuku', chipSubtitles: 'Jazyky titulků', badgeDark: 'Tmavé pilulky', badgeAccent: 'Pilulky ve zvýrazňovací barvě', badgeScale: 'Velikost', badgeLanguages: 'Jazyky zvuku jako', badgeSubLanguages: 'Jazyky titulků jako', badgeLangFlags: 'Jen vlajky (kreslí skript, nic se nestahuje)', badgeLangCodes: 'Kódy (EN, CS, DE)', badgeLangBoth: 'Vlajky i kódy', badgesMobile: 'Schovat na mobilu', badgesTv: 'Schovat na TV', badgeLangPickHeading: 'Které jazyky', badgeLangPickHint: 'Karta ukáže nejvýš tolik jazyků. Preferované jdou první, pokud je položka má (kódy jako CS, EN – v tvém pořadí); zbytek se doplní nejrozšířenějšími z toho, co zbývá.', badgeAudioMax: 'Jazyků zvuku nejvýš', badgeAudioPreferred: 'Preferované jazyky zvuku', badgeSubtitleMax: 'Jazyků titulků nejvýš', badgeSubtitlePreferred: 'Preferované jazyky titulků', badgeLookHeading: 'Velikost a zařízení',
            seerr: 'Řádky ze Seerru', seerrHint: 'Řádky na domovské stránce ze Seerru (Jellyseerr / Overseerr): co je požadované a brzy vyjde, poslední požadavky, co je trendy. Se Seerrem mluví server s API klíčem; klíč se do prohlížeče nedostane a není součástí sdíleného tématu. Plakát otevře položku tady, když ji knihovna má, jinak její stránku v Seerru.', seerrUrl: 'Adresa Seerru (jak ji vidí server)', seerrKey: 'API klíč (Seerr → Nastavení → Obecné)', seerrTest: 'Otestovat spojení', seerrTestOk: 'Spojení funguje: {0}', seerrTestFail: 'Nepodařilo se: {0}',
            seerrRowsHeading: 'Řádky', seerrRowsHint: 'Každý řádek ukazuje jednu věc ze Seerru; zaškrtni, co mají plakáty nést. Prázdný nadpis dostane výchozí text v jazyce diváka.', seerrAddRow: 'Přidat řádek', seerrKind: 'Co zobrazit', kindUpcoming: 'Brzy vyjde – schválené požadavky, které ještě nejsou v knihovně, nejbližší vydání první', kindRecent: 'Naposledy požadované – poslední požadavky v jakémkoli stavu', kindPending: 'Čeká na schválení', kindAvailable: 'Požadavky, které už dorazily do knihovny', kindTrending: 'Trendy (Seerr discover)', kindPopularMovies: 'Populární filmy (Seerr discover)', kindPopularTv: 'Populární seriály (Seerr discover)', rowTitle: 'Nadpis (prázdné = výchozí v jazyce diváka)', rowPosition: 'Umístění', rowTop: 'Nad řádky Jellyfinu', rowBottom: 'Pod nimi', rowLimit: 'Plakátů nejvýš', rowShowHint: 'Na plakátech:', rowShowTitle: 'Název', rowShowSubtitle: 'Rok / žadatel pod názvem', rowShowDate: 'Datum vydání v rohu', rowShowState: 'Stav požadavku (+ … ½ ✓)', rowShowType: 'Film / Seriál', rowShowRequester: 'Kdo to požadoval',
            slideshow: 'Slideshow na Domů', slideshowHint: 'Velký karusel nahoře na domovské stránce: backdrop, logo nebo název, popis a tlačítko na položku. Vyžaduje zapnuté skriptové funkce (sekce Vlastní tlačítka v liště). V náhledu se objeví po Uložit nebo Použít.',
            slideshowEnabled: 'Zobrazit slideshow', slideshowSource: 'Co ukazovat', ssRandom: 'Náhodné položky', ssLatest: 'Nedávno přidané', ssContinue: 'Pokračovat ve sledování', ssFavorites: 'Oblíbené', ssGenre: 'Žánr (název níže)', ssTag: 'Štítek (název níže)',
            slideshowFilter: 'Název žánru / štítku', slideshowTypes: 'Typy položek', ssBoth: 'Filmy a seriály', ssMovies: 'Filmy', ssSeries: 'Seriály', slideshowCount: 'Počet položek', slideshowInterval: 'Sekund na snímek', slideshowHeight: 'Výška (% okna)',
            slideshowLogo: 'Použít obrázkové logo titulu, když existuje', slideshowOverview: 'Ukázat popis', slideshowButton: 'Ukázat tlačítko na položku',
            scriptsEnabled: 'Zapnout skriptové funkce (tlačítka, slideshow)', scriptsEnabled: 'Zapnout skriptové funkce', scriptsAdd: 'Přidat tlačítko', scriptsCopy: 'Zkopírovat skript pro Injector',
            scriptsHint: 'Vlastní tlačítka v horní liště - třeba „Requests“, které otevře Seerr v překryvu pod lištou. V náhledu se objeví po Uložit nebo Použít.',
            scriptsFt: 'File Transformation je nainstalovaný - skript se do webového klienta vloží automaticky po Použít.',
            scriptsInjector: 'Je nainstalovaný JavaScript Injector - zkopíruj vygenerovaný skript do něj.',
            scriptsNone: 'Tlačítka v liště a slideshow potřebují JavaScript v klientu. Nainstaluj plugin File Transformation (doporučeno, automatické) nebo JavaScript Injector.',
            btnLabel: 'Popisek', btnIcon: 'Ikona (Material Icons)', btnUrl: 'Adresa', btnAction: 'Akce', btnOverlay: 'Otevřít v překryvu pod lištou', btnNewTab: 'Otevřít v nové záložce', btnNavigate: 'Přejít na adresu',
            detailScale: 'Velikost tlačítek na detailu', detailLabels: 'Ukázat na detailu vedle ikon i text', hoverLift: 'Nadzvednout při najetí', uppercase: 'Text velkými písmeny',
            iconRadius: 'Zaoblení ikonových tlačítek (-1 = kulatá)', playHeading: 'Tlačítko Přehrát (detail)', playInherit: 'Jako ostatní tlačítka', playAccent: 'Vyplněné',
            playColor: 'Barva Přehrát (prázdné = zvýraznění)', playRadius: 'Zaoblení Přehrát (-1 = jako tlačítka)', playLabel: 'Ukázat vedle ikony text „Přehrát“',
            btnPlacement: 'Umístění', btnIcons: 'Mezi ikonami vpravo', btnNav: 'Za odkazy vlevo', btnHideTv: 'Schovat na TV', btnKeepAlive: 'Překryv nechat načtený po zavření', iconPick: 'Vybrat…', iconSearch: 'Hledat ikonu…', iconNone: 'Nic nenalezeno - název jde napsat i ručně.', close: 'Zavřít', btnBeforeLogin: 'Ukázat i před přihlášením (na přihlašovací stránce)', btnRemove: 'Odebrat', btnEnabled: 'Zapnuto'
        }
    };
    var LANG_KEY = 'jellycanvas-lang';
    var lang = 'en';
    try {
        lang = strings[localStorage.getItem(LANG_KEY)] ? localStorage.getItem(LANG_KEY) : 'en';
    } catch (e) {
        // localStorage unavailable - English it is
    }

    function t(key, arg) {
        var s = (strings[lang] && strings[lang][key]) || strings.en[key] || key;
        return arg === undefined ? s : s.replace('{0}', arg);
    }

    // The English text of every translatable element is kept in data-en so
    // the language can be switched back and forth without reloading.
    // Sliders and color rows keep their text in a <label> once built; before
    // that the row itself holds it.
    function translate(root) {
        root.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            el.placeholder = t(el.getAttribute('data-i18n-placeholder'));
        });
        ['data-i18n', 'data-i18n-label'].forEach(function (attr) {
            root.querySelectorAll('[' + attr + ']').forEach(function (el) {
                var target = attr === 'data-i18n-label' ? (el.querySelector('label') || el) : el;
                if (!el.hasAttribute('data-en')) {
                    el.setAttribute('data-en', target.textContent.trim());
                }
                var s = lang === 'en' ? el.getAttribute('data-en') : strings[lang][el.getAttribute(attr)];
                if (s) {
                    target.textContent = s;
                }
            });
        });
    }

    function setLang(l) {
        lang = strings[l] ? l : 'en';
        try {
            localStorage.setItem(LANG_KEY, lang);
        } catch (e) {
            // ignore
        }
        translate(page);
        if (status) {
            refreshStatus();
        }
    }

    // ------------------------------------------------------------------
    // State = one object shaped like PluginConfiguration. Controls write
    // into it through "paths" (Header.Radius) and read from it.
    // ------------------------------------------------------------------
    var state = null;
    var presets = [];
    var status = null;
    var device = 'web';

    function getPath(obj, path) {
        return path.split('.').reduce(function (o, k) { return o == null ? undefined : o[k]; }, obj);
    }

    function setPath(obj, path, value) {
        var keys = path.split('.');
        var last = keys.pop();
        var target = keys.reduce(function (o, k) {
            if (o[k] == null) {
                o[k] = {};
            }
            return o[k];
        }, obj);
        target[last] = value;
    }

    // ------------------------------------------------------------------
    // Building the controls. The HTML only holds placeholders with data
    // attributes (data-slider, data-color); here they become real sliders
    // and color pickers. One function for thirty sliders is less work and
    // fewer mistakes than thirty copies of the markup.
    // ------------------------------------------------------------------
    function buildSliders() {
        page.querySelectorAll('[data-slider]').forEach(function (row) {
            var path = row.getAttribute('data-slider');
            var unit = row.getAttribute('data-unit') || '';
            var label = document.createElement('label');
            label.textContent = row.textContent.trim();
            var input = document.createElement('input');
            input.type = 'range';
            input.className = 'jc-slider';
            input.min = row.getAttribute('data-min');
            input.max = row.getAttribute('data-max');
            input.step = row.getAttribute('data-step') || '1';
            input.setAttribute('data-path', path);
            // The number next to the slider is editable: anything the slider
            // cannot reach (or a value beyond its range) can be typed in.
            var num = document.createElement('div');
            num.className = 'jc-num';
            var numInput = document.createElement('input');
            numInput.type = 'number';
            numInput.className = 'emby-input';
            numInput.min = input.min;
            numInput.step = input.step;
            var unitEl = document.createElement('span');
            unitEl.textContent = unit;
            num.appendChild(numInput);
            num.appendChild(unitEl);
            row.textContent = '';
            row.appendChild(label);
            row.appendChild(input);
            row.appendChild(num);

            function commit(v, fromNumber) {
                if (isNaN(v)) {
                    return;
                }
                v = Math.max(parseInt(input.min, 10), v);
                if (!fromNumber) {
                    v = Math.min(parseInt(input.max, 10), v);
                }
                input.value = v;
                if (!fromNumber || document.activeElement !== numInput) {
                    numInput.value = v;
                }
                setPath(state, path, v);
                followPreview(path);
                if (path.indexOf('Scripts.') === 0) {
                    scheduleScriptPreview();
                } else {
                    schedulePreview();
                    if (path.indexOf('InfoBar.') === 0 || path.indexOf('Backdrop.') === 0) {
                        scheduleScriptPreview(); // the close button and the backdrop rotation live in the script
                    }
                }
            }

            input.addEventListener('input', function () { commit(parseInt(input.value, 10), false); });
            numInput.addEventListener('input', function () { commit(parseInt(numInput.value, 10), true); });
            numInput.addEventListener('blur', function () { numInput.value = getPath(state, path); });
            input.jcShow = function (v) {
                input.value = v;
                numInput.value = v;
            };
        });
    }

    function buildColors() {
        page.querySelectorAll('[data-color]').forEach(function (row) {
            var path = row.getAttribute('data-color');
            var optional = row.getAttribute('data-optional') === '1';
            var label = document.createElement('label');
            label.textContent = row.textContent.trim();
            label.style.flex = '1';
            var picker = document.createElement('input');
            picker.type = 'color';
            var hex = document.createElement('input');
            hex.type = 'text';
            // Class only, not is="emby-input": that component expects a
            // <label> next to it and throws on insertion without one.
            hex.className = 'jc-hex emby-input';
            hex.maxLength = 7;
            hex.placeholder = optional ? '(auto)' : '#000000';
            row.textContent = '';
            row.appendChild(label);
            row.appendChild(picker);
            row.appendChild(hex);
            if (optional) {
                var reset = document.createElement('span');
                reset.className = 'jc-reset';
                reset.textContent = '✕';
                reset.title = 'auto';
                row.appendChild(reset);
                reset.addEventListener('click', function () { apply(''); });
            }

            function apply(v) {
                setPath(state, path, v);
                show(v);
                followPreview(path);
                schedulePreview();
            }

            function show(v) {
                hex.value = v || '';
                picker.value = /^#[0-9a-f]{6}$/i.test(v || '') ? v : '#000000';
                picker.style.opacity = v ? '1' : '.35';
            }

            picker.addEventListener('input', function () { apply(picker.value); });
            hex.addEventListener('change', function () {
                var v = hex.value.trim();
                if (v && v[0] !== '#') {
                    v = '#' + v;
                }
                if (/^#[0-9a-f]{6}$/i.test(v)) {
                    apply(v.toLowerCase());
                } else if (!v && optional) {
                    apply('');
                } else {
                    show(getPath(state, path));
                }
            });
            row.jcShow = show;
            row.setAttribute('data-path', path);
        });
    }

    var BOUND_INPUTS = 'select[data-path], input[type=checkbox][data-path], input[type=text][data-path], textarea[data-path]';

    function bindInputs(root) {
        root.querySelectorAll(BOUND_INPUTS).forEach(function (el) {
            if (el.jcBound) {
                return;
            }
            el.jcBound = true;
            var path = el.getAttribute('data-path');
            var event = el.tagName === 'SELECT' || el.type === 'checkbox' ? 'change' : 'input';
            el.addEventListener(event, function () {
                setPath(state, path, el.type === 'checkbox' ? el.checked : el.type === 'number' ? (parseInt(el.value, 10) || 0) : el.value);
                followPreview(path);
                if (path === 'Header.Layout') {
                    renderSlots();
                }
                if (path === 'Scripts.Slideshow.Enabled' && el.checked && !state.Scripts.Enabled) {
                    // Turning the slideshow on implies the script features.
                    state.Scripts.Enabled = true;
                    page.querySelector('#jcScriptsEnabled').checked = true;
                }
                if (path.indexOf('Scripts.') === 0 || path.indexOf('Seerr.') === 0) {
                    scheduleScriptPreview();
                } else {
                    schedulePreview();
                    if (path.indexOf('InfoBar.') === 0 || path.indexOf('Backdrop.') === 0) {
                        scheduleScriptPreview(); // the close button and the backdrop rotation live in the script
                    }
                }
            });
        });
    }

    /**
     * Rows marked data-when="Some.Path=Value" (or "=A|B") are shown only
     * while that setting has one of the values - the sidebar width means
     * nothing without the sidebar layout.
     */
    function refreshConditions() {
        var holds = function (cond) {
            var parts = cond.split('=');
            return parts[1].split('|').indexOf(String(getPath(state, parts[0]))) >= 0;
        };
        var scriptAvailable = !!(status && (status.FileTransformation || status.JsInjector));
        page.querySelectorAll('[data-when]').forEach(function (el) {
            // data-when2: a second condition that must hold as well; a row
            // that also needs the client script stays hidden without one.
            el.hidden = !holds(el.getAttribute('data-when')) || (el.hasAttribute('data-when2') && !holds(el.getAttribute('data-when2')))
                || (el.classList.contains('jc-needs-script') && !scriptAvailable);
        });
    }

    /** Pushes "state" into every control (after load, a preset, a reset). */
    function refreshControls() {
        refreshConditions();
        page.querySelectorAll('[data-slider]').forEach(function (row) {
            var input = row.querySelector('input[type=range]');
            input.jcShow(getPath(state, input.getAttribute('data-path')));
        });
        page.querySelectorAll('[data-color]').forEach(function (row) {
            row.jcShow(getPath(state, row.getAttribute('data-color')) || '');
        });
        page.querySelectorAll(BOUND_INPUTS).forEach(function (el) {
            if (el.closest('.jc-btn')) {
                return; // the toolbar-button rows are rendered from state by renderButtons
            }
            var v = getPath(state, el.getAttribute('data-path'));
            if (el.type === 'checkbox') {
                el.checked = !!v;
            } else {
                el.value = v == null ? '' : v;
            }
        });
        renderButtons();
        renderSeerrRows();
        renderSlots();
        renderBadgeZones();
    }

    // ------------------------------------------------------------------
    // Top bar slots: three drop zones, three draggable chips. The result is
    // stored as comma-separated group ids per slot (Header.SlotLeft...).
    // ------------------------------------------------------------------
    var SLOT_GROUPS = { nav: { icon: 'menu', key: 'chipNav' }, icons: { icon: 'apps', key: 'chipIcons' }, user: { icon: 'account_circle', key: 'chipUser' } };
    var SLOT_NAMES = ['SlotLeft', 'SlotCenter', 'SlotRight'];
    var dragging = null;

    function slotList(name) {
        return (state.Header[name] || '').split(',').map(function (s) { return s.trim(); }).filter(function (s) { return SLOT_GROUPS[s]; });
    }

    function renderSlots() {
        var seen = {};
        // The same three slots read top / middle / bottom for a sidebar.
        var sidebar = state.Header.Layout === 'Sidebar';
        var titles = sidebar ? ['slotTop', 'slotMiddle', 'slotBottom'] : ['slotLeft', 'slotCenter', 'slotRight'];
        SLOT_NAMES.forEach(function (name, i) {
            page.querySelector('.jc-slot[data-slot="' + name + '"] .jc-slot-title').textContent = t(titles[i]);
        });
        SLOT_NAMES.forEach(function (name) {
            var zone = page.querySelector('.jc-slot[data-slot="' + name + '"]');
            zone.querySelectorAll('.jc-chip').forEach(function (c) { c.remove(); });
            slotList(name).forEach(function (id) {
                if (seen[id]) {
                    return;
                }
                seen[id] = true;
                zone.appendChild(makeChip(id));
            });
        });
        // A group missing from every slot goes back to its stock place.
        Object.keys(SLOT_GROUPS).forEach(function (id) {
            if (!seen[id]) {
                var name = id === 'nav' ? 'SlotLeft' : 'SlotRight';
                page.querySelector('.jc-slot[data-slot="' + name + '"]').appendChild(makeChip(id));
                state.Header[name] = slotList(name).concat([id]).join(',');
            }
        });
    }

    function makeChip(id) {
        var chip = document.createElement('div');
        chip.className = 'jc-chip';
        chip.draggable = true;
        chip.setAttribute('data-group', id);
        chip.innerHTML = '<span class="material-icons" aria-hidden="true">' + SLOT_GROUPS[id].icon + '</span><span></span>';
        chip.lastElementChild.textContent = t(SLOT_GROUPS[id].key);
        chip.addEventListener('dragstart', function (e) {
            dragging = chip;
            chip.classList.add('is-dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', id);
        });
        chip.addEventListener('dragend', function () {
            chip.classList.remove('is-dragging');
            dragging = null;
        });
        return chip;
    }

    page.querySelectorAll('#jcSlots .jc-slot').forEach(function (zone) {
        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            zone.classList.add('is-over');
        });
        zone.addEventListener('dragleave', function () { zone.classList.remove('is-over'); });
        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            zone.classList.remove('is-over');
            if (!dragging || !dragging.hasAttribute('data-group')) {
                return;
            }
            // Drop before the chip under the cursor, otherwise at the end.
            var target = e.target.closest('.jc-chip');
            if (target && target !== dragging && target.parentElement === zone) {
                zone.insertBefore(dragging, target);
            } else {
                zone.appendChild(dragging);
            }
            SLOT_NAMES.forEach(function (name) {
                var ids = [].map.call(page.querySelectorAll('.jc-slot[data-slot="' + name + '"] .jc-chip'), function (c) { return c.getAttribute('data-group'); });
                state.Header[name] = ids.join(',');
            });
            schedulePreview();
        });
    });

    // ------------------------------------------------------------------
    // Card badge corners: the same drag & drop as the bar slots, with four
    // corners and an "Off" pool. Stored per corner in
    // Scripts.CardBadges.TopLeft... as comma-separated badge ids.
    // ------------------------------------------------------------------
    var BADGE_CHIPS = { resolution: { icon: 'hd', key: 'chipResolution' }, hdr: { icon: 'hdr_on', key: 'chipHdr' }, codec: { icon: 'movie', key: 'chipCodec' }, sound: { icon: 'surround_sound', key: 'chipSound' }, audio: { icon: 'volume_up', key: 'chipAudio' }, subtitles: { icon: 'subtitles', key: 'chipSubtitles' } };
    var BADGE_CORNERS = ['TopLeft', 'TopRight', 'BottomLeft', 'BottomRight'];

    function badgeList(name) {
        return (state.Scripts.CardBadges[name] || '').split(',').map(function (s) { return s.trim(); }).filter(function (s) { return BADGE_CHIPS[s]; });
    }

    function makeBadgeChip(id) {
        var chip = document.createElement('div');
        chip.className = 'jc-chip';
        chip.draggable = true;
        chip.setAttribute('data-badge', id);
        chip.innerHTML = '<span class="material-icons" aria-hidden="true">' + BADGE_CHIPS[id].icon + '</span><span></span>';
        chip.lastElementChild.textContent = t(BADGE_CHIPS[id].key);
        chip.addEventListener('dragstart', function (e) {
            dragging = chip;
            chip.classList.add('is-dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', id);
        });
        chip.addEventListener('dragend', function () {
            chip.classList.remove('is-dragging');
            dragging = null;
        });
        return chip;
    }

    function renderBadgeZones() {
        if (!state.Scripts || !state.Scripts.CardBadges) {
            return;
        }
        var seen = {};
        page.querySelectorAll('#jcBadgeZones .jc-chip').forEach(function (c) { c.remove(); });
        BADGE_CORNERS.forEach(function (name) {
            var zone = page.querySelector('#jcBadgeZones .jc-slot[data-badge-slot="' + name + '"]');
            badgeList(name).forEach(function (id) {
                if (!seen[id]) {
                    seen[id] = true;
                    zone.appendChild(makeBadgeChip(id));
                }
            });
        });
        var off = page.querySelector('#jcBadgeZones .jc-slot[data-badge-slot="Off"]');
        Object.keys(BADGE_CHIPS).forEach(function (id) {
            if (!seen[id]) {
                off.appendChild(makeBadgeChip(id));
            }
        });
    }

    page.querySelectorAll('#jcBadgeZones .jc-slot').forEach(function (zone) {
        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            zone.classList.add('is-over');
        });
        zone.addEventListener('dragleave', function () { zone.classList.remove('is-over'); });
        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            zone.classList.remove('is-over');
            if (!dragging || !dragging.hasAttribute('data-badge')) {
                return;
            }
            var target = e.target.closest('.jc-chip');
            if (target && target !== dragging && target.parentElement === zone) {
                zone.insertBefore(dragging, target);
            } else {
                zone.appendChild(dragging);
            }
            BADGE_CORNERS.forEach(function (name) {
                var ids = [].map.call(page.querySelectorAll('#jcBadgeZones .jc-slot[data-badge-slot="' + name + '"] .jc-chip'), function (c) { return c.getAttribute('data-badge'); });
                state.Scripts.CardBadges[name] = ids.join(',');
            });
            scheduleScriptPreview();
        });
    });

    // ------------------------------------------------------------------
    // Presets - the tiles at the top. The settings come from the server so
    // they live in one place (Theme/Presets.cs).
    // ------------------------------------------------------------------
    function renderPresets() {
        var box = page.querySelector('#jcPresets');
        box.textContent = '';
        presets.forEach(function (p) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'jc-preset';
            var name = document.createElement('strong');
            name.textContent = p.Name;
            var desc = document.createElement('small');
            desc.textContent = p.Description;
            var sw = document.createElement('div');
            sw.className = 'jc-swatches';
            [p.Settings.Colors.Background, p.Settings.Colors.Surface, p.Settings.Colors.Accent, p.Settings.Colors.Text].forEach(function (c) {
                var s = document.createElement('span');
                s.style.background = c;
                sw.appendChild(s);
            });
            btn.appendChild(name);
            btn.appendChild(desc);
            btn.appendChild(sw);
            btn.addEventListener('click', function () { loadPreset(p.Settings); });
            box.appendChild(btn);
        });
    }

    /** A preset replaces the look but keeps what is tied to this server: enabled flag, logo, toolbar buttons. */
    function loadPreset(settings) {
        var keep = { Enabled: state.Enabled, Scripts: state.Scripts, LogoUrl: state.Header.LogoUrl };
        state = JSON.parse(JSON.stringify(settings));
        state.Enabled = keep.Enabled;
        state.Scripts = keep.Scripts;
        state.Header.LogoUrl = keep.LogoUrl;
        refreshControls();
        schedulePreview();
    }

    // ------------------------------------------------------------------
    // Toolbar buttons (the script section). Rendered from state as small
    // forms cloned from a <template>; each field writes back through a
    // data-path that includes the button's index.
    // ------------------------------------------------------------------
    function renderButtons() {
        var box = page.querySelector('#jcButtons');
        var tpl = page.querySelector('#jcButtonTemplate');
        box.textContent = '';
        var list = (state.Scripts && state.Scripts.ToolbarButtons) || [];
        list.forEach(function (b, i) {
            var node = tpl.content.firstElementChild.cloneNode(true);
            node.querySelectorAll('[data-field]').forEach(function (el) {
                var field = el.getAttribute('data-field');
                el.setAttribute('data-path', 'Scripts.ToolbarButtons.' + i + '.' + field);
                if (el.type === 'checkbox') {
                    el.checked = !!b[field];
                } else {
                    el.value = b[field] == null ? '' : b[field];
                }
            });
            var iconInput = node.querySelector('[data-field="Icon"]');
            var iconPreview = node.querySelector('.jc-icon-preview');
            iconPreview.textContent = iconInput.value;
            iconInput.addEventListener('input', function () { iconPreview.textContent = iconInput.value; });
            node.querySelector('.jc-icon-pick').addEventListener('click', function () { openIconPicker(iconInput); });
            node.querySelector('.jc-btn-remove').addEventListener('click', function () {
                state.Scripts.ToolbarButtons.splice(i, 1);
                renderButtons();
                scheduleScriptPreview();
            });
            translate(node);
            box.appendChild(node);
        });
        bindInputs(box);
    }

    // Seerr rows: the same kind of list as the toolbar buttons.
    function renderSeerrRows() {
        var box = page.querySelector('#jcSeerrRows');
        var tpl = page.querySelector('#jcSeerrRowTemplate');
        box.textContent = '';
        var list = (state.Seerr && state.Seerr.Rows) || [];
        list.forEach(function (r, i) {
            var node = tpl.content.firstElementChild.cloneNode(true);
            node.querySelectorAll('[data-field]').forEach(function (el) {
                var field = el.getAttribute('data-field');
                el.setAttribute('data-path', 'Seerr.Rows.' + i + '.' + field);
                if (el.type === 'checkbox') {
                    el.checked = !!r[field];
                } else {
                    el.value = r[field] == null ? '' : r[field];
                }
            });
            node.querySelector('.jc-btn-remove').addEventListener('click', function () {
                state.Seerr.Rows.splice(i, 1);
                renderSeerrRows();
                scheduleScriptPreview();
            });
            translate(node);
            box.appendChild(node);
        });
        bindInputs(box);
    }

    page.querySelector('#jcBtnAddSeerrRow').addEventListener('click', function () {
        if (!state.Seerr) {
            state.Seerr = { Url: '', ApiKey: '', Rows: [] };
        }
        if (!state.Seerr.Rows) {
            state.Seerr.Rows = [];
        }
        state.Seerr.Rows.push({ Enabled: true, Kind: 'Upcoming', Title: '', Position: 'Top', Limit: 20, ShowTitle: true, ShowSubtitle: true, ShowDate: true, ShowState: true, ShowType: false, ShowRequester: false });
        renderSeerrRows();
        scheduleScriptPreview();
    });

    page.querySelector('#jcBtnAddButton').addEventListener('click', function () {
        if (!state.Scripts) {
            state.Scripts = { Enabled: true, ToolbarButtons: [] };
        }
        state.Scripts.Enabled = true;
        page.querySelector('#jcScriptsEnabled').checked = true;
        state.Scripts.ToolbarButtons.push({ Enabled: true, Label: 'Requests', Icon: 'playlist_add', Url: '', Action: 'Overlay', Placement: 'Icons', HideOnTv: true, KeepAlive: true, ShowBeforeLogin: false });
        renderButtons();
        scheduleScriptPreview();
    });

    // ------------------------------------------------------------------
    // Icon picker. Material Icons cannot be enumerated from the font, so a
    // curated list of the names people actually put in a toolbar; anything
    // else can still be typed into the field.
    // ------------------------------------------------------------------
    var ICONS = ('home search settings menu apps widgets dashboard grid_view view_list list tune filter_list sort ' +
        'star star_border favorite favorite_border bookmark bookmarks label sell flag thumb_up thumb_down ' +
        'playlist_add playlist_play queue_music library_add library_books library_music video_library collections ' +
        'movie movie_filter local_movies theaters tv live_tv smart_display ondemand_video slideshow subscriptions ' +
        'music_note album radio podcasts headphones mic speaker volume_up ' +
        'play_arrow play_circle pause stop skip_next skip_previous replay shuffle repeat cast cast_connected ' +
        'book menu_book auto_stories article description note_add task checklist request_page request_quote ' +
        'rss_feed newspaper feed forum chat chat_bubble comment mail email send notifications notifications_active ' +
        'person people group groups person_add manage_accounts admin_panel_settings badge verified_user ' +
        'analytics bar_chart insights trending_up query_stats leaderboard emoji_events military_tech ' +
        'info help help_outline support contact_support bug_report build construction extension science ' +
        'link open_in_new launch public language translate subtitles closed_caption explore travel_explore map ' +
        'calendar_today calendar_month event schedule history update sync refresh cached ' +
        'cloud cloud_download cloud_upload download upload file_download file_upload folder folder_open storage dns ' +
        'security lock lock_open key vpn_key shield policy privacy_tip ' +
        'add add_circle remove delete edit save print share ios_share content_copy ' +
        'devices computer laptop tablet phone_android phone_iphone watch router wifi bluetooth memory ' +
        'lightbulb tips_and_updates whatshot new_releases fiber_new celebration rocket_launch bolt ' +
        'sports_esports games casino pets school work shopping_cart store local_offer ' +
        'photo image photo_library camera videocam brush palette color_lens format_paint ' +
        'more_horiz more_vert arrow_forward arrow_back expand_more open_in_full fullscreen visibility visibility_off').split(' ');

    var picker = null;
    var pickerTarget = null;

    function openIconPicker(input) {
        pickerTarget = input;
        if (!picker) {
            picker = document.createElement('div');
            picker.className = 'jc-picker';
            picker.innerHTML = '<div class="jc-picker-box"><div class="jc-picker-head">' +
                '<input type="text" class="emby-input jc-picker-search"><button type="button" class="raised emby-button jc-picker-close"></button></div>' +
                '<div class="jc-picker-grid"></div></div>';
            page.appendChild(picker);
            picker.addEventListener('click', function (e) {
                if (e.target === picker) {
                    closeIconPicker();
                }
            });
            picker.querySelector('.jc-picker-close').addEventListener('click', closeIconPicker);
            picker.querySelector('.jc-picker-search').addEventListener('input', function () { renderIcons(this.value); });
            picker.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') {
                    closeIconPicker();
                }
            });
        }
        picker.querySelector('.jc-picker-search').placeholder = t('iconSearch');
        picker.querySelector('.jc-picker-close').textContent = t('close');
        picker.querySelector('.jc-picker-search').value = '';
        renderIcons('');
        picker.hidden = false;
        picker.querySelector('.jc-picker-search').focus();
    }

    function closeIconPicker() {
        if (picker) {
            picker.hidden = true;
        }
        pickerTarget = null;
    }

    function renderIcons(filter) {
        var grid = picker.querySelector('.jc-picker-grid');
        grid.textContent = '';
        var q = (filter || '').trim().toLowerCase().replace(/\s+/g, '_');
        var shown = 0;
        ICONS.forEach(function (name) {
            if (q && name.indexOf(q) < 0) {
                return;
            }
            shown++;
            var item = document.createElement('button');
            item.type = 'button';
            item.className = 'jc-picker-item';
            item.innerHTML = '<span class="material-icons" aria-hidden="true"></span><small></small>';
            item.querySelector('.material-icons').textContent = name;
            item.querySelector('small').textContent = name;
            item.addEventListener('click', function () {
                if (pickerTarget) {
                    pickerTarget.value = name;
                    // "input" is what the binding listens to - it writes state and refreshes the script preview.
                    pickerTarget.dispatchEvent(new Event('input', { bubbles: true }));
                }
                closeIconPicker();
            });
            grid.appendChild(item);
        });
        if (!shown) {
            var empty = document.createElement('div');
            empty.className = 'jc-picker-empty';
            empty.textContent = t('iconNone');
            grid.appendChild(empty);
        }
    }

    page.querySelector('#jcBtnCopyScript').addEventListener('click', function () {
        var out = page.querySelector('#jcScriptOut');
        if (!out.value) {
            return;
        }
        if (navigator.clipboard) {
            navigator.clipboard.writeText(out.value).then(function () { toast(t('copied')); }, fail);
        } else {
            out.select();
            document.execCommand('copy');
            toast(t('copied'));
        }
    });

    /** Shows the script section only when the server reports a plugin that can inject the script. */
    // The plugins Jellycanvas can work with, what each one unlocks here,
    // and where it comes from. Shown as a status list at the top of the page.
    var COMPANIONS = [
        { key: 'FileTransformation', name: 'File Transformation', url: 'https://github.com/IAmParadox27/jellyfin-plugin-file-transformation', descKey: 'pluginFtDesc' },
        { key: 'JsInjector', name: 'JavaScript Injector', url: 'https://github.com/n00bcodr/Jellyfin-JavaScript-Injector', descKey: 'pluginInjectorDesc' }
    ];

    function renderPlugins() {
        var box = page.querySelector('#jcPlugins');
        box.textContent = '';
        COMPANIONS.forEach(function (c) {
            var on = !!(status && status[c.key]);
            var row = document.createElement('div');
            row.className = 'jc-plugin ' + (on ? 'is-on' : 'is-off');
            var icon = document.createElement('span');
            icon.className = 'material-icons';
            icon.setAttribute('aria-hidden', 'true');
            icon.textContent = on ? 'check_circle' : 'radio_button_unchecked';
            var name = document.createElement('div');
            name.className = 'jc-plugin-name';
            var link = document.createElement('a');
            link.href = c.url;
            link.target = '_blank';
            link.rel = 'noopener';
            link.textContent = c.name;
            name.appendChild(link);
            var state = document.createElement('div');
            state.className = 'jc-plugin-state';
            state.textContent = on ? t('pluginInstalled') : t('pluginMissing');
            var desc = document.createElement('div');
            desc.className = 'jc-plugin-desc';
            desc.textContent = t(c.descKey);
            row.appendChild(icon);
            row.appendChild(name);
            row.appendChild(state);
            row.appendChild(desc);
            box.appendChild(row);
        });
    }

    function refreshScriptSection() {
        var available = !!(status && (status.FileTransformation || status.JsInjector));
        renderPlugins();
        page.querySelector('#jcScriptsSection').hidden = !available;
        page.querySelector('#jcSlideshowSection').hidden = !available;
        page.querySelector('#jcSeerrSection').hidden = !available;
        page.querySelector('#jcBadgesSection').hidden = !available;
        page.querySelectorAll('.jc-needs-script:not([data-when])').forEach(function (el) { el.hidden = !available; });
        refreshConditions();
        page.querySelector('#jcScriptsMissing').hidden = available;
        if (!available) {
            page.querySelector('#jcScriptsMissing').textContent = t('scriptsNone');
            return;
        }
        page.querySelector('#jcScriptsNote').textContent = status.FileTransformation ? t('scriptsFt') : t('scriptsInjector');
        page.querySelector('#jcBtnCopyScript').hidden = !!status.FileTransformation;
    }

    var scriptTimer = null;
    var lastScript = '';
    function scheduleScriptPreview() {
        clearTimeout(scriptTimer);
        scriptTimer = setTimeout(function () {
            ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl('Jellycanvas/ScriptPreview'),
                data: JSON.stringify(state),
                contentType: 'application/json',
                dataType: 'text'
            }).then(function (js) {
                page.querySelector('#jcScriptOut').value = js;
                lastScript = js;
                injectScript();
            }).catch(function (e) { console.error('Jellycanvas script preview failed', e); });
        }, 200);
    }

    /**
     * Runs the script built from the settings being edited inside the
     * preview, in place of the saved copy the client loaded (through File
     * Transformation) - so a moved or renamed button shows up right away,
     * not after Save and a reload. Only when a plugin that can inject the
     * script is installed: without one the preview would promise something
     * the server cannot deliver.
     */
    function injectScript() {
        if (!lastScript || !(status && (status.FileTransformation || status.JsInjector))) {
            return;
        }
        var doc;
        try {
            doc = frame.contentDocument;
        } catch (e) {
            return;
        }
        if (!doc || !doc.body) {
            return;
        }
        var running = doc.defaultView.__jellycanvasScript;
        if (running && typeof running.dispose === 'function') {
            running.dispose();
        }
        var old = doc.getElementById('jellycanvas-preview-script');
        if (old) {
            old.remove();
        }
        // Closing the info bar in the preview stays in the preview; the
        // strip comes back on the next script preview.
        doc.defaultView.__jellycanvasPreview = true;
        doc.documentElement.classList.remove('jellycanvas-infobar-closed');
        var script = doc.createElement('script');
        script.id = 'jellycanvas-preview-script';
        script.textContent = lastScript;
        doc.body.appendChild(script);
    }

    // ------------------------------------------------------------------
    // Preview. An iframe with the real client; our <style> goes into its
    // <head>. Same origin, so we are allowed to reach in.
    // ------------------------------------------------------------------
    var frame = page.querySelector('#jcFrame');
    var frameWrap = page.querySelector('#jcFrameWrap');
    var stage = page.querySelector('#jcStage');
    var lastCss = '';
    var previewTimer = null;
    var previewInFlight = false;
    var previewDirty = false;

    var devices = {
        // 1366 rather than 1280: at 80em and below jellyfin-web makes every
        // dialog full-screen, which is not what most desktops see.
        web: { w: 1366, h: 800, cls: 'layout-desktop' },
        tv: { w: 1920, h: 1080, cls: 'layout-tv' },
        mobile: { w: 390, h: 844, cls: 'layout-mobile' }
    };

    function schedulePreview() {
        refreshConditions();
        // A slider fires dozens of events a second; one request per 120 ms
        // is plenty. While one is in flight the next one waits.
        clearTimeout(previewTimer);
        previewTimer = setTimeout(requestPreview, 120);
    }

    function requestPreview() {
        if (previewInFlight) {
            previewDirty = true;
            return;
        }
        previewInFlight = true;
        ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('Jellycanvas/Preview'),
            data: JSON.stringify(state),
            contentType: 'application/json',
            dataType: 'text'
        }).then(function (css) {
            lastCss = css;
            page.querySelector('#jcCssOut').value = css;
            injectCss();
        }).catch(function (e) {
            console.error('Jellycanvas preview failed', e);
        }).finally(function () {
            previewInFlight = false;
            if (previewDirty) {
                previewDirty = false;
                requestPreview();
            }
        });
    }

    // Ctrl+click (Cmd+click on a Mac) in the preview jumps to the settings
    // of the element clicked: [selector, section, anchor]. The anchor is the
    // sub-heading (its data-i18n key) or a control ("[data-path=...]") to
    // scroll to inside the section. Checked from the most specific selector
    // to the most general; the first match wins.
    var CLICK_MAP = [
        ['header.MuiAppBar-root a[href="#/"], .pageTitleWithDefaultLogo', 'logo'],
        ['header.MuiAppBar-root .MuiToolbar-root:nth-child(2)', 'header', 'libraryRowHeading'],
        ['header.MuiAppBar-root .MuiStack-root > a.MuiButton-sizeMedium, header.MuiAppBar-root [aria-controls], header.MuiAppBar-root a[href^="#/search"]', 'header', 'navHeading'],
        ['#jellycanvasInfoClose', 'infobar'],
        ['[data-jellycanvas]', 'scripts'],
        ['header.MuiAppBar-root, .skinHeader', 'header', 'lookHeading'],
        ['.mainDrawer, .MuiDrawer-paper', 'drawer'],
        ['#jellycanvasSlideshow', 'slideshow'],
        ['.upNextContainer', 'dialogs', '[data-path="Dialogs.UpNext"]'],
        ['.skip-button-container', 'player', 'skipHeading'],
        ['.videoOsdBottom, .osdHeader', 'player', 'osdHeading'],
        ['.dialog, .MuiMenu-paper, .MuiPopover-paper, .MuiDialog-paper, .toast', 'dialogs'],
        ['#loginPage h1, #loginPage .btnQuick, #loginPage .btnForgotPassword', 'login', 'loginTextsHeading'],
        ['#loginPage .emby-input, #loginPage .emby-button', 'login', 'loginFieldsHeading'],
        ['#loginPage .manualLoginForm, #loginPage .visualLoginForm', 'login', 'loginFormHeading'],
        ['#loginPage', 'login', 'backgroundHeading'],
        ['.mainDetailButtons .btnPlay, .mainDetailButtons .btnPlaySimple, .btnPlay', 'buttons', 'playHeading'],
        ['.detailButton', 'buttons', 'buttonsDetailHeading'],
        ['.emby-button, .MuiButton-root, .emby-input, .emby-select', 'buttons', 'buttonsAllHeading'],
        ['.personCard', 'detail', 'peopleHeading'],
        ['.playedIndicator, .cardIndicators, .indicators, .itemProgressBar, .itemLinearProgress', 'cards', 'playedHeading'],
        ['.card, .cardBox, .listItem', 'cards', 'lookHeading'],
        ['.detailRibbon', 'detail', 'ribbonHeading'],
        ['.detailImageContainer, .itemDetailImage, .detailLogo', 'detail', 'posterHeading'],
        ['.detailPageWrapperContainer, .itemBackdrop', 'detail'],
        ['h1, h2, h3, .sectionTitle, p', 'typography'],
        ['.backgroundContainer, .backdropContainer, .mainAnimatedPage', 'backdrop']
    ];

    function isTextOnly(el) {
        if (!el || !el.textContent || !el.textContent.trim()) {
            return false;
        }
        if (el.matches && el.matches('.material-icons, img, svg, input, select, textarea')) {
            return false;
        }
        for (var i = 0; i < el.childNodes.length; i++) {
            var n = el.childNodes[i];
            if (n.nodeType === 1 && !(n.matches('bdi, b, i, em, strong, span, a') && isTextOnly(n))) {
                return false;
            }
        }
        return true;
    }

    function hookPreviewClicks(doc) {
        if (doc.jcHooked) {
            return;
        }
        doc.jcHooked = true;
        doc.addEventListener('click', function (e) {
            if (!e.ctrlKey && !e.metaKey) {
                return;
            }
            e.preventDefault();
            e.stopPropagation();
            var target = e.target;
            var section = 'colors';
            var anchor = null;
            // Plain text (a title, a card caption, a description) belongs
            // to typography even when it sits inside a card or the header;
            // text on a button or link still belongs to that control.
            if (isTextOnly(target) && !target.closest('button, .emby-button, .MuiButtonBase-root, header a')) {
                section = 'typography';
            } else {
                for (var i = 0; i < CLICK_MAP.length; i++) {
                    if (target.closest && target.closest(CLICK_MAP[i][0])) {
                        section = CLICK_MAP[i][1];
                        anchor = CLICK_MAP[i][2] || null;
                        break;
                    }
                }
            }
            // The bar itself: its own sub-heading depends on the layout.
            if (section === 'header' && anchor === 'lookHeading') {
                anchor = state.Header.Layout === 'Sidebar' ? 'sidebarHeading' : 'topBarHeading';
            }
            // Under a top bar the library row has no settings of its own.
            if (anchor === 'libraryRowHeading' && state.Header.Layout !== 'Sidebar') {
                anchor = 'topBarHeading';
            }
            openSection(section, anchor);
        }, true);
    }

    function openSection(name, anchor) {
        var details = page.querySelector('.jc-section[data-section="' + name + '"]');
        if (!details || details.hidden) {
            return;
        }
        details.open = true;
        // A sub-heading or a control inside the section, when one is known
        // and currently shown; otherwise the section's top.
        var spot = null;
        if (anchor) {
            spot = details.querySelector(anchor.charAt(0) === '[' ? anchor : '.jc-sub[data-i18n="' + anchor + '"]');
            if (spot && spot.hidden) {
                spot = null;
            }
        }
        var flash = spot || details;
        (spot || details).scrollIntoView({ behavior: 'smooth', block: spot ? 'center' : 'start' });
        flash.classList.add('jc-flash');
        setTimeout(function () { flash.classList.remove('jc-flash'); }, 1500);
    }

    function injectCss() {
        var doc;
        try {
            doc = frame.contentDocument;
        } catch (e) {
            return;
        }
        if (!doc || !doc.head) {
            return;
        }
        var style = doc.getElementById('jellycanvas-preview');
        if (!style) {
            style = doc.createElement('style');
            style.id = 'jellycanvas-preview';
        }
        // Once a theme is applied, the client renders the saved CSS in a
        // <style> inside <body>; ours must come after it, or the saved rules
        // win on equal specificity and the preview looks frozen. So: the
        // saved copies are switched off in the preview and ours goes last.
        if (style !== doc.body.lastElementChild) {
            doc.body.appendChild(style);
        }
        doc.querySelectorAll('style').forEach(function (other) {
            if (other !== style && other.textContent.indexOf('JELLYCANVAS START') >= 0) {
                other.disabled = true;
            }
        });
        if (style.textContent !== lastCss) {
            style.textContent = lastCss;
        }
        // The client picks its layout class from the device; for the preview
        // we override it with the one the user selected.
        var html = doc.documentElement;
        ['layout-desktop', 'layout-tv', 'layout-mobile'].forEach(function (c) { html.classList.remove(c); });
        html.classList.add(devices[device].cls);
        hookPreviewClicks(doc);
    }

    function layoutStage() {
        var d = devices[device];
        var available = stage.clientWidth;
        if (!available) {
            return;
        }
        // Scale so the preview fits both the width and the window height
        // (a portrait phone would otherwise run off the bottom).
        var maxHeight = Math.max(320, window.innerHeight - 230);
        var scale = Math.min(1, available / d.w, maxHeight / d.h);
        frame.style.width = d.w + 'px';
        frame.style.height = d.h + 'px';
        frameWrap.style.transform = 'scale(' + scale + ')';
        frameWrap.style.width = d.w + 'px';
        frameWrap.style.left = Math.round((available - (d.w * scale)) / 2) + 'px';
        stage.style.height = Math.round(d.h * scale) + 'px';
    }

    function previewUrl(kind) {
        // The client lives at /web/ (or wherever Jellyfin puts it) - take the
        // same path as this page, just with a different hash route. The
        // query parameter defeats the browser cache of index.html: File
        // Transformation keeps the file's ETag, so a cached copy would never
        // pick up the injected script tag.
        var base = location.origin + location.pathname + '?jc=' + Date.now();
        switch (kind) {
            case 'library':
                return findLibrary().then(function (lib) { return base + (lib ? libraryRoute(lib) : '#/home'); });
            case 'detail':
                return findItem().then(function (id) { return base + (id ? '#/details?id=' + id : '#/home'); });
            case 'login':
                return Promise.resolve(base + '#/login');
            case 'player':
            case 'upnext':
            case 'stillwatching':
                // The player prompts are mocked on top of the home page (injectMock).
                return Promise.resolve(base + '#/home');
            default:
                return Promise.resolve(base + '#/home');
        }
    }

    function findLibrary() {
        return ApiClient.getUserViews({}, ApiClient.getCurrentUserId()).then(function (r) {
            return (r.Items || []).find(function (i) { return i.CollectionType === 'movies' || i.CollectionType === 'tvshows'; }) || (r.Items || [])[0] || null;
        }).catch(function () { return null; });
    }

    /** Jellyfin 12 has a route per library type; the generic "#/list" is only for the rest. */
    function libraryRoute(lib) {
        var q = '?topParentId=' + lib.Id + '&collectionType=' + (lib.CollectionType || '');
        switch (lib.CollectionType) {
            case 'movies': return '#/movies' + q;
            case 'tvshows': return '#/tv' + q;
            case 'music': return '#/music' + q;
            case 'livetv': return '#/livetv';
            default: return '#/list?parentId=' + lib.Id;
        }
    }

    function findItem() {
        return ApiClient.getItems(ApiClient.getCurrentUserId(), {
            IncludeItemTypes: 'Movie,Series', Recursive: true, Limit: 1, SortBy: 'Random', ImageTypes: 'Backdrop'
        }).then(function (r) {
            return r.Items && r.Items[0] ? r.Items[0].Id : null;
        }).catch(function () { return null; });
    }

    // "Follow settings": switch the preview to the page where the setting
    // being changed can actually be seen. Only prefixes with an obvious home
    // are listed; everything else (colors, bar, cards...) shows on any page,
    // so the preview stays where it is.
    var FOLLOW = [
        ['Login.', 'login'],
        ['Detail.', 'detail'],
        // Other dialog settings stay where they are: a menu opened in the
        // preview to look at is worth more than a jump to the mock prompt.
        ['Dialogs.UpNext', 'upnext'],
        ['Player.', 'player'],
        ['Buttons.Play', 'detail'],
        ['Buttons.Detail', 'detail'],
        ['Header.LibraryRow', 'library'],
        ['Cards.Played', 'library'],
        ['Cards.Progress', 'library'],
        ['Scripts.Slideshow.', 'home'],
        ['Backdrop.', 'home'],
        ['InfoBar.', 'home']
    ];

    // Device-specific settings also switch the preview device.
    var FOLLOW_DEVICE = [
        ['Tv.', 'tv'],
        ['Mobile.', 'mobile'],
        ['Drawer.', 'mobile']
    ];

    function followPreview(path) {
        if (!page.querySelector('#jcFollow').checked) {
            return;
        }
        for (var d = 0; d < FOLLOW_DEVICE.length; d++) {
            if (path.indexOf(FOLLOW_DEVICE[d][0]) === 0 && device !== FOLLOW_DEVICE[d][1]) {
                selectDevice(FOLLOW_DEVICE[d][1]);
                break;
            }
        }
        var target = null;
        for (var i = 0; i < FOLLOW.length; i++) {
            if (path.indexOf(FOLLOW[i][0]) === 0) {
                target = FOLLOW[i][1];
                break;
            }
        }
        var select = page.querySelector('#jcPage');
        if (target && select.value !== target) {
            select.value = target;
            loadPreview();
        }
    }

    function loadPreview() {
        var kind = page.querySelector('#jcPage').value;
        previewUrl(kind).then(function (url) {
            // Changing only the hash would not reload the page (and our
            // <style> would stay); "about:blank" in between forces a clean load.
            frame.src = 'about:blank';
            setTimeout(function () {
                pushPreviewLayout();
                frame.src = url;
            }, 30);
        });
    }

    frame.addEventListener('load', function () {
        restorePreviewLayout();
        injectCss();
        injectScript();
        // The client keeps building the page for a while after start and
        // rewrites the classes on <html>; a few repeats settle it.
        var n = 0;
        var timer = setInterval(function () {
            injectCss();
            injectMock();
            if (++n > 20) {
                clearInterval(timer);
            }
        }, 250);
    });

    // ------------------------------------------------------------------
    // Player prompts. They only exist while a video plays, which the
    // preview cannot do - so the same markup the client builds (from its
    // upNextDialog and dialog components) is put over the page instead.
    // ------------------------------------------------------------------
    var MOCK_KINDS = { player: true, upnext: true, stillwatching: true };

    function mockHtml(kind) {
        var esc = function (s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); };
        if (kind === 'player') {
            // The player's own rules come along (from jellyfin-web's
            // videoosd.scss, emby-slider.scss and the skip button); the
            // theme's rules win over them, as in the real player.
            var btn = function (icon, cls) { return '<button is="paper-icon-button-light" class="' + (cls || '') + ' paper-icon-button-light autoSize" type="button"><span class="xlargePaperIconButton material-icons ' + icon + '" aria-hidden="true"></span></button>'; };
            return '<style>' +
                '#jellycanvas-mock .osdHeader{position:fixed;top:0;left:0;right:0;height:7.5em;background:linear-gradient(180deg,rgba(16,16,16,.75),rgba(16,16,16,0));color:#eee;padding:.5em 1em;display:flex;align-items:center}' +
                '#jellycanvas-mock .videoOsdBottom{background:linear-gradient(0deg,rgba(16,16,16,.75),rgba(16,16,16,0));bottom:0;color:#fff;display:flex;justify-content:center;left:0;padding:2em 1em 1.75em;position:fixed;right:0;transition:opacity .3s ease-out}' +
                '#jellycanvas-mock .osdControls{flex-grow:1;padding:0 .8em;max-width:1400px}' +
                '#jellycanvas-mock .osdTextContainer{display:flex;align-items:center;margin-bottom:.7em;padding-left:.5em}' +
                '#jellycanvas-mock .osdTitle{margin:0 1em 0 0;font-size:1.4em}' +
                '#jellycanvas-mock .sliderContainer{position:relative}' +
                '#jellycanvas-mock .mdl-slider-container{display:flex;height:1.25em;position:relative}' +
                '#jellycanvas-mock .mdl-slider-background-flex-container{box-sizing:border-box;padding:0 .54em;position:absolute;top:50%;width:100%}' +
                '#jellycanvas-mock .mdl-slider-background-flex{background:hsla(0,0%,100%,.3);height:.2em;margin-top:-.1em;overflow:hidden;width:100%;display:flex}' +
                '#jellycanvas-mock .mdl-slider-background-flex-inner{position:relative;width:100%}' +
                '#jellycanvas-mock .mdl-slider-background-lower{background-color:#00a4dc;bottom:0;left:0;position:absolute;top:0;width:38%}' +
                '#jellycanvas-mock .mdl-slider{appearance:none;-webkit-appearance:none;background:transparent;width:100%;height:150%;margin:0;position:relative;z-index:1}' +
                '#jellycanvas-mock .mdl-slider::-webkit-slider-runnable-track{background:transparent}' +
                '#jellycanvas-mock .mdl-slider::-webkit-slider-thumb{-webkit-appearance:none;appearance:none;background:#00a4dc;border:none;border-radius:50%;height:1.08em;width:1.08em}' +
                '#jellycanvas-mock .buttons{display:flex;align-items:center;flex-wrap:wrap;padding:.25em 0 0}' +
                '#jellycanvas-mock .buttons .osdTimeText{margin:0 1em}' +
                '#jellycanvas-mock .paper-icon-button-light{background:transparent;border:0;color:inherit;width:2.9em;height:2.9em;margin:0 .29em;border-radius:50%;display:inline-flex;align-items:center;justify-content:center;font-size:1em;cursor:pointer}' +
                '#jellycanvas-mock .xlargePaperIconButton{font-size:2.2em}' +
                '#jellycanvas-mock .skip-button-container{bottom:8rem;left:0;pointer-events:none;position:fixed;right:0;z-index:10000;display:flex}' +
                '#jellycanvas-mock .skip-button{align-items:center;background-color:#303030;border:none;border-radius:.2em;color:hsla(0,0%,100%,.87);cursor:pointer;display:flex;font-size:1.2em;font-weight:700;gap:3px;margin-left:auto;margin-right:6rem;padding:12px 20px;pointer-events:auto;font-family:inherit}' +
                '</style>' +
                '<div class="jc-mock-video" style="position:fixed;top:0;right:0;bottom:0;left:0;background:#000 url(&quot;../Jellycanvas/Backdrop&quot;) center/cover;filter:brightness(.7);"></div>' +
                '<div class="skinHeader skinHeader-withBackground osdHeader"><button type="button" class="paper-icon-button-light headerButton headerBackButton"><span class="material-icons arrow_back" aria-hidden="true" style="font-size:1.6em"></span></button></div>' +
                '<div class="videoOsdBottom videoOsdBottom-maincontrols"><div class="osdControls">' +
                '<div class="osdTextContainer osdMainTextContainer"><h3 class="osdTitle">' + esc(t('mockEpisode')) + '</h3></div>' +
                '<div class="flex flex-direction-row align-items-center" style="display:flex;align-items:center">' +
                '<div class="osdTextContainer startTimeText osdPositionText" style="margin:0 .25em 0 0">17:42</div>' +
                '<div class="sliderContainer flex-grow" style="margin:.5em 0 .25em;flex-grow:1"><div class="mdl-slider-container"><div class="mdl-slider-background-flex-container"><div class="mdl-slider-background-flex"><div class="mdl-slider-background-flex-inner"><div class="mdl-slider-background-lower"></div></div></div></div><input type="range" min="0" max="100" value="38" class="mdl-slider osdPositionSlider" aria-label="position"></div></div>' +
                '<div class="osdTextContainer endTimeText osdDurationText" style="margin:0 0 0 .25em">46:10</div></div>' +
                '<div class="buttons focuscontainer-x"><div>' + btn('skip_previous', 'btnPreviousTrack') + btn('fast_rewind', 'btnRewind') + btn('pause', 'btnPause') + btn('fast_forward', 'btnFastForward') + btn('skip_next', 'btnNextTrack') + '</div>' +
                '<div class="osdTimeText"><span class="endsAtText">' + esc(t('mockEndsAt')) + '</span></div>' +
                '<div style="margin-left:auto;display:flex;align-items:center">' + btn('closed_caption', 'btnSubtitles') + btn('audiotrack', 'btnAudio') + btn('volume_up', 'buttonMute') + btn('settings', 'btnVideoOsdSettings') + btn('fullscreen', 'btnFullscreen') + '</div>' +
                '</div></div></div>' +
                '<div class="skip-button-container"><button is="emby-button" class="skip-button emby-button" type="button">' + esc(t('mockSkip')) + '<span class="material-icons skip_next" aria-hidden="true"></span></button></div>';
        }
        if (kind === 'upnext') {
            // The prompt's own stylesheet is part of the video player chunk,
            // which the home page never loads - so its rules come along
            // (copied from jellyfin-web's upNextDialog.scss; the theme's
            // !important rules win over them, exactly as in the player).
            return '<style>' +
                '#jellycanvas-mock .upNextContainer{background-color:rgba(0,0,0,.7);bottom:0;right:0;color:#fff;display:flex;flex-direction:row;padding:1em;position:fixed;width:30em;margin:0 2em 2em 0;user-select:none}' +
                '#jellycanvas-mock .upNextDialog-title{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;width:25.5em}' +
                '#jellycanvas-mock .upNextDialog-buttons{justify-content:end;width:29.75em}' +
                '#jellycanvas-mock .upNextDialog-button{background:#404040;color:#fff}' +
                '#jellycanvas-mock .upNextDialog-countdownText{font-weight:500;white-space:nowrap}' +
                '</style>' +
                '<div class="jc-mock-video" style="position:fixed;inset:0;background:#000 url(&quot;../Jellycanvas/Backdrop&quot;) center/cover;filter:brightness(.55);"></div>' +
                '<div class="upNextContainer upNextDialog">' +
                '<div class="flex flex-direction-column flex-grow">' +
                '<h2 class="upNextDialog-nextVideoText" style="margin:.25em 0;">' + esc(t('mockNext', '10')).replace('10', '<span class="upNextDialog-countdownText">10</span>') + '</h2>' +
                '<h3 class="upNextDialog-title" style="margin:.25em 0 .5em;">' + esc(t('mockEpisode')) + '</h3>' +
                '<div class="flex flex-direction-row upNextDialog-mediainfo"></div>' +
                '<div class="flex flex-direction-row upNextDialog-buttons" style="margin-top:1em;">' +
                '<button type="button" is="emby-button" class="raised raised-mini btnStartNow upNextDialog-button emby-button">' + esc(t('mockStartNow')) + '</button>' +
                '<button type="button" is="emby-button" class="raised raised-mini btnHide upNextDialog-button emby-button">' + esc(t('mockHide')) + '</button>' +
                '</div></div></div>';
        }
        return '<div class="dialogBackdrop dialogBackdropOpened"></div>' +
            '<div class="dialogContainer"><div class="focuscontainer dialog formDialog centeredDialog opened align-items-center justify-content-center dialog-fullscreen-lowres">' +
            '<div class="formDialogHeader formDialogHeader-clear justify-content-center"><h1 class="formDialogHeaderTitle hide" style="margin-top:.5em;padding:0 1em"></h1></div>' +
            '<div class="formDialogContent smoothScrollY no-grow" style="max-width:500px"><div class="dialogContentInner dialog-content-centered" style="padding-top:1em;padding-bottom:1em;text-align:center"><div class="text">' + esc(t('mockStill')) + '</div></div></div>' +
            '<div class="formDialogFooter formDialogFooter-clear formDialogFooter-flex" style="margin:1em">' +
            '<button is="emby-button" type="button" class="btnOption raised formDialogFooterItem formDialogFooterItem-autosize button-cancel emby-button">' + esc(t('mockStop')) + '</button>' +
            '<button is="emby-button" type="button" class="btnOption raised formDialogFooterItem formDialogFooterItem-autosize button-submit emby-button">' + esc(t('mockContinue')) + '</button>' +
            '</div></div></div>';
    }

    function injectMock() {
        var kind = page.querySelector('#jcPage').value;
        var doc;
        try {
            doc = frame.contentDocument;
        } catch (e) {
            return;
        }
        if (!doc || !doc.body) {
            return;
        }
        var old = doc.getElementById('jellycanvas-mock');
        if (!MOCK_KINDS[kind]) {
            if (old) {
                old.remove();
            }
            return;
        }
        if (old && old.getAttribute('data-kind') === kind) {
            return;
        }
        if (old) {
            old.remove();
        }
        var box = doc.createElement('div');
        box.id = 'jellycanvas-mock';
        box.setAttribute('data-kind', kind);
        // Above the bar (MUI AppBar sits at 1100) like the real player.
        box.style.cssText = 'position:fixed;inset:0;z-index:1300;';
        box.innerHTML = mockHtml(kind);
        doc.body.appendChild(box);
    }

    function selectDevice(d) {
        var changed = device !== d;
        device = d;
        page.querySelectorAll('.jc-device').forEach(function (b) {
            b.classList.toggle('jc-active', b.getAttribute('data-device') === d);
        });
        layoutStage();
        injectCss();
        if (changed && frame.src && frame.src !== 'about:blank') {
            // The TV layout is a different DOM (the legacy header), not just
            // a class - the client has to start in that layout.
            loadPreview();
        }
    }

    // ------------------------------------------------------------------
    // The client picks its layout at startup from localStorage["layout"]
    // (desktop / mobile / tv; missing = by device). The iframe shares this
    // page's localStorage, so the wanted layout is written just before the
    // preview loads and the admin's own value is put back as soon as the
    // client inside has started - the Dashboard around us keeps running
    // with the layout it started with.
    // ------------------------------------------------------------------
    var layoutBefore;
    var layoutRestoreTimer = null;

    function pushPreviewLayout() {
        try {
            if (layoutRestoreTimer === null) {
                layoutBefore = localStorage.getItem('layout');
            }
            localStorage.setItem('layout', device === 'tv' ? 'tv' : device === 'mobile' ? 'mobile' : 'desktop');
        } catch (e) {
            return;
        }
        clearTimeout(layoutRestoreTimer);
        // Safety net: put it back even if the frame never fires "load".
        layoutRestoreTimer = setTimeout(restorePreviewLayout, 6000);
    }

    function restorePreviewLayout() {
        if (layoutRestoreTimer === null) {
            return;
        }
        clearTimeout(layoutRestoreTimer);
        layoutRestoreTimer = null;
        try {
            if (layoutBefore === null || layoutBefore === undefined) {
                localStorage.removeItem('layout');
            } else {
                localStorage.setItem('layout', layoutBefore);
            }
        } catch (e) {
            // storage unavailable - nothing was written either
        }
    }

    // ------------------------------------------------------------------
    // Buttons: save, apply, remove, reset, logo.
    // ------------------------------------------------------------------

    /**
     * POST to /Jellycanvas/<path>. "expectJson" only where the server returns
     * something (Apply) - Save and Disable answer 204 with no body, and
     * parsing an empty response as JSON would fail.
     */
    function post(path, body, expectJson) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('Jellycanvas/' + path),
            data: body === undefined ? undefined : JSON.stringify(body),
            contentType: body === undefined ? undefined : 'application/json',
            dataType: expectJson ? 'json' : undefined
        });
    }

    function toast(msg) {
        if (window.Dashboard && Dashboard.alert) {
            Dashboard.alert(msg);
        } else {
            alert(msg);
        }
    }

    function fail(e) {
        console.error('Jellycanvas', e);
        var msg = e && e.message ? e.message : (e && e.statusText ? e.statusText : String(e));
        toast(t('failed', msg));
    }

    // Theme files on the server (Misc): listed for information; a repair
    // button when old ones are around.
    function renderThemeFiles(r) {
        var box = page.querySelector('#jcThemeFiles');
        box.textContent = '';
        if (!r.Themes.length) {
            box.textContent = t('themeNone', r.Path);
            page.querySelector('#jcThemeRepairRow').hidden = true;
            return;
        }
        var old = false;
        r.Themes.forEach(function (f) {
            var line = document.createElement('div');
            var label = f.Error ? t('themeRepairFailed', f.Error) : f.Repaired ? t('themePatched') : f.Current ? t('themeCurrent') : t('themeOld', f.Size);
            line.textContent = f.Name + ': ' + label;
            line.style.color = f.Error ? '#ff8a80' : f.Current ? '' : '#ffd166';
            box.appendChild(line);
            if (!f.Current && !f.Repaired) {
                old = true;
            }
        });
        page.querySelector('#jcThemeRepairRow').hidden = !old;
    }

    function refreshThemeFiles() {
        return ApiClient.getJSON(ApiClient.getUrl('Jellycanvas/Themes')).then(renderThemeFiles).catch(fail);
    }

    page.querySelector('#jcBtnThemeRepair').addEventListener('click', function () {
        post('Themes/Repair', undefined, true).then(function (r) {
            renderThemeFiles(r);
            toast(r.Themes.some(function (f) { return f.Error; }) ? t('themeRepairFailed', r.Themes.filter(function (f) { return f.Error; }).map(function (f) { return f.Name; }).join(', ')) : t('themeRepairDone'));
        }).catch(fail);
    });

    page.querySelector('#jcBtnSeerrTest').addEventListener('click', function () {
        var out = page.querySelector('#jcSeerrTestResult');
        out.textContent = '…';
        post('Seerr/Test', { Url: state.Seerr.Url, ApiKey: state.Seerr.ApiKey }, true).then(function (r) {
            out.textContent = r.Ok ? t('seerrTestOk', r.Message.replace(/^ok\s*/, '')) : t('seerrTestFail', r.Message);
            out.style.color = r.Ok ? '#7ed957' : '#ff8a80';
        }).catch(fail);
    });

    function refreshStatus() {
        return ApiClient.getJSON(ApiClient.getUrl('Jellycanvas/Status')).then(function (s) {
            status = s;
            var el = page.querySelector('#jcStatus');
            var msg = s.Enabled ? (s.PresentInBranding ? t('statusOn') : t('statusMissing')) : t('statusOff');
            if (s.ForeignCssLength > 0) {
                msg += ' ' + t('statusForeign', s.ForeignCssLength);
            }
            el.textContent = msg;
            el.classList.toggle('jc-status-on', s.Enabled && s.PresentInBranding);
            page.querySelector('#jcBtnDisable').style.display = s.Enabled || s.PresentInBranding ? '' : 'none';
            refreshScriptSection();
            // The first script preview may have arrived before the status did.
            injectScript();
            refreshThemeFiles();
        });
    }

    page.querySelector('#jcBtnApply').addEventListener('click', function () {
        // A strip the admin closed earlier on this browser shows again
        // after an Apply - otherwise a freshly enabled close button looks
        // like it hides the strip by itself.
        try {
            localStorage.removeItem('jellycanvas.infobar.dismissed');
        } catch (e) {
            // storage unavailable
        }
        Dashboard.showLoadingMsg();
        post('Apply', state, true).then(function (r) {
            state.Enabled = true;
            lastCss = r.Css;
            toast(t('applied'));
            // The client script is served from the saved settings, so the
            // toolbar buttons in the preview only update after a reload.
            loadPreview();
            return refreshStatus();
        }).catch(fail).finally(Dashboard.hideLoadingMsg);
    });

    page.querySelector('#jcBtnSave').addEventListener('click', function () {
        Dashboard.showLoadingMsg();
        post('Save', state).then(function () {
            toast(t('saved'));
            loadPreview();
        }).catch(fail).finally(Dashboard.hideLoadingMsg);
    });

    page.querySelector('#jcBtnDisable').addEventListener('click', function () {
        if (!confirm(t('confirmDisable'))) {
            return;
        }
        Dashboard.showLoadingMsg();
        post('Disable').then(function () {
            state.Enabled = false;
            toast(t('removed'));
            return refreshStatus();
        }).catch(fail).finally(Dashboard.hideLoadingMsg);
    });

    // ------------------------------------------------------------------
    // Share / import. The theme travels as the settings object itself; on
    // import only keys the current settings know are taken over (so a file
    // from another version, or a hand-edited one, cannot smuggle in junk),
    // and the server-side bits (Enabled, the uploaded logo) stay as they are.
    // ------------------------------------------------------------------
    // Only what a theme looks like travels. Anything that points at this
    // server or reads as its own stays home: the custom buttons (their
    // targets), the info bar text, the login title, the uploaded logo.
    // Image links go along only on a public https address. The keys are
    // removed rather than blanked, so an import keeps the importer's own.
    function exportJson() {
        var copy = JSON.parse(JSON.stringify(state));
        delete copy.Enabled;
        if (copy.Scripts) {
            delete copy.Scripts.ToolbarButtons;
        }
        if (copy.Seerr) {
            delete copy.Seerr.Url;
            delete copy.Seerr.ApiKey;
        }
        if (copy.InfoBar) {
            delete copy.InfoBar.Text;
        }
        if (copy.Login) {
            delete copy.Login.Title;
            if (!isPublicUrl(copy.Login.BackgroundUrl)) {
                delete copy.Login.BackgroundUrl;
            }
        }
        if (copy.Header) {
            delete copy.Header.LogoUrl;
        }
        if (copy.Backdrop && !isPublicUrl(copy.Backdrop.Url)) {
            delete copy.Backdrop.Url;
        }
        return JSON.stringify(copy, null, 2);
    }

    // https on a real host name - not localhost, a bare name, a private
    // address or a home-network suffix.
    function isPublicUrl(url) {
        var m = /^https:\/\/([^/:?#]+)/i.exec(url || '');
        if (!m) {
            return false;
        }
        var host = m[1].toLowerCase();
        return host !== 'localhost' && host.indexOf('.') >= 0 &&
            !/^(10\.|127\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.|\[)/.test(host) &&
            !/\.(local|lan|home|internal|localdomain)$/.test(host);
    }

    function mergeKnown(target, source) {
        Object.keys(target).forEach(function (key) {
            if (!(key in source)) {
                return;
            }
            var cur = target[key];
            var val = source[key];
            if (cur && typeof cur === 'object' && !Array.isArray(cur)) {
                if (val && typeof val === 'object' && !Array.isArray(val)) {
                    mergeKnown(cur, val);
                }
            } else if (Array.isArray(cur)) {
                if (Array.isArray(val)) {
                    target[key] = JSON.parse(JSON.stringify(val));
                }
            } else if (typeof val === typeof cur || val === null) {
                target[key] = val;
            }
        });
    }

    function importJson(text) {
        text = (text || '').trim();
        // A link instead of the JSON: fetch it, then import what it holds.
        // (A GitHub "blob" page link is turned into its raw file.)
        if (/^https?:\/\/\S+$/i.test(text)) {
            var url = text.replace(/^https:\/\/github\.com\/([^/]+\/[^/]+)\/blob\//i, 'https://raw.githubusercontent.com/$1/');
            fetch(url).then(function (r) {
                if (!r.ok) {
                    throw new Error(r.status);
                }
                return r.text();
            }).then(importJson).catch(function () { toast(t('importFetchFail')); });
            return;
        }
        var data;
        try {
            data = JSON.parse(text);
        } catch (e) {
            data = null;
        }
        if (!data || typeof data !== 'object' || Array.isArray(data) || !(data.Colors || data.Header || data.Cards)) {
            toast(t('importBad'));
            return;
        }
        var keep = { Enabled: state.Enabled, LogoUrl: state.Header.LogoUrl };
        mergeKnown(state, data);
        state.Enabled = keep.Enabled;
        if (!data.Header || !data.Header.LogoUrl) {
            state.Header.LogoUrl = keep.LogoUrl;
        }
        refreshControls();
        schedulePreview();
        scheduleScriptPreview();
        toast(t('importDone'));
    }

    function downloadJson() {
        var blob = new Blob([exportJson()], { type: 'application/json' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = 'jellycanvas-theme.json';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    }

    // The same export / import controls live in two places (Share and
    // Extra CSS); one file input serves both.
    function wireShare(suffix) {
        var textBox = page.querySelector('#jcImportText' + suffix);
        page.querySelector('#jcBtnExportCopy' + suffix).addEventListener('click', function () {
            var text = exportJson();
            if (navigator.clipboard) {
                navigator.clipboard.writeText(text).then(function () { toast(t('copied')); }, fail);
            } else {
                textBox.value = text;
                toast(t('copied'));
            }
        });
        page.querySelector('#jcBtnExportFile' + suffix).addEventListener('click', downloadJson);
        page.querySelector('#jcBtnImport' + suffix).addEventListener('click', function () {
            importJson(textBox.value);
        });
        page.querySelector('#jcBtnImportFile' + suffix).addEventListener('click', function () {
            var input = page.querySelector('#jcImportFileInput');
            input.jcTarget = textBox;
            input.click();
        });
    }

    wireShare('');

    page.querySelector('#jcImportFileInput').addEventListener('change', function () {
        var file = this.files && this.files[0];
        var target = this.jcTarget || page.querySelector('#jcImportText');
        this.value = '';
        if (!file) {
            return;
        }
        var reader = new FileReader();
        reader.onload = function () {
            target.value = reader.result;
            importJson(reader.result);
        };
        reader.readAsText(file);
    });

    page.querySelector('#jcBtnReset').addEventListener('click', function () {
        if (!confirm(t('confirmReset'))) {
            return;
        }
        var stock = presets.find(function (p) { return p.Id === 'jellyfin'; });
        if (stock) {
            loadPreset(stock.Settings);
        }
    });

    // Logo upload: the file goes to the server as multipart and the server
    // answers with the URL to store. ApiClient.ajax cannot be used here - it
    // turns an object in "data" into form-encoded text, which breaks
    // FormData - so plain fetch with the token header Jellyfin expects.
    var logoFile = page.querySelector('#jcLogoFile');
    var logoUploading = false;
    page.querySelector('#jcBtnLogoUpload').addEventListener('click', function () { logoFile.click(); });
    logoFile.addEventListener('change', function () {
        if (!logoFile.files || !logoFile.files[0] || logoUploading) {
            return;
        }
        logoUploading = true;
        var form = new FormData();
        form.append('file', logoFile.files[0]);
        Dashboard.showLoadingMsg();
        fetch(ApiClient.getUrl('Jellycanvas/Logo'), {
            method: 'POST',
            body: form,
            headers: { Authorization: 'MediaBrowser Token="' + ApiClient.accessToken() + '"' }
        }).then(function (res) {
            if (!res.ok) {
                return res.text().then(function (msg) { throw new Error(msg || res.statusText); });
            }
            return res.json();
        }).then(function (r) {
            state.Header.LogoUrl = r.Url;
            state.Header.Logo = 'Custom';
            refreshControls();
            schedulePreview();
            toast(t('logoUploaded'));
        }).catch(fail).finally(function () {
            Dashboard.hideLoadingMsg();
            logoFile.value = '';
            logoUploading = false;
        });
    });
    page.querySelector('#jcBtnLogoDelete').addEventListener('click', function () {
        ApiClient.ajax({ type: 'DELETE', url: ApiClient.getUrl('Jellycanvas/Logo') }).then(function () {
            if (state.Header.LogoUrl.indexOf('Jellycanvas/Logo') >= 0) {
                state.Header.LogoUrl = '';
                state.Header.Logo = 'Default';
                refreshControls();
                schedulePreview();
            }
            toast(t('logoDeleted'));
        }).catch(fail);
    });

    page.querySelectorAll('.jc-device').forEach(function (b) {
        b.addEventListener('click', function () { selectDevice(b.getAttribute('data-device')); });
    });
    var langSelect = page.querySelector('#jcLang');
    langSelect.value = lang;
    langSelect.addEventListener('change', function () { setLang(langSelect.value); });
    page.querySelector('#jcPage').addEventListener('change', loadPreview);
    page.querySelector('#jcBtnReload').addEventListener('click', loadPreview);

    if (window.ResizeObserver) {
        new ResizeObserver(layoutStage).observe(stage);
    } else {
        window.addEventListener('resize', layoutStage);
    }

    // ------------------------------------------------------------------
    // Start: translate, build the controls once, then on every show of the
    // page load the settings and presets and start the preview.
    // ------------------------------------------------------------------
    translate(page);
    buildSliders();
    buildColors();
    bindInputs(page);

    function init() {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(PLUGIN_ID),
            ApiClient.getJSON(ApiClient.getUrl('Jellycanvas/Presets'))
        ]).then(function (r) {
            state = r[0];
            presets = r[1];
            renderPresets();
            refreshControls();
            selectDevice('web');
            loadPreview();
            requestPreview();
            scheduleScriptPreview();
            return refreshStatus();
        }).catch(fail).finally(Dashboard.hideLoadingMsg);
    }

    page.addEventListener('pageshow', init);
})();
