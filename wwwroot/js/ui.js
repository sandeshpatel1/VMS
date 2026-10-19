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
})();
