var JoinJs = (function () {

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
        $ApiRequest('Join/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('jn-toast');
        t.textContent = msg;
        t.classList.add('jn-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('jn-open');
        }, 3600);
    }

    function openNav() {
        el('jn-nav').classList.add('jn-open');
        el('jn-scrim').classList.add('jn-open');
    }

    function closeNav() {
        el('jn-nav').classList.remove('jn-open');
        el('jn-scrim').classList.remove('jn-open');
    }

    function gone() {
        dropCookie('bp_stu');
        location.href = 'Join?next=' + encodeURIComponent('Join');
    }

    function signout() {
        scall('SignOut');
        dropCookie('bp_stu');
        location.href = 'Home';
    }

    function who() {
        var a = el('jn-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('jn-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('jn-open');
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
        askBox.className = 'jn-ask';
        askBox.innerHTML = '<div class="jn-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="jn-err" role="alert"></em><div class="jn-askacts"><button type="button" class="jn-btn jn-btn-ghost"></button><button type="button" class="jn-btn"></button></div></div>';
        el('jn-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('jn-open')) {
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
        x.b[1].className = 'jn-btn' + (o.danger ? ' jn-btn-danger' : '');
        askBox.firstChild.classList.toggle('jn-askin', input);
        askBox.classList.toggle('jn-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('jn-open');
        if (input) {
            x.t.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function mode(m) {
        el('jn-form-in').hidden = m !== 'in';
        el('jn-form-new').hidden = m === 'in';
        el('jn-tab-in').classList.toggle('jn-act', m === 'in');
        el('jn-tab-new').classList.toggle('jn-act', m !== 'in');
        el('jn-tab-in').setAttribute('aria-selected', m === 'in' ? 'true' : 'false');
        el('jn-tab-new').setAttribute('aria-selected', m === 'in' ? 'false' : 'true');
    }

    function signIn() {
        call('SignIn', [{ key: 'jemail', vlu: val('jn-email') }, { key: 'jpass', vlu: el('jn-pass').value }]);
        return false;
    }

    function signUp() {
        call('SignUp', [{ key: 'jname', vlu: val('jn-name') }, { key: 'jemail2', vlu: val('jn-email2') }, { key: 'jphone', vlu: val('jn-phone') }, { key: 'jpass2', vlu: el('jn-pass2').value }, { key: 'jpass3', vlu: el('jn-pass3').value }]);
        return false;
    }

    function target() {
        var m = location.search.match(/[?&]next=([^&]*)/);
        var n = m ? decodeURIComponent(m[1]) : '';
        return /^[A-Za-z]+(\?[A-Za-z0-9=&%._-]*)?$/.test(n) && n.indexOf('Join') !== 0 ? n : 'MyCourses';
    }

    function signed(tok) {
        setCookie('bp_stu', tok, 14);
        location.href = target();
    }

    function reveal() {
        who();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        mode(val('jn-mode') === 'new' ? 'new' : 'in');
        if (getCookie('bp_stu')) {
            el('jn-note').textContent = 'You are already signed in.';
        }
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        gone: gone,
        signout: signout,mode: mode, signIn: signIn, signUp: signUp, signed: signed
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', JoinJs.reveal);
} else {
    JoinJs.reveal();
}
