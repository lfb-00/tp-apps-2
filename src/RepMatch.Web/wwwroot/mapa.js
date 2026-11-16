// Mapa de casas de repuestos cercanas (componente CasasCercanas). Dos cosas que solo puede hacer
// el navegador: pedir la ubicacion (con el permiso de la persona) y dibujar el mapa con Leaflet y
// las teselas de OpenStreetMap. Lo que se busca y lo que se muestra lo decide el servidor.
//
// Ojo con la privacidad: las coordenadas van solo al servidor de RepMatch, pero al dibujar el mapa
// el navegador le pide a tile.openstreetmap.org las imagenes de esa zona, asi que OpenStreetMap ve
// la zona aproximada y la IP. Por eso Leaflet se carga recien aca, al dibujar el primer mapa, y no
// en todas las paginas: quien nunca abre el mapa no le pide nada a cdnjs ni a OpenStreetMap.

// Version fija con hash (SRI): si el CDN sirviera otro archivo, el navegador no lo ejecuta.
const LEAFLET = {
    css: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.css",
    cssHash: "sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=",
    js: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.js",
    jsHash: "sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="
};

let cargandoLeaflet = null;

// Carga Leaflet una sola vez. Resuelve false si no se pudo (sin internet hacia el CDN o con el
// hash cambiado): el componente igual muestra la lista.
function cargarLeaflet() {
    if (window.L) {
        return Promise.resolve(true);
    }
    if (cargandoLeaflet) {
        return cargandoLeaflet;
    }

    const estilos = document.createElement("link");
    estilos.rel = "stylesheet";
    estilos.href = LEAFLET.css;
    estilos.integrity = LEAFLET.cssHash;
    estilos.crossOrigin = "anonymous";
    document.head.appendChild(estilos);

    cargandoLeaflet = new Promise(resolver => {
        const script = document.createElement("script");
        script.src = LEAFLET.js;
        script.integrity = LEAFLET.jsHash;
        script.crossOrigin = "anonymous";
        script.onload = () => resolver(Boolean(window.L));
        script.onerror = () => {
            // Se puede volver a intentar con el proximo mapa.
            script.remove();
            cargandoLeaflet = null;
            resolver(false);
        };
        document.head.appendChild(script);
    });
    return cargandoLeaflet;
}

// La ubicacion del navegador. Rechaza con "denegada" si la persona no da permiso y con
// "no-disponible" si el navegador no puede ubicarse: el componente ofrece escribir la zona.
export function ubicacion() {
    return new Promise((resolver, rechazar) => {
        if (!navigator.geolocation) {
            rechazar(new Error("no-disponible"));
            return;
        }
        navigator.geolocation.getCurrentPosition(
            p => resolver({ latitud: p.coords.latitude, longitud: p.coords.longitude }),
            e => rechazar(new Error(e.code === 1 ? "denegada" : "no-disponible")),
            { timeout: 10000, maximumAge: 600000 });
    });
}

const mapas = new Map();

// Dibuja (o vuelve a dibujar) el mapa en el elemento con ese id. Devuelve false si Leaflet no
// cargo (sin internet hacia el CDN): la lista igual queda en la pagina.
export async function mostrar(id, centro, casas) {
    if (!await cargarLeaflet()) {
        return false;
    }
    // Se busca despues de cargar: mientras tanto la pagina pudo volver a dibujarse.
    const elemento = document.getElementById(id);
    if (!elemento) {
        return false;
    }
    quitar(id);

    const mapa = L.map(elemento, { scrollWheelZoom: false }).setView([centro.latitud, centro.longitud], 13);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(mapa);

    const puntos = [];
    for (const casa of casas) {
        // textContent y no HTML: los nombres los carga cualquiera en OpenStreetMap.
        const globo = document.createElement("div");
        const nombre = document.createElement("strong");
        nombre.textContent = casa.nombre;
        globo.appendChild(nombre);
        if (casa.direccion) {
            globo.appendChild(document.createElement("br"));
            globo.appendChild(document.createTextNode(casa.direccion));
        }
        L.marker([casa.latitud, casa.longitud], { title: casa.nombre }).addTo(mapa).bindPopup(globo);
        puntos.push([casa.latitud, casa.longitud]);
    }
    if (puntos.length > 0) {
        mapa.fitBounds(puntos, { padding: [28, 28], maxZoom: 15 });
    }

    mapas.set(id, mapa);
    return true;
}

export function quitar(id) {
    const anterior = mapas.get(id);
    if (anterior) {
        anterior.remove();
        mapas.delete(id);
    }
}
