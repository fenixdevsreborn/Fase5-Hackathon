// Helper minimo para o AnimatedCounter respeitar a preferencia de acessibilidade
// "prefers-reduced-motion". Carregado sob demanda via import() no OnAfterRenderAsync.
export function prefersReducedMotion() {
    try {
        return typeof window !== "undefined"
            && typeof window.matchMedia === "function"
            && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    } catch {
        return false;
    }
}
