import http from 'k6/http';
import { b64encode } from 'k6/encoding';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const escenario = __ENV.ESCENARIO || 'catalogo';
const vus = Number(__ENV.VUS || 10);
const duracion = __ENV.DURACION || '30s';
const iteracionesCola = Number(__ENV.MENSAJES || 200);
const usuariosCola = Number(__ENV.VUS_COLA || 10);

const catalogoMs = new Trend('catalogo_ms', true);
const diagnosticoMs = new Trend('diagnostico_ms', true);
const diagnosticoFallback = new Counter('diagnostico_fallback');
const diagnosticoLlm = new Counter('diagnostico_llm');
const publicacionColaMs = new Trend('cola_publicacion_ms', true);
const drenadoColaMs = new Trend('cola_drenado_ms', true);
const publicacionFallida = new Rate('cola_publicacion_fallida');
const colaDrenada = new Rate('cola_drenada');

const escenarios = {
  catalogo: {
    executor: 'ramping-vus',
    startVUs: 1,
    stages: [
      { duration: '10s', target: vus },
      { duration: duracion, target: vus },
      { duration: '5s', target: 0 }
    ]
  },
  diagnostico: {
    executor: 'ramping-vus',
    startVUs: 1,
    stages: [
      { duration: '10s', target: vus },
      { duration: duracion, target: vus },
      { duration: '5s', target: 0 }
    ]
  },
  cola: {
    executor: 'shared-iterations',
    vus: usuariosCola,
    iterations: iteracionesCola,
    maxDuration: '2m'
  }
};

if (!Object.prototype.hasOwnProperty.call(escenarios, escenario)) {
  throw new Error(`ESCENARIO desconocido: ${escenario}`);
}

const thresholds = { http_req_failed: ['rate<0.01'] };
if (escenario === 'cola') {
  thresholds.cola_publicacion_fallida = ['rate<0.01'];
  thresholds.cola_drenada = ['rate==1'];
}

export const options = {
  scenarios: {
    [escenario]: {
      ...escenarios[escenario],
      exec: escenario
    }
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  thresholds
};

export function catalogo() {
  const modo = __ENV.MODO || 'remoto';
  const base = modo === 'local' ? 'http://web-local:8080' : 'http://web:8080';
  const response = http.get(
    `${base}/evidencia/catalogo?marca=Volkswagen&modelo=Gol&anio=2015&motor=1.6`,
    { tags: { modo } }
  );
  catalogoMs.add(response.timings.duration, { modo });
  check(response, {
    [`catalogo ${modo} responde 200`]: (r) => r.status === 200,
    [`catalogo ${modo} devuelve su modo`]: (r) =>
      r.status === 200 && r.json('modo').toLowerCase() === modo
  });
  sleep(0.1);
}

export function diagnostico() {
  const body = JSON.stringify({
    texto: 'Al frenar, el auto vibra y se escucha un chirrido en las ruedas delanteras.',
    vehiculo: { marca: 'Volkswagen', modelo: 'Gol', anio: 2015, motor: '1.6' },
    candidatos: [
      { codigo: 'past-vw-gol-del', nombre: 'Pastillas de freno delanteras', sistema: 'Frenos' },
      { codigo: 'disco-vw-gol-del', nombre: 'Discos de freno delanteros', sistema: 'Frenos' },
      { codigo: 'amort-vw-gol-del', nombre: 'Amortiguadores delanteros', sistema: 'Suspension' }
    ]
  });
  const response = http.post('http://ia-api:8080/api/diagnostico', body, {
    headers: { 'Content-Type': 'application/json' }
  });
  diagnosticoMs.add(response.timings.duration);
  const ok = check(response, {
    'diagnostico responde 200': (r) => r.status === 200,
    'diagnostico informa fuente': (r) => r.status === 200 && Boolean(r.json('fuente'))
  });
  if (ok) {
    const fuente = response.json('fuente');
    if (fuente === 'fallback-local') {
      diagnosticoFallback.add(1);
    } else if (fuente === 'llm') {
      diagnosticoLlm.add(1);
    }
  }
  sleep(0.1);
}

export function setup() {
  if (escenario !== 'cola') {
    return null;
  }

  const credentials = `${__ENV.RABBITMQ_USUARIO || 'repmatch'}:${__ENV.RABBITMQ_CONTRASENA || 'repmatch'}`;
  const params = {
    headers: {
      Authorization: `Basic ${b64encode(credentials)}`,
      'Content-Type': 'application/json'
    }
  };
  const url = 'http://rabbitmq:15672/api/queues/%2f/repmatch.actividad';
  for (let intento = 0; intento < 60; intento++) {
    const response = http.get(url, params);
    if (response.status === 200) {
      const datos = response.json();
      if (datos.messages_ready === 0 && datos.messages_unacknowledged === 0) {
        return { inicio: Date.now() };
      }
    }
    sleep(1);
  }
  throw new Error('La cola repmatch.actividad no se vacio antes de la prueba de carga.');
}

export function cola(data) {
  const identificador = guid();
  const mensaje = {
    mensajeId: identificador,
    tipo: 'busqueda.creada',
    version: 1,
    ocurridoEn: new Date().toISOString(),
    correlationId: `carga-${identificador}`,
    datos: {
      busquedaId: identificador,
      clienteId: '00000000-0000-4000-8000-000000000001',
      marca: 'Volkswagen',
      modelo: 'Gol',
      anio: 2015,
      motor: '1.6'
    }
  };
  const credentials = `${__ENV.RABBITMQ_USUARIO || 'repmatch'}:${__ENV.RABBITMQ_CONTRASENA || 'repmatch'}`;
  const response = http.post(
    'http://rabbitmq:15672/api/exchanges/%2f/repmatch.eventos/publish',
    JSON.stringify({
      properties: { delivery_mode: 2 },
      routing_key: mensaje.tipo,
      payload: JSON.stringify(mensaje),
      payload_encoding: 'string'
    }),
    {
      headers: {
        Authorization: `Basic ${b64encode(credentials)}`,
        'Content-Type': 'application/json'
      }
    }
  );
  publicacionColaMs.add(response.timings.duration);
  const ok = check(response, {
    'RabbitMQ acepta el mensaje': (r) => r.status === 200 && r.json('routed') === true
  });
  publicacionFallida.add(!ok);
}

export function teardown() {
  if (escenario !== 'cola') {
    return;
  }

  const credentials = `${__ENV.RABBITMQ_USUARIO || 'repmatch'}:${__ENV.RABBITMQ_CONTRASENA || 'repmatch'}`;
  const params = {
    headers: { Authorization: `Basic ${b64encode(credentials)}` }
  };
  const url = 'http://rabbitmq:15672/api/queues/%2f/repmatch.actividad';
  const inicio = Date.now();
  for (let intento = 0; intento < 120; intento++) {
    const response = http.get(url, params);
    if (response.status === 200) {
      const datos = response.json();
      if (datos.messages_ready === 0 && datos.messages_unacknowledged === 0) {
        const ms = Date.now() - inicio;
        drenadoColaMs.add(ms);
        colaDrenada.add(true);
        console.log(`La cola dreno ${iteracionesCola} mensajes en ${ms} ms.`);
        return;
      }
    }
    sleep(0.5);
  }
  drenadoColaMs.add(Date.now() - inicio);
  colaDrenada.add(false);
  console.error('La cola no se vacio dentro de los 60 segundos.');
}

function guid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (caracter) => {
    const valor = Math.floor(Math.random() * 16);
    return (caracter === 'x' ? valor : (valor & 0x3) | 0x8).toString(16);
  });
}
