var VerifyJs = (function () {

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
        $ApiRequest('Verify/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('vf-toast');
        t.textContent = msg;
        t.classList.add('vf-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('vf-open');
        }, 3600);
    }

    function openNav() {
        el('vf-nav').classList.add('vf-open');
        el('vf-scrim').classList.add('vf-open');
    }

    function closeNav() {
        el('vf-nav').classList.remove('vf-open');
        el('vf-scrim').classList.remove('vf-open');
    }

    function gone() {
        dropCookie('bp_stu');
        location.href = 'Join?next=' + encodeURIComponent('Verify');
    }

    function signout() {
        scall('SignOut');
        dropCookie('bp_stu');
        location.href = 'Home';
    }

    function who() {
        var a = el('vf-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('vf-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('vf-open');
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
        askBox.className = 'vf-ask';
        askBox.innerHTML = '<div class="vf-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="vf-err" role="alert"></em><div class="vf-askacts"><button type="button" class="vf-btn vf-btn-ghost"></button><button type="button" class="vf-btn"></button></div></div>';
        el('vf-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('vf-open')) {
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
        x.b[1].className = 'vf-btn' + (o.danger ? ' vf-btn-danger' : '');
        askBox.firstChild.classList.toggle('vf-askin', input);
        askBox.classList.toggle('vf-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('vf-open');
        if (input) {
            x.t.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function check() {
        call('Check', [{ key: 'vcode', vlu: val('vf-code') }]);
        return false;
    }

    function checked(code) {
        if (code) {
            history.replaceState(null, '', 'Verify?code=' + code);
        }
    }

    function auto() {
        if (val('vf-code')) {
            check();
        }
    }

    function reveal() {
        who();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        auto();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        gone: gone,
        signout: signout,check: check, checked: checked
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', VerifyJs.reveal);
} else {
    VerifyJs.reveal();
}
