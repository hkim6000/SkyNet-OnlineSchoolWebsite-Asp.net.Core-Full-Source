var AdminQuizzesJs = (function () {

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
        $ApiRequest('AdminQuizzes/' + method, JSON.stringify(list));
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
        var t = el('qa-toast');
        t.textContent = msg;
        t.classList.add('qa-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('qa-open');
        }, 4200);
    }

    function openMenu() {
        el('qa-rail').classList.add('qa-open');
        el('qa-scrim').classList.add('qa-open');
    }

    function closeMenu() {
        el('qa-rail').classList.remove('qa-open');
        el('qa-scrim').classList.remove('qa-open');
    }

    function openModal(title) {
        var m = el('qa-modal');
        el('qa-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('qa-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('qa-modal');
        if (m) {
            m.classList.remove('qa-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('qa-modal');
        if (!m || !m.classList.contains('qa-open')) {
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
        if (!askBox || !askBox.classList.contains('qa-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('qa-open');
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
        askBox.className = 'qa-ask';
        askBox.innerHTML = '<div class="qa-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="qa-err" role="alert"></em><div class="qa-askacts"><button type="button" class="qa-btn qa-btn-ghost"></button><button type="button" class="qa-btn"></button></div></div>';
        document.getElementById('qa-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('qa-open')) {
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
        x.b[1].className = 'qa-btn' + (o.danger ? ' qa-askdel' : '');
        askBox.firstChild.classList.toggle('qa-askin', input);
        askBox.classList.toggle('qa-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('qa-open');
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
        api('List', [{ key: 'q', vlu: state.q }, { key: 'pg', vlu: String(state.pg) }]);
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
            state.q = val('qa-q');
            state.pg = 1;
            list();
        });
    }

    function page(n) {
        state.pg = n;
        list();
    }

    function open(id) {
        api('Open', [{ key: 'qid', vlu: id }]);
    }

    function opened(title) {
        openModal(title);
    }

    var model = [];

    function esc(v) {
        return String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function form() {
        return el('qa-form');
    }

    function editable() {
        return form().getAttribute('data-edit') === '1';
    }

    function fillAfter(current) {
        var map = JSON.parse(form().getAttribute('data-lessons') || '{}');
        var lessons = map[val('qa-course')] || [];
        var html = '<option value="">End of course</option>';
        lessons.forEach(function (l) {
            html += '<option value="' + esc(l[0]) + '"' + (l[0] === current ? ' selected' : '') + '>After: ' + esc(l[1]) + '</option>';
        });
        el('qa-after').innerHTML = html;
    }

    function course() {
        markDirty();
        fillAfter('');
    }

    function draw() {
        var ro = editable() ? '' : ' disabled';
        var html = '';
        model.forEach(function (q, i) {
            html += '<div class="qa-qbox" data-i="' + i + '"><div class="qa-bar"><b>Question ' + (i + 1) + '</b>';
            if (editable()) {
                html += '<span class="qa-lbtns"><button type="button" class="qa-ibtn" aria-label="Move up"' + (i === 0 ? ' disabled' : '') + ' onclick="AdminQuizzesJs.moveQuestion(' + i + ', -1)">&uarr;</button><button type="button" class="qa-ibtn" aria-label="Move down"' + (i === model.length - 1 ? ' disabled' : '') + ' onclick="AdminQuizzesJs.moveQuestion(' + i + ', 1)">&darr;</button><button type="button" class="qa-btn qa-btn-sm qa-btn-danger" onclick="AdminQuizzesJs.removeQuestion(' + i + ')">Remove</button></span>';
            }
            html += '</div><label class="qa-field"><span>Question</span><input type="text" data-f="text" maxlength="200" value="' + esc(q.text) + '"' + ro + '></label><div class="qa-opts"><span class="qa-optlabel">Answers (choose the right one)</span>';
            q.options.forEach(function (o, j) {
                html += '<div class="qa-opt"><input type="radio" name="qa-ans-' + i + '" data-f="answer" value="' + j + '"' + (q.answer === j ? ' checked' : '') + ro + ' aria-label="Right answer"><input type="text" data-f="opt" data-j="' + j + '" maxlength="120" value="' + esc(o) + '"' + ro + ' aria-label="Answer ' + (j + 1) + '">';
                if (editable() && q.options.length > 2) {
                    html += '<button type="button" class="qa-ibtn" aria-label="Remove answer" onclick="AdminQuizzesJs.removeOption(' + i + ', ' + j + ')">&times;</button>';
                }
                html += '</div>';
            });
            if (editable() && q.options.length < 5) {
                html += '<button type="button" class="qa-btn qa-btn-sm qa-btn-ghost" onclick="AdminQuizzesJs.addOption(' + i + ')">Add an answer</button>';
            }
            html += '</div><label class="qa-field"><span>Why it is right (shown after the answer)</span><input type="text" data-f="why" maxlength="240" value="' + esc(q.why) + '"' + ro + '></label></div>';
        });
        el('qa-qs').innerHTML = html || '<p class="qa-hint">No questions yet.</p>';
    }

    function opened(title) {
        openModal(title);
        var raw = JSON.parse(form().getAttribute('data-q') || '[]');
        model = raw.map(function (q) {
            return { text: q.text, options: q.options.slice(), answer: q.answer, why: q.why || '' };
        });
        fillAfter(form().getAttribute('data-after') || '');
        draw();
        el('qa-qs').addEventListener('input', function (e) {
            var box = e.target.closest('.qa-qbox');
            if (!box) {
                return;
            }
            var q = model[parseInt(box.getAttribute('data-i'), 10)];
            var f = e.target.getAttribute('data-f');
            if (f === 'opt') {
                q.options[parseInt(e.target.getAttribute('data-j'), 10)] = e.target.value;
            } else if (f === 'answer') {
                q.answer = parseInt(e.target.value, 10);
            } else if (f) {
                q[f] = e.target.value;
            }
            markDirty();
        });
        dirty = false;
    }

    function addQuestion() {
        model.push({ text: '', options: ['', ''], answer: 0, why: '' });
        markDirty();
        draw();
    }

    function removeQuestion(i) {
        model.splice(i, 1);
        markDirty();
        draw();
    }

    function moveQuestion(i, d) {
        var q = model.splice(i, 1)[0];
        model.splice(i + d, 0, q);
        markDirty();
        draw();
    }

    function addOption(i) {
        model[i].options.push('');
        markDirty();
        draw();
    }

    function removeOption(i, j) {
        var q = model[i];
        q.options.splice(j, 1);
        if (q.answer >= q.options.length || q.answer === j) {
            q.answer = 0;
        } else if (q.answer > j) {
            q.answer -= 1;
        }
        markDirty();
        draw();
    }

    function save(id) {
        api('Save', [{ key: 'qid', vlu: id }, { key: 'qcourse', vlu: val('qa-course') }, { key: 'qafter', vlu: val('qa-after') }, { key: 'qtitle', vlu: val('qa-title') }, { key: 'qpass', vlu: val('qa-pass') }, { key: 'qdata', vlu: JSON.stringify(model) }]);
    }

    function saved() {
        toast('Saved.');
        shut();
        list();
    }

    function remove(id) {
        ask({ title: 'Delete this quiz?', text: 'Students no longer see it in the course. Their earlier results are kept on their enrollments.', ok: 'Delete', no: 'Keep it', danger: true }, function () {
            api('Remove', [{ key: 'qid', vlu: id }]);
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
        openModal: openModal,open: open, opened: opened, search: search, page: page, course: course, addQuestion: addQuestion, removeQuestion: removeQuestion, moveQuestion: moveQuestion, addOption: addOption, removeOption: removeOption, save: save, saved: saved, remove: remove
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminQuizzesJs.reveal);
} else {
    AdminQuizzesJs.reveal();
}
