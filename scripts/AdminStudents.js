var AdminStudentsJs = (function () {

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
        $ApiRequest('AdminStudents/' + method, JSON.stringify(list));
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
        var t = el('su-toast');
        t.textContent = msg;
        t.classList.add('su-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('su-open');
        }, 4200);
    }

    function openMenu() {
        el('su-rail').classList.add('su-open');
        el('su-scrim').classList.add('su-open');
    }

    function closeMenu() {
        el('su-rail').classList.remove('su-open');
        el('su-scrim').classList.remove('su-open');
    }

    function openModal(title) {
        var m = el('su-modal');
        el('su-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('su-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('su-modal');
        if (m) {
            m.classList.remove('su-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('su-modal');
        if (!m || !m.classList.contains('su-open')) {
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
        if (!askBox || !askBox.classList.contains('su-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('su-open');
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
        askBox.className = 'su-ask';
        askBox.innerHTML = '<div class="su-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="su-err" role="alert"></em><div class="su-askacts"><button type="button" class="su-btn su-btn-ghost"></button><button type="button" class="su-btn"></button></div></div>';
        document.getElementById('su-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('su-open')) {
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
        x.b[1].className = 'su-btn' + (o.danger ? ' su-askdel' : '');
        askBox.firstChild.classList.toggle('su-askin', input);
        askBox.classList.toggle('su-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('su-open');
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
        api('List', [{ key: 'st', vlu: state.st }, { key: 'q', vlu: state.q }, { key: 'pg', vlu: String(state.pg) }]);
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
            state.q = val('su-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'sid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
    }

    function save(id) {
        api('Save', [{ key: 'sid', vlu: id }, { key: 'name', vlu: val('su-name') }, { key: 'email', vlu: val('su-email') }, { key: 'phone', vlu: val('su-phone') }]);
    }

    function saved() {
        toast('Student saved.');
        shut();
        list();
    }

    function reset(id) {
        ask({ title: 'Reset this password?', text: 'A new password is made and the student is signed out. You will see it once, so pass it on safely.', ok: 'Reset password', no: 'Cancel', danger: true }, function () {
            api('Reset', [{ key: 'sid', vlu: id }]);
        });
    }

    function shown(pass) {
        var box = el('su-pw');
        box.hidden = false;
        box.textContent = 'New password: ' + pass + '  (shown once)';
        toast('Password reset.');
    }

    function toggle(id, on) {
        var run = function () {
            api('Toggle', [{ key: 'sid', vlu: id }]);
        };
        if (on) {
            ask({ title: 'Switch this account off?', text: 'The student is signed out and cannot sign in. Their records stay.', ok: 'Switch off', no: 'Cancel', danger: true }, run);
        } else {
            run();
        }
    }

    function switched(message) {
        toast(message);
        shut();
        list();
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
        openModal: openModal,open: open, opened: opened, chip: chip, search: search, page: page, save: save, saved: saved, reset: reset, shown: shown, toggle: toggle, switched: switched
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminStudentsJs.reveal);
} else {
    AdminStudentsJs.reveal();
}
