var AdminRefundsJs = (function () {

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
        $ApiRequest('AdminRefunds/' + method, JSON.stringify(list));
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
        var t = el('rf-toast');
        t.textContent = msg;
        t.classList.add('rf-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rf-open');
        }, 4200);
    }

    function openMenu() {
        el('rf-rail').classList.add('rf-open');
        el('rf-scrim').classList.add('rf-open');
    }

    function closeMenu() {
        el('rf-rail').classList.remove('rf-open');
        el('rf-scrim').classList.remove('rf-open');
    }

    function openModal(title) {
        var m = el('rf-modal');
        el('rf-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('rf-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('rf-modal');
        if (m) {
            m.classList.remove('rf-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('rf-modal');
        if (!m || !m.classList.contains('rf-open')) {
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
        if (!askBox || !askBox.classList.contains('rf-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('rf-open');
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
        askBox.className = 'rf-ask';
        askBox.innerHTML = '<div class="rf-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="rf-err" role="alert"></em><div class="rf-askacts"><button type="button" class="rf-btn rf-btn-ghost"></button><button type="button" class="rf-btn"></button></div></div>';
        document.getElementById('rf-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('rf-open')) {
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
        x.b[1].className = 'rf-btn' + (o.danger ? ' rf-askdel' : '');
        askBox.firstChild.classList.toggle('rf-askin', input);
        askBox.classList.toggle('rf-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('rf-open');
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

    var state = { q: '', st: 'open', pg: 1 };

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
            state.q = val('rf-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'rid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
        kind();
    }

    function picked() {
        var r = document.querySelector('input[name="rf-kind"]:checked');
        return r ? r.value : '';
    }

    function kind() {
        var k = picked();
        var box = el('rf-amtbox');
        var label = el('rf-notelabel');
        if (box) {
            box.hidden = k !== 'partial';
        }
        if (label) {
            label.textContent = k === 'decline' ? 'Reason for declining (the student sees it)' : 'Note to the student (optional)';
        }
    }

    function decide(id, maxPartial, left) {
        var k = picked();
        if (!k) {
            toast('Choose full refund, partial refund or decline.');
            return;
        }
        var send = function () {
            api('Decide', [{ key: 'rid', vlu: id }, { key: 'kind', vlu: k }, { key: 'amount', vlu: val('rf-amt') }, { key: 'note', vlu: val('rf-note') }]);
        };
        if (k === 'full') {
            ask({ title: 'Refund in full?', text: '$' + left.toFixed(2) + ' goes back to the student. Access to the course ends and a certificate is revoked.', ok: 'Refund in full', no: 'Cancel', danger: true }, send);
        } else if (k === 'partial') {
            var a = parseFloat(val('rf-amt'));
            if (!(a > 0)) {
                toast('Enter the amount to refund.');
                return;
            }
            if (a > maxPartial) {
                toast('The most you can refund as goodwill is $' + maxPartial.toFixed(2) + '.');
                return;
            }
            ask({ title: 'Refund $' + a.toFixed(2) + '?', text: 'The student keeps access to the course.', ok: 'Refund', no: 'Cancel' }, send);
        } else {
            if (val('rf-note').length < 3) {
                toast('Write the reason for declining. The student will see it.');
                return;
            }
            send();
        }
    }

    function decided(ok, message) {
        toast(message);
        if (ok) {
            shut();
            list();
        }
    }

    function reopen(id) {
        ask({ title: 'Reopen this request?', text: 'It goes back to the waiting list so it can be decided again.', ok: 'Reopen', no: 'Cancel' }, function () {
            api('Reopen', [{ key: 'rid', vlu: id }]);
        });
    }

    function reopened(ok) {
        toast(ok ? 'The request is open again.' : 'The request could not be reopened. Another request on the same order may be open.');
        if (ok) {
            shut();
            list();
        }
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
        openModal: openModal,open: open, opened: opened, kind: kind, decide: decide, decided: decided, reopen: reopen, reopened: reopened, chip: chip, search: search, page: page
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminRefundsJs.reveal);
} else {
    AdminRefundsJs.reveal();
}
