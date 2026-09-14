/*
 * Jellycanvas client script.
 *
 * Served by the plugin at /Jellycanvas/Script.js and injected into the web
 * client's index.html through the File Transformation plugin (or pasted into
 * a JavaScript Injector plugin by hand). It carries the features that plain
 * CSS cannot do - today: custom buttons in the top bar.
 *
 * The configuration is baked in by the server where the placeholder below
 * stands. The toolbar buttons need no API at all and work before login;
 * the slideshow asks the server for items through the client's own
 * ApiClient (so it uses the signed-in user's token and permissions).
 *
 * Runs on every page load, on old TV browsers too: ES5 syntax only, no
 * optional chaining, no arrow functions, insertBefore instead of after().
 */
(function () {
    'use strict';

    var CONFIG = /*JELLYCANVAS_CONFIG*/{ "buttons": [], "slideshow": null, "infoBar": null, "badges": null };

    // Only one copy may run - the script can arrive twice when both File
    // Transformation and an injector plugin are installed. The global holds
    // a dispose() so the plugin page's preview can swap in a copy with the
    // settings being edited.
    if (window.__jellycanvasScript) {
        return;
    }
    window.__jellycanvasScript = { dispose: dispose };

    var buttons = (CONFIG.buttons || []).filter(function (b) { return b.enabled && b.url; });
    var slideshow = CONFIG.slideshow || null;
    var infoBar = CONFIG.infoBar || null;
    var badges = CONFIG.badges || null;
    var disposed = false;
    var observers = [];
    var timers = [];

    // Takes everything this copy put on the page back out, so a copy with
    // other settings can start clean. Element ids are spelled out because
    // the constants below are not assigned when the script bails out early.
    function dispose() {
        disposed = true;
        observers.forEach(function (o) { o.disconnect(); });
        timers.forEach(function (id) { clearInterval(id); });
        window.removeEventListener('popstate', checkUrl);
        window.removeEventListener('hashchange', checkUrl);
        window.removeEventListener('resize', onResize);
        document.removeEventListener('keydown', onKeyDown);
        buttons.forEach(function (b) {
            removeButton(b);
            removeDrawerItem(b);
        });
        updateScrollLock();
        ssStop();
        document.documentElement.classList.remove('jellycanvas-infobar-closed');
        badgesRemoveAll();
        ['jellycanvasSlideshow', 'jellycanvas-inject-style', 'jellycanvasInfoClose'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) {
                el.remove();
            }
        });
        delete window.__jellycanvasScript;
    }

    if (!buttons.length && !slideshow && !infoBar && !badges) {
        return;
    }

    // ------------------------------------------------------------------
    // Card badges: resolution / HDR / audio / subtitle languages in the
    // corners of movie and episode cards. The card markup carries only the
    // item id (data-id) and type (data-type); the media streams come from
    // the server in batches, once per item, and are cached for the session.
    // ------------------------------------------------------------------

    var BADGE_LANGS = {
        eng: 'EN', cze: 'CS', ces: 'CS', slo: 'SK', slk: 'SK', ger: 'DE', deu: 'DE', fre: 'FR', fra: 'FR', spa: 'ES', ita: 'IT',
        por: 'PT', rus: 'RU', pol: 'PL', hun: 'HU', dut: 'NL', nld: 'NL', jpn: 'JA', kor: 'KO', chi: 'ZH', zho: 'ZH', swe: 'SV',
        nor: 'NO', dan: 'DA', fin: 'FI', tur: 'TR', ara: 'AR', hin: 'HI', ukr: 'UK', ell: 'EL', gre: 'EL', rum: 'RO', ron: 'RO',
        bul: 'BG', hrv: 'HR', srp: 'SR', slv: 'SL', tha: 'TH', vie: 'VI', ind: 'ID', heb: 'HE', cat: 'CA', lit: 'LT', lav: 'LV', est: 'ET'
    };
    var BADGE_TYPES = /^(Movie|Episode|Video|MusicVideo|Trailer)$/;
    var badgeCache = {};
    var badgePending = {};
    var badgeQueue = [];
    var badgeTimer = null;

    function badgeCss() {
        if (!badges) {
            return '';
        }
        var look;
        switch (badges.style) {
            case 'Accent':
                look = 'background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, #fff);';
                break;
            case 'Glass':
                look = 'background: rgba(255, 255, 255, 0.18); color: #fff; backdrop-filter: blur(6px); -webkit-backdrop-filter: blur(6px); border: 1px solid rgba(255, 255, 255, 0.25);';
                break;
            default:
                look = 'background: rgba(0, 0, 0, 0.68); color: #fff;';
        }
        var size = (11 * badges.scale / 100).toFixed(1);
        return '.jellycanvas-badges { position: absolute; z-index: 3; display: flex; flex-wrap: wrap; gap: 3px; padding: 5px; max-width: 100%; box-sizing: border-box; pointer-events: none; font-size: ' + size + 'px; font-weight: 700; line-height: 1; }' +
            '.jellycanvas-badges-tl { top: 0; left: 0; } .jellycanvas-badges-tr { top: 0; right: 0; justify-content: flex-end; }' +
            '.jellycanvas-badges-bl { bottom: 0; left: 0; } .jellycanvas-badges-br { bottom: 0; right: 0; justify-content: flex-end; }' +
            '.jellycanvas-badge { display: inline-flex; align-items: center; gap: 3px; padding: 3px 6px; border-radius: 4px; letter-spacing: 0.02em; white-space: nowrap; ' + look + ' }' +
            '.jellycanvas-badge .material-icons { font-size: 1.15em; }';
    }

    function badgeLang(code) {
        var c = (code || '').toLowerCase();
        if (!c || c === 'und') {
            return '';
        }
        return BADGE_LANGS[c] || c.slice(0, 3).toUpperCase();
    }

    // What the badges say for one item; null when it has no video stream.
    function describeItem(item) {
        var streams = item.MediaStreams || (item.MediaSources && item.MediaSources[0] && item.MediaSources[0].MediaStreams) || [];
        var video = null;
        var audio = [];
        var subs = [];
        streams.forEach(function (st) {
            if (st.Type === 'Video' && !video) {
                video = st;
            } else if (st.Type === 'Audio') {
                var a = badgeLang(st.Language);
                if (a && audio.indexOf(a) < 0) {
                    audio.push(a);
                }
            } else if (st.Type === 'Subtitle') {
                var sName = badgeLang(st.Language);
                if (sName && subs.indexOf(sName) < 0) {
                    subs.push(sName);
                }
            }
        });
        if (!video) {
            return null;
        }
        var w = video.Width || 0;
        var h = video.Height || 0;
        var res = w >= 3800 || h >= 2100 ? '4K' : w >= 1900 || h >= 1000 ? '1080p' : w >= 1260 || h >= 700 ? '720p' : 'SD';
        var range = (video.VideoRangeType || video.VideoRange || '').toUpperCase();
        var hdr = /DOVI|DOLBY/.test(range) ? 'DV' : /HLG/.test(range) ? 'HLG' : /HDR/.test(range) ? 'HDR' : '';
        return { resolution: res, hdr: hdr, audio: audio.join(' '), subtitles: subs.join(' ') };
    }

    function badgeHtml(id, info) {
        var text = info[id];
        if (!text) {
            return '';
        }
        var icon = id === 'audio' ? 'volume_up' : id === 'subtitles' ? 'subtitles' : '';
        var el = document.createElement('span');
        el.className = 'jellycanvas-badge jellycanvas-badge-' + id;
        if (icon) {
            var i = document.createElement('span');
            i.className = 'material-icons';
            i.setAttribute('aria-hidden', 'true');
            i.textContent = icon;
            el.appendChild(i);
        }
        el.appendChild(document.createTextNode(text));
        return el;
    }

    function renderBadges(card, info) {
        card.setAttribute('data-jc-badges', info ? 'done' : 'none');
        if (!info) {
            return;
        }
        var host = card.querySelector('.cardScalable');
        if (!host) {
            return;
        }
        Object.keys(badges.corners).forEach(function (corner) {
            var ids = badges.corners[corner];
            var box = null;
            ids.forEach(function (id) {
                var el = badgeHtml(id, info);
                if (!el) {
                    return;
                }
                if (!box) {
                    box = document.createElement('div');
                    box.className = 'jellycanvas-badges jellycanvas-badges-' + corner;
                }
                box.appendChild(el);
            });
            if (box) {
                host.appendChild(box);
            }
        });
    }

    function badgesRemoveAll() {
        var all = document.querySelectorAll('.jellycanvas-badges');
        for (var i = 0; i < all.length; i++) {
            all[i].remove();
        }
        var cards = document.querySelectorAll('[data-jc-badges]');
        for (var j = 0; j < cards.length; j++) {
            cards[j].removeAttribute('data-jc-badges');
        }
    }

    function badgeFetch() {
        badgeTimer = null;
        var api = window.ApiClient;
        if (!api || !badgeQueue.length) {
            return;
        }
        var chunk = badgeQueue.splice(0, 60);
        api.getItems(api.getCurrentUserId(), { Ids: chunk.join(','), Fields: 'MediaStreams', EnableImages: false, EnableUserData: false })
            .then(function (result) {
                (result.Items || []).forEach(function (item) {
                    badgeCache[item.Id] = describeItem(item);
                });
            })
            .catch(function () { /* offline or forbidden - the cards just stay bare */ })
            .then(function () {
                chunk.forEach(function (id) {
                    delete badgePending[id];
                    if (!badgeCache.hasOwnProperty(id)) {
                        badgeCache[id] = null;
                    }
                });
                if (badgeQueue.length) {
                    badgeTimer = setTimeout(badgeFetch, 50);
                }
                badgeSync();
            });
    }

    function badgeSync() {
        if (!badges || disposed) {
            return;
        }
        if (badges.hideOnMobile && isMobile()) {
            return;
        }
        var cards = document.querySelectorAll('.card[data-id]:not([data-jc-badges])');
        var queued = false;
        for (var i = 0; i < cards.length; i++) {
            var card = cards[i];
            var type = card.getAttribute('data-type');
            if ((type && !BADGE_TYPES.test(type)) || card.closest('.personCard, .mainDrawer')) {
                card.setAttribute('data-jc-badges', 'skip');
                continue;
            }
            var id = card.getAttribute('data-id');
            if (badgeCache.hasOwnProperty(id)) {
                renderBadges(card, badgeCache[id]);
            } else if (!badgePending[id]) {
                badgePending[id] = true;
                badgeQueue.push(id);
                queued = true;
            }
        }
        if (queued && !badgeTimer) {
            badgeTimer = setTimeout(badgeFetch, 120);
        }
    }

    // ------------------------------------------------------------------
    // Info bar close button. The strip itself is a CSS pseudo-element (so
    // it works without any script); the script only adds an "x" over its
    // right end and, once clicked, marks <html> so the theme hides the
    // strip and drops the space it took. The dismissal is remembered per
    // browser until the text changes.
    // ------------------------------------------------------------------

    var INFO_KEY = 'jellycanvas.infobar.dismissed';
    var INFO_CLOSED = 'jellycanvas-infobar-closed';

    function infoDismissed() {
        try {
            return localStorage.getItem(INFO_KEY) === infoBar.text;
        } catch (e) {
            return false;
        }
    }

    function closeInfoBar() {
        try {
            localStorage.setItem(INFO_KEY, infoBar.text);
        } catch (e) {
            // private mode - closes for this page load only
        }
        document.documentElement.classList.add(INFO_CLOSED);
        var btn = document.getElementById('jellycanvasInfoClose');
        if (btn) {
            btn.remove();
        }
    }

    // The strip is one of: the header's ::after (top bar layouts, in the
    // flow under the bar), main::before (next to a sidebar, fixed) or
    // main::after (bottom edge, fixed). Whichever has content is the one.
    function infoBarBox() {
        var header = document.querySelector('header.MuiAppBar-root');
        var main = document.querySelector('header.MuiAppBar-root ~ main');
        var candidates = [
            { el: header, pseudo: '::after' },
            { el: main, pseudo: '::before' },
            { el: main, pseudo: '::after' }
        ];
        for (var i = 0; i < candidates.length; i++) {
            var c = candidates[i];
            if (!c.el) {
                continue;
            }
            var cs = getComputedStyle(c.el, c.pseudo);
            if (cs.content === 'none' || cs.content === 'normal' || cs.display === 'none') {
                continue;
            }
            var height = parseFloat(cs.height) || 0;
            if (height <= 0) {
                continue;
            }
            var box = { height: height, color: cs.color, right: parseFloat(cs.marginRight) || 0 };
            if (cs.position === 'fixed') {
                box.right += parseFloat(cs.right) || 0;
                box.top = cs.bottom !== 'auto' && cs.top === 'auto'
                    ? window.innerHeight - (parseFloat(cs.bottom) || 0) - height
                    : parseFloat(cs.top) || 0;
            } else {
                // In the flow at the bottom of the header, above its bottom margin.
                box.top = c.el.getBoundingClientRect().bottom - height - (parseFloat(cs.marginBottom) || 0);
            }
            return box;
        }
        return null;
    }

    function syncInfoBar() {
        if (!infoBar) {
            return;
        }
        var root = document.documentElement;
        if (infoDismissed()) {
            root.classList.add(INFO_CLOSED);
            return;
        }
        var box = infoBarBox();
        var btn = document.getElementById('jellycanvasInfoClose');
        if (!box) {
            if (btn) {
                btn.remove();
            }
            return;
        }
        if (!btn) {
            btn = document.createElement('button');
            btn.id = 'jellycanvasInfoClose';
            btn.type = 'button';
            btn.setAttribute('aria-label', 'Close');
            btn.setAttribute('data-jellycanvas', '1');
            btn.innerHTML = '<span class="material-icons" aria-hidden="true">close</span>';
            btn.addEventListener('click', closeInfoBar);
            document.body.appendChild(btn);
        }
        var size = Math.max(20, Math.min(32, box.height - 8));
        btn.style.cssText = 'position:fixed;z-index:1201;display:flex;align-items:center;justify-content:center;' +
            'top:' + Math.round(box.top + (box.height - size) / 2) + 'px;right:' + Math.round(box.right + 6) + 'px;' +
            'width:' + size + 'px;height:' + size + 'px;padding:0;border:0;border-radius:50%;background:transparent;' +
            'color:' + box.color + ';cursor:pointer;font-size:' + Math.round(size * 0.7) + 'px;';
        btn.firstChild.style.fontSize = 'inherit';
    }

    // ------------------------------------------------------------------
    // TV detection. Checked continuously, not just at start: jellyfin-web
    // applies the layout after scripts load and it can change at runtime.
    // ------------------------------------------------------------------

    var TV_UA = /web0s|webos|netcast|tizen|smart-?tv|hbbtv|googletv|android tv|crkey|aft[bmnst]|bravia|viera|vidaa|philipstv|roku|appletv|xbox|playstation|nintendo/
        .test((navigator.userAgent || '').toLowerCase());

    function isTv() {
        var root = document.documentElement;
        if (root && root.classList.contains('layout-tv')) {
            return true;
        }
        try {
            for (var i = 0; i < localStorage.length; i++) {
                var k = localStorage.key(i);
                if (k && /layout$/i.test(k) && (localStorage.getItem(k) || '').toLowerCase() === 'tv') {
                    return true;
                }
            }
        } catch (e) {
            // localStorage unavailable
        }
        return TV_UA;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    function visible(el) {
        if (!el || el.offsetParent === null) {
            return false;
        }
        var s = getComputedStyle(el);
        return s.display !== 'none' && s.visibility !== 'hidden';
    }

    function findToolbar() {
        var all = document.querySelectorAll('header.MuiAppBar-root .MuiToolbar-root');
        for (var i = 0; i < all.length; i++) {
            if (visible(all[i])) {
                return all[i];
            }
        }
        return null;
    }

    function iconGroup(bar) {
        // The icon group is the .MuiBox-root that holds the icon buttons
        // (SyncPlay, Cast, Search); the user-menu box is a separate one.
        var boxes = bar.querySelectorAll(':scope > .MuiBox-root');
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].querySelector('[aria-controls="app-sync-play-menu"], [aria-controls="app-remote-play-menu"], a[href^="#/search"]')) {
                return boxes[i];
            }
        }
        return boxes[0] || null;
    }

    function navGroup(bar) {
        return bar.querySelector(':scope > .MuiStack-root');
    }

    function templateIconButton(bar) {
        var c = bar.querySelectorAll('.MuiIconButton-root');
        for (var i = 0; i < c.length; i++) {
            if (visible(c[i]) && !c[i].hasAttribute('data-jellycanvas')) {
                return c[i];
            }
        }
        return null;
    }

    function templateNavLink(bar) {
        return bar.querySelector('.MuiStack-root > a.MuiButton-sizeMedium:not([data-jellycanvas])');
    }

    // Where the overlay may go: under a top bar, or to the right of a
    // sidebar (a bar taller than it is wide, hugging the left edge). The
    // toolbar is measured rather than the <header>, whose old .skinHeader
    // twin has no height.
    function overlayArea() {
        var bar = findToolbar();
        var rect = bar ? bar.getBoundingClientRect() : null;
        if (!rect || rect.width <= 0 || rect.height <= 0) {
            return { top: 64, left: 0 };
        }
        var sidebar = rect.height > rect.width && rect.right < window.innerWidth / 2;
        if (!sidebar) {
            return { top: Math.round(rect.bottom), left: 0 };
        }
        // A collapsible sidebar is slid out while its button is clicked;
        // the theme publishes the resting edge so the overlay does not
        // keep the gap after the bar folds back.
        var edge = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--jellycanvas-sidebar-edge'));
        return { top: 0, left: edge > 0 ? Math.round(edge) : Math.round(rect.right) };
    }

    // ------------------------------------------------------------------
    // Overlay iframe (one per button). The iframe is a replaced element:
    // with height:auto it takes its own intrinsic height and ignores
    // bottom:0, so the size is computed by hand.
    // ------------------------------------------------------------------

    function frameId(b) {
        return 'jellycanvasFrame-' + b.id;
    }

    function isOpen(b) {
        var f = document.getElementById(frameId(b));
        return !!f && f.style.display !== 'none';
    }

    function applyFrameGeometry(f) {
        var area = overlayArea();
        f.style.cssText = 'position:fixed;left:' + area.left + 'px;top:' + area.top + 'px;' +
            'width:calc(100% - ' + area.left + 'px);height:calc(100% - ' + area.top + 'px);' +
            'border:0;z-index:1100;background:#101010;display:block;';
    }

    // While an overlay is open the page under it must not scroll: a wheel
    // that reaches the end of the iframe's content would otherwise carry
    // on into the page (scroll chaining). Locking the document's overflow
    // is the one thing that stops it across browsers.
    var savedOverflow = null;

    function updateScrollLock() {
        var anyOpen = buttons.some(isOpen);
        var root = document.documentElement;
        if (anyOpen && savedOverflow === null) {
            savedOverflow = root.style.overflow;
            root.style.overflow = 'hidden';
        } else if (!anyOpen && savedOverflow !== null) {
            root.style.overflow = savedOverflow;
            savedOverflow = null;
        }
    }

    function openFrame(b) {
        var f = document.getElementById(frameId(b));
        if (!f) {
            f = document.createElement('iframe');
            f.id = frameId(b);
            f.src = b.url;
            f.setAttribute('title', b.label);
            document.body.appendChild(f);
        }
        applyFrameGeometry(f);
        updateScrollLock();
    }

    function closeFrame(b) {
        var f = document.getElementById(frameId(b));
        if (!f) {
            return;
        }
        if (b.keepAlive) {
            f.style.display = 'none';
        } else {
            f.remove();
        }
        updateScrollLock();
    }

    function onResize() {
        buttons.forEach(function (b) {
            if (isOpen(b)) {
                applyFrameGeometry(document.getElementById(frameId(b)));
            }
        });
    }

    function closeAll() {
        buttons.forEach(function (b) {
            if (isOpen(b)) {
                closeFrame(b);
            }
        });
    }

    function onClick(b, event) {
        if (b.action === 'Overlay') {
            event.preventDefault();
            event.stopPropagation();
            if (event.detail !== 0 && event.currentTarget) {
                event.currentTarget.blur();
            }
            var open = isOpen(b);
            closeAll();
            if (!open) {
                openFrame(b);
            }
        } else if (b.action === 'NewTab') {
            event.preventDefault();
            window.open(b.url, '_blank', 'noopener');
        }
        // Navigate: the <a href> does it by itself.
    }

    // ------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------

    function buttonId(b) {
        return 'jellycanvasBtn-' + b.id;
    }

    function drawerItemId(b) {
        return 'jellycanvasDrawer-' + b.id;
    }

    function isMobile() {
        return document.documentElement.classList.contains('layout-mobile');
    }

    // ------------------------------------------------------------------
    // Phone layout: the navigation links live in the hamburger drawer, so
    // a "Nav" button goes there too. Jellyfin 12 renders the drawer as a
    // MUI Drawer only while it is open; the entry is added each time it
    // appears (the mutation observer calls sync) and disappears with it.
    // The legacy .customMenuOptions block is used when present instead.
    // ------------------------------------------------------------------

    function closeDrawer() {
        var backdrop = document.querySelector('.MuiDrawer-root .MuiBackdrop-root, .tmla-mask.backdrop');
        if (backdrop) {
            backdrop.click();
            return;
        }
        var drawer = document.querySelector('.mainDrawer.drawer-open');
        if (drawer) {
            drawer.classList.remove('drawer-open');
        }
    }

    function removeDrawerItem(b) {
        var el = document.getElementById(drawerItemId(b));
        if (el) {
            el.remove();
        }
    }

    // The first list of the MUI drawer (server name, Home, Favorites) -
    // our entry goes after its last link, next to the navigation.
    function findDrawerList() {
        var list = document.querySelector('.MuiDrawer-paper ul.MuiList-root');
        return list && list.querySelector('li a[href]') ? list : null;
    }

    function createMuiDrawerItem(b, list) {
        // Borrow the markup of an existing entry so the look (padding, hover,
        // icon column) is exactly Jellyfin's, whatever its class names are.
        var items = list.querySelectorAll('li');
        var template = items[items.length - 1];
        var li = template.cloneNode(true);
        li.id = drawerItemId(b);
        li.setAttribute('data-jellycanvas', '1');
        var a = li.querySelector('a');
        a.classList.remove('Mui-selected');
        a.removeAttribute('aria-current');
        a.href = b.action === 'Navigate' ? b.url : '#';
        if (b.action === 'NewTab') {
            a.target = '_blank';
            a.rel = 'noopener';
        }
        var iconHost = a.querySelector('.MuiListItemIcon-root');
        if (iconHost) {
            iconHost.innerHTML = '<span class="material-icons" aria-hidden="true"></span>';
            iconHost.firstChild.textContent = b.icon;
        }
        var textHost = a.querySelector('.MuiListItemText-root');
        if (textHost) {
            var span = textHost.querySelector('span, h6') || textHost;
            textHost.innerHTML = '';
            span.textContent = b.label;
            textHost.appendChild(span);
        }
        a.addEventListener('click', function (event) {
            closeDrawer();
            onClick(b, event);
        });
        return li;
    }

    function createLegacyDrawerItem(b) {
        var el = document.createElement('a');
        el.id = drawerItemId(b);
        el.className = 'navMenuOption lnkMediaFolder emby-button';
        el.setAttribute('data-jellycanvas', '1');
        el.href = b.action === 'Navigate' ? b.url : '#';
        if (b.action === 'NewTab') {
            el.target = '_blank';
            el.rel = 'noopener';
        }
        el.innerHTML = '<span class="material-icons navMenuOptionIcon" aria-hidden="true"></span><span class="navMenuOptionText"></span>';
        el.querySelector('.navMenuOptionIcon').textContent = b.icon;
        el.querySelector('.navMenuOptionText').textContent = b.label;
        el.addEventListener('click', function (event) {
            closeDrawer();
            onClick(b, event);
        });
        return el;
    }

    function syncDrawerItem(b) {
        var list = findDrawerList();
        var host = list || document.querySelector('.mainDrawer .customMenuOptions');
        if (!host) {
            return;
        }
        var el = document.getElementById(drawerItemId(b));
        if (el) {
            if (el.parentElement !== host) {
                host.appendChild(el);
            }
            return;
        }
        host.appendChild(list ? createMuiDrawerItem(b, list) : createLegacyDrawerItem(b));
    }

    function removeButton(b) {
        var el = document.getElementById(buttonId(b));
        if (el) {
            el.remove();
        }
        var f = document.getElementById(frameId(b));
        if (f) {
            f.remove();
        }
    }

    function createButton(b, bar) {
        var el = document.createElement('a');
        el.id = buttonId(b);
        el.setAttribute('data-jellycanvas', '1');
        el.setAttribute('aria-label', b.label);
        el.title = b.label;
        el.href = b.action === 'Navigate' ? b.url : '#';
        if (b.action === 'NewTab') {
            el.target = '_blank';
            el.rel = 'noopener';
        }

        if (b.placement === 'Nav') {
            // Same structure as Jellyfin's own links (icon in a
            // MuiButton-startIcon wrapper), so themes that style or collapse
            // the links treat ours the same way.
            el.innerHTML = '<span class="MuiButton-icon MuiButton-startIcon"><span class="material-icons" aria-hidden="true" style="font-size:1.25em">' + b.icon + '</span></span>' + b.label;
        } else {
            el.innerHTML = '<span class="material-icons" aria-hidden="true">' + b.icon + '</span>';
        }

        applyLook(el, b, bar);
        el.addEventListener('click', function (event) { onClick(b, event); });
        return el;
    }

    // Borrow size, padding and hover from a neighbouring button so ours
    // matches whatever theme is active. The neighbours may not exist yet
    // when the button is first created (the toolbar renders in steps), so
    // this runs on every sync until a template turns up.
    function applyLook(el, b, bar) {
        if (el.getAttribute('data-jellycanvas-look') === 'template') {
            return;
        }
        var tpl = b.placement === 'Nav' ? templateNavLink(bar) : templateIconButton(bar);
        if (tpl) {
            el.className = tpl.className;
            el.style.cssText = '';
            el.setAttribute('data-jellycanvas-look', 'template');
        } else if (!el.getAttribute('data-jellycanvas-look')) {
            el.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:48px;height:48px;padding:0;background:transparent;border:0;border-radius:50%;color:inherit;cursor:pointer;';
            el.setAttribute('data-jellycanvas-look', 'fallback');
        }
        el.style.setProperty('flex', '0 0 auto', 'important');
        el.style.setProperty('text-decoration', 'none', 'important');
    }

    // React moves foreign nodes to the end when it re-renders the toolbar,
    // so the position is checked, not just set once.
    function placeButton(el, b, bar) {
        var group = b.placement === 'Nav' ? navGroup(bar) : iconGroup(bar);
        if (!group) {
            if (el.parentElement !== bar) {
                bar.appendChild(el);
            }
            return;
        }

        if (b.placement === 'Nav') {
            if (el.parentElement !== group || el !== group.lastElementChild) {
                group.appendChild(el);
            }
            return;
        }

        // Icons: right before Search when it exists, otherwise at the end.
        // (Library pages link to "#/search?parentId=...", hence the prefix match.)
        var search = group.querySelector('a[href^="#/search"]');
        var correct = search ? el.nextElementSibling === search && el.parentElement === group : el.parentElement === group && el === group.lastElementChild;
        if (!correct) {
            group.insertBefore(el, search || null);
        }
    }

    // The single place that decides whether and where each button is.
    // The admin dashboard has a toolbar of its own; user-facing buttons do
    // not belong there.
    // Login, server selection and password reset all live outside the
    // signed-in app; buttons stay off them unless a button opts in.
    function isBeforeLogin() {
        var h = location.hash || '';
        if (/^#\/(login|selectserver|forgotpassword|forgotpasswordpin|wizard)/i.test(h)) {
            return true;
        }
        var login = document.getElementById('loginPage');
        return !!login && !login.classList.contains('hide');
    }

    function sync() {
        if (disposed) {
            return;
        }
        ssSync();
        syncInfoBar();
        badgeSync();
        var tv = isTv();
        var mobile = isMobile();
        var dashboard = document.body && document.body.classList.contains('dashboardDocument');
        var beforeLogin = isBeforeLogin();
        var bar = dashboard ? null : findToolbar();
        buttons.forEach(function (b) {
            if ((b.hideOnTv && tv) || dashboard || (beforeLogin && !b.showBeforeLogin)) {
                removeButton(b);
                removeDrawerItem(b);
                return;
            }
            if (b.placement === 'Nav' && mobile) {
                // The links are in the drawer on phones; follow them there.
                removeButton(b);
                syncDrawerItem(b);
                return;
            }
            removeDrawerItem(b);
            if (!bar) {
                return;
            }
            var el = document.getElementById(buttonId(b));
            if (!el) {
                el = createButton(b, bar);
            } else {
                applyLook(el, b, bar);
            }
            placeButton(el, b, bar);
        });
    }

    // ------------------------------------------------------------------
    // Home-page slideshow. Inserted as the first block of the home tab
    // (#homeTab .homeSectionsContainer). Items come from the server through
    // the client's ApiClient; images are plain <img> URLs. Slides cross-fade
    // on a timer, pause while hovered, and the dots switch by hand.
    // ------------------------------------------------------------------

    var SS_ID = 'jellycanvasSlideshow';
    var ssItems = null;
    var ssLoading = false;
    var ssIndex = 0;
    var ssTimer = null;

    function ssApi() {
        return window.ApiClient && typeof window.ApiClient.getCurrentUserId === 'function' && window.ApiClient.getCurrentUserId() ? window.ApiClient : null;
    }

    function ssContainer() {
        var home = document.querySelector('#indexPage:not(.hide) #homeTab .homeSectionsContainer');
        return home || null;
    }

    function ssWanted() {
        if (!slideshow) {
            return false;
        }
        if (slideshow.hideOnTv && isTv()) {
            return false;
        }
        if (slideshow.hideOnMobile && document.documentElement.classList.contains('layout-mobile')) {
            return false;
        }
        return !!ssContainer() && !!ssApi();
    }

    function ssQuery(api) {
        var userId = api.getCurrentUserId();
        var types = slideshow.types === 'Movies' ? 'Movie' : slideshow.types === 'Series' ? 'Series' : 'Movie,Series';
        var q = {
            IncludeItemTypes: types,
            Recursive: true,
            Limit: slideshow.count,
            Fields: 'Overview,ProductionYear',
            ImageTypes: 'Backdrop',
            EnableImageTypes: 'Backdrop,Logo,Primary',
            SortBy: 'Random'
        };
        switch (slideshow.source) {
            case 'Latest':
                q.SortBy = 'DateCreated';
                q.SortOrder = 'Descending';
                break;
            case 'Favorites':
                q.Filters = 'IsFavorite';
                break;
            case 'Genre':
                q.Genres = slideshow.filter;
                break;
            case 'Tag':
                q.Tags = slideshow.filter;
                break;
            case 'ContinueWatching':
                q.Filters = 'IsResumable';
                q.SortBy = 'DatePlayed';
                q.SortOrder = 'Descending';
                q.IncludeItemTypes = 'Movie,Episode';
                break;
        }
        return api.getItems(userId, q);
    }

    function ssLoad() {
        if (ssItems || ssLoading) {
            return;
        }
        var api = ssApi();
        if (!api) {
            return;
        }
        ssLoading = true;
        ssQuery(api).then(function (r) {
            ssItems = (r && r.Items || []).filter(function (i) { return i.BackdropImageTags && i.BackdropImageTags.length || i.ParentBackdropImageTags && i.ParentBackdropImageTags.length; });
            ssLoading = false;
            ssRender();
        }, function () {
            ssLoading = false;
        });
    }

    function ssImage(api, item, type) {
        // Episodes borrow the series' backdrop and logo.
        if (type === 'Backdrop') {
            if (item.BackdropImageTags && item.BackdropImageTags.length) {
                return api.getImageUrl(item.Id, { type: 'Backdrop', index: 0, maxWidth: 1920, tag: item.BackdropImageTags[0] });
            }
            if (item.ParentBackdropItemId && item.ParentBackdropImageTags && item.ParentBackdropImageTags.length) {
                return api.getImageUrl(item.ParentBackdropItemId, { type: 'Backdrop', index: 0, maxWidth: 1920, tag: item.ParentBackdropImageTags[0] });
            }
            return null;
        }
        if (item.ImageTags && item.ImageTags.Logo) {
            return api.getImageUrl(item.Id, { type: 'Logo', maxWidth: 600, tag: item.ImageTags.Logo });
        }
        if (item.ParentLogoItemId && item.ParentLogoImageTag) {
            return api.getImageUrl(item.ParentLogoItemId, { type: 'Logo', maxWidth: 600, tag: item.ParentLogoImageTag });
        }
        return null;
    }

    function ssTitle(item) {
        if (item.Type === 'Episode' && item.SeriesName) {
            return item.SeriesName;
        }
        return item.Name || '';
    }

    function ssSubtitle(item) {
        if (item.Type === 'Episode') {
            var parts = [];
            if (item.ParentIndexNumber != null && item.IndexNumber != null) {
                parts.push('S' + item.ParentIndexNumber + ':E' + item.IndexNumber);
            }
            if (item.Name) {
                parts.push(item.Name);
            }
            return parts.join(' - ');
        }
        return item.ProductionYear ? String(item.ProductionYear) : '';
    }

    function ssStyle() {
        if (document.getElementById(SS_ID + '-style')) {
            return;
        }
        var style = document.createElement('style');
        style.id = SS_ID + '-style';
        style.textContent =
            '#' + SS_ID + ' { position: relative; height: ' + slideshow.height + 'vh; min-height: 260px; margin: 0 0 1.5em; border-radius: var(--jf-card-borderRadius, 0.2em); overflow: hidden; background: #000; contain: layout paint; }' +
            '#' + SS_ID + ' .jcs-slide { position: absolute; inset: 0; opacity: 0; transition: opacity 0.9s ease; pointer-events: none; }' +
            '#' + SS_ID + ' .jcs-slide.is-active { opacity: 1; pointer-events: auto; }' +
            '#' + SS_ID + ' .jcs-bg { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }' +
            '#' + SS_ID + ' .jcs-shade { position: absolute; inset: 0; background: linear-gradient(90deg, rgba(0,0,0,0.82) 0%, rgba(0,0,0,0.45) 45%, rgba(0,0,0,0) 75%), linear-gradient(0deg, rgba(0,0,0,0.75) 0%, rgba(0,0,0,0) 40%); }' +
            '#' + SS_ID + ' .jcs-text { position: absolute; left: 4%; right: 30%; bottom: 10%; color: #fff; text-shadow: 0 2px 8px rgba(0,0,0,0.6); }' +
            '#' + SS_ID + ' .jcs-logo { max-width: min(420px, 45%); max-height: 22%; margin-bottom: 0.8em; display: block; }' +
            '#' + SS_ID + ' .jcs-title { font-size: 2.2em; font-weight: 700; margin: 0 0 0.2em; line-height: 1.1; }' +
            '#' + SS_ID + ' .jcs-sub { opacity: 0.85; margin: 0 0 0.6em; font-size: 1em; }' +
            '#' + SS_ID + ' .jcs-overview { font-size: 1em; line-height: 1.45; max-height: 4.4em; overflow: hidden; margin: 0 0 1em; opacity: 0.92; }' +
            '#' + SS_ID + ' .jcs-btn { display: inline-flex; align-items: center; gap: 0.4em; padding: 0.6em 1.2em; border-radius: var(--jf-card-borderRadius, 0.2em); background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, #000); font-weight: 600; text-decoration: none; }' +
            '#' + SS_ID + ' .jcs-btn:hover { filter: brightness(1.1); }' +
            '#' + SS_ID + ' .jcs-dots { position: absolute; right: 2%; bottom: 6%; display: flex; gap: 6px; z-index: 2; }' +
            '#' + SS_ID + ' .jcs-dot { width: 10px; height: 10px; border-radius: 50%; background: rgba(255,255,255,0.4); border: 0; padding: 0; cursor: pointer; }' +
            '#' + SS_ID + ' .jcs-dot.is-active { background: var(--jf-palette-primary-main, #00a4dc); }' +
            '.layout-mobile #' + SS_ID + ' .jcs-text { right: 4%; bottom: 14%; } .layout-mobile #' + SS_ID + ' .jcs-title { font-size: 1.5em; } .layout-mobile #' + SS_ID + ' .jcs-overview { display: none; }';
        document.head.appendChild(style);
    }

    function ssRender() {
        var container = ssContainer();
        var api = ssApi();
        if (!container || !api || !ssItems) {
            return;
        }
        var root = document.getElementById(SS_ID);
        if (root && root.parentElement === container && container.firstElementChild === root) {
            return;
        }
        if (root) {
            root.remove();
        }
        if (!ssItems.length) {
            return;
        }
        ssStyle();
        root = document.createElement('div');
        root.id = SS_ID;
        root.setAttribute('data-jellycanvas', '1');
        ssItems.forEach(function (item, i) {
            var slide = document.createElement('div');
            slide.className = 'jcs-slide' + (i === 0 ? ' is-active' : '');
            var bg = document.createElement('img');
            bg.className = 'jcs-bg';
            bg.alt = '';
            bg.src = ssImage(api, item, 'Backdrop');
            bg.loading = i === 0 ? 'eager' : 'lazy';
            slide.appendChild(bg);
            var shade = document.createElement('div');
            shade.className = 'jcs-shade';
            slide.appendChild(shade);
            var text = document.createElement('div');
            text.className = 'jcs-text';
            var logoUrl = slideshow.showLogo ? ssImage(api, item, 'Logo') : null;
            if (logoUrl) {
                var logo = document.createElement('img');
                logo.className = 'jcs-logo';
                logo.alt = ssTitle(item);
                logo.src = logoUrl;
                text.appendChild(logo);
            } else {
                var title = document.createElement('h2');
                title.className = 'jcs-title';
                title.textContent = ssTitle(item);
                text.appendChild(title);
            }
            var sub = ssSubtitle(item);
            if (sub) {
                var subEl = document.createElement('p');
                subEl.className = 'jcs-sub';
                subEl.textContent = sub;
                text.appendChild(subEl);
            }
            if (slideshow.showOverview && item.Overview) {
                var ov = document.createElement('p');
                ov.className = 'jcs-overview';
                ov.textContent = item.Overview;
                text.appendChild(ov);
            }
            var href = '#/details?id=' + item.Id + '&serverId=' + (item.ServerId || api.serverId());
            if (slideshow.showButton) {
                var btn = document.createElement('a');
                btn.className = 'jcs-btn';
                btn.href = href;
                btn.innerHTML = '<span class="material-icons" aria-hidden="true">info</span>';
                btn.appendChild(document.createTextNode(item.Type === 'Episode' ? (item.Name || '') : ssTitle(item)));
                text.appendChild(btn);
            }
            slide.appendChild(text);
            slide.addEventListener('click', function (e) {
                if (!e.target.closest('a')) {
                    location.hash = href;
                }
            });
            root.appendChild(slide);
        });
        var dots = document.createElement('div');
        dots.className = 'jcs-dots';
        ssItems.forEach(function (item, i) {
            var d = document.createElement('button');
            d.type = 'button';
            d.className = 'jcs-dot' + (i === 0 ? ' is-active' : '');
            d.setAttribute('aria-label', String(i + 1));
            d.addEventListener('click', function () { ssShow(i); ssStart(); });
            dots.appendChild(d);
        });
        root.appendChild(dots);
        root.addEventListener('mouseenter', ssStop);
        root.addEventListener('mouseleave', ssStart);
        container.insertBefore(root, container.firstChild);
        ssIndex = 0;
        ssAdjust();
        ssStart();
    }

    // The top bar is fixed and Jellyfin reserves 48px for it; a floating,
    // taller or info-bar-equipped bar sticks out further and would cover the
    // top of the slideshow. Measure and push the slideshow down by the overlap.
    function ssAdjust() {
        var root = document.getElementById(SS_ID);
        var header = document.querySelector('header.MuiAppBar-root');
        if (!root || !header) {
            return;
        }
        var hb = header.getBoundingClientRect();
        var rb = root.getBoundingClientRect();
        var current = parseFloat(root.style.marginTop) || 0;
        var overlap = hb.bottom - (rb.top - current);
        // A sidebar (bar on the left) does not overlap at all.
        if (hb.width < window.innerWidth * 0.5 || overlap <= 0) {
            root.style.marginTop = '';
            return;
        }
        root.style.marginTop = Math.round(overlap) + 'px';
    }

    function ssShow(i) {
        var root = document.getElementById(SS_ID);
        if (!root) {
            return;
        }
        var slides = root.querySelectorAll('.jcs-slide');
        var dots = root.querySelectorAll('.jcs-dot');
        if (!slides.length) {
            return;
        }
        ssIndex = (i + slides.length) % slides.length;
        for (var k = 0; k < slides.length; k++) {
            slides[k].classList.toggle('is-active', k === ssIndex);
            if (dots[k]) {
                dots[k].classList.toggle('is-active', k === ssIndex);
            }
        }
    }

    function ssStart() {
        ssStop();
        ssTimer = setInterval(function () { ssShow(ssIndex + 1); }, slideshow.interval * 1000);
    }

    function ssStop() {
        if (ssTimer) {
            clearInterval(ssTimer);
            ssTimer = null;
        }
    }

    function ssSync() {
        if (!slideshow) {
            return;
        }
        if (!ssWanted()) {
            var root = document.getElementById(SS_ID);
            if (root) {
                root.remove();
                ssStop();
            }
            return;
        }
        if (!ssItems) {
            ssLoad();
            return;
        }
        ssRender();
        ssAdjust();
    }

    // ------------------------------------------------------------------
    // Closing: Esc, and any navigation.
    // ------------------------------------------------------------------

    function onKeyDown(event) {
        if (event.key === 'Escape') {
            closeAll();
        }
    }
    document.addEventListener('keydown', onKeyDown);

    var lastUrl = location.href;
    function checkUrl() {
        if (disposed) {
            return;
        }
        if (location.href !== lastUrl) {
            lastUrl = location.href;
            closeAll();
            sync();
        }
    }

    ['pushState', 'replaceState'].forEach(function (name) {
        var original = history[name];
        history[name] = function () {
            var result = original.apply(this, arguments);
            checkUrl();
            setTimeout(checkUrl, 0);
            return result;
        };
    });
    window.addEventListener('popstate', checkUrl);
    window.addEventListener('hashchange', checkUrl);

    // ------------------------------------------------------------------
    // Re-sync when the UI re-renders (coalesced to one per frame).
    // ------------------------------------------------------------------

    var queued = false;
    function schedule() {
        if (queued) {
            return;
        }
        queued = true;
        requestAnimationFrame(function () {
            queued = false;
            sync();
        });
    }

    // MUI's hover feedback is either very faint (text links) or driven by
    // React's ripple component, which knows nothing about our elements -
    // so the buttons get a hover of their own.
    function installStyle() {
        if (document.getElementById('jellycanvas-inject-style')) {
            return;
        }
        var style = document.createElement('style');
        style.id = 'jellycanvas-inject-style';
        style.textContent =
            '[data-jellycanvas] { cursor: pointer; transition: background-color 0.15s ease, color 0.15s ease; }' +
            '[data-jellycanvas]:hover, [data-jellycanvas]:focus-visible { background-color: rgba(var(--jf-palette-text-primaryChannel, 255 255 255) / 0.12) !important; }' +
            '[data-jellycanvas].MuiIconButton-root:hover { color: var(--jf-palette-primary-main, #00a4dc) !important; }' +
            badgeCss();
        document.head.appendChild(style);
    }

    function start() {
        installStyle();
        var bodyObserver = new MutationObserver(schedule);
        bodyObserver.observe(document.body, { childList: true, subtree: true });
        // layout-tv lives on <html>, which needs its own observer.
        var rootObserver = new MutationObserver(schedule);
        rootObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
        observers.push(bodyObserver, rootObserver);
        window.addEventListener('resize', onResize);
        // Safety net for navigations that bypass both the History API and mutations.
        timers.push(setInterval(function () { checkUrl(); sync(); }, 1000));
        sync();
    }

    if (document.body) {
        start();
    } else {
        document.addEventListener('DOMContentLoaded', start);
    }
})();
