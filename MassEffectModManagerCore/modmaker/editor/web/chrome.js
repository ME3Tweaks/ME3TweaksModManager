/* The editor's title bar, drawn by the page and driven by the host.

   The host used to own these controls as WPF buttons above the WebView2. That put a strip of another
   application's design language across the top of every page, and it could not participate in the
   page at all - it could not show which editor was current, it could not grey out with the page, and
   it looked pasted on because it was.

   The split of responsibility has NOT changed: the host still owns the project file, the dialogs and
   the compile. This is a view of that state and a way to ask for it. The page holds no project state
   of its own and every button posts a command rather than deciding anything.

   Two messages, mirroring the edit channel already in place:

     page -> host   { type: 'modmaker.command', command, index?, value? }
     host -> page   window.__modmakerChrome.apply(state)

   `window.__CHROME__` carries the first state, injected before this script runs for the same reason
   the payload is: a bar that appears empty and then populates is a flicker on every page switch. */

(function () {
  'use strict';

  var host = window.chrome && window.chrome.webview;

  /* Enough to develop a page in a plain browser. The commands then go nowhere, which is correct:
     there is no project outside the app for them to act on. */
  var state = window.__CHROME__ || {
    project: { name: 'Untitled', dirty: false, requireAllPlayers: false },
    pages: [],
    status: 'not running in ModMaker',
    canCompile: false
  };

  function send(command, extra) {
    if (!host) return;
    var msg = { type: 'modmaker.command', command: command };
    if (extra) for (var k in extra) msg[k] = extra[k];
    try { host.postMessage(msg); } catch (e) { /* the host went away; the page stays usable */ }
  }

  var bar = null, closeMenu = null;

  function draw() {
    if (!bar) return;
    if (closeMenu) closeMenu();
    bar.innerHTML = '';

    bar.appendChild(projectChip());
    bar.appendChild(tabs());

    var spacer = document.createElement('span');
    spacer.className = 'spacer';
    bar.appendChild(spacer);

    var status = document.createElement('span');
    status.className = 'status';
    status.textContent = state.status || '';
    if (state.problems) status.classList.add('bad');
    status.title = status.textContent;
    bar.appendChild(status);

    var compile = document.createElement('button');
    compile.className = 'compile';
    compile.textContent = state.busy ? 'Compiling…' : 'Compile';
    compile.disabled = !!state.busy || !state.canCompile;
    /* Said on the button rather than discovered by clicking it: the editor can be opened against a
       fixture, and then there is no install to compile against and never will be. */
    compile.title = state.canCompile
      ? 'Build this project into a DLC mod'
      : 'This editor was opened without an install to compile against';
    compile.onclick = function () { send('compile'); };
    bar.appendChild(compile);
  }

  function projectChip() {
    var p = state.project || {};
    var chip = document.createElement('button');
    chip.className = 'proj';

    if (p.dirty) {
      var dot = document.createElement('span');
      dot.className = 'dot';
      dot.title = 'Unsaved changes';
      chip.appendChild(dot);
    }

    var name = document.createElement('span');
    name.className = 'name';
    name.textContent = p.name || 'Untitled';
    chip.appendChild(name);

    var caret = document.createElement('span');
    caret.className = 'caret';
    caret.textContent = '▾';
    chip.appendChild(caret);

    chip.title = (p.file || 'This project has never been saved')
      + (p.dirty ? ' — unsaved changes' : '');
    chip.onclick = function (e) { e.stopPropagation(); openMenu(chip); };
    return chip;
  }

  function openMenu(anchor) {
    if (closeMenu) { closeMenu(); return; }

    var p = state.project || {};
    var menu = document.createElement('div');
    menu.className = 'mmc-menu';

    menu.appendChild(item('New project', 'Ctrl+N', function () { send('new'); }));
    menu.appendChild(item('Open…', 'Ctrl+O', function () { send('open'); }));
    menu.appendChild(item('Save', 'Ctrl+S', function () { send('save'); }));
    menu.appendChild(item('Save as…', 'Ctrl+Shift+S', function () { send('saveAs'); }));

    var sep = document.createElement('div');
    sep.className = 'sep';
    menu.appendChild(sep);

    menu.appendChild(item('All players need this mod', null, function () {
      send('requireAllPlayers', { value: !p.requireAllPlayers });
    }, p.requireAllPlayers));

    var why = document.createElement('div');
    why.className = 'why';
    why.textContent = 'Drops the mount flag that lets you match with players who do not have this '
      + 'mod. Config edits do desync host and clients, but usually harmlessly — turn this on '
      + 'only when your mod cannot tolerate it.';
    menu.appendChild(why);

    document.body.appendChild(menu);
    var r = anchor.getBoundingClientRect();
    menu.style.left = Math.round(r.left) + 'px';
    menu.style.top = Math.round(r.bottom + 5) + 'px';

    function away(e) { if (!menu.contains(e.target)) closeMenu(); }
    function esc(e) { if (e.key === 'Escape') closeMenu(); }

    closeMenu = function () {
      document.removeEventListener('mousedown', away, true);
      document.removeEventListener('keydown', esc, true);
      menu.remove();
      closeMenu = null;
    };
    document.addEventListener('mousedown', away, true);
    document.addEventListener('keydown', esc, true);
  }

  function item(text, key, onclick, ticked) {
    var b = document.createElement('button');

    var tick = document.createElement('span');
    tick.className = 'tick';
    tick.textContent = ticked ? '✓' : '';
    b.appendChild(tick);

    b.appendChild(document.createTextNode(text));

    if (key) {
      var k = document.createElement('span');
      k.className = 'key';
      k.textContent = key;
      b.appendChild(k);
    }

    b.onclick = function () { if (closeMenu) closeMenu(); onclick(); };
    return b;
  }

  function tabs() {
    var wrap = document.createElement('span');
    wrap.className = 'tabs';

    /* One editor built means there is nothing to switch between, and a single tab that cannot be
       clicked off is furniture. */
    var pages = state.pages || [];
    if (pages.length < 2) return wrap;

    pages.forEach(function (page, i) {
      var b = document.createElement('button');
      b.textContent = page.title;
      if (page.active) b.className = 'on';
      b.onclick = function () { if (!page.active) send('page', { index: i }); };
      wrap.appendChild(b);
    });
    return wrap;
  }

  /* Ctrl+S is the one a user reaches for without thinking, and losing it to the WebView's own "save
     this page" would be a real loss of work. The others come along because a menu that advertises a
     shortcut has to honour it. */
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
    var k = String(e.key).toLowerCase();
    if (k === 's') { e.preventDefault(); send(e.shiftKey ? 'saveAs' : 'save'); }
    else if (k === 'o') { e.preventDefault(); send('open'); }
    else if (k === 'n') { e.preventDefault(); send('new'); }
  }, true);

  /* The host pushes a whole state rather than a delta, for the same reason the page posts a whole
     edit document: a message that goes missing then costs nothing. */
  window.__modmakerChrome = {
    apply: function (next) {
      if (!next) return;
      state = next;
      draw();
    }
  };

  function build() {
    bar = document.createElement('div');
    bar.className = 'mmc';
    document.body.insertBefore(bar, document.body.firstChild);
    draw();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', build);
  else build();
})();
