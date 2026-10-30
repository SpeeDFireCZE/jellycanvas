(function () {
    // Jellycanvas preview guard. Inlined at the top of every client's
    // index.html (File Transformation) and run before any of the client's
    // own code; it does something only in the settings page's preview frame
    // (an iframe with data-jellycanvas-preview, see configPage.js). Without
    // File Transformation the settings page runs it in the frame itself, as
    // early as it can.
    //
    // The preview is a client of its own that shares localStorage with every
    // tab of the browser - and its device id with the admin's tab:
    //  - "Remember me" unticked: a starting client drops the stored token
    //    and asks for the password. The preview reads auto login as on.
    //  - A client whose check of the token is cut short (the preview moved
    //    to another page meanwhile) drops the token and saves that, and the
    //    sign-in form saves the box's state: the preview's writes of the
    //    sign-in (and of the cached user, "user-...") to localStorage go
    //    nowhere; the client keeps them in memory. (Its mirror of them in
    //    Cache Storage, "embydata", is out of reach - nothing reads it.)
    //  - Signing in or out sends the server a sign-in for this device, which
    //    ends the admin's own session: off in the preview.
    // Old browsers (TVs) run this file too: ES5 only, and never an error.
    try {
        var frame = window.frameElement;
        if (!frame || !frame.hasAttribute('data-jellycanvas-preview') || window.__jellycanvasPreviewGuard) {
            return;
        }
        var readOnly = function (key) {
            return key === 'enableAutoLogin' || key === 'jellyfin_credentials' || /^user-/.test(String(key));
        };
        var proto = window.Storage.prototype;
        var getItem = proto.getItem;
        var setItem = proto.setItem;
        var removeItem = proto.removeItem;
        proto.getItem = function (key) {
            return key === 'enableAutoLogin' ? 'true' : getItem.call(this, key);
        };
        proto.setItem = function (key, value) {
            if (!readOnly(key)) {
                setItem.call(this, key, value);
            }
        };
        proto.removeItem = function (key) {
            if (!readOnly(key)) {
                removeItem.call(this, key);
            }
        };
        proto.clear = function () {
            // the storage belongs to every tab, not to the preview
        };

        var signInOrOut = /\/(Users\/AuthenticateByName|Users\/AuthenticateWithQuickConnect|Sessions\/Logout)\/?([?#;]|$)/i;
        var refusal = 'Jellycanvas preview: signing in or out is off here (it would end your own session)';
        if (window.fetch) {
            var fetch0 = window.fetch;
            window.fetch = function (input) {
                var url = typeof input === 'string' ? input : (input && input.url) || String(input);
                if (signInOrOut.test(url)) {
                    return Promise.reject(new TypeError(refusal));
                }
                return fetch0.apply(this, arguments);
            };
        }
        // A refused request goes to a blob that does not exist instead:
        // nothing leaves the browser, and it fails as a network error does,
        // with the whole run of events (readystatechange to 4, error,
        // loadend) that any caller waits for. ("data:," would succeed.)
        var xhr = window.XMLHttpRequest.prototype;
        var open0 = xhr.open;
        var nowhere = 'blob:' + window.location.protocol + '//' + window.location.host + '/00000000-0000-0000-0000-000000000000';
        xhr.open = function (method, url) {
            var args = Array.prototype.slice.call(arguments);
            if (signInOrOut.test(String(url))) {
                args[1] = nowhere;
            }
            return open0.apply(this, args);
        };
        // Last: a guard that broke halfway is tried again (by the settings page).
        window.__jellycanvasPreviewGuard = true;
    } catch (e) {
        // a guard that breaks the client would be worse than none
    }
})();
