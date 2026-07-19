// Comportamentos globais, pequenos e sem dependencias: header, progresso de leitura,
// voltar ao topo e revelacao progressiva de secoes durante o scroll.
(function () {
    var revealObserver;
    var mutationObserver;
    var ticking = false;
    var initialized = false;

    function updateScrollUi() {
        var header = document.querySelector('.cs-site-header');
        var progress = document.querySelector('.cs-scroll-progress');
        var backToTop = document.querySelector('.cs-back-to-top');
        var scrollTop = window.scrollY || document.documentElement.scrollTop || 0;
        var max = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);

        if (header) {
            header.classList.toggle('cs-site-header--scrolled', scrollTop > 8);
        }
        if (progress) {
            progress.style.width = Math.min(100, (scrollTop / max) * 100) + '%';
        }
        if (backToTop) {
            backToTop.classList.toggle('is-visible', scrollTop > 520);
        }

        ticking = false;
    }

    function requestScrollUpdate() {
        if (!ticking) {
            ticking = true;
            window.requestAnimationFrame(updateScrollUi);
        }
    }

    function animateWithGsap(element, index) {
        element.classList.add('is-revealed');
        if (window.gsap) {
            gsap.fromTo(element, 
                { opacity: 0, y: 16 },
                { opacity: 1, y: 0, duration: 0.4, ease: 'power1.out' }
            );
        }
    }

    function prepareRevealElements(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var selectors = [
            '[data-reveal]',
            '.cs-feature-card',
            '.cs-panel',
            '.cs-data-shell',
            '.cs-section',
            '.cs-impact-strip',
            '.cs-institutions',
            '.cs-about',
            '.cs-tp-stat',
            '.cs-tp-main',
            '.cs-tp-side',
            '.cs-tp-campaign',
            '.campaign-stat',
            '.cs-card',
            '.cs-footer__cta'
        ].join(',');

        scope.querySelectorAll(selectors).forEach(function (element, index) {
            if (element.dataset.revealObserved === 'true') return;
            element.dataset.revealObserved = 'true';
            element.classList.add('js-reveal-ready');

            if (revealObserver) {
                element.dataset.revealIndex = index;
                revealObserver.observe(element);
            } else {
                element.classList.add('is-revealed');
            }
        });
    }

    function setupReveal() {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return;
        }

        if (window.gsap && window.ScrollTrigger) {
            window.gsap.registerPlugin(window.ScrollTrigger);
        }

        if ('IntersectionObserver' in window) {
            revealObserver = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) return;
                    var idx = parseInt(entry.target.dataset.revealIndex || '0', 10);
                    animateWithGsap(entry.target, idx);
                    revealObserver.unobserve(entry.target);
                });
            }, { rootMargin: '0px 0px -6% 0px', threshold: 0.05 });
        }

        prepareRevealElements(document);

        mutationObserver = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        prepareRevealElements(node.parentElement || node);
                    }
                });
            });
            requestScrollUpdate();
        });

        mutationObserver.observe(document.body, { childList: true, subtree: true });
    }

    function setupBackToTop() {
        document.addEventListener('click', function (event) {
            var button = event.target.closest && event.target.closest('.cs-back-to-top');
            if (button) {
                window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        });
    }

    window.conexaoSolidaria = window.conexaoSolidaria || {};
    window.conexaoSolidaria.checkHealth = async function () {
        try {
            var response = await fetch('/health', { cache: 'no-store', headers: { 'Accept': 'text/plain' } });
            return response.ok;
        } catch (_) {
            return false;
        }
    };

    function init() {
        if (initialized) return;
        initialized = true;
        setupBackToTop();
        setupReveal();
        updateScrollUi();
    }

    window.addEventListener('scroll', requestScrollUpdate, { passive: true });
    window.addEventListener('resize', requestScrollUpdate, { passive: true });
    window.addEventListener('load', init, { once: true });
    document.addEventListener('DOMContentLoaded', init, { once: true });
    document.addEventListener('enhancedload', function () {
        prepareRevealElements(document);
        requestScrollUpdate();
    });

    if (document.readyState !== 'loading') init();
})();
