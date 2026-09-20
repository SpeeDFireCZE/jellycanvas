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

    var CONFIG = /*JELLYCANVAS_CONFIG*/{ "buttons": [], "slideshow": null, "infoBar": null, "badges": null, "backdrop": null, "rows": [], "seerrOpen": 0 };

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
    var backdrop = CONFIG.backdrop || null;
    var rows = CONFIG.rows || [];
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
        backdropStop();
        rowsRemoveAll();
        ['jellycanvasSlideshow', 'jellycanvas-inject-style', 'jellycanvasInfoClose', 'jellycanvasRows-style'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) {
                el.remove();
            }
        });
        delete window.__jellycanvasScript;
    }

    if (!buttons.length && !slideshow && !infoBar && !badges && !backdrop && !rows.length) {
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
    // Languages by how many people speak them, for filling up the badge
    // when the preferred ones are missing (an item with EN, FR and JA and
    // room for two shows EN and FR).
    var LANG_RANK = ['EN', 'ZH', 'HI', 'ES', 'FR', 'AR', 'BN', 'PT', 'RU', 'UR', 'ID', 'DE', 'JA', 'SW', 'TE', 'TR', 'KO', 'VI', 'IT', 'TH', 'PL', 'UK', 'NL', 'RO', 'EL', 'CS', 'SV', 'HU', 'HE', 'DA', 'FI', 'NO', 'SK', 'BG', 'HR', 'SR', 'SL', 'LT', 'LV', 'ET', 'CA'];

    // Which of an item's languages the badge shows: the preferred ones
    // first (in the admin's order), then the most widely spoken of the
    // rest, then whatever is left - cut to the limit.
    function pickLanguages(codes, preferred, max) {
        var rank = function (c) {
            var i = LANG_RANK.indexOf(c);
            return i < 0 ? LANG_RANK.length : i;
        };
        var out = [];
        (preferred || []).forEach(function (p) {
            if (codes.indexOf(p) >= 0 && out.indexOf(p) < 0) {
                out.push(p);
            }
        });
        codes.slice().sort(function (a, b) { return rank(a) - rank(b); }).forEach(function (c) {
            if (out.indexOf(c) < 0) {
                out.push(c);
            }
        });
        return out.slice(0, Math.max(1, max || 4));
    }
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
            case 'Colorful':
                look = 'background: rgba(0, 0, 0, 0.68); color: #fff;';
                break;
            default:
                look = 'background: rgba(0, 0, 0, 0.68); color: #fff;';
        }
        // Colorful: every badge gets its own color from badgeColor() inline.
        var colorful = '';
        var size = (11 * badges.scale / 100).toFixed(1);
        // Stacked: one badge per line, hugging the corner's side; the flags
        // of a language badge then form a little column like a flag pole.
        var stacked = badges.stacked
            ? '.jellycanvas-badges { flex-direction: column; align-items: flex-start; flex-wrap: nowrap; } .jellycanvas-badges-tr, .jellycanvas-badges-br { align-items: flex-end; }'
            : '';
        // A column that would run into the title (a short landscape card, a
        // phone) lies down into rows instead - the script switches it.
        var rows = '.jellycanvas-badges.jellycanvas-badges-rows { flex-direction: row; flex-wrap: wrap; align-items: center; } .jellycanvas-badges-tr.jellycanvas-badges-rows, .jellycanvas-badges-br.jellycanvas-badges-rows { justify-content: flex-end; }';
        // Paddings and gaps in em: a badge shrunk for a small card shrinks
        // as a whole, not just its letters.
        return '.jellycanvas-badges { position: absolute; z-index: 3; display: flex; flex-wrap: wrap; gap: 0.27em; padding: 5px; max-width: 100%; box-sizing: border-box; pointer-events: none; font-size: ' + size + 'px; font-weight: 700; line-height: 1; }' +
            '.jellycanvas-badges-tl { top: 0; left: 0; } .jellycanvas-badges-tr { top: 0; right: 0; justify-content: flex-end; }' +
            '.jellycanvas-badges-bl { bottom: 0; left: 0; } .jellycanvas-badges-br { bottom: 0; right: 0; justify-content: flex-end; }' +
            stacked + rows +
            '.jellycanvas-badge { display: inline-flex; align-items: center; gap: 0.27em; padding: 0.27em 0.55em; border-radius: 0.36em; letter-spacing: 0.02em; white-space: nowrap; max-width: 100%; overflow: hidden; text-overflow: ellipsis; ' + look + ' }' +
            colorful +
            '.jellycanvas-badge.jellycanvas-flagonly { background: transparent !important; border: 0 !important; padding: 0 !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; }' +
            '.jellycanvas-badge.jellycanvas-flagonly .jellycanvas-flag { height: 1.6em; box-shadow: 0 1px 3px rgba(0, 0, 0, 0.6); }' +
            '.jellycanvas-badge .material-icons { font-size: 1.15em; }' +
            '.jellycanvas-badge .jellycanvas-badge-icon { width: 1.15em; height: 1.15em; flex: 0 0 auto; }' +
            '.jellycanvas-badge .jellycanvas-flag { height: 1.15em; width: auto; border-radius: 2px; box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.35); }' +
            '.jellycanvas-badge > span:not(.material-icons) + span, .jellycanvas-badge .jellycanvas-flag + span { margin-left: 1px; }' +
            '.jellycanvas-lang { display: inline-flex; align-items: center; gap: 0.27em; }';
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
        var sound = null;
        streams.forEach(function (st) {
            if (st.Type === 'Video' && !video) {
                video = st;
            } else if (st.Type === 'Audio') {
                if (!sound || st.IsDefault) {
                    sound = st;
                }
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
        var codec = (video.Codec || '').toLowerCase();
        var codecName = codec === 'h264' ? 'H264' : codec === 'hevc' || codec === 'h265' ? 'HEVC' : codec === 'av1' ? 'AV1' : codec === 'vp9' ? 'VP9' : codec === 'mpeg4' ? 'MPEG-4' : codec.toUpperCase();
        return { resolution: res, hdr: hdr, codec: codecName, sound: soundName(sound), audio: audio, subtitles: subs };
    }

    // "DD+ Atmos 5.1", "DTS-HD 7.1", "AAC 2.0" - short enough for a card
    // (the full "Dolby Digital+ Atmos 5.1" ran past the poster's edge).
    function soundName(st) {
        if (!st) {
            return '';
        }
        var c = (st.Codec || '').toLowerCase();
        var profile = (st.Profile || '').toLowerCase();
        var name = c === 'eac3' ? 'DD+' : c === 'ac3' ? 'DD' : c === 'truehd' ? 'TrueHD'
            : c === 'dts' ? (/hd|x|ma/.test(profile) ? 'DTS-HD' : 'DTS') : c === 'aac' ? 'AAC' : c === 'flac' ? 'FLAC' : c === 'opus' ? 'Opus'
            : c === 'mp3' ? 'MP3' : c === 'pcm' || /pcm/.test(c) ? 'PCM' : c.toUpperCase();
        if (/atmos/.test(profile)) {
            name += ' Atmos';
        }
        var ch = st.Channels || 0;
        var layout = ch >= 8 ? '7.1' : ch >= 6 ? '5.1' : ch === 2 ? '2.0' : ch === 1 ? '1.0' : '';
        return layout ? name + ' ' + layout : name;
    }

    // ------------------------------------------------------------------
    // Flags, drawn as tiny inline SVGs so nothing is downloaded and they
    // show the same everywhere (Windows has no flag emoji). A flag is a
    // few stripes plus at most one figure; a language without a flag here
    // shows its code. Language -> country is a rough call (en -> GB,
    // es -> ES, pt -> PT), like every "language as flag" list.
    // ------------------------------------------------------------------
    var FLAGS = {
        EN: { h: ['#012169'], gb: true },
        CS: { h: ['#fff', '#d7141a'], tri: '#11457e' },
        SK: { h: ['#fff', '#0b4ea2', '#ee1c25'] },
        DE: { h: ['#000', '#dd0000', '#ffce00'] },
        FR: { v: ['#0055a4', '#fff', '#ef4135'] },
        ES: { h: ['#aa151b', '#f1bf00', '#f1bf00', '#aa151b'] },
        IT: { v: ['#009246', '#fff', '#ce2b37'] },
        PT: { v: ['#006600', '#ff0000', '#ff0000'], dot: '#ffe000', dx: 0.36 },
        RU: { h: ['#fff', '#0039a6', '#d52b1e'] },
        PL: { h: ['#fff', '#dc143c'] },
        HU: { h: ['#cd2a3e', '#fff', '#436f4d'] },
        NL: { h: ['#ae1c28', '#fff', '#21468b'] },
        JA: { h: ['#fff'], dot: '#bc002d' },
        KO: { h: ['#fff'], dot: '#cd2e3a', dot2: '#0047a0' },
        ZH: { h: ['#de2910'], star: '#ffde00', sx: 0.22, sy: 0.35 },
        SV: { h: ['#006aa7'], cross: '#fecc00' },
        NO: { h: ['#ba0c2f'], cross: '#fff', cross2: '#00205b' },
        DA: { h: ['#c8102e'], cross: '#fff' },
        FI: { h: ['#fff'], cross: '#002f6c' },
        TR: { h: ['#e30a17'], dot: '#fff', dx: 0.38, dot2: '#e30a17', dx2: 0.46 },
        AR: { h: ['#006c35'], bar: '#fff' },
        HI: { h: ['#ff9933', '#fff', '#138808'], ring: '#000080' },
        UK: { h: ['#0057b7', '#ffd700'] },
        EL: { h: ['#0d5eaf', '#fff', '#0d5eaf', '#fff', '#0d5eaf', '#fff', '#0d5eaf', '#fff', '#0d5eaf'], canton: '#0d5eaf' },
        RO: { v: ['#002b7f', '#fcd116', '#ce1126'] },
        BG: { h: ['#fff', '#00966e', '#d62612'] },
        HR: { h: ['#ff0000', '#fff', '#171796'] },
        SR: { h: ['#c6363c', '#0c4076', '#fff'] },
        SL: { h: ['#fff', '#005da4', '#ed1c24'] },
        TH: { h: ['#a51931', '#f4f5f8', '#2d2a4a', '#2d2a4a', '#f4f5f8', '#a51931'] },
        VI: { h: ['#da251d'], star: '#ffff00', sx: 0.5, sy: 0.5 },
        ID: { h: ['#ff0000', '#fff'] },
        HE: { h: ['#fff', '#0038b8', '#fff', '#fff', '#0038b8', '#fff'], hex: '#0038b8' },
        LT: { h: ['#fdb913', '#006a44', '#c1272d'] },
        LV: { h: ['#9e3039', '#9e3039', '#fff', '#9e3039', '#9e3039'] },
        ET: { h: ['#0072ce', '#000', '#fff'] },
        CA: { h: ['#fcdd09', '#da121a', '#fcdd09', '#da121a', '#fcdd09', '#da121a', '#fcdd09', '#da121a', '#fcdd09'] }
    };
    var NS = 'http://www.w3.org/2000/svg';

    function svgEl(name, attrs) {
        var el = document.createElementNS(NS, name);
        Object.keys(attrs).forEach(function (k) { el.setAttribute(k, attrs[k]); });
        return el;
    }

    /** A small inline icon for the language pills (Material Design shapes, 24-unit grid). */
    function badgeIcon(kind) {
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('class', 'jellycanvas-badge-icon');
        svg.setAttribute('aria-hidden', 'true');
        var path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('fill', 'currentColor');
        path.setAttribute('d', kind === 'volume'
            ? 'M3 9v6h4l5 5V4L7 9H3zm13.5 3A4.5 4.5 0 0 0 14 7.97v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z'
            : 'M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM4 12h4v2H4v-2zm10 6H4v-2h10v2zm6 0h-4v-2h4v2zm0-4H10v-2h10v2z');
        svg.appendChild(path);
        return svg;
    }

    function flagSvg(code) {
        var f = FLAGS[code];
        if (!f) {
            return null;
        }
        var W = 30;
        var H = 20;
        var svg = svgEl('svg', { viewBox: '0 0 ' + W + ' ' + H, 'class': 'jellycanvas-flag', 'aria-label': code });
        var stripes = f.h || f.v;
        var n = stripes.length;
        stripes.forEach(function (color, i) {
            svg.appendChild(f.h
                ? svgEl('rect', { x: 0, y: (H * i / n).toFixed(2), width: W, height: (H / n + 0.3).toFixed(2), fill: color })
                : svgEl('rect', { x: (W * i / n).toFixed(2), y: 0, width: (W / n + 0.3).toFixed(2), height: H, fill: color }));
        });
        if (f.gb) {
            svg.appendChild(svgEl('path', { d: 'M0 0 L30 20 M30 0 L0 20', stroke: '#fff', 'stroke-width': 5 }));
            svg.appendChild(svgEl('path', { d: 'M0 0 L30 20 M30 0 L0 20', stroke: '#c8102e', 'stroke-width': 2 }));
            svg.appendChild(svgEl('path', { d: 'M15 0 V20 M0 10 H30', stroke: '#fff', 'stroke-width': 6 }));
            svg.appendChild(svgEl('path', { d: 'M15 0 V20 M0 10 H30', stroke: '#c8102e', 'stroke-width': 3.5 }));
        }
        if (f.tri) {
            svg.appendChild(svgEl('path', { d: 'M0 0 L15 10 L0 20 Z', fill: f.tri }));
        }
        if (f.cross) {
            svg.appendChild(svgEl('path', { d: 'M11 0 V20 M0 10 H30', stroke: f.cross, 'stroke-width': f.cross2 ? 5 : 4 }));
            if (f.cross2) {
                svg.appendChild(svgEl('path', { d: 'M11 0 V20 M0 10 H30', stroke: f.cross2, 'stroke-width': 2.5 }));
            }
        }
        if (f.dot) {
            svg.appendChild(svgEl('circle', { cx: W * (f.dx || 0.5), cy: H / 2, r: 5.5, fill: f.dot }));
            if (f.dot2) {
                svg.appendChild(f.dx2
                    ? svgEl('circle', { cx: W * f.dx2, cy: H / 2, r: 4.5, fill: f.dot2 })
                    : svgEl('path', { d: 'M9.5 10 A5.5 5.5 0 0 0 20.5 10 Z', fill: f.dot2 }));
            }
        }
        if (f.star) {
            var cx = W * f.sx;
            var cy = H * f.sy;
            var pts = [];
            for (var i = 0; i < 10; i++) {
                var r = i % 2 ? 2.3 : 5.5;
                var a = -Math.PI / 2 + i * Math.PI / 5;
                pts.push((cx + r * Math.cos(a)).toFixed(1) + ',' + (cy + r * Math.sin(a)).toFixed(1));
            }
            svg.appendChild(svgEl('polygon', { points: pts.join(' '), fill: f.star }));
        }
        if (f.bar) {
            svg.appendChild(svgEl('rect', { x: 7, y: 8, width: 16, height: 4, fill: f.bar }));
        }
        if (f.ring) {
            svg.appendChild(svgEl('circle', { cx: W / 2, cy: H / 2, r: 3, fill: 'none', stroke: f.ring, 'stroke-width': 1.5 }));
        }
        if (f.canton) {
            svg.appendChild(svgEl('rect', { x: 0, y: 0, width: 12, height: 11.2, fill: f.canton }));
            svg.appendChild(svgEl('path', { d: 'M6 0 V11.2 M0 5.6 H12', stroke: '#fff', 'stroke-width': 2.2 }));
        }
        if (f.hex) {
            svg.appendChild(svgEl('path', { d: 'M15 5 L19.5 13 H10.5 Z M15 15 L10.5 7 H19.5 Z', fill: 'none', stroke: f.hex, 'stroke-width': 1.3 }));
        }
        return svg;
    }

    // ------------------------------------------------------------------
    // Colorful style: a color per value, so 4K, 1080p, HEVC or Atmos can
    // be told apart at a glance. Known values have a fixed rank, so the
    // same thing is always the same color; anything else is hashed. The
    // palette turns a rank into a hue within its range.
    // ------------------------------------------------------------------

    var BADGE_RANKS = {
        '4K': 0, '1080p': 1, '720p': 2, 'SD': 3,
        'DV': 4, 'HDR': 5, 'HLG': 6,
        'HEVC': 7, 'H264': 8, 'AV1': 9, 'VP9': 10, 'MPEG-4': 11
    };

    // Hues by rank (see BADGE_RANKS and the sound families in badgeRank):
    // 4K pink, 1080p blue, 720p green, SD grey; DV purple, HDR amber, HLG
    // orange; HEVC teal, H264 indigo, AV1 red-orange, VP9 magenta, MPEG-4
    // steel; then the sound families. Neighbours in a list are far apart
    // on the wheel so the values a card is likely to show side by side
    // never look alike. Hashed values (languages) spread over the wheel.
    var VIVID_HUES = [335, 210, 130, -1, 275, 45, 25, 175, 240, 15, 300, 200, 265, 285, 255, 190, 205, 90, 65, 30, 145, 170, 0, 220];

    function paletteHue(hues, r) {
        return r < hues.length ? hues[r] : (r * 47) % 360;
    }

    var BADGE_PALETTES = {
        Vivid: { hue: function (r) { return paletteHue(VIVID_HUES, r); }, s: 70, l: 46, text: '#fff' },
        // teal to purple, neighbours 53 degrees apart
        Cool: { hue: function (r) { return r === 3 ? -1 : 165 + (r * 53) % 125; }, s: 62, l: 44, text: '#fff' },
        // magenta through red to yellow
        Warm: { hue: function (r) { return r === 3 ? -1 : (330 + (r * 37) % 90) % 360; }, s: 80, l: 47, text: '#fff' },
        Pastel: { hue: function (r) { return paletteHue(VIVID_HUES, r); }, s: 65, l: 78, text: '#1b1b1b' },
        Neon: { hue: function (r) { return paletteHue(VIVID_HUES, r); }, s: 100, l: 58, text: '#111' }
    };

    function badgeRank(id, value) {
        var key = String(value);
        if (BADGE_RANKS.hasOwnProperty(key)) {
            return BADGE_RANKS[key];
        }
        // Sound: the codec family decides, the channel layout does not.
        if (id === 'sound') {
            var fam = key.replace(/\s[0-9.]+$/, '');
            var sounds = ['TrueHD Atmos', 'DD+ Atmos', 'TrueHD', 'DTS-HD', 'DTS', 'DD+', 'DD', 'AAC', 'FLAC', 'Opus', 'MP3', 'PCM'];
            var at = sounds.indexOf(fam);
            if (at >= 0) {
                return 12 + at;
            }
        }
        var h = 0;
        for (var i = 0; i < key.length; i++) {
            h = (h * 31 + key.charCodeAt(i)) % 1000;
        }
        return 24 + h;
    }

    function badgeColor(el, id, value) {
        if (badges.style !== 'Colorful') {
            return;
        }
        var p = BADGE_PALETTES[badges.palette] || BADGE_PALETTES.Vivid;
        var hue = p.hue(badgeRank(id, value));
        // -1 = neutral grey (SD), so the plain thing does not shout.
        el.style.background = hue < 0 ? 'hsl(0, 0%, ' + Math.round(p.l * 0.95) + '%)' : 'hsl(' + hue + ', ' + p.s + '%, ' + p.l + '%)';
        el.style.color = p.text;
    }

    function badgeHtml(id, info) {
        var value = info[id];
        if (!value || !value.length) {
            return '';
        }
        var isLang = id === 'audio' || id === 'subtitles';
        if (isLang) {
            value = id === 'audio'
                ? pickLanguages(value, badges.audioPreferred, badges.audioMax)
                : pickLanguages(value, badges.subtitlePreferred, badges.subtitleMax);
        }
        var el = document.createElement('span');
        el.className = 'jellycanvas-badge jellycanvas-badge-' + id;
        // Audio and subtitle languages each have their own way of showing.
        var mode = (id === 'subtitles' ? badges.subtitleLanguages : badges.languages) || 'Codes';
        if (isLang && mode === 'Flags') {
            var flags = [];
            var rest = [];
            value.forEach(function (code) {
                var f = flagSvg(code);
                if (f) {
                    var holder = document.createElement('span');
                    holder.className = 'jellycanvas-badge jellycanvas-badge-' + id + ' jellycanvas-flagonly';
                    holder.appendChild(f);
                    flags.push(holder);
                } else {
                    rest.push(code);
                }
            });
            if (rest.length) {
                var pill = document.createElement('span');
                pill.className = 'jellycanvas-badge jellycanvas-badge-' + id;
                pill.textContent = rest.join(' ');
                badgeColor(pill, id, rest[0]);
                flags.push(pill);
            }
            return flags.length ? flags : '';
        }
        if (isLang) {
            // The icon is an inline SVG, not the icon font: before that font
            // has loaded the ligature text ("subtitles") would be measured
            // as the pill's width, and the corners would be placed for a
            // pill three times as wide as it ends up (seen on phones).
            el.appendChild(badgeIcon(id === 'audio' ? 'volume' : 'subtitles'));
            badgeColor(el, id, value[0]);
            value.forEach(function (code) {
                // Each language in its own holder, so the pill can fold the
                // ones past the second into "+N" when the corners collide.
                var holder = document.createElement('span');
                holder.className = 'jellycanvas-lang';
                var flag = mode === 'Codes' ? null : flagSvg(code);
                if (flag) {
                    holder.appendChild(flag);
                }
                if (!flag || mode === 'FlagsAndCodes') {
                    var t = document.createElement('span');
                    t.textContent = code;
                    holder.appendChild(t);
                }
                el.appendChild(holder);
            });
            if (value.length > 2) {
                el.setAttribute('data-more', String(value.length - 2));
            }
            return el;
        }
        el.appendChild(document.createTextNode(value));
        badgeColor(el, id, value);
        if (id === 'sound') {
            // A long sound name ("TrueHD Atmos 7.1") has a short form for a
            // card where the two top corners would otherwise not fit side by
            // side: Atmos says enough on its own, elsewhere the layout goes.
            var short = /Atmos/.test(value) ? value.replace(/^\S+\s+/, '') : value.replace(/\s+\d\.\d$/, '');
            if (short !== value) {
                el.setAttribute('data-short', short);
            }
        }
        return el;
    }

    /** Swaps the long badge texts of a box for their short forms ("TrueHD 5.1" -> "TrueHD", "CS EN FR DE" -> "CS EN +2"); true when something changed. */
    function compactBadges(box) {
        var changed = false;
        box.querySelectorAll('[data-short]').forEach(function (el) {
            var short = el.getAttribute('data-short');
            if (el.textContent !== short) {
                el.textContent = short;
                changed = true;
            }
        });
        box.querySelectorAll('[data-more]:not([data-folded])').forEach(function (el) {
            el.setAttribute('data-folded', '1');
            el.querySelectorAll('.jellycanvas-lang').forEach(function (holder, i) {
                if (i >= 2) {
                    holder.style.display = 'none';
                }
            });
            var more = document.createElement('span');
            more.textContent = '+' + el.getAttribute('data-more');
            el.appendChild(more);
            changed = true;
        });
        return changed;
    }

    var fontsGraceUntil = Date.now() + 4000;

    function renderBadges(card, info) {
        // While the page's fonts are still coming in, every width would be
        // measured in a fallback font; wait (a few seconds at most).
        if (info && document.fonts && document.fonts.status === 'loading' && Date.now() < fontsGraceUntil) {
            return;
        }
        card.setAttribute('data-jc-badges', info ? 'done' : 'none');
        if (!info) {
            return;
        }
        var host = card.querySelector('.cardScalable');
        if (!host) {
            return;
        }
        // Rounded cards: a badge in the very corner would stick out past
        // the curve, so the inset grows with the radius (the curve gives
        // up about 0.3 r at 45 degrees).
        var image = card.querySelector('.cardImageContainer') || host;
        var radius = parseFloat(getComputedStyle(image).borderTopLeftRadius) || 0;
        // On a narrow card (a dense grid, a phone) every pixel of inset is
        // taken from the badges themselves: the margin beyond the curve
        // shrinks there.
        var hostRect = host.getBoundingClientRect();
        if (!hostRect.width || !hostRect.height) {
            // Not laid out yet (a hidden tab, a row still being built): the
            // next sync tries again.
            card.removeAttribute('data-jc-badges');
            return;
        }
        card.setAttribute('data-jc-badges-w', String(Math.round(hostRect.width)));
        var hostWidth = hostRect.width;
        var inset = Math.round((hostWidth < 140 ? 3 : 5) + radius * 0.3);
        // Jellyfin's own indicators (played tick, unplayed count top right,
        // media source top left) keep their corner; badges there start
        // under them.
        var hostTop = hostRect.top;
        function under(selector) {
            var ind = card.querySelector(selector);
            if (!ind || !ind.offsetHeight) {
                return 0;
            }
            return Math.max(0, Math.round(ind.getBoundingClientRect().bottom - hostTop) - inset + 2);
        }
        var below = { tr: under('.playedIndicator, .countIndicator, .indicator'), tl: under('.mediaSourceIndicator') };
        // TV cards (and the "overlay" title style) print the title over the
        // bottom of the image; badges at the bottom move up above it.
        var textTop = hostRect.bottom;
        var texts = card.querySelectorAll('.cardText');
        for (var t = 0; t < texts.length; t++) {
            var tr = texts[t].getBoundingClientRect();
            if (tr.height > 0 && tr.top < hostRect.bottom - 2 && tr.bottom > hostRect.top && getComputedStyle(texts[t]).position === 'absolute') {
                textTop = Math.min(textTop, tr.top);
            }
        }
        var above = Math.max(0, Math.round(hostRect.bottom - textTop) - inset + 2);
        // Buttons that sit on the image for good (the play button touch
        // clients draw at the bottom right) keep their side of the bottom
        // edge: badges there stay above them.
        var reserve = { l: above, r: above };
        var fixed = card.querySelectorAll('.MuiButtonGroup-root, .cardOverlayButton-br, .cardOverlayFab-primary');
        for (var f = 0; f < fixed.length; f++) {
            var fb = fixed[f];
            if (!fb.offsetHeight) {
                continue;
            }
            var opacity = 1;
            for (var anc = fb; anc && anc !== host; anc = anc.parentElement) {
                opacity *= parseFloat(getComputedStyle(anc).opacity);
            }
            if (opacity < 0.5) {
                continue; // a hover overlay, not there until the pointer is
            }
            var fr = fb.getBoundingClientRect();
            var side = fr.left + fr.width / 2 > hostRect.left + hostRect.width / 2 ? 'r' : 'l';
            if (fr.top < hostRect.top + hostRect.height / 2) {
                // A button placed at the top (the play button in a top corner):
                // the badges of that corner start under it.
                var corner = side === 'l' ? 'tl' : 'tr';
                below[corner] = Math.max(below[corner], Math.round(fr.bottom - hostRect.top) - inset + 3);
                continue;
            }
            reserve[side] = Math.max(reserve[side], Math.round(hostRect.bottom - fr.top) - inset + 3);
        }
        // Smaller cards (a dense library grid) get smaller badges: the size
        // follows the card's width, down to about three fifths on the
        // narrowest.
        var shrink = Math.max(0.6, Math.min(1, hostWidth / 220));
        var baseSize = 11 * badges.scale / 100 * shrink;
        var made = {};
        Object.keys(badges.corners).forEach(function (corner) {
            var ids = badges.corners[corner];
            var box = null;
            ids.forEach(function (id) {
                var els = badgeHtml(id, info);
                if (!els) {
                    return;
                }
                if (!box) {
                    box = document.createElement('div');
                    box.className = 'jellycanvas-badges jellycanvas-badges-' + corner;
                    // The inset clears the rounded corner: it is needed on the
                    // box's outer sides only; the sides facing the middle of
                    // the card keep a hair, which leaves narrow cards room.
                    var gap = 2;
                    box.style.padding = corner === 'tl' ? inset + 'px ' + gap + 'px ' + gap + 'px ' + inset + 'px'
                        : corner === 'tr' ? inset + 'px ' + inset + 'px ' + gap + 'px ' + gap + 'px'
                        : corner === 'bl' ? gap + 'px ' + gap + 'px ' + inset + 'px ' + inset + 'px'
                        : gap + 'px ' + inset + 'px ' + inset + 'px ' + gap + 'px';
                    if (shrink < 1) {
                        box.style.fontSize = baseSize.toFixed(1) + 'px';
                    }
                    if (below[corner]) {
                        box.style.marginTop = below[corner] + 'px';
                    }
                    var lift = reserve[corner === 'bl' ? 'l' : 'r'];
                    if (lift && (corner === 'bl' || corner === 'br')) {
                        box.style.marginBottom = lift + 'px';
                    }
                }
                (els.length === undefined ? [els] : els).forEach(function (el) { box.appendChild(el); });
            });
            if (box) {
                host.appendChild(box);
                made[corner] = box;
            }
        });
        // Two corners of one edge that would run into each other: first
        // both shrink a little (down to two thirds of their size) to sit
        // side by side; only when that is not enough does the right one
        // move past the left one (down at the top, up at the bottom).
        [['tl', 'tr', 'marginTop'], ['bl', 'br', 'marginBottom']].forEach(function (pair) {
            var left = made[pair[0]];
            var right = made[pair[1]];
            if (!left || !right) {
                return;
            }
            var lr = left.getBoundingClientRect();
            var rr = right.getBoundingClientRect();
            var overlaps = function () {
                return lr.right > rr.left && lr.bottom > rr.top && lr.top < rr.bottom;
            };
            if (!overlaps()) {
                return;
            }
            // The two boxes need this much of the card between them; the
            // insets on the outer sides are fixed, the content scales. Up to
            // two rounds (the parts do not all scale alike), never below
            // two thirds of the size.
            // First the long texts go short (a name without its layout, a
            // pill with "+2") - that keeps the letters big; then the size.
            var compactedLeft = compactBadges(left);
            var compactedRight = compactBadges(right);
            if (compactedLeft || compactedRight) {
                lr = left.getBoundingClientRect();
                rr = right.getBoundingClientRect();
            }
            var tries = 0;
            while (overlaps() && tries++ < 3) {
                var need = (lr.width - inset) + (rr.width - inset) + 2;
                var room = hostWidth - 2 * inset;
                var factor = Math.min(0.97, (room / need) * 0.97);
                var size = parseFloat(left.style.fontSize) || baseSize;
                if (size * factor < baseSize * 0.62) {
                    break;
                }
                [left, right].forEach(function (box) {
                    var own = parseFloat(box.style.fontSize) || baseSize;
                    box.style.fontSize = (own * factor).toFixed(1) + 'px';
                });
                lr = left.getBoundingClientRect();
                rr = right.getBoundingClientRect();
            }
            if (!overlaps()) {
                return;
            }
            // Past the left box: measured from the edge, so a left box that
            // itself starts lower (under a tick or a button) is cleared too.
            var past = pair[2] === 'marginTop' ? lr.bottom - hostRect.top : hostRect.bottom - lr.top;
            right.style[pair[2]] = Math.max(parseFloat(right.style[pair[2]]) || 0, Math.round(past) - inset + 2) + 'px';
        });
        // A column at the top (under a played tick, or moved past the other
        // corner) must end above the title strip and the fixed buttons: it
        // loses badges from its end until it does - extra flags of a run
        // first, then whole badges; a box that cannot fit at all goes.
        ['tl', 'tr'].forEach(function (corner) {
            var box = made[corner];
            if (!box) {
                return;
            }
            var limit = hostRect.bottom - reserve[corner === 'tl' ? 'l' : 'r'] - inset;
            var other = made[corner === 'tl' ? 'tr' : 'tl'];
            var clash = function () {
                if (!other) {
                    return false;
                }
                var a = box.getBoundingClientRect();
                var b = other.getBoundingClientRect();
                return a.right > b.left && a.left < b.right && a.bottom > b.top && a.top < b.bottom;
            };
            // A column that runs too long first shrinks to fit (down to
            // half) - small flags under the tick beat a block of rows.
            var br0 = box.getBoundingClientRect();
            // For support: what the box measured against (top / bottom / the limit, card-relative).
            box.setAttribute('data-jc-fit', Math.round(br0.top - hostRect.top) + '/' + Math.round(br0.bottom - hostRect.top) + '/' + Math.round(limit - hostRect.top));
            if (br0.bottom > limit && br0.height > 0) {
                // The box's padding (the inset on top, a hair at the bottom) is fixed; only the content scales.
                var fixedPart = inset + 2;
                var factor = Math.min(0.98, ((limit - br0.top) - fixedPart) / Math.max(1, br0.height - fixedPart) * 0.98);
                var size0 = parseFloat(box.style.fontSize) || baseSize;
                if (size0 * factor >= baseSize * 0.45) {
                    box.style.fontSize = (size0 * factor).toFixed(1) + 'px';
                }
            }
            // A stacked column that still runs too long lies down into rows
            // (a row of four flags is short); only if that clashes with the
            // other corner does it stay a column and lose badges instead.
            if (badges.stacked && box.getBoundingClientRect().bottom > limit) {
                box.classList.add('jellycanvas-badges-rows');
                // Not a long row across the poster: two flags a row, so the
                // block stays narrow and reads as a (double) column.
                var flag = box.querySelector('.jellycanvas-flagonly');
                if (flag) {
                    var fontPx = parseFloat(box.style.fontSize) || baseSize;
                    var widest = 0;
                    for (var w = 0; w < box.children.length; w++) {
                        widest = Math.max(widest, box.children[w].getBoundingClientRect().width);
                    }
                    var twoFlags = 2 * flag.getBoundingClientRect().width + 0.27 * fontPx;
                    box.style.maxWidth = Math.ceil(Math.max(twoFlags, widest) + inset + 2 + 1) + 'px';
                }
                if (clash()) {
                    box.classList.remove('jellycanvas-badges-rows');
                    box.style.maxWidth = '';
                }
            }
            var guard = 12;
            while (box.getBoundingClientRect().bottom > limit && guard-- > 0) {
                if (box.children.length <= 1) {
                    box.remove();
                    delete made[corner];
                    return;
                }
                var victim = null;
                for (var c = box.children.length - 1; c > 0; c--) {
                    var el = box.children[c];
                    var prev = box.children[c - 1];
                    if (el.classList.contains('jellycanvas-flagonly') && prev.classList.contains('jellycanvas-flagonly') && prev.className === el.className) {
                        victim = el;
                        break;
                    }
                }
                (victim || box.lastElementChild).remove();
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
            cards[j].removeAttribute('data-jc-badges-w');
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
        if ((badges.hideOnMobile && isMobile()) || (badges.hideOnTv && isTv())) {
            return;
        }
        // A card laid out since its badges were placed (the grid settled,
        // the phone turned, a font arrived) gets them placed again: the
        // placement was measured against the old size.
        var done = document.querySelectorAll('.card[data-jc-badges="done"][data-jc-badges-w]');
        for (var d = 0; d < done.length; d++) {
            var hostNow = done[d].querySelector('.cardScalable');
            var wNow = hostNow ? Math.round(hostNow.getBoundingClientRect().width) : 0;
            if (wNow && Math.abs(wNow - parseInt(done[d].getAttribute('data-jc-badges-w'), 10)) > 2) {
                done[d].querySelectorAll('.jellycanvas-badges').forEach(function (b) { b.remove(); });
                done[d].removeAttribute('data-jc-badges');
                done[d].removeAttribute('data-jc-badges-w');
            }
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
    // Rotating random backdrops. The theme's CSS can only swap URLs and
    // hope the picture is there; the script asks the server for the next
    // random backdrop ahead of time, waits for the picture to load, and
    // only then cross-fades to it on a second layer. The CSS layers are
    // switched off by the class on <html>.
    // ------------------------------------------------------------------
    var bdTimer = null;
    var bdHost = null;
    var bdLayers = [];
    var bdCurrent = 0;
    var bdBusy = false;
    var bdDetailId = null;

    // The item whose page is open (#/details?id=...), or null elsewhere.
    function detailItemId() {
        var m = /^#\/details\?(.*)$/.exec(location.hash || '');
        if (!m) {
            return null;
        }
        var q = /(?:^|&)id=([^&]+)/.exec(m[1]);
        return q ? q[1] : null;
    }

    // Loads the picture, then cross-fades to it on the other layer.
    function backdropShow(url) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { resolve(url); };
            img.onerror = reject;
            img.src = url;
        }).then(function (finalUrl) {
            // The pair in use is captured: the rotation may be stopped
            // (and the layers dropped) while the image loads or between
            // the two frames below.
            var layers = bdLayers;
            var host = bdHost;
            var next = 1 - bdCurrent;
            if (!layers[next]) {
                return;
            }
            layers[next].style.backgroundImage = 'url("' + finalUrl + '")';
            // Two frames later so the browser paints the new image before the fade.
            requestAnimationFrame(function () {
                requestAnimationFrame(function () {
                    if (layers !== bdLayers) {
                        return;
                    }
                    layers[next].classList.add('is-on');
                    layers[bdCurrent].classList.remove('is-on');
                    host.classList.add('is-active');
                    bdCurrent = next;
                });
            });
        });
    }

    // Both layers off: whatever the CSS paints underneath shows again.
    function backdropClear() {
        bdLayers.forEach(function (l) { l.classList.remove('is-on'); });
        if (bdHost) {
            bdHost.classList.remove('is-active');
        }
    }

    // The item's own backdrop (an episode's comes from its series).
    function backdropShowItem(id) {
        var api = window.ApiClient;
        if (!api || !api.getCurrentUserId()) {
            return;
        }
        api.getItem(api.getCurrentUserId(), id).then(function (item) {
            if (bdDetailId !== id || !item) {
                return;
            }
            var own = item.BackdropImageTags && item.BackdropImageTags.length;
            var owner = own ? item.Id : item.ParentBackdropItemId;
            var tag = own ? item.BackdropImageTags[0] : (item.ParentBackdropImageTags || [])[0];
            if (!owner) {
                return;
            }
            return backdropShow(api.getScaledImageUrl(owner, { type: 'Backdrop', maxWidth: 1920, tag: tag }));
        }).catch(function () { /* no backdrop for this item - keep what is there */ });
    }

    function backdropUrl() {
        // Relative to /web/ (or wherever the client lives), like the CSS.
        var base = location.pathname.replace(/[^/]*$/, '');
        return base + '../Jellycanvas/Backdrop?n=' + Math.floor(Math.random() * 1e9);
    }

    // Rotation is off on a TV that asked for a still background.
    function backdropRotates() {
        return backdrop.seconds > 0 && !(backdrop.tvStatic && isTv());
    }

    function backdropNext() {
        // Paused while an item's page shows its own backdrop.
        if (bdBusy || !bdHost || document.hidden || bdDetailId || !backdropRotates()) {
            return;
        }
        bdBusy = true;
        // The endpoint redirects to a random backdrop; the final URL is what
        // goes into the layer (asking the endpoint again would give another).
        fetch(backdropUrl(), { credentials: 'same-origin', cache: 'no-store' })
            .then(function (res) {
                if (!res.ok) {
                    throw new Error(res.status);
                }
                return res.url;
            })
            .then(backdropShow)
            .catch(function () { /* no backdrop right now - keep the current one */ })
            .then(function () { bdBusy = false; });
    }

    function backdropStop() {
        if (bdTimer) {
            clearInterval(bdTimer);
            bdTimer = null;
        }
        if (bdHost) {
            bdHost.remove();
            bdHost = null;
            bdLayers = [];
        }
        document.documentElement.classList.remove('jellycanvas-js-backdrop');
    }

    function backdropSync() {
        if (!backdrop || disposed) {
            return;
        }
        var container = document.querySelector('.backgroundContainer');
        if (!container) {
            return;
        }
        if (!bdHost || bdHost.parentElement !== container) {
            backdropStop();
            bdHost = document.createElement('div');
            bdHost.className = 'jellycanvas-backdrop';
            bdLayers = [document.createElement('div'), document.createElement('div')];
            bdHost.appendChild(bdLayers[0]);
            bdHost.appendChild(bdLayers[1]);
            container.appendChild(bdHost);
            bdCurrent = 1;
            bdDetailId = null;
            if (backdropRotates()) {
                document.documentElement.classList.add('jellycanvas-js-backdrop');
                backdropNext();
                bdTimer = setInterval(backdropNext, backdrop.seconds * 1000);
            }
        }
        if (!backdrop.detail) {
            return;
        }
        // An item's page shows that item's backdrop; leaving it goes back
        // to the rotation (next random picture) or to the still background.
        var id = detailItemId();
        if (id === bdDetailId) {
            return;
        }
        // The client may not be signed in yet right after load; try again
        // on the next sync rather than remembering the page as done.
        if (id && !(window.ApiClient && window.ApiClient.getCurrentUserId())) {
            return;
        }
        bdDetailId = id;
        if (id) {
            backdropShowItem(id);
        } else if (backdropRotates()) {
            backdropNext();
        } else {
            backdropClear();
        }
    }

    // ------------------------------------------------------------------
    // Seerr rows on the home page: posters of requested (coming soon),
    // recently requested or trending titles, drawn as Jellyfin cards so the
    // theme's card settings apply. The data comes from the plugin's own
    // endpoint (the server talks to Seerr). A poster opens the item here
    // when the library has it, otherwise its page in Seerr.
    // ------------------------------------------------------------------
    var rowsData = {};
    var rowsLoading = {};

    // Default headings, in the viewer's language.
    var ROW_TITLES = {
        Upcoming: { en: 'Coming soon (requested)', cs: 'Brzy vyjde (požadované)', sk: 'Čoskoro vyjde (požadované)', de: 'Demnächst (angefragt)' },
        Recent: { en: 'Recently requested', cs: 'Naposledy požadované', sk: 'Naposledy požadované', de: 'Zuletzt angefragt' },
        Pending: { en: 'Waiting for approval', cs: 'Čeká na schválení', sk: 'Čaká na schválenie', de: 'Wartet auf Freigabe' },
        Available: { en: 'Requests now available', cs: 'Požadované – nově dostupné', sk: 'Požadované – novo dostupné', de: 'Angefragt – jetzt verfügbar' },
        Trending: { en: 'Trending', cs: 'Trendy', sk: 'Trendy', de: 'Im Trend' },
        PopularMovies: { en: 'Popular movies', cs: 'Populární filmy', sk: 'Populárne filmy', de: 'Beliebte Filme' },
        PopularTv: { en: 'Popular series', cs: 'Populární seriály', sk: 'Populárne seriály', de: 'Beliebte Serien' }
    };
    var ROW_WORDS = {
        en: { movie: 'Movie', tv: 'Series', requested: 'Requested', processing: 'Processing', partial: 'Partly available', available: 'Available', open: 'Open', seerr: 'Open in Seerr' },
        cs: { movie: 'Film', tv: 'Seriál', requested: 'Požadováno', processing: 'Zpracovává se', partial: 'Částečně dostupné', available: 'Dostupné', open: 'Otevřít', seerr: 'Otevřít v Seerru' },
        sk: { movie: 'Film', tv: 'Seriál', requested: 'Požadované', processing: 'Spracúva sa', partial: 'Čiastočne dostupné', available: 'Dostupné', open: 'Otvoriť', seerr: 'Otvoriť v Seerri' },
        de: { movie: 'Film', tv: 'Serie', requested: 'Angefragt', processing: 'In Bearbeitung', partial: 'Teilweise verfügbar', available: 'Verfügbar', open: 'Öffnen', seerr: 'In Seerr öffnen' }
    };

    function rowLang() {
        var l = (document.documentElement.lang || navigator.language || 'en').slice(0, 2).toLowerCase();
        return ROW_WORDS[l] ? l : 'en';
    }

    function rowWord(key) {
        return (ROW_WORDS[rowLang()] || ROW_WORDS.en)[key] || ROW_WORDS.en[key];
    }

    function rowTitle(row) {
        if (row.title) {
            return row.title;
        }
        var t = ROW_TITLES[row.kind] || {};
        return t[rowLang()] || t.en || row.kind;
    }

    function rowsContainer() {
        return document.querySelector('#indexPage:not(.hide) #homeTab .homeSectionsContainer');
    }

    function rowsStyle() {
        if (document.getElementById('jellycanvasRows-style')) {
            return;
        }
        var style = document.createElement('style');
        style.id = 'jellycanvasRows-style';
        style.textContent =
            '.jellycanvas-row .cardImageContainer { background-size: cover; background-position: center; }' +
            '.jellycanvas-row .jellycanvas-row-tag { position: absolute; top: 0.4em; right: 0.4em; padding: 0.2em 0.5em; font-size: 0.72em; font-weight: 700; color: #fff; background: rgba(0, 0, 0, 0.7); border-radius: 4px; pointer-events: none; }' +
            '.jellycanvas-row .jellycanvas-row-corner { position: absolute; top: 0.4em; left: 0.4em; display: flex; gap: 0.3em; pointer-events: none; }' +
            '.jellycanvas-row .jellycanvas-row-state { padding: 0.2em 0.5em; font-size: 0.72em; font-weight: 700; border-radius: 4px; background: rgba(0, 0, 0, 0.7); color: #fff; }' +
            '.jellycanvas-row .jellycanvas-row-state.is-available { background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, #000); }' +
            '.jellycanvas-row .jellycanvas-row-type { padding: 0.2em 0.5em; font-size: 0.72em; font-weight: 700; border-radius: 4px; background: rgba(0, 0, 0, 0.7); color: #fff; }' +
            // The same hover overlay as Jellyfin's cards: a dim with one
            // button in the middle (open here, or in Seerr).
            '.jellycanvas-row .cardOverlayContainer { position: absolute; top: 0; right: 0; bottom: 0; left: 0; display: flex; align-items: center; justify-content: center; background: rgba(0, 0, 0, 0.45); opacity: 0; transition: opacity 0.2s ease; pointer-events: none; }' +
            '.jellycanvas-row .card:hover .cardOverlayContainer, .jellycanvas-row .card:focus-within .cardOverlayContainer { opacity: 1; }' +
            '.jellycanvas-row .cardOverlayButton { width: 3em; height: 3em; border-radius: 50%; border: 0; display: inline-flex; align-items: center; justify-content: center; background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, #000); }' +
            '.jellycanvas-row .cardOverlayButton .material-icons { font-size: 1.6em; }' +
            '.jellycanvas-row .sectionTitleContainer { display: flex; align-items: center; }' +
            '.jellycanvas-row .jellycanvas-row-arrows { margin-left: auto; }' +
            // Jellyfin's "display: flex" on .emby-scrollbuttons would beat the hidden attribute.
            '.jellycanvas-row .jellycanvas-row-arrows[hidden] { display: none !important; }' +
            '.jellycanvas-row .jellycanvas-row-items::-webkit-scrollbar { display: none; }' +
            'html.layout-mobile .jellycanvas-row .jellycanvas-row-arrows, html.layout-tv .jellycanvas-row .jellycanvas-row-arrows { display: none; }';
        document.head.appendChild(style);
    }

    function rowsRemoveAll() {
        var all = document.querySelectorAll('.jellycanvas-row');
        for (var i = 0; i < all.length; i++) {
            all[i].remove();
        }
    }

    function rowState(item) {
        if (item.Status >= 5) {
            return 'available';
        }
        if (item.Status === 4) {
            return 'partial';
        }
        if (item.Status === 3) {
            return 'processing';
        }
        return 'requested';
    }

    function rowIsRequests(row) {
        return row.kind === 'Upcoming' || row.kind === 'Recent' || row.kind === 'Pending' || row.kind === 'Available';
    }

    /** The custom button Seerr links go through, if the settings name one that is on. */
    function seerrButton() {
        var id = CONFIG.seerrOpen || 0;
        if (!id) {
            return null;
        }
        for (var i = 0; i < buttons.length; i++) {
            if (buttons[i].id === id && buttons[i].action !== 'NewTab') {
                return buttons[i];
            }
        }
        return null;
    }

    /** Opens a Seerr page the way the button does: in its overlay (pointed at the page), or in place. */
    function openSeerrVia(b, url, event) {
        event.preventDefault();
        event.stopPropagation();
        if (b.action !== 'Overlay') {
            location.href = url;
            return;
        }
        closeAll();
        openFrame(b, url);
    }

    function rowCard(item, row) {
        var api = window.ApiClient;
        var card = document.createElement('div');
        card.className = 'card overflowPortraitCard card-hoverable card-withuserdata';
        card.setAttribute('data-jellycanvas', '1');
        card.setAttribute('data-type', item.Type === 'tv' ? 'Series' : 'Movie');
        var box = document.createElement('div');
        box.className = 'cardBox cardBox-bottompadded';
        var scalable = document.createElement('div');
        scalable.className = 'cardScalable';
        var padder = document.createElement('div');
        padder.className = 'cardPadder cardPadder-overflowPortrait';
        scalable.appendChild(padder);
        // Coming soon always leads to Seerr: a series there may already be
        // known to the library (some seasons in), but the row is about what
        // is still to come.
        var inLibrary = !!(item.JellyfinId && api) && row.kind !== 'Upcoming';
        var href = inLibrary ? '#/details?id=' + item.JellyfinId + '&serverId=' + api.serverId() : item.SeerrUrl;
        // A Seerr link opens in a new tab - or, when the admin picked a
        // custom button that points at Seerr, the way that button opens
        // (its overlay, or in place): no second Seerr tab then.
        var via = inLibrary ? null : seerrButton();
        var link = document.createElement('a');
        link.className = 'cardImageContainer coveredImage cardContent';
        link.href = href;
        if (via) {
            link.addEventListener('click', function (e) { openSeerrVia(via, href, e); });
        } else if (!inLibrary) {
            link.target = '_blank';
            link.rel = 'noopener';
        }
        if (item.Poster && api) {
            // Through the plugin (server-side fetch, cached), not TMDB directly.
            link.style.backgroundImage = 'url("' + api.getUrl('Jellycanvas/Seerr/Image', { p: item.Poster }) + '")';
        }
        // Top-left corner: "Movie" / "Series" (mixed rows only), then the request state.
        var state = rowState(item);
        var corner = document.createElement('span');
        corner.className = 'jellycanvas-row-corner';
        if (row.showType && (row.media || 'Both') === 'Both') {
            var ty = document.createElement('span');
            ty.className = 'jellycanvas-row-type';
            ty.textContent = rowWord(item.Type === 'tv' ? 'tv' : 'movie');
            corner.appendChild(ty);
        }
        // Coming soon is by definition not there yet - no state for it.
        if (row.showState && rowIsRequests(row) && row.kind !== 'Upcoming') {
            var st = document.createElement('span');
            st.className = 'jellycanvas-row-state is-' + state;
            st.textContent = state === 'available' ? '✓' : state === 'processing' ? '…' : state === 'partial' ? '½' : '+';
            st.title = rowWord(state);
            corner.appendChild(st);
        }
        if (corner.childNodes.length) {
            link.appendChild(corner);
        }
        if (row.showDate && item.Date) {
            var tag = document.createElement('span');
            tag.className = 'jellycanvas-row-tag';
            tag.textContent = row.kind === 'Upcoming' ? item.Date : item.Date.slice(0, 4);
            link.appendChild(tag);
        }
        scalable.appendChild(link);
        // Hover: the same kind of overlay Jellyfin's cards have, with one
        // button - to the item here, or to Seerr.
        var overlay = document.createElement('div');
        overlay.className = 'cardOverlayContainer';
        var btn = document.createElement('a');
        btn.className = 'cardOverlayButton';
        btn.href = href;
        btn.title = rowWord(inLibrary ? 'open' : 'seerr');
        if (via) {
            btn.addEventListener('click', function (e) { openSeerrVia(via, href, e); });
        } else if (!inLibrary) {
            btn.target = '_blank';
            btn.rel = 'noopener';
        }
        btn.innerHTML = '<span class="material-icons ' + (inLibrary ? 'play_arrow' : 'open_in_new') + '" aria-hidden="true"></span>';
        overlay.appendChild(btn);
        scalable.appendChild(overlay);
        box.appendChild(scalable);
        if (row.showTitle) {
            var text = document.createElement('div');
            text.className = 'cardText cardTextCentered cardText-first';
            text.textContent = item.Title;
            box.appendChild(text);
        }
        // Under the title: the year and / or who requested it, each on its own tick.
        var parts = [];
        if (row.showSubtitle && item.Date) {
            parts.push(item.Date.slice(0, 4));
        }
        if (row.showRequester && rowIsRequests(row) && item.RequestedBy) {
            parts.push(item.RequestedBy);
        }
        if (parts.length) {
            var sub = document.createElement('div');
            sub.className = 'cardText cardTextCentered cardText-secondary';
            sub.textContent = parts.join(' · ');
            box.appendChild(sub);
        }
        card.appendChild(box);
        return card;
    }

    function rowsRender() {
        var container = rowsContainer();
        if (!container) {
            return;
        }
        rowsStyle();
        // Build (or keep) every row's element, then put them where they
        // belong in the settings' order: the top rows one after another
        // right after the slideshow, the bottom rows at the very end.
        var top = [];
        var bottom = [];
        rows.forEach(function (row) {
            var items = rowsData[row.id];
            var id = 'jellycanvasRow-' + row.id;
            var el = document.getElementById(id);
            if (!items || !items.length) {
                if (el) {
                    el.remove();
                }
                return;
            }
            if (!el) {
                el = document.createElement('div');
                el.id = id;
                el.className = 'verticalSection jellycanvas-row';
                el.setAttribute('data-jellycanvas', '1');
                el.setAttribute('data-position', row.position);
                var head = document.createElement('div');
                head.className = 'sectionTitleContainer sectionTitleContainer-cards padded-left';
                var h2 = document.createElement('h2');
                h2.className = 'sectionTitle sectionTitle-cards';
                h2.textContent = rowTitle(row);
                head.appendChild(h2);
                el.appendChild(head);
                // Not ".itemsContainer": the home tab calls pause() / resume()
                // on every element with that class (its own custom element),
                // and a plain div would throw.
                var scroller = document.createElement('div');
                scroller.className = 'jellycanvas-row-items scrollX hiddenScrollX padded-left padded-right';
                scroller.style.cssText = 'display:flex;overflow-x:auto;white-space:nowrap;scroll-behavior:smooth;scrollbar-width:none;';
                items.forEach(function (item) { scroller.appendChild(rowCard(item, row)); });
                el.appendChild(scroller);
                // The arrows Jellyfin's rows have at the right of the title
                // (its classes, so they look and hide - on phones - the same).
                var arrows = document.createElement('div');
                arrows.className = 'emby-scrollbuttons padded-right jellycanvas-row-arrows';
                [['chevron_left', -1], ['chevron_right', 1]].forEach(function (a) {
                    var b = document.createElement('button');
                    b.type = 'button';
                    b.className = 'emby-scrollbuttons-button paper-icon-button-light';
                    b.innerHTML = '<span class="material-icons ' + a[0] + '" aria-hidden="true"></span>';
                    b.addEventListener('click', function () {
                        scroller.scrollBy({ left: a[1] * Math.round(scroller.clientWidth * 0.85), behavior: 'smooth' });
                    });
                    arrows.appendChild(b);
                });
                head.appendChild(arrows);
            }
            // The arrows only when there is something to scroll to.
            var arrowsEl = el.querySelector('.jellycanvas-row-arrows');
            var scrollerEl = el.querySelector('.jellycanvas-row-items');
            if (arrowsEl && scrollerEl) {
                arrowsEl.hidden = scrollerEl.scrollWidth <= scrollerEl.clientWidth + 2;
            }
            (row.position === 'Top' ? top : bottom).push(el);
        });
        var prev = document.getElementById(SS_ID);
        top.forEach(function (el) {
            var want = prev ? prev.nextSibling : container.firstChild;
            if (el.parentElement !== container || el !== want) {
                container.insertBefore(el, want);
            }
            prev = el;
        });
        // Bottom rows: the last one ends the container, each earlier one
        // sits right before the next.
        for (var i = bottom.length - 1; i >= 0; i--) {
            var follower = i + 1 < bottom.length ? bottom[i + 1] : null;
            if (bottom[i].parentElement !== container || bottom[i].nextSibling !== follower) {
                container.insertBefore(bottom[i], follower);
            }
        }
    }

    function rowsLoad(row) {
        var api = window.ApiClient;
        if (!api || !api.getCurrentUserId() || rowsLoading[row.id] || rowsData[row.id]) {
            return;
        }
        rowsLoading[row.id] = true;
        api.getJSON(api.getUrl('Jellycanvas/Seerr/' + row.kind.toLowerCase(), { limit: row.limit, media: (row.media || 'Both').toLowerCase() })).then(function (items) {
            rowsData[row.id] = items || [];
            rowsLoading[row.id] = false;
            rowsRender();
        }, function () {
            rowsData[row.id] = [];
            rowsLoading[row.id] = false;
        });
    }

    function rowsSync() {
        if (!rows.length || disposed) {
            return;
        }
        if (!rowsContainer()) {
            return;
        }
        rows.forEach(rowsLoad);
        rowsRender();
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

    // The designer's preview runs this script too, on the same origin as
    // the real site: a close clicked there must not hide the strip for the
    // admin everywhere, and a strip closed on the site must still show in
    // the preview. The designer marks its frame.
    function inPreview() {
        return window.__jellycanvasPreview === true;
    }

    function infoDismissed() {
        if (inPreview() || !infoBar.remember) {
            return false;
        }
        try {
            return localStorage.getItem(INFO_KEY) === infoBar.text;
        } catch (e) {
            return false;
        }
    }

    function closeInfoBar() {
        try {
            if (!inPreview() && infoBar.remember) {
                localStorage.setItem(INFO_KEY, infoBar.text);
            }
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
        // The overlay of a custom button covers the strip; a close button
        // floating above the overlay would be the one thing left of it.
        var covered = buttons.some(isOpen);
        if (!box || covered) {
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
        // The TV layout has no MUI header: it keeps the old .skinHeader,
        // whose .headerTop row (logo left, icon buttons right) is the bar.
        // On the desktop that element is still in the DOM but empty.
        var legacy = document.querySelector('.skinHeader .headerTop');
        if (legacy && visible(legacy) && legacy.getBoundingClientRect().height > 0) {
            return legacy;
        }
        return null;
    }

    function isLegacyBar(bar) {
        return bar.classList.contains('headerTop');
    }

    function iconGroup(bar) {
        if (isLegacyBar(bar)) {
            return bar.querySelector('.headerRight');
        }
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
        if (isLegacyBar(bar)) {
            // The TV tabs are driven by their index, a foreign one would
            // confuse them; links go next to the logo instead.
            return bar.querySelector('.headerLeft');
        }
        return bar.querySelector(':scope > .MuiStack-root');
    }

    function templateIconButton(bar) {
        var c = bar.querySelectorAll(isLegacyBar(bar) ? '.headerButton' : '.MuiIconButton-root');
        for (var i = 0; i < c.length; i++) {
            if (visible(c[i]) && !c[i].hasAttribute('data-jellycanvas')) {
                return c[i];
            }
        }
        return null;
    }

    function templateNavLink(bar) {
        if (isLegacyBar(bar)) {
            return templateIconButton(bar);
        }
        return bar.querySelector('.MuiStack-root > a.MuiButton-sizeMedium:not([data-jellycanvas])');
    }

    // A template's own role classes (headerSearchButton, headerCastButton)
    // must not travel with its look: the theme may hide those.
    function templateClasses(tpl) {
        return tpl.className.split(/\s+/).filter(function (c) {
            return c && !/^(header(Search|Cast|Sync|User|Home|Back)|syncButton|castButton|searchButton|userButton)/.test(c);
        }).join(' ');
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

    function openFrame(b, url) {
        var f = document.getElementById(frameId(b));
        if (!f) {
            f = document.createElement('iframe');
            f.id = frameId(b);
            f.src = url || b.url;
            f.setAttribute('title', b.label);
            document.body.appendChild(f);
        } else if (url && f.getAttribute('src') !== url) {
            f.setAttribute('src', url); // a kept-alive frame shows its last page; point it at this one
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
        // A wider window may fit a whole Seerr row: its arrows go, or come back.
        if (rows.length && rowsContainer()) {
            rowsRender();
        }
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

        // The label is text, never markup (it comes from the settings, but
        // the script runs for every user).
        if (b.placement === 'Nav' && isLegacyBar(bar)) {
            // On TV a link is an icon button with its label beside the icon.
            el.innerHTML = '<span class="material-icons" aria-hidden="true">' + b.icon + '</span><span style="margin-left:0.35em;font-size:0.8em;white-space:nowrap"></span>';
            el.lastChild.textContent = b.label;
        } else if (b.placement === 'Nav') {
            // Same structure as Jellyfin's own links (icon in a
            // MuiButton-startIcon wrapper), so themes that style or collapse
            // the links treat ours the same way.
            el.innerHTML = '<span class="MuiButton-icon MuiButton-startIcon"><span class="material-icons" aria-hidden="true" style="font-size:1.25em">' + b.icon + '</span></span>';
            el.appendChild(document.createTextNode(b.label));
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
            el.className = templateClasses(tpl);
            el.style.cssText = '';
            if (b.placement === 'Nav' && isLegacyBar(bar)) {
                // The TV icon button is a fixed square; a labelled one is wider.
                el.style.cssText = 'width:auto;padding:0 0.7em;border-radius:999px;';
            }
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
    // "next" is the custom button that follows this one on the same side
    // (already in the bar), or null for the last one. Two buttons on one
    // side used to fight for the same spot - each sync moved one of them,
    // and a node moved between mousedown and mouseup never gets its click.
    function placeButton(el, b, bar, next) {
        var group = b.placement === 'Nav' ? navGroup(bar) : iconGroup(bar);
        if (!group) {
            if (el.parentElement !== bar) {
                bar.appendChild(el);
            }
            return;
        }

        // Links go at the end of their group; icons right before Search
        // when it exists (library pages link to "#/search?parentId=...",
        // hence the prefix match; the TV header has a .headerSearchButton).
        var anchor = next && next.parentElement === group ? next
            : b.placement === 'Nav' ? null
            : group.querySelector('a[href^="#/search"], .headerSearchButton');
        var correct = el.parentElement === group && (anchor ? el.nextElementSibling === anchor : el === group.lastElementChild);
        if (!correct) {
            group.insertBefore(el, anchor);
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
        backdropSync();
        rowsSync();
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
                // Only the bar button goes - removeButton() would also take
                // the overlay the drawer entry has just opened.
                var barButton = document.getElementById(buttonId(b));
                if (barButton) {
                    barButton.remove();
                }
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
            var following = null;
            for (var n = buttons.indexOf(b) + 1; n < buttons.length && !following; n++) {
                if (buttons[n].placement === b.placement) {
                    following = document.getElementById(buttonId(buttons[n]));
                }
            }
            placeButton(el, b, bar, following);
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
            case 'TopRated':
                q.SortBy = 'CommunityRating,Random';
                q.SortOrder = 'Descending';
                q.MinCommunityRating = 1;
                break;
            case 'NewReleases':
                q.SortBy = 'PremiereDate,DateCreated';
                q.SortOrder = 'Descending';
                break;
            case 'Unplayed':
                q.Filters = 'IsUnplayed';
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

    /** The button's look from the settings: fill, radius and size. */
    function ssButtonLook() {
        var radius = slideshow.buttonRadius >= 0 ? slideshow.buttonRadius + 'px' : 'var(--jf-card-borderRadius, 0.2em)';
        var look = 'border-radius: ' + radius + '; font-size: ' + (slideshow.buttonScale || 100) / 100 + 'em; ';
        switch (slideshow.buttonStyle) {
            case 'Outline':
                return look + 'background: transparent; color: #fff; border: 2px solid rgba(255, 255, 255, 0.85);';
            case 'Glass':
                return look + 'background: rgba(255, 255, 255, 0.16); color: #fff; border: 1px solid rgba(255, 255, 255, 0.3); -webkit-backdrop-filter: blur(10px); backdrop-filter: blur(10px);';
            case 'Light':
                return look + 'background: #fff; color: #111;';
            default:
                return look + 'background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, #000);';
        }
    }

    function ssStyle() {
        if (document.getElementById(SS_ID + '-style')) {
            return;
        }
        var style = document.createElement('style');
        style.id = SS_ID + '-style';
        style.textContent =
            '#' + SS_ID + ' { position: relative; height: ' + slideshow.height + 'vh; min-height: 260px; margin: 0 0 1.5em; border-radius: var(--jf-card-borderRadius, 0.2em); overflow: hidden; background: #000; contain: layout paint; }' +
            '#' + SS_ID + ' .jcs-slide { position: absolute; top: 0; right: 0; bottom: 0; left: 0; opacity: 0; transition: opacity 0.9s ease; pointer-events: none; }' +
            '#' + SS_ID + ' .jcs-slide.is-active { opacity: 1; pointer-events: auto; }' +
            '#' + SS_ID + ' .jcs-bg { position: absolute; top: 0; right: 0; bottom: 0; left: 0; width: 100%; height: 100%; object-fit: cover; }' +
            '#' + SS_ID + ' .jcs-shade { position: absolute; top: 0; right: 0; bottom: 0; left: 0; background: linear-gradient(90deg, rgba(0,0,0,0.82) 0%, rgba(0,0,0,0.45) 45%, rgba(0,0,0,0) 75%), linear-gradient(0deg, rgba(0,0,0,0.75) 0%, rgba(0,0,0,0) 40%); }' +
            '#' + SS_ID + ' .jcs-text { position: absolute; left: 4%; right: 30%; bottom: 10%; color: #fff; text-shadow: 0 2px 8px rgba(0,0,0,0.6); }' +
            '#' + SS_ID + ' .jcs-logo { max-width: min(420px, 45%); max-height: 22%; margin-bottom: 0.8em; display: block; }' +
            '#' + SS_ID + ' .jcs-title { font-size: 2.2em; font-weight: 700; margin: 0 0 0.2em; line-height: 1.1; }' +
            '#' + SS_ID + ' .jcs-sub { opacity: 0.85; margin: 0 0 0.6em; font-size: 1em; }' +
            '#' + SS_ID + ' .jcs-overview { font-size: 1em; line-height: 1.45; max-height: 4.4em; overflow: hidden; margin: 0 0 1em; opacity: 0.92; }' +
            '#' + SS_ID + ' .jcs-btn { display: inline-flex; align-items: center; gap: 0.4em; padding: 0.6em 1.2em; font-weight: 600; text-decoration: none; ' + ssButtonLook() + ' }' +
            '#' + SS_ID + ' .jcs-btn .material-icons { font-size: 1.25em; }' +
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
                if (slideshow.buttonIcon) {
                    var icon = document.createElement('span');
                    icon.className = 'material-icons';
                    icon.setAttribute('aria-hidden', 'true');
                    icon.textContent = slideshow.buttonIcon;
                    btn.appendChild(icon);
                }
                btn.appendChild(document.createTextNode(slideshow.buttonLabel || (item.Type === 'Episode' ? (item.Name || '') : ssTitle(item))));
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
        // The rects are viewport-relative and the page may be scrolled (this
        // runs on every sync): measure against where the slideshow sits
        // with the page at the top, or the margin would grow with every
        // scroll and push the whole page down.
        var scrolled = window.pageYOffset || 0;
        for (var p = root.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            scrolled += p.scrollTop || 0; // a scrolling page container, if the client has one
        }
        var overlap = hb.bottom - (rb.top + scrolled - current);
        // A sidebar (bar on the left) does not overlap at all.
        var margin = hb.width < window.innerWidth * 0.5 || overlap <= 0 ? '' : Math.round(overlap) + 'px';
        if (root.style.marginTop !== margin) {
            root.style.marginTop = margin;
        }
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
            // Hover for the toolbar buttons and drawer entries only - the
            // slideshow, the Seerr rows and their cards carry the marker
            // too, and a highlight across a whole row is not wanted.
            'a[data-jellycanvas], button[data-jellycanvas], li[data-jellycanvas] { cursor: pointer; transition: background-color 0.15s ease, color 0.15s ease; }' +
            'a[data-jellycanvas]:hover, a[data-jellycanvas]:focus-visible, button[data-jellycanvas]:hover, button[data-jellycanvas]:focus-visible, li[data-jellycanvas]:hover, li[data-jellycanvas]:focus-visible { background-color: rgba(var(--jf-palette-text-primaryChannel, 255 255 255) / 0.12) !important; }' +
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
        // A font that arrives after the badges were placed changes their
        // widths: place them again (the pills' icons are SVG, but the text
        // badges follow the page font).
        if (document.fonts && document.fonts.addEventListener) {
            document.fonts.addEventListener('loadingdone', function () {
                if (badges && !disposed) {
                    badgesRemoveAll();
                    badgeSync();
                }
            });
        }
        sync();
    }

    if (document.body) {
        start();
    } else {
        document.addEventListener('DOMContentLoaded', start);
    }
})();
