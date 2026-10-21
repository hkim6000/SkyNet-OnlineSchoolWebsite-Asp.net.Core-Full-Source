var AdminCoursesJs = (function () {

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
        $ApiRequest('AdminCourses/' + method, JSON.stringify(list));
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
        var t = el('cu-toast');
        t.textContent = msg;
        t.classList.add('cu-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('cu-open');
        }, 4200);
    }

    function openMenu() {
        el('cu-rail').classList.add('cu-open');
        el('cu-scrim').classList.add('cu-open');
    }

    function closeMenu() {
        el('cu-rail').classList.remove('cu-open');
        el('cu-scrim').classList.remove('cu-open');
    }

    function openModal(title) {
        var m = el('cu-modal');
        el('cu-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('cu-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('cu-modal');
        if (m) {
            m.classList.remove('cu-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('cu-modal');
        if (!m || !m.classList.contains('cu-open')) {
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
        if (!askBox || !askBox.classList.contains('cu-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('cu-open');
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
        askBox.className = 'cu-ask';
        askBox.innerHTML = '<div class="cu-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="cu-err" role="alert"></em><div class="cu-askacts"><button type="button" class="cu-btn cu-btn-ghost"></button><button type="button" class="cu-btn"></button></div></div>';
        document.getElementById('cu-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('cu-open')) {
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
        x.b[1].className = 'cu-btn' + (o.danger ? ' cu-askdel' : '');
        askBox.firstChild.classList.toggle('cu-askin', input);
        askBox.classList.toggle('cu-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('cu-open');
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
            state.q = val('cu-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'cid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
    }

    function picked(id) {
        markDirty();
        var img = el(id + '-prev');
        var s = el(id);
        if (img) {
            img.hidden = !s.value;
            img.src = s.value;
        }
    }

    function flag(id) {
        var e = el(id);
        return e && e.checked ? '1' : '0';
    }

    function save(id) {
        var list = [{ key: 'cid', vlu: id }, { key: 'ctitle', vlu: val('cu-title') }, { key: 'ctopic', vlu: val('cu-topic') }, { key: 'clevel', vlu: val('cu-level') }, { key: 'cprice', vlu: val('cu-price') }, { key: 'csummary', vlu: val('cu-summary') }, { key: 'cdesc', vlu: val('cu-desc') }, { key: 'coutc', vlu: val('cu-outc') }, { key: 'ccover', vlu: val('cu-cover') }, { key: 'cfeat', vlu: flag('cu-feat') }];
        if (el('cu-instr')) {
            list.push({ key: 'cinstr', vlu: val('cu-instr') });
        }
        api('Save', list);
    }

    function saved(id, fresh) {
        toast(fresh ? 'Draft created. Add lessons next.' : 'Course saved.');
        shut();
        list();
        if (fresh) {
            open(id);
        }
    }

    function publish(id, live) {
        var run = function () {
            api('Publish', [{ key: 'cid', vlu: id }]);
        };
        if (live) {
            ask({ title: 'Unpublish this course?', text: 'It disappears from the catalog. Students who already joined keep access.', ok: 'Unpublish', no: 'Cancel', danger: true }, run);
        } else {
            run();
        }
    }

    function remove(id) {
        ask({ title: 'Delete this course?', text: 'The course, its lessons and its quizzes are removed. This cannot be undone.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('Remove', [{ key: 'cid', vlu: id }]);
        });
    }

    function changed(message, id) {
        toast(message);
        shut();
        list();
        if (id) {
            open(id);
        }
    }

    function lesson(cid, lid) {
        api('LessonForm', [{ key: 'cid', vlu: cid }, { key: 'lid', vlu: lid }]);
    }

    function lessonOpened(title) {
        el('cu-mtitle').textContent = title;
        dirty = false;
        el('cu-modal').scrollTop = 0;
    }

    function back(cid) {
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, function () {
                open(cid);
            });
            return;
        }
        open(cid);
    }

    function saveLesson(cid, lid) {
        api('SaveLesson', [{ key: 'cid', vlu: cid }, { key: 'lid', vlu: lid }, { key: 'ltitle', vlu: val('cu-ltitle') }, { key: 'lmin', vlu: val('cu-lmin') }, { key: 'lvideo', vlu: val('cu-lvideo') }, { key: 'lposter', vlu: val('cu-lposter') }, { key: 'lsum', vlu: val('cu-lsum') }, { key: 'lnotes', vlu: val('cu-lnotes') }, { key: 'lfree', vlu: flag('cu-lfree') }]);
    }

    function lessonSaved(cid) {
        toast('Saved.');
        dirty = false;
        list();
        open(cid);
    }

    function removeLesson(cid, lid) {
        ask({ title: 'Delete this lesson?', text: 'Students lose it from the course. If it was the last lesson, the course goes back to a draft.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('RemoveLesson', [{ key: 'cid', vlu: cid }, { key: 'lid', vlu: lid }]);
        });
    }

    function move(cid, lid, dir) {
        api('Move', [{ key: 'cid', vlu: cid }, { key: 'lid', vlu: lid }, { key: 'dir', vlu: String(dir) }]);
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
        openModal: openModal,open: open, opened: opened, chip: chip, search: search, page: page, save: save, saved: saved, publish: publish, remove: remove, changed: changed, lesson: lesson, lessonOpened: lessonOpened, back: back, saveLesson: saveLesson, lessonSaved: lessonSaved, removeLesson: removeLesson, move: move, picked: picked
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminCoursesJs.reveal);
} else {
    AdminCoursesJs.reveal();
}
