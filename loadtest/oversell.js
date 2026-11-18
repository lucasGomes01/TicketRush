// Teste de carga com k6: 500 compradores disputam os MESMOS 10 assentos ao mesmo tempo.
// Resultado esperado: no máximo 10 reservas aceitas (201) e todo o resto recusado (409).
//
// O setup cria 100 contas (cada uma usada por 5 VUs). Como o cadastro tem rate limit por IP,
// suba a API com um limite maior antes de rodar:
//
//   AUTH_RATE_LIMIT=1000 docker compose up -d api
//   k6 run loadtest/oversell.js
import http from 'k6/http'
import { check } from 'k6'
import { Counter } from 'k6/metrics'

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080'
const EVENT_ID = '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05' // Pré-estreia Secreta (10 lugares)
const SEATS = ['A', 'B'].flatMap((row) => [1, 2, 3, 4, 5].map((n) => `SAL-${row}${n}`))
const USERS = 100
const JSON_HEADERS = { 'Content-Type': 'application/json' }

const accepted = new Counter('holds_accepted')
const rejected = new Counter('holds_rejected')

export const options = {
  setupTimeout: '120s',
  scenarios: {
    rush: { executor: 'per-vu-iterations', vus: 500, iterations: 1, maxDuration: '30s' },
  },
  thresholds: {
    holds_accepted: ['count<=10'], // nunca mais reservas do que assentos
    checks: ['rate==1'],
  },
}

export function setup() {
  const run = Date.now()
  const tokens = []
  for (let i = 0; i < USERS; i++) {
    const res = http.post(
      `${BASE_URL}/api/auth/register`,
      JSON.stringify({ name: `Comprador ${i}`, email: `k6-${run}-${i}@loadtest.local`, password: 'k6-loadtest-pass' }),
      { headers: JSON_HEADERS },
    )
    if (res.status !== 201) throw new Error(`Cadastro falhou (${res.status}). Subiu a API com AUTH_RATE_LIMIT=1000?`)
    tokens.push(res.json('token'))
  }
  return { tokens }
}

export default function ({ tokens }) {
  const seat = SEATS[Math.floor(Math.random() * SEATS.length)]
  const res = http.post(`${BASE_URL}/api/holds`, JSON.stringify({ eventId: EVENT_ID, seatIds: [seat] }), {
    headers: { ...JSON_HEADERS, Authorization: `Bearer ${tokens[__VU % tokens.length]}` },
    responseCallback: http.expectedStatuses(201, 409),
  })

  if (res.status === 201) accepted.add(1)
  else if (res.status === 409) rejected.add(1)

  check(res, { 'status é 201 ou 409': (r) => r.status === 201 || r.status === 409 })
}
