var CheckoutJs = (function () {

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
        $ApiRequest('Checkout/' + method, JSON.stringify(list || []));
    }

    function scall(method, list) {
        list = list || [];
        list.push({ key: 'stok', vlu: getCookie('bp_stu') });
        call(method, list);
    }

    function toast(msg) {
        var t = el('ck-toast');
        t.textContent = msg;
        t.classList.add('ck-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ck-open');
        }, 3600);
    }

    function openNav() {
        el('ck-nav').classList.add('ck-open');
        el('ck-scrim').classList.add('ck-open');
    }

    function closeNav() {
        el('ck-nav').classList.remove('ck-open');
        el('ck-scrim').classList.remove('ck-open');
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
        var a = el('ck-acct');
        if (a && getCookie('bp_stu')) {
            a.textContent = 'My courses';
            a.setAttribute('href', 'MyCourses');
        }
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), t: askBox.querySelector('textarea'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ck-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.t.value.trim(), need = askBox.getAttribute('data-need') === '1';
        if (ok && need && v.length < 3) {
            x.e.textContent = 'Please write a short reason.';
            x.t.focus();
            return;
        }
        askBox.classList.remove('ck-open');
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
        askBox.className = 'ck-ask';
        askBox.innerHTML = '<div class="ck-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><textarea maxlength="500" aria-label="Reason"></textarea><em class="ck-err" role="alert"></em><div class="ck-askacts"><button type="button" class="ck-btn ck-btn-ghost"></button><button type="button" class="ck-btn"></button></div></div>';
        el('ck-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', function () { askClose(true); });
        x.t.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ck-open')) {
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
        x.b[1].className = 'ck-btn' + (o.danger ? ' ck-btn-danger' : '');
        askBox.firstChild.classList.toggle('ck-askin', input);
        askBox.classList.toggle('ck-askin', input);
        x.t.value = input ? o.value : '';
        x.t.placeholder = o.place || '';
        askBox.classList.add('ck-open');
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

    function order() {
        return [{ key: 'oid', vlu: qs('o') }];
    }

    function load() {
        var list = order();
        list.push({ key: 'cback', vlu: qs('back') });
        scall('Load', list);
    }

    function start() {
        scall('Start', order());
    }

    function demo() {
        scall('DemoPay', order());
    }

    function go(url) {
        location.href = url;
    }

    function paid() {
        toast('Payment received');
    }

    function cancel() {
        ask({ title: 'Cancel this order?', text: 'No payment has been taken. You can start a new order from the course page.', ok: 'Cancel order', no: 'Keep it', danger: true }, function () {
            scall('Cancel', order());
        });
    }

    function cancelled() {
        location.href = 'MyCourses';
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
        signout: signout,start: start, demo: demo, go: go, paid: paid, cancel: cancel, cancelled: cancelled
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', CheckoutJs.reveal);
} else {
    CheckoutJs.reveal();
}
