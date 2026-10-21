var AdminUsersJs = (function () {

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
        $ApiRequest('AdminUsers/' + method, JSON.stringify(list));
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
        var t = el('us-toast');
        t.textContent = msg;
        t.classList.add('us-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('us-open');
        }, 4200);
    }

    function openMenu() {
        el('us-rail').classList.add('us-open');
        el('us-scrim').classList.add('us-open');
    }

    function closeMenu() {
        el('us-rail').classList.remove('us-open');
        el('us-scrim').classList.remove('us-open');
    }

    function openModal(title) {
        var m = el('us-modal');
        el('us-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('us-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('us-modal');
        if (m) {
            m.classList.remove('us-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('us-modal');
        if (!m || !m.classList.contains('us-open')) {
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
        if (!askBox || !askBox.classList.contains('us-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('us-open');
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
        askBox.className = 'us-ask';
        askBox.innerHTML = '<div class="us-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="us-err" role="alert"></em><div class="us-askacts"><button type="button" class="us-btn us-btn-ghost"></button><button type="button" class="us-btn"></button></div></div>';
        document.getElementById('us-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('us-open')) {
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
        x.b[1].className = 'us-btn' + (o.danger ? ' us-askdel' : '');
        askBox.firstChild.classList.toggle('us-askin', input);
        askBox.classList.toggle('us-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('us-open');
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
            state.q = val('us-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'uid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
    }

    function box(id) {
        return el(id);
    }

    function flag(id) {
        var e = box(id);
        return e && e.checked ? '1' : '0';
    }

    function boxes(a) {
        var o = {};
        document.querySelectorAll('#us-rights input[data-a="' + a + '"]').forEach(function (n) {
            o[n.getAttribute('data-r')] = n;
        });
        return o;
    }

    function right(n) {
        var b = boxes(n.getAttribute('data-a'));
        if (n.checked && n.getAttribute('data-r') !== 'V' && b.V) {
            b.V.checked = true;
        }
        if (n.checked && n.getAttribute('data-r') === 'X' && b.E) {
            b.E.checked = true;
        }
        if (!n.checked && n.getAttribute('data-r') === 'V') {
            if (b.E) {
                b.E.checked = false;
            }
            if (b.X) {
                b.X.checked = false;
            }
        }
        if (!n.checked && n.getAttribute('data-r') === 'E' && b.X) {
            b.X.checked = false;
        }
        markDirty();
    }

    function manager() {
        var on = box('us-admin').checked;
        el('us-rights').classList.toggle('us-off', on);
        el('us-rights').querySelectorAll('input, select').forEach(function (n) {
            n.disabled = on;
        });
        markDirty();
    }

    function opened(title) {
        openModal(title);
        manager();
        dirty = false;
    }

    function perms() {
        var map = {};
        document.querySelectorAll('#us-rights input[data-a]').forEach(function (n) {
            if (n.checked) {
                var a = n.getAttribute('data-a');
                map[a] = (map[a] || '') + n.getAttribute('data-r');
            }
        });
        return Object.keys(map).map(function (a) {
            return a + ':' + map[a];
        }).join(';');
    }

    function save(id) {
        api('Save', [{ key: 'uid', vlu: id }, { key: 'uname', vlu: val('us-name') }, { key: 'ulogin', vlu: val('us-login') }, { key: 'utitle', vlu: val('us-title') }, { key: 'uadmin', vlu: flag('us-admin') }, { key: 'uactive', vlu: flag('us-active') }, { key: 'uprov', vlu: val('us-prov') }, { key: 'uown', vlu: flag('us-own') }, { key: 'uperms', vlu: perms() }]);
    }

    function saved(pass, login) {
        list();
        if (pass) {
            dirty = false;
            shown(pass, login);
            toast('User created.');
            return;
        }
        toast('Saved.');
        shut();
    }

    function shown(pass, login) {
        var b = el('us-pw');
        b.hidden = false;
        b.textContent = (login ? 'User name: ' + login + '   ' : '') + 'First password: ' + pass + '   (shown once; they choose their own at the first sign-in)';
        b.scrollIntoView({ block: 'center' });
    }

    function reset(id) {
        ask({ title: 'Reset this password?', text: 'A new first password is made, the person is signed out and must choose their own at the next sign-in. You will see it once.', ok: 'Reset password', no: 'Cancel', danger: true }, function () {
            api('Reset', [{ key: 'uid', vlu: id }]);
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
        openModal: openModal,open: open, opened: opened, chip: chip, search: search, page: page, save: save, saved: saved, reset: reset, shown: shown, right: right, manager: manager
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminUsersJs.reveal);
} else {
    AdminUsersJs.reveal();
}
