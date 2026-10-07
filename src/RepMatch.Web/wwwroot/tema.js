// Tema claro/oscuro. El script inline de App.razor ya puso data-theme antes del primer
// pintado; este módulo lo lee y lo cambia desde Blazor.
const CLAVE = "repmatch-tema";

export function obtener() {
    return document.documentElement.getAttribute("data-theme") === "dark" ? "oscuro" : "claro";
}

export function aplicar(tema) {
    document.documentElement.setAttribute("data-theme", tema === "oscuro" ? "dark" : "light");
    try { localStorage.setItem(CLAVE, tema); } catch { /* almacenamiento bloqueado */ }
}
