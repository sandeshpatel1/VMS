(function () {
  'use strict';
  var body = document.body;

  // Drawer (mobile / tablet)
  document.querySelectorAll('[data-open-drawer]').forEach(function (b) {
    b.addEventListener('click', function () { body.classList.add('drawer-open'); });
  });
  document.querySelectorAll('[data-close-drawer]').forEach(function (b) {
    b.addEventListener('click', function () { body.classList.remove('drawer-open'); });
  });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') body.classList.remove('drawer-open'); });

  // Show / hide password
  document.querySelectorAll('[data-toggle-pw]').forEach(function (b) {
    b.addEventListener('click', function () {
      var input = document.querySelector(b.getAttribute('data-toggle-pw'));
      if (!input) return;
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      b.textContent = show ? 'Hide' : 'Show';
    });
  });

  // Type-to-filter for long <select> lists (flat / resident pickers)
  document.querySelectorAll('[data-filter-select]').forEach(function (box) {
    var sel = document.querySelector(box.getAttribute('data-filter-select'));
    if (!sel) return;
    var all = Array.prototype.map.call(sel.options, function (o) { return { v: o.value, t: o.text }; });
    box.addEventListener('input', function () {
      var q = box.value.trim().toLowerCase();
      var current = sel.value;
      sel.innerHTML = '';
      all.filter(function (o) { return !o.v || o.t.toLowerCase().indexOf(q) > -1; })
         .forEach(function (o) { sel.add(new Option(o.t, o.v, false, o.v === current)); });
      if (q && sel.options.length === 2) sel.selectedIndex = 1;
    });
  });

  // Confirm dangerous actions
  document.querySelectorAll('form[data-confirm]').forEach(function (f) {
    f.addEventListener('submit', function (e) { if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault(); });
  });

  // Auto-hide success toast
  var toast = document.querySelector('.toast.ok');
  if (toast) setTimeout(function () { toast.style.transition = 'opacity .4s'; toast.style.opacity = '0'; setTimeout(function () { toast.remove(); }, 450); }, 5000);

  // Photo capture (webcam or file) -> base64 JPEG in a hidden field
  var dlg = document.getElementById('cameraDlg');
  var hidden = document.getElementById('PhotoData');
  if (dlg && hidden) {
    var video = document.getElementById('camVideo');
    var shotImg = document.getElementById('camShot');
    var canvas = document.getElementById('camCanvas');
    var snap = document.getElementById('camSnap');
    var retake = document.getElementById('camRetake');
    var use = document.getElementById('camUse');
    var preview = document.getElementById('photoPreview');
    var empty = document.getElementById('photoEmpty');
    var clear = document.getElementById('photoClear');
    var stream = null, shot = null;

    function stop() { if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; } }
    function live(on) {
      video.hidden = !on; shotImg.hidden = on;
      snap.hidden = !on; retake.hidden = on; use.hidden = on;
    }
    function setPhoto(dataUrl) {
      hidden.value = dataUrl || '';
      if (dataUrl) { preview.src = dataUrl; preview.hidden = false; empty.hidden = true; clear.hidden = false; }
      else { preview.hidden = true; empty.hidden = false; clear.hidden = true; }
    }
    function scaleToJpeg(source, w, h) {
      var s = Math.min(1, 640 / Math.max(w, h));
      canvas.width = Math.round(w * s); canvas.height = Math.round(h * s);
      canvas.getContext('2d').drawImage(source, 0, 0, canvas.width, canvas.height);
      return canvas.toDataURL('image/jpeg', 0.82);
    }

    document.getElementById('openCamera').addEventListener('click', function () {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        alert('This browser cannot open the camera here. Use Upload photo instead.'); return;
      }
      navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: { ideal: 1280 } }, audio: false })
        .then(function (s) { stream = s; video.srcObject = s; live(true); dlg.showModal(); })
        .catch(function () { alert('The camera is blocked or missing. Allow camera access, or use Upload photo instead.'); });
    });
    snap.addEventListener('click', function () {
      shot = scaleToJpeg(video, video.videoWidth || 640, video.videoHeight || 480);
      shotImg.src = shot; live(false);
    });
    retake.addEventListener('click', function () { shot = null; live(true); });
    use.addEventListener('click', function () { setPhoto(shot); dlg.close(); });
    document.getElementById('camCancel').addEventListener('click', function () { dlg.close(); });
    dlg.addEventListener('close', stop);
    clear.addEventListener('click', function () { setPhoto(''); });

    document.getElementById('photoFile').addEventListener('change', function (e) {
      var file = e.target.files && e.target.files[0];
      if (!file) return;
      var url = URL.createObjectURL(file), img = new Image();
      img.onload = function () { setPhoto(scaleToJpeg(img, img.naturalWidth, img.naturalHeight)); URL.revokeObjectURL(url); };
      img.onerror = function () { alert('That file is not a readable image.'); URL.revokeObjectURL(url); };
      img.src = url;
    });

    if (hidden.value) setPhoto(hidden.value);
  }
})();
