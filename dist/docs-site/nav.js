(function () {
  // ---- group toggle ----
  window.toggleGroup = function (header) {
    var group = header.closest('[data-group]');
    var open  = group.dataset.open === 'true';
    group.dataset.open = open ? 'false' : 'true';
  };

  // ---- sidebar nav (mobile) ----
  window.toggleNav = function () {
    var sidebar  = document.getElementById('jt-sidebar');
    var backdrop = document.getElementById('nav-backdrop');
    if (!sidebar) return;
    var isOpen = sidebar.classList.contains('open');
    sidebar.classList.toggle('open', !isOpen);
    backdrop.classList.toggle('open', !isOpen);
  };

  // ---- nav filter ----
  window.filterNav = function (q) {
    q = q.toLowerCase().trim();
    document.querySelectorAll('.nav-group').forEach(function (group) {
      var items   = group.querySelectorAll('.nav-group-items a');
      var anyVis  = false;
      items.forEach(function (a) {
        var match = !q || a.textContent.toLowerCase().includes(q);
        a.classList.toggle('nav-hidden', !match);
        if (match) anyVis = true;
      });
      group.classList.toggle('nav-hidden', !anyVis);
      if (q && anyVis) group.dataset.open = 'true';
    });
  };

  // ---- copy code ----
  window.copyCode = function (btn) {
    var pre = btn.closest('.code-block').querySelector('pre');
    if (!pre) return;
    var text = pre.innerText || pre.textContent;
    try {
      navigator.clipboard.writeText(text);
      btn.textContent = 'copied!';
      setTimeout(function () { btn.textContent = 'copy'; }, 1800);
    } catch (e) {
      btn.textContent = 'error';
    }
  };

  // ---- scroll active item into view ----
  var active = document.querySelector('.nav-group-items a.active');
  if (active) active.scrollIntoView({ block: 'nearest' });
})();