var CertificateJs = (function () {

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
        $ApiRequest('Certificate/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('ce-toast');
        t.textContent = msg;
        t.classList.add('ce-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ce-open');
        }, 3600);
    }

    function openNav() {
        el('ce-nav').classList.add('ce-open');
        el('ce-scrim').classList.add('ce-open');
    }

    function closeNav() {
        el('ce-nav').classList.remove('ce-open');
        el('ce-scrim').classList.remove('ce-open');
    }

    function gone() {
        dropCookie('bp_stu');
        location.href = 'Join?next=' + encodeURIComponent(location.pathname.replace(/^.*\//, '') + location.search);
    }

    function signout() {
        scall('SignOut');
        dropCookie('bp_stu');
        location.href = 'Home';
    }

    function who() {
        var a = el('ce-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ce-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('ce-open');
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
        askBox.className = 'ce-ask';
        askBox.innerHTML = '<div class="ce-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="ce-err" role="alert"></em><div class="ce-askacts"><button type="button" class="ce-btn ce-btn-ghost"></button><button type="button" class="ce-btn"></button></div></div>';
        el('ce-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ce-open')) {
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
        x.b[1].className = 'ce-btn' + (o.danger ? ' ce-btn-danger' : '');
        askBox.firstChild.classList.toggle('ce-askin', input);
        askBox.classList.toggle('ce-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('ce-open');
        if (input) {
            x.t.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function qs(k) {
        var m = location.search.match(new RegExp('[?&]' + k + '=([^&]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function load() {
        scall('Load', [{ key: 'ccode', vlu: qs('code') }]);
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
        load();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        gone: gone,
        signout: signout,print: print
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', CertificateJs.reveal);
} else {
    CertificateJs.reveal();
}
