// The Filmes, Séries, Animes and Coleções pages, and the page of a collection, are the home page showing only their titles:
// #/home?topParentId=<id>. The header marks the right tab by itself. The original grid stays one click away.
(function () {
    var LIBRARY = /^#\/(movies|tv|mixed|boxsets|homevideos)\?/;
    var DETAILS = /^#\/details\?/;
    var GRID_KEY = 'encore-grid-library'; // the library whose grid was asked for: its links keep the grid until a menu click
    var HOME = /^#\/home(\.html)?(\?|$)/;
    var known = null, asked = false, lastHash = null;
    var saved = {}, last = null, restoring = null; // where each page was left, for the way back to it
    var emptySince = null, healed = 0;
    var redrawFor = null, REDRAW_WAIT = 5000;

    function params(hash) {
        return new URLSearchParams((hash || '').split('?')[1] || '');
    }

    function storage(action, value) {
        try {
            return action === 'get' ? sessionStorage.getItem(GRID_KEY) : action === 'set' ? sessionStorage.setItem(GRID_KEY, value) : sessionStorage.removeItem(GRID_KEY);
        } catch (e) {
            return null;
        }
    }

    function isView(hash) {
        return /^#\/home\?/.test(hash) && params(hash).has('topParentId') && !params(hash).has('tab');
    }

    function collectionUrl(id) {
        return '#/home?topParentId=' + (known && known.CollectionsLibrary) + '&encoreCollection=' + id;
    }

    function ask() {
        if (asked || !window.ApiClient || !window.ApiClient.getCurrentUserId || !window.ApiClient.getCurrentUserId()) {
            return;
        }
        asked = true;
        window.ApiClient.getJSON(window.ApiClient.getUrl('HomeScreen/Encore/Views', { UserId: window.ApiClient.getCurrentUserId() })).then(function (response) {
            known = response;
        }, function () {
            asked = false;
        });
    }

    // Where a link should go, or null to leave it alone.
    function target(hash, fromMenu) {
        var query = params(hash), id;
        if (!known || !known.Enabled) {
            return null; // with the plugin off, or before it answers, the original pages stay
        }
        if (LIBRARY.test(hash) && (id = query.get('topParentId'))) {
            if (!fromMenu && storage('get') === id) {
                return null;
            }
            return '#/home?topParentId=' + id + (hash.indexOf('#/boxsets') === 0 ? '&encoreKind=boxsets' : '');
        }
        if (DETAILS.test(hash) && (id = (query.get('id') || '').replace(/-/g, '')) && known.Collections.indexOf(id) !== -1) {
            return collectionUrl(id);
        }
        return null;
    }

    function current() {
        var hash = location.hash;
        return isView(hash) ? params(hash).get('encoreCollection') || params(hash).get('topParentId') : null;
    }

    function gridUrl(hash) {
        var query = params(hash), collection = query.get('encoreCollection'), id = query.get('topParentId');
        if (collection) {
            return '#/list?parentId=' + collection + '&serverId=' + window.ApiClient.serverId();
        }
        var link = document.querySelector('a[href*="topParentId=' + id + '"]:not(#encoreSeeAll a)');
        return link ? link.getAttribute('href') : null;
    }

    // The home page stays open between these pages, and only redraws when told its settings changed.
    function redraw() {
        if (!/^#\/home(\?|$)/.test(location.hash)) {
            return; // leaving for a title: the page is still in view, and keeps its rows and position for the way back
        }
        var sections = document.querySelector('.homePage:not(.hide) .sections');
        if (!sections || sections.dataset.encoreView === undefined || params(location.hash).has('tab')) {
            return;
        }
        if (sections.dataset.encoreView !== (current() || '')) {
            // The redraw only marks the page when it starts, which takes a while on a busy page: asking again would load it twice.
            if (redrawFor && redrawFor.hash === location.hash && Date.now() - redrawFor.at < REDRAW_WAIT) {
                return;
            }
            redrawFor = { hash: location.hash, at: Date.now() };
            sections.dispatchEvent(new CustomEvent('settingschange'));
            (document.scrollingElement || document.body).scrollTop = 0;
            document.body.scrollTop = 0;
        }
    }

    // The last resort for a page left without rows, whatever the cause: draw it again, at most twice per visit.
    function healEmptyPage() {
        var sections = document.querySelector('.homePage:not(.hide) .sections');
        if (!HOME.test(location.hash) || !sections) {
            emptySince = null;
            return;
        }
        var rows = sections.querySelectorAll('.verticalSection');
        for (var i = 0; i < rows.length; i++) {
            if (rows[i].offsetHeight > 0 && rows[i].querySelector('.sectionTitle')) {
                emptySince = null;
                return;
            }
        }
        emptySince = emptySince || Date.now();
        if (Date.now() - emptySince > 10000 && healed < 2) {
            healed++;
            emptySince = null;
            sections.dispatchEvent(new CustomEvent('settingschange'));
        }
    }

    function drawnHere() {
        var sections = document.querySelector('.homePage:not(.hide) .sections');
        return HOME.test(location.hash) && window.HssPageMeta && sections && sections.dataset.encoreView === (current() || '') && sections.querySelector('.verticalSection');
    }

    function remember() {
        if (!restoring && drawnHere()) {
            last = { hash: location.hash, scroll: window.scrollY, pageHash: window.HssPageMeta.PageHash };
        }
    }

    // Going back shows the page as it was left: same rows in the same order, at the same height. Scrolling down also loads
    // the pages of rows on the way, so it repeats until the height is reached. A menu or link click opens the page anew.
    function restore() {
        if (!restoring) {
            return;
        }
        if (location.hash !== restoring.hash || Date.now() > restoring.until) {
            restoring = null;
            return;
        }
        if (!drawnHere() || window.HssPageMeta.PageHash !== restoring.pageHash) {
            return;
        }
        window.scrollTo(0, restoring.scroll);
        restoring.steady = Math.abs(window.scrollY - restoring.scroll) < 5 ? restoring.steady + 1 : 0;
        if (restoring.steady >= 4) {
            restoring = null;
        }
    }

    ['wheel', 'touchstart', 'mousedown', 'keydown'].forEach(function (name) {
        window.addEventListener(name, function () {
            // The user took over. A swipe back on a trackpad still sends wheel events for a moment after it ends.
            if (restoring && (name !== 'wheel' || Date.now() - restoring.started > 1500)) {
                restoring = null;
            }
        }, { capture: true, passive: true });
    });
    window.addEventListener('scroll', remember, { passive: true });
    setInterval(restore, 150);

    function seeAll() {
        document.body.classList.toggle('encore-view', !!current());
        var sections = document.querySelector('.homePage:not(.hide) .homeSectionsContainer');
        if (!sections || !current()) {
            return;
        }
        var url = gridUrl(location.hash);
        if (!url) {
            return;
        }
        var existing = sections.querySelector('#encoreSeeAll a');
        if (existing) {
            if (existing.getAttribute('href') !== url) {
                existing.setAttribute('href', url); // the same rows area can already be showing another page
            }
            return;
        }
        var row = document.createElement('div');
        row.id = 'encoreSeeAll';
        row.className = 'verticalSection';
        row.style.cssText = 'order:-1;display:flex;justify-content:flex-end;padding:0 3.3%';
        var link = document.createElement('a');
        link.href = url;
        link.className = 'emby-button button-flat encoreSeeAllLink';
        link.textContent = 'Ver tudo';
        link.addEventListener('click', function () {
            if (!params(location.hash).has('encoreCollection')) {
                storage('set', params(location.hash).get('topParentId'));
            }
        }, true);
        row.appendChild(link);
        sections.insertBefore(row, sections.firstChild);
    }

    // The label of the grid of a mixed library is "Folders", which says nothing here.
    function nameMixedGrid() {
        if (location.hash.indexOf('#/mixed') !== 0) {
            return;
        }
        document.querySelectorAll('[class*="MuiButton"]').forEach(function (button) {
            var walker = document.createTreeWalker(button, NodeFilter.SHOW_TEXT), node;
            while ((node = walker.nextNode())) {
                if (node.nodeValue === 'Pastas') {
                    node.nodeValue = 'Todos';
                }
            }
        });
    }

    // A details page that was still opening when its link was sent here finishes on top of the rows.
    function hideOtherPages() {
        if (current()) {
            document.querySelectorAll('.page:not(.homePage):not(.hide)').forEach(function (page) {
                page.classList.add('hide');
            });
        }
    }

    window.addEventListener('click', function (event) {
        var link = event.target.closest && event.target.closest('a[href]');
        if (!link || link.closest('#encoreSeeAll')) {
            return;
        }
        var hash = link.getAttribute('href'), goTo = target(hash, true);
        delete saved[hash];
        if (!goTo && HOME.test(hash) && HOME.test(location.hash)) {
            // The router waits for a new page to appear before the next navigation, and here the same page stays open: it
            // would hold every click after this one until some other page opened, and then run them late.
            goTo = hash;
        }
        if (goTo) {
            delete saved[goTo];
            event.preventDefault();
            event.stopImmediatePropagation();
            storage('remove');
            if (goTo === location.hash) {
                window.scrollTo(0, 0);
            } else {
                location.hash = goTo;
            }
        }
    }, true);

    function check() {
        ask();
        var hash = location.hash;
        if (hash !== lastHash) {
            lastHash = hash;
            emptySince = null;
            healed = 0;
            if (last && last.hash !== hash) {
                saved[last.hash] = last;
                last = null;
            }
            restoring = null;
            if (saved[hash] && saved[hash].scroll > 0) {
                restoring = { hash: hash, scroll: saved[hash].scroll, pageHash: saved[hash].pageHash, steady: 0, started: Date.now(), until: Date.now() + 15000 };
            }
            var goTo = target(hash, false);
            if (goTo) {
                location.replace(goTo);
                return;
            }
        }
        hideOtherPages();
        remember();
        redraw();
        healEmptyPage();
        seeAll();
        nameMixedGrid();
    }

    window.EncoreViews = {
        current: current,
        collectionUrl: collectionUrl,
        savedPageHash: function () {
            return saved[location.hash] ? saved[location.hash].pageHash : null;
        }
    };
    window.addEventListener('hashchange', check);
    setInterval(check, 300); // the app also navigates without a hashchange event
})();
