var AdminLoginJs = (function () {

    var changing = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? e.value : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function call(method) {
        var list = [{ key: 'luser', vlu: val('lg-user').trim() }, { key: 'lpass', vlu: val('lg-pass') }];
        if (changing) {
            list.push({ key: 'lnew', vlu: val('lg-new') });
            list.push({ key: 'lnew2', vlu: val('lg-new2') });
        }
        el('lg-go').disabled = true;
        setTimeout(function () {
            el('lg-go').disabled = false;
        }, 6000);
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list));
    }

    function submit() {
        call(changing ? 'Choose' : 'SignIn');
        return false;
    }

    function mustChange() {
        changing = true;
        el('lg-change').hidden = false;
        el('lg-go').textContent = 'Save and sign in';
        el('lg-go').disabled = false;
        el('lg-new').focus();
    }

    function signed(tok) {
        document.cookie = 'bp_tok=' + encodeURIComponent(tok) + '; path=' + base() + '; max-age=' + (8 * 3600) + '; samesite=lax';
        location.href = 'Admin';
    }

    function reveal() {
        var note = el('lg-note');
        new MutationObserver(function () {
            el('lg-go').disabled = false;
        }).observe(note, { childList: true, characterData: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', reveal);
    } else {
        reveal();
    }

    return { submit: submit, mustChange: mustChange, signed: signed };
})();
