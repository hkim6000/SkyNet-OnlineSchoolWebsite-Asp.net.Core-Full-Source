var AdminInstructorsJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;
    var timer = null;
    var cur = { page: 1 };

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('bp_tok') });
        $ApiRequest('AdminInstructors/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'bp_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        api('SignOut');
        document.cookie = 'bp_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function toast(msg) {
        var t = el('ia-toast');
        t.textContent = msg;
        t.classList.add('ia-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ia-open');
        }, 4200);
    }

    function openMenu() {
        el('ia-rail').classList.add('ia-open');
        el('ia-scrim').classList.add('ia-open');
    }

    function closeMenu() {
        el('ia-rail').classList.remove('ia-open');
        el('ia-scrim').classList.remove('ia-open');
    }

    function openModal(title) {
        var m = el('ia-modal');
        el('ia-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ia-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ia-modal');
        if (m) {
            m.classList.remove('ia-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ia-modal');
        if (!m || !m.classList.contains('ia-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ia-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ia-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ia-ask';
        askBox.innerHTML = '<div class="ia-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ia-err" role="alert"></em><div class="ia-askacts"><button type="button" class="ia-btn ia-btn-ghost"></button><button type="button" class="ia-btn"></button></div></div>';
        document.getElementById('ia-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ia-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ia-btn' + (o.danger ? ' ia-askdel' : '');
        askBox.firstChild.classList.toggle('ia-askin', input);
        askBox.classList.toggle('ia-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ia-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function later(fn) {
        clearTimeout(timer);
        timer = setTimeout(fn, 250);
    }

    var state = { q: '', st: 'all', pg: 1 };

    function list() {
        api('List', [{ key: 'q', vlu: state.q }, { key: 'pg', vlu: String(state.pg) }]);
    }

    function loaded() {
        list();
    }

    function chip(k) {
        state.st = k;
        state.pg = 1;
        list();
    }

    function search() {
        later(function () {
            state.q = val('ia-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'iid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
    }

    function picked(id) {
        markDirty();
        var img = el(id + '-prev');
        var s = el(id);
        if (img) {
            img.hidden = !s.value;
            img.src = s.value;
        }
    }

    function flag(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function save(id) {
        api('Save', [{ key: 'iid', vlu: id }, { key: 'iname', vlu: val('ia-name') }, { key: 'irole', vlu: val('ia-role') }, { key: 'itopic', vlu: val('ia-topic') }, { key: 'ibio', vlu: val('ia-bio') }, { key: 'iphoto', vlu: val('ia-photo') }, { key: 'iactive', vlu: flag('ia-active') }]);
    }

    function saved() {
        toast('Saved.');
        shut();
        list();
    }

    function remove(id, courses) {
        if (courses > 0) {
            toast('This instructor still has courses. Move the courses to someone else first, or hide the profile instead.');
            return;
        }
        ask({ title: 'Delete this instructor?', text: 'The profile is removed from the site.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('Remove', [{ key: 'iid', vlu: id }]);
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeMenu();
                closeModal();
            }
        });
        api('Load', []);
    }

    return {
        reveal: reveal,
        toast: toast,
        expired: expired,
        signout: signout,
        openMenu: openMenu,
        closeMenu: closeMenu,
        closeModal: closeModal,
        markDirty: markDirty,
        loaded: loaded,
        shut: shut,
        openModal: openModal,open: open, opened: opened, search: search, page: page, save: save, saved: saved, remove: remove, picked: picked
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminInstructorsJs.reveal);
} else {
    AdminInstructorsJs.reveal();
}
