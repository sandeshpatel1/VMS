(function () {
    'use strict';
    var root = document.documentElement;
    var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---- Day / night theme -------------------------------------------------
    function paint(theme, persist) {
        root.dataset.theme = theme;
        if (persist) { try { localStorage.setItem('dwaarix-theme', theme); } catch (e) { } }
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.content = theme === 'dark' ? '#0A1226' : '#EDF1F9';
        document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
            b.setAttribute('aria-pressed', String(theme === 'dark'));
            b.setAttribute('aria-label', theme === 'dark' ? 'Switch to day theme' : 'Switch to night theme');
        });
    }
    paint(root.dataset.theme === 'dark' ? 'dark' : 'light', false);

    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var next = root.dataset.theme === 'dark' ? 'light' : 'dark';
            if (reduce) { paint(next, true); return; }

            if (!document.startViewTransition) {           // fallback: colours cross-fade
                root.classList.add('theme-anim');
                paint(next, true);
                setTimeout(function () { root.classList.remove('theme-anim'); }, 600);
                return;
            }
            // Circular reveal that grows out of the button, so the change has a visible origin.
            var r = btn.getBoundingClientRect();
            var x = r.left + r.width / 2, y = r.top + r.height / 2;
            var end = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
            var vt = document.startViewTransition(function () { paint(next, true); });
            vt.ready.then(function () {
                root.animate(
                    { clipPath: ['circle(0px at ' + x + 'px ' + y + 'px)', 'circle(' + end + 'px at ' + x + 'px ' + y + 'px)'] },
                    { duration: 700, easing: 'cubic-bezier(.2,.9,.25,1)', pseudoElement: '::view-transition-new(root)' });
            }).catch(function () { });
        });
    });

    // ---- 3D page exit: the page tips back into depth before navigating ------
    document.addEventListener('click', function (e) {
        if (reduce || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (!a || a.target || a.hasAttribute('download')) return;
        var raw = a.getAttribute('href') || '';
        if (/^(tel:|mailto:|javascript:|#)/i.test(raw)) return;
        var u = new URL(a.href, location.href);
        if (u.origin !== location.origin || u.pathname === location.pathname && u.search === location.search && u.hash) return;
        if (!document.querySelector('.main')) return;
        e.preventDefault();
        document.body.classList.remove('drawer-open');
        document.body.classList.add('leaving');
        setTimeout(function () { location.href = a.href; }, 240);
    });
    window.addEventListener('pageshow', function () { document.body.classList.remove('leaving'); });

    // ---- Tile tilt: follows the pointer, with a soft specular highlight ------
    if (!reduce && window.matchMedia('(hover: hover)').matches) {
        document.querySelectorAll('.tile').forEach(function (el) {
            el.addEventListener('pointermove', function (e) {
                var r = el.getBoundingClientRect();
                var px = (e.clientX - r.left) / r.width - .5, py = (e.clientY - r.top) / r.height - .5;
                el.style.transition = 'box-shadow .3s';
                el.style.transform = 'perspective(800px) rotateX(' + (-py * 9).toFixed(2) + 'deg) rotateY(' + (px * 9).toFixed(2) + 'deg) translateZ(8px)';
                el.style.setProperty('--gx', ((px + .5) * 100).toFixed(1) + '%');
                el.style.setProperty('--gy', ((py + .5) * 100).toFixed(1) + '%');
            });
            el.addEventListener('pointerleave', function () { el.style.transition = ''; el.style.transform = ''; });
        });
    }

    // ---- Sidebar rail: collapse to icons on desktop, remembered between visits ----
    var railBtn = document.querySelector('[data-rail-toggle]');
    if (railBtn) {
        var syncRail = function () {
            var on = root.dataset.rail === '1';
            railBtn.setAttribute('aria-pressed', String(on));
            var text = on ? 'Expand sidebar' : 'Collapse sidebar';
            railBtn.setAttribute('aria-label', text); railBtn.title = text;
        };
        syncRail();
        railBtn.addEventListener('click', function () {
            var on = root.dataset.rail !== '1';
            if (on) root.dataset.rail = '1'; else delete root.dataset.rail;
            try { localStorage.setItem('dwaarix-rail', on ? '1' : '0'); } catch (e) { }
            syncRail();
        });
    }

    // ---- Notification tray: bell opens a small panel, "View all" opens the page ----
    var trayBtn = document.querySelector('[data-tray-toggle]');
    var tray = document.getElementById('notifTray');
    if (trayBtn && tray) {
        var closeTimer = null;
        var loadTray = function () {
            return fetch(tray.getAttribute('data-url'), { credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' } })
                .then(function (r) { if (!r.ok) throw new Error(); return r.text(); })
                .then(function (html) { tray.innerHTML = html; })
                .catch(function () {
                    tray.innerHTML = '<div class="tray-empty">Could not load notifications.<br><a href="' + trayBtn.getAttribute('href') + '">Open the full page</a></div>';
                });
        };
        var openTray = function () {
            clearTimeout(closeTimer);
            tray.classList.remove('closing');
            tray.innerHTML = '<div class="tray-empty">Loading...</div>';
            tray.hidden = false;
            trayBtn.setAttribute('aria-expanded', 'true');
            loadTray();
        };
        var closeTray = function (returnFocus) {
            if (tray.hidden || tray.classList.contains('closing')) return;
            trayBtn.setAttribute('aria-expanded', 'false');
            tray.classList.add('closing');
            closeTimer = setTimeout(function () { tray.hidden = true; tray.classList.remove('closing'); }, reduce ? 0 : 170);
            if (returnFocus) trayBtn.focus();
        };
        trayBtn.addEventListener('click', function (e) {
            e.preventDefault();                                   // plain link still works if scripts fail
            if (tray.hidden || tray.classList.contains('closing')) openTray(); else closeTray();
        });
        document.addEventListener('click', function (e) {
            if (!tray.hidden && !tray.contains(e.target) && !trayBtn.contains(e.target)) closeTray();
        });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeTray(true); });
        tray.addEventListener('submit', function (e) {          // "Mark all read" without leaving the page
            var form = e.target.closest('form[data-tray-clear]');
            if (!form) return;
            e.preventDefault();
            fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' } })
                .then(function () {
                    document.querySelectorAll('.appbar .dot, .tabbar .dot, .nav .badge').forEach(function (n) { n.remove(); });
                    return loadTray();
                });
        });
    }
})();