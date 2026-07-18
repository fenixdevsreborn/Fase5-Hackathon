// Alterna o fundo branco do header conforme o scroll da pagina.
// Listener global (uma vez): consulta o header a cada evento, entao funciona mesmo
// com o header sendo (re)renderizado pelo Blazor Server e atravessa navegacoes SPA.
(function () {
    function updateHeader() {
        var header = document.querySelector('.cs-site-header');
        if (header) {
            header.classList.toggle('cs-site-header--scrolled', window.scrollY > 8);
        }
    }

    window.addEventListener('scroll', updateHeader, { passive: true });
    window.addEventListener('load', updateHeader);
    document.addEventListener('DOMContentLoaded', updateHeader);
    updateHeader();
})();
