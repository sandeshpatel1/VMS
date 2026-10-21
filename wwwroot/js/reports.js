(function () {
    'use strict';
    var app = document.getElementById('rpApp');
    if (!app) return;

    var $ = function (id) { return document.getElementById(id); };
    var url = app.dataset.url, exportUrl = app.dataset.export, detailsUrl = app.dataset.details;
    var DEFAULTS = { preset: '7d', from: '', to: '', purpose: '', status: '', flat: '', loggedBy: '', q: '', compare: true };
    var state = Object.assign({}, DEFAULTS);
    var data = null, timer = null, ctrl = null;
    var tbl = { key: 'created', dir: -1, page: 0, size: 20, q: '' };

    // ---------- helpers ----------
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function num(v) { return Math.round(v).toLocaleString('en-IN'); }
    function mins(v) { return !v ? '-' : v < 60 ? Math.round(v) + ' min' : (v / 60).toFixed(1) + ' h'; }
    function fmt(v, f) { return f === 'pct' ? v.toFixed(1) + '%' : f === 'min' ? mins(v) : num(v); }
    function when(iso) { return iso ? new Date(iso).toLocaleString('en-IN', { day: '2-digit', month: 'short', hour: 'numeric', minute: '2-digit' }) : '-'; }
    function qs(extra) {
        var p = new URLSearchParams(), s = Object.assign({}, state, extra || {});
        Object.keys(s).forEach(function (k) {
            var v = s[k];
            if (k === 'compare') { if (!v) p.set('compare', 'false'); return; }
            if (v !== '' && v != null && !(k === 'preset' && v === DEFAULTS.preset)) p.set(k, v);
        });
        if (s.preset !== 'custom') { p.delete('from'); p.delete('to'); }
        return p.toString();
    }
    function pillClass(s) { return s === 'CheckedIn' ? 'in' : s === 'CheckedOut' ? 'out' : s === 'Pending' ? 'pending' : s === 'Approved' ? 'ok' : s === 'Rejected' ? 'bad' : 'muted'; }
    function pillText(s) { return s === 'CheckedIn' ? 'IN' : s === 'CheckedOut' ? 'OUT' : s; }
    var tip = $('rpTip');
    function showTip(html, x, y) {
        tip.innerHTML = html; tip.hidden = false;
        var w = tip.offsetWidth, h = tip.offsetHeight;
        tip.style.left = Math.max(8, Math.min(innerWidth - w - 8, x + 14)) + 'px';
        tip.style.top = Math.max(8, y - h - 12) + 'px';
    }
    function hideTip() { tip.hidden = true; }

    // ---------- state <-> URL <-> controls ----------
    (function readUrl() {
        var p = new URLSearchParams(location.search);
        Object.keys(DEFAULTS).forEach(function (k) { if (p.has(k)) state[k] = k === 'compare' ? p.get(k) !== 'false' : p.get(k); });
        if ((state.from || state.to) && state.preset === DEFAULTS.preset) state.preset = 'custom';
    })();

    function syncControls() {
        document.querySelectorAll('[data-preset]').forEach(function (b) { b.setAttribute('aria-pressed', String(b.dataset.preset === state.preset)); });
        $('rpCustom').hidden = state.preset !== 'custom';
        if (data) { $('rpFrom').value = data.range.from; $('rpTo').value = data.range.to; }
        document.querySelectorAll('[data-f]').forEach(function (el) { if (document.activeElement !== el) el.value = state[el.dataset.f] || ''; });
        $('rpCompare').checked = state.compare;
        var names = { purpose: 'Purpose', status: 'Status', flat: 'Flat', loggedBy: 'Logged by', q: 'Search' };
        var chips = Object.keys(names).filter(function (k) { return state[k]; }).map(function (k) {
            var label = state[k];
            if (k === 'loggedBy') { var o = document.querySelector('#rpStaffSel option[value="' + state[k] + '"]'); if (o) label = o.textContent; }
            return '<button type="button" class="chip" data-clear="' + k + '" aria-label="Remove ' + names[k] + ' filter">' + names[k] + ': ' + esc(label) + ' <span aria-hidden="true">&times;</span></button>';
        });
        $('rpChips').innerHTML = chips.join('');
        $('rpExport').href = exportUrl + '?' + qs();
    }

    function load(now) {
        clearTimeout(timer);
        timer = setTimeout(run, now ? 0 : 220);
    }
    function run() {
        if (ctrl) ctrl.abort();
        ctrl = new AbortController();
        app.classList.add('is-loading');
        history.replaceState(null, '', location.pathname + (qs() ? '?' + qs() : ''));
        $('rpExport').href = exportUrl + '?' + qs();
        fetch(url + '?' + qs(), { credentials: 'same-origin', signal: ctrl.signal, headers: { 'X-Requested-With': 'fetch' } })
            .then(function (r) { if (!r.ok) throw new Error('bad'); return r.json(); })
            .then(function (d) { data = d; tbl.page = 0; render(); syncControls(); app.classList.remove('is-loading'); })
            .catch(function (e) {
                if (e.name === 'AbortError') return;
                app.classList.remove('is-loading');
                $('rpError').textContent = 'The report could not be loaded. Check your connection and try again.'; $('rpError').hidden = false;
            });
    }

    // ---------- controls ----------
    document.querySelectorAll('[data-preset]').forEach(function (b) {
        b.addEventListener('click', function () { state.preset = b.dataset.preset; if (state.preset !== 'custom') { state.from = ''; state.to = ''; } syncControls(); load(true); });
    });
    ['rpFrom', 'rpTo'].forEach(function (id) {
        $(id).addEventListener('change', function () { state.preset = 'custom'; state.from = $('rpFrom').value; state.to = $('rpTo').value; load(); });
    });
    document.querySelectorAll('[data-f]').forEach(function (el) {
        el.addEventListener(el.tagName === 'SELECT' ? 'change' : 'input', function () { state[el.dataset.f] = el.value.trim(); syncControls(); load(el.tagName === 'SELECT'); });
    });
    $('rpCompare').addEventListener('change', function () { state.compare = this.checked; load(true); });
    $('rpReset').addEventListener('click', function () { state = Object.assign({}, DEFAULTS); syncControls(); load(true); });
    $('rpChips').addEventListener('click', function (e) {
        var b = e.target.closest('[data-clear]'); if (!b) return;
        state[b.dataset.clear] = ''; syncControls(); load(true);
    });
    $('rpPrint').addEventListener('click', function () { window.print(); });
    // Charts and lists apply filters when clicked
    app.addEventListener('click', function (e) {
        var d = e.target.closest('[data-drill]'); if (!d) return;
        var k = d.dataset.drill, v = d.dataset.value;
        state[k] = state[k] === v ? '' : v;
        syncControls(); load(true);
        window.scrollTo({ top: 0, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    });

    // ---------- renderers ----------
    function render() {
        var r = data.range;
        $('rpRange').textContent = r.label + (r.prevLabel ? '  \u00B7  compared with ' + r.prevLabel : '');
        $('rpError').hidden = true; renderKpis(); renderLine();
        bars('rpPurpose', data.purposes, { drill: 'purpose', share: true });
        renderFunnel(); renderHeat();
        bars('rpFlats', data.flats, { drill: 'flat' });
        bars('rpStay', data.stayByPurpose, { unit: 'min' });
        renderRepeat();
        bars('rpStaff', data.staff, { drill: 'loggedBy', idFromName: true });
        renderOther(); renderTable();
    }

    function renderKpis() {
        $('rpKpis').innerHTML = data.kpis.map(function (k) {
            var d = '';
            if (k.delta != null) {
                var cls = k.delta > 0 ? 'up' : k.delta < 0 ? 'down' : 'flat';
                var arrow = k.delta > 0 ? '\u2191' : k.delta < 0 ? '\u2193' : '\u2192';
                d = '<span class="delta ' + cls + '" title="Previous period: ' + esc(fmt(k.prev, k.fmt)) + '">' + arrow + ' ' + Math.abs(k.delta) + (k.unit === 'pts' ? ' pts' : '%') + '</span>';
            }
            return '<div class="card kpi"><span class="kpi-l">' + esc(k.label) + '</span><b>' + esc(fmt(k.value, k.fmt)) + '</b>' + d + '<small>' + esc(k.hint) + '</small></div>';
        }).join('');
    }

    // Line / area chart with previous-period overlay, hover read-out and drill-down
    function niceMax(m) { if (m <= 4) return 4; var p = Math.pow(10, Math.floor(Math.log10(m))), f = m / p; return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * p; }
    var lastTap = -1;
    function renderLine() {
        var el = $('rpLine'), s = data.series, g = data.range.gran;
        $('rpGran').textContent = g === 'hour' ? 'By hour' : g === 'day' ? 'By day. Select a day to see its hours.' : 'By week. Select a week to see its days.';
        var W = Math.max(280, el.clientWidth), H = 280, L = 40, R = 14, T = 14, B = 30;
        var max = niceMax(Math.max.apply(null, s.map(function (b) { return Math.max(b.count, data.range.prevLabel ? b.prev : 0); }).concat([1])));
        var x = function (i) { return L + (s.length === 1 ? (W - L - R) / 2 : i * (W - L - R) / (s.length - 1)); };
        var y = function (v) { return T + (1 - v / max) * (H - T - B); };
        function path(key) {
            return s.map(function (b, i) {
                if (i === 0) return 'M' + x(0) + ' ' + y(b[key]);
                var mx = (x(i - 1) + x(i)) / 2;
                return 'C' + mx + ' ' + y(s[i - 1][key]) + ' ' + mx + ' ' + y(b[key]) + ' ' + x(i) + ' ' + y(b[key]);
            }).join(' ');
        }
        var grid = '', ticks = 4;
        for (var t = 0; t <= ticks; t++) {
            var v = max * t / ticks, yy = y(v);
            grid += '<line class="gl" x1="' + L + '" x2="' + (W - R) + '" y1="' + yy + '" y2="' + yy + '"/><text class="ax" x="' + (L - 8) + '" y="' + (yy + 4) + '" text-anchor="end">' + Math.round(v) + '</text>';
        }
        var every = Math.ceil(s.length / Math.max(2, Math.floor((W - L - R) / 62))), xl = '';
        s.forEach(function (b, i) { if (i % every === 0) xl += '<text class="ax" x="' + x(i) + '" y="' + (H - 8) + '" text-anchor="middle">' + esc(b.label) + '</text>'; });
        var area = path('count') + ' L' + x(s.length - 1) + ' ' + y(0) + ' L' + x(0) + ' ' + y(0) + ' Z';
        el.innerHTML =
            '<svg viewBox="0 0 ' + W + ' ' + H + '" width="' + W + '" height="' + H + '" role="img" aria-label="Visitors over time">' +
            '<defs><linearGradient id="rpGrad" x1="0" y1="0" x2="0" y2="1"><stop offset="0" style="stop-color:var(--brand);stop-opacity:.30"/><stop offset="1" style="stop-color:var(--brand);stop-opacity:0"/></linearGradient></defs>' +
            grid + xl +
            (data.range.prevLabel ? '<path class="prev" d="' + path('prev') + '"/>' : '') +
            '<path class="area" d="' + area + '"/><path class="line" d="' + path('count') + '"/>' +
            '<line class="guide" id="rpGuide" y1="' + T + '" y2="' + (H - B) + '" hidden/><circle class="pt" id="rpPt" r="5" hidden/>' +
            '<rect class="hit" x="' + L + '" y="0" width="' + (W - L - R) + '" height="' + H + '"/></svg>';
        var svg = el.firstChild, hit = svg.querySelector('.hit'), guide = svg.querySelector('#rpGuide'), pt = svg.querySelector('#rpPt');
        function at(e) {
            var r = svg.getBoundingClientRect(), px = (e.clientX - r.left) * (W / r.width);
            return Math.max(0, Math.min(s.length - 1, Math.round((px - L) / ((W - L - R) / Math.max(1, s.length - 1)))));
        }
        function over(e) {
            var i = at(e), b = s[i];
            guide.removeAttribute('hidden'); pt.removeAttribute('hidden');
            guide.setAttribute('x1', x(i)); guide.setAttribute('x2', x(i)); pt.setAttribute('cx', x(i)); pt.setAttribute('cy', y(b.count));
            showTip('<strong>' + esc(b.label) + '</strong><br>' + b.count + ' visitor' + (b.count === 1 ? '' : 's') + (data.range.prevLabel ? '<br><span class="m">Previous: ' + b.prev + '</span>' : ''), e.clientX, e.clientY);
        }
        hit.addEventListener('pointermove', over);
        hit.addEventListener('pointerleave', function () { guide.setAttribute('hidden', ''); pt.setAttribute('hidden', ''); hideTip(); lastTap = -1; });
        if (g !== 'hour') {
            hit.style.cursor = 'pointer';
            hit.addEventListener('click', function (e) {
                var i = at(e);
                if (e.pointerType === 'touch' || e.pointerType === 'pen') { if (lastTap !== i) { lastTap = i; over(e); return; } }
                var from = s[i].key, to = g === 'week' ? addDays(from, Math.min(6, data.range.span - i * 7 - 1)) : from;
                state.preset = 'custom'; state.from = from; state.to = to; syncControls(); load(true);
            });
        }
    }
    function addDays(iso, n) { var d = new Date(iso + 'T00:00:00'); d.setDate(d.getDate() + n); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
    var ro = new ResizeObserver(function () { if (data) { clearTimeout(ro.t); ro.t = setTimeout(renderLine, 80); } });
    ro.observe($('rpLine'));

    // Horizontal bars. Clickable ones apply a filter.
    function bars(id, items, o) {
        var el = $(id);
        if (!items || !items.length) { el.innerHTML = '<p class="rp-none">No data for these filters.</p>'; return; }
        var max = Math.max.apply(null, items.map(function (i) { return i.count; })), total = items.reduce(function (a, i) { return a + i.count; }, 0);
        el.innerHTML = '<div class="hbars">' + items.map(function (i) {
            var on = o.drill && state[o.drill] === i.name;
            var val = o.unit === 'min' ? mins(i.count) : o.share ? i.count + ' <em>' + Math.round(100 * i.count / total) + '%</em>' : i.count;
            var inner = '<span class="hb-n">' + esc(i.name) + '</span><span class="hb-t"><i style="width:' + Math.max(3, 100 * i.count / max) + '%"></i></span><span class="hb-v">' + val + '</span>';
            if (o.drill && !o.idFromName) return '<button type="button" class="hb' + (on ? ' on' : '') + '" data-drill="' + o.drill + '" data-value="' + esc(i.name) + '">' + inner + '</button>';
            if (o.drill && o.idFromName) { var opt = Array.prototype.find.call(document.querySelectorAll('#rpStaffSel option'), function (x) { return x.textContent === i.name; }); if (opt && opt.value) return '<button type="button" class="hb' + (state.loggedBy === opt.value ? ' on' : '') + '" data-drill="loggedBy" data-value="' + opt.value + '">' + inner + '</button>'; }
            return '<div class="hb">' + inner + '</div>';
        }).join('') + '</div>';
    }

    function renderFunnel() {
        var f = data.funnel, top = f[0].count || 1;
        $('rpFunnel').innerHTML = f[0].count === 0 ? '<p class="rp-none">No data for these filters.</p>' :
            '<div class="funnel">' + f.map(function (s, i) {
                var conv = i === 0 ? '' : Math.round(100 * s.count / (f[i - 1].count || 1)) + '% of ' + f[i - 1].label.toLowerCase();
                return '<div class="fn"><div class="fn-bar" style="width:' + Math.max(6, 100 * s.count / top) + '%"><b>' + s.count + '</b></div><span>' + esc(s.label) + (conv ? ' <em>' + conv + '</em>' : '') + '</span></div>';
            }).join('') + '</div>';
    }

    function renderHeat() {
        var days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'], max = 1;
        data.heat.forEach(function (r) { r.forEach(function (v) { if (v > max) max = v; }); });
        var head = '<span></span>';
        for (var h = 0; h < 24; h++) head += '<span class="hh">' + (h % 3 === 0 ? (h === 0 ? '12a' : h < 12 ? h + 'a' : h === 12 ? '12p' : (h - 12) + 'p') : '') + '</span>';
        var body = data.heat.map(function (r, d) {
            return '<span class="hd">' + days[d] + '</span>' + r.map(function (v, h) {
                var label = days[d] + ' ' + (h % 12 || 12) + (h < 12 ? ' AM' : ' PM') + ': ' + v + ' visitor' + (v === 1 ? '' : 's');
                return '<span class="hc" tabindex="-1" data-tip="' + esc(label) + '" style="--v:' + (v / max).toFixed(3) + '"></span>';
            }).join('');
        }).join('');
        $('rpHeat').innerHTML = '<div class="heat">' + head + body + '</div><div class="heat-key"><span>Fewer</span><i></i><span>More</span></div>';
    }
    document.addEventListener('pointermove', function (e) {
        var c = e.target.closest && e.target.closest('.hc');
        if (c) showTip(esc(c.dataset.tip), e.clientX, e.clientY); else if (!e.target.closest || !e.target.closest('#rpLine')) hideTip();
    });

    function renderRepeat() {
        var r = data.repeat, el = $('rpRepeat');
        el.innerHTML = !r.length ? '<p class="rp-none">Nobody visited more than once.</p>' :
            '<div class="reps">' + r.map(function (v) {
                return '<button type="button" class="rep" data-drill="q" data-value="' + esc(v.mobile) + '"><span class="avatar">' + esc(v.name.split(' ').map(function (p) { return p[0]; }).slice(0, 2).join('').toUpperCase()) + '</span><span><strong>' + esc(v.name) + '</strong><small>' + esc(v.mobile) + ', last on ' + esc(v.last) + '</small></span><b>' + v.count + 'x</b></button>';
            }).join('') + '</div>';
    }

    function renderOther() {
        var o = data.other;
        $('rpOther').innerHTML =
            '<dl class="kv2"><dt>Deliveries</dt><dd>' + o.deliveries + '</dd><dt>Waiting at the gate</dt><dd>' + o.waiting + '</dd><dt>Average pickup time</dt><dd>' + mins(o.avgPickup) + '</dd><dt>Emergency alerts</dt><dd>' + o.emergencies + '</dd></dl>' +
            (o.byCompany.length ? '<h3 class="rp-sub">By company</h3><div id="rpCompany"></div>' : '');
        if (o.byCompany.length) bars('rpCompany', o.byCompany, {});
    }

    // Sortable, searchable, paged table
    function renderTable() {
        var rows = data.rows.slice(), q = tbl.q.toLowerCase();
        if (q) rows = rows.filter(function (r) { return (r.name + ' ' + r.mobile + ' ' + r.code + ' ' + r.flat + ' ' + r.purpose + ' ' + r.loggedBy).toLowerCase().indexOf(q) > -1; });
        var k = tbl.key, dir = tbl.dir;
        rows.sort(function (a, b) {
            var x = a[k], y = b[k];
            if (x == null && y == null) return 0; if (x == null) return 1; if (y == null) return -1;
            return (typeof x === 'number' ? x - y : String(x).localeCompare(String(y), undefined, { numeric: true })) * dir;
        });
        var pages = Math.max(1, Math.ceil(rows.length / tbl.size)); tbl.page = Math.min(tbl.page, pages - 1);
        var slice = rows.slice(tbl.page * tbl.size, tbl.page * tbl.size + tbl.size);
        document.querySelector('#rpTable tbody').innerHTML = slice.length ? slice.map(function (r) {
            return '<tr><td><a href="' + detailsUrl + '/' + r.id + '">' + esc(r.code) + '</a></td><td>' + esc(r.name) + '<br><small class="muted">' + esc(r.mobile) + '</small></td><td>' + esc(r.flat) + '</td><td>' + esc(r.purpose) + (r.pre ? ' <small class="muted">pre-approved</small>' : '') + '</td><td><span class="pill ' + pillClass(r.status) + '">' + pillText(r.status) + '</span></td><td>' + when(r.created) + '</td><td>' + when(r.inTime) + '</td><td>' + when(r.outTime) + '</td><td>' + (r.stay == null ? '-' : mins(r.stay)) + '</td><td>' + esc(r.loggedBy) + '</td></tr>';
        }).join('') : '<tr><td colspan="10" class="rp-none">No visits match.</td></tr>';
        document.querySelectorAll('#rpTable th[data-sort]').forEach(function (th) { th.setAttribute('aria-sort', th.dataset.sort === k ? (dir > 0 ? 'ascending' : 'descending') : 'none'); });
        var first = rows.length ? tbl.page * tbl.size + 1 : 0;
        $('rpPageInfo').textContent = 'Showing ' + first + ' to ' + (first ? first + slice.length - 1 : 0) + ' of ' + rows.length + (data.total > data.rows.length ? ' (latest ' + data.rows.length + ' of ' + data.total + '; export for all)' : '');
        $('rpCount').textContent = '(' + data.total + ')';
        $('rpPrev').disabled = tbl.page === 0; $('rpNext').disabled = tbl.page >= pages - 1;
    }
    document.querySelectorAll('#rpTable th[data-sort]').forEach(function (th) {
        th.tabIndex = 0;
        var go = function () { if (tbl.key === th.dataset.sort) tbl.dir *= -1; else { tbl.key = th.dataset.sort; tbl.dir = 1; } renderTable(); };
        th.addEventListener('click', go); th.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); go(); } });
    });
    $('rpTableSearch').addEventListener('input', function () { tbl.q = this.value.trim(); tbl.page = 0; renderTable(); });
    $('rpPrev').addEventListener('click', function () { tbl.page--; renderTable(); });
    $('rpNext').addEventListener('click', function () { tbl.page++; renderTable(); });

    syncControls();
    load(true);
})();