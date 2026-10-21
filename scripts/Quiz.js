var QuizJs = (function () {

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
        $ApiRequest('Quiz/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('qz-toast');
        t.textContent = msg;
        t.classList.add('qz-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('qz-open');
        }, 3600);
    }

    function openNav() {
        el('qz-nav').classList.add('qz-open');
        el('qz-scrim').classList.add('qz-open');
    }

    function closeNav() {
        el('qz-nav').classList.remove('qz-open');
        el('qz-scrim').classList.remove('qz-open');
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
        var a = el('qz-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('qz-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('qz-open');
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
        askBox.className = 'qz-ask';
        askBox.innerHTML = '<div class="qz-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="qz-err" role="alert"></em><div class="qz-askacts"><button type="button" class="qz-btn qz-btn-ghost"></button><button type="button" class="qz-btn"></button></div></div>';
        el('qz-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('qz-open')) {
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
        x.b[1].className = 'qz-btn' + (o.danger ? ' qz-btn-danger' : '');
        askBox.firstChild.classList.toggle('qz-askin', input);
        askBox.classList.toggle('qz-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('qz-open');
        if (input) {
            x.t.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    var count = 0;

    function qs(k) {
        var m = location.search.match(new RegExp('[?&]' + k + '=([^&]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function load() {
        scall('Load', [{ key: 'qc', vlu: qs('c') }, { key: 'qq', vlu: qs('q') }]);
    }

    function ready(n) {
        count = n;
    }

    function submit() {
        var list = [];
        for (var i = 0; i < count; i++) {
            var r = document.querySelector('input[name="qz-r' + i + '"]:checked');
            list.push(r ? r.value : '-1');
        }
        scall('Submit', [{ key: 'qc', vlu: qs('c') }, { key: 'qq', vlu: qs('q') }, { key: 'ans', vlu: list.join(',') }]);
        return false;
    }

    function graded(passed) {
        var r = el('qz-result');
        if (r && r.scrollIntoView) {
            r.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        var f = el('qz-form');
        if (f && passed) {
            f.style.display = 'none';
        }
    }

    function again() {
        el('qz-result').innerHTML = '';
        var f = el('qz-form');
        f.reset();
        f.style.display = '';
        window.scrollTo({ top: 0, behavior: 'smooth' });
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
        signout: signout,ready: ready, submit: submit, graded: graded, again: again
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', QuizJs.reveal);
} else {
    QuizJs.reveal();
}
