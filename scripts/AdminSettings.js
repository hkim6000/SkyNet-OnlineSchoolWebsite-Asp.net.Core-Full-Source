var AdminSettingsJs = (function () {

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
        $ApiRequest('AdminSettings/' + method, JSON.stringify(list));
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
        var t = el('st-toast');
        t.textContent = msg;
        t.classList.add('st-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('st-open');
        }, 4200);
    }

    function openMenu() {
        el('st-rail').classList.add('st-open');
        el('st-scrim').classList.add('st-open');
    }

    function closeMenu() {
        el('st-rail').classList.remove('st-open');
        el('st-scrim').classList.remove('st-open');
    }

    function openModal(title) {
        var m = el('st-modal');
        el('st-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('st-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('st-modal');
        if (m) {
            m.classList.remove('st-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('st-modal');
        if (!m || !m.classList.contains('st-open')) {
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
        if (!askBox || !askBox.classList.contains('st-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('st-open');
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
        askBox.className = 'st-ask';
        askBox.innerHTML = '<div class="st-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="st-err" role="alert"></em><div class="st-askacts"><button type="button" class="st-btn st-btn-ghost"></button><button type="button" class="st-btn"></button></div></div>';
        document.getElementById('st-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('st-open')) {
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
        x.b[1].className = 'st-btn' + (o.danger ? ' st-askdel' : '');
        askBox.firstChild.classList.toggle('st-askin', input);
        askBox.classList.toggle('st-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('st-open');
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

    function touch() {
        markDirty();
    }

    function flag(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function loaded() {
        dirty = false;
    }

    function save() {
        api('Save', [{ key: 'sbrand', vlu: val('st-school') }, { key: 'stag', vlu: val('st-tag') }, { key: 'semail', vlu: val('st-email') }, { key: 'sphone', vlu: val('st-phone') }, { key: 'saddr', vlu: val('st-addr') }, { key: 'shours', vlu: val('st-hours') }, { key: 'sdays', vlu: val('st-days') }, { key: 'slimit', vlu: val('st-limit') }, { key: 'snocert', vlu: flag('st-nocert') }, { key: 'svid', vlu: val('st-vid') }]);
        return false;
    }

    function saved() {
        dirty = false;
        toast('Settings saved.');
        api('Load');
    }

    function initialize() {
        ask({ title: 'Initialize all data?', text: 'Every student, enrollment, order, refund request, certificate, message, log entry and staff account is removed. Courses, quizzes, pages and media stay. Type INITIALIZE to confirm.', value: '', place: 'INITIALIZE', ok: 'Initialize', no: 'Keep everything', danger: true, check: function (v) { return v === 'INITIALIZE' ? '' : 'Type INITIALIZE in capitals.'; } }, function (word) {
            api('Initialize', [{ key: 'word', vlu: word }]);
        });
    }

    function initialized(n) {
        toast(n + ' records removed. Signing you out.');
        setTimeout(expired, 1500);
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
        openModal: openModal,save: save, saved: saved, touch: touch, initialize: initialize, initialized: initialized
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminSettingsJs.reveal);
} else {
    AdminSettingsJs.reveal();
}
