// Mapa de casas de repuestos cercanas (componente CasasCercanas). Dos cosas que solo puede hacer
// el navegador: pedir la ubicacion (con el permiso de la persona) y dibujar el mapa con Leaflet y
// las teselas de OpenStreetMap. Lo que se busca y lo que se muestra lo decide el servidor.

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
export function mostrar(id, centro, casas) {
    const elemento = document.getElementById(id);
    if (!window.L || !elemento) {
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
