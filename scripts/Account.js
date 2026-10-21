var AccountJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askBack = null;

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

    function setCookie(name, value, days) {
        document.cookie = name + '=' + encodeURIComponent(value) + '; path=' + base() + '; max-age=' + (days * 86400) + '; samesite=lax';
    }

    function dropCookie(name) {
        document.cookie = name + '=; path=' + base() + '; max-age=0';
    }

    function call(method, list) {
        $ApiRequest('Account/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('ac-toast');
        t.textContent = msg;
        t.classList.add('ac-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ac-open');
        }, 3600);
    }

    function openNav() {
        el('ac-nav').classList.add('ac-open');
        el('ac-scrim').classList.add('ac-open');
    }

    function closeNav() {
        el('ac-nav').classList.remove('ac-open');
        el('ac-scrim').classList.remove('ac-open');
    }

    function gone() {
        dropCookie('bp_stu');
        location.href = 'Join?next=' + encodeURIComponent('Account');
    }

    function signout() {
        scall('SignOut');
        dropCookie('bp_stu');
        location.href = 'Home';
    }

    function who() {
        var a = el('ac-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ac-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('ac-open');
        askDone = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ac-ask';
        askBox.innerHTML = '<div class="ac-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="ac-err" role="alert"></em><div class="ac-askacts"><button type="button" class="ac-btn ac-btn-ghost"></button><button type="button" class="ac-btn"></button></div></div>';
        el('ac-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ac-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Tab') {
                var f = [x.t, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
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
        askBox.setAttribute('data-need', o.need ? '1' : '0');
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ac-btn' + (o.danger ? ' ac-btn-danger' : '');
        askBox.firstChild.classList.toggle('ac-askin', input);
        askBox.classList.toggle('ac-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('ac-open');
        if (input) {
            x.t.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    var dirty = false;

    function load() {
        if (!getCookie('bp_stu')) {
            gone();
            return;
        }
        scall('Load');
    }

    function opened(title) {
        var m = el('ac-modal');
        el('ac-mtitle').textContent = title || '';
        m.classList.add('ac-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function closeModal() {
        var m = el('ac-modal');
        if (m) {
            m.classList.remove('ac-open');
            m.setAttribute('aria-hidden', 'true');
        }
    }

    function profile() {
        scall('Profile');
    }

    function saveProfile() {
        scall('SaveProfile', [{ key: 'pname', vlu: val('ac-pname') }, { key: 'pphone', vlu: val('ac-pphone') }]);
        return false;
    }

    function profileSaved() {
        closeModal();
        toast('Your profile is saved.');
        load();
    }

    function receipt(oid) {
        scall('Receipt', [{ key: 'oid', vlu: oid }]);
    }

    function refund(oid) {
        scall('RefundInfo', [{ key: 'oid', vlu: oid }]);
    }

    function sendRefund(oid) {
        var t = el('ac-reason');
        if (!t || t.value.trim().length < 5) {
            el('ac-e-reason').textContent = 'Please tell us why in a sentence or two.';
            return;
        }
        scall('RequestRefund', [{ key: 'oid', vlu: oid }, { key: 'reason', vlu: t.value.trim() }]);
    }

    function requested(msg) {
        closeModal();
        toast(msg);
        load();
    }

    function withdraw(rid) {
        ask({ title: 'Withdraw this refund request?', text: 'Our team will not look at it any more. You can send a new request later.', ok: 'Withdraw request', no: 'Keep it', danger: true }, function () {
            scall('Withdraw', [{ key: 'rid', vlu: rid }]);
        });
    }

    function withdrawn(ok, msg) {
        toast(msg);
        load();
    }

    function print() {
        window.print();
    }

    function reveal() {
        who();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeModal();
            }
        });
        load();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        gone: gone,
        signout: signout,opened: opened, closeModal: closeModal, profile: profile, saveProfile: saveProfile, profileSaved: profileSaved, receipt: receipt, refund: refund, sendRefund: sendRefund, requested: requested, withdraw: withdraw, withdrawn: withdrawn, print: print
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AccountJs.reveal);
} else {
    AccountJs.reveal();
}
