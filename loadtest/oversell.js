// Teste de carga com k6: 500 compradores disputam os MESMOS 10 assentos ao mesmo tempo.
// Resultado esperado: exatamente 10 reservas aceitas (201) e todo o resto recusado (409).
//
//   docker compose up -d
//   k6 run loadtest/oversell.js
//
// (o rate limit é por X-Client-Id; cada VU usa um id diferente)
import http from 'k6/http'
import { check } from 'k6'
import { Counter } from 'k6/metrics'

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080'
const EVENT_ID = '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05' // Pré-estreia Secreta (10 lugares)
const SEATS = ['A', 'B'].flatMap((row) => [1, 2, 3, 4, 5].map((n) => `SAL-${row}${n}`))

const accepted = new Counter('holds_accepted')
const rejected = new Counter('holds_rejected')

export const options = {
  scenarios: {
    rush: { executor: 'per-vu-iterations', vus: 500, iterations: 1, maxDuration: '30s' },
  },
  thresholds: {
    holds_accepted: ['count<=10'], // nunca mais reservas do que assentos
    http_req_failed: [{ threshold: 'rate<1', abortOnFail: false }],
  },
}

export default function () {
  const seat = SEATS[Math.floor(Math.random() * SEATS.length)]
  const res = http.post(`${BASE_URL}/api/holds`, JSON.stringify({ eventId: EVENT_ID, seatIds: [seat] }), {
    headers: { 'Content-Type': 'application/json', 'X-Client-Id': `k6-${__VU}-${Date.now()}` },
    responseCallback: http.expectedStatuses(201, 409),
  })

  if (res.status === 201) accepted.add(1)
  else if (res.status === 409) rejected.add(1)

  check(res, { 'status é 201 ou 409': (r) => r.status === 201 || r.status === 409 })
}
