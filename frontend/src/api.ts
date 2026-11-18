// Cliente da API. O X-Client-Id identifica o comprador anônimo (gerado uma vez por navegador).

export type EventSummary = {
  id: string
  title: string
  category: string
  venue: string
  city: string
  startsAt: string
  minPrice: number
  totalSeats: number
}

export type Section = { code: string; name: string; price: number; rows: number; seatsPerRow: number }

export type EventDetails = Omit<EventSummary, 'minPrice'> & { description: string; sections: Section[] }

export type SeatMap = { eventId: string; sold: string[]; held: string[] }

export type Hold = {
  holdId: string
  eventId: string
  seats: { seatId: string; sectionName: string; price: number }[]
  total: number
  expiresAt: string
}

export type OrderStatus = 'Pending' | 'Paid' | 'Completed' | 'Refunded' | 'Expired'

export type Order = {
  id: string
  status: OrderStatus
  eventId: string
  eventTitle: string
  eventVenue: string
  eventStartsAt: string
  customerName: string
  customerEmail: string
  total: number
  items: { seatId: string; sectionName: string; price: number }[]
  tickets: { id: string; code: string; seatId: string; price: number; issuedAt: string }[]
  createdAt: string
  failureReason: string | null
  holdExpiresAt: string | null
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public details?: { seatIds?: string[] },
  ) {
    super(message)
  }
}

function getClientId() {
  const key = 'ticketrush:clientId'
  let id = localStorage.getItem(key)
  if (!id) {
    id = crypto.randomUUID()
    localStorage.setItem(key, id)
  }
  return id
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', 'X-Client-Id': getClientId(), ...init?.headers },
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : null
    throw new ApiError(response.status, validation || problem?.title || 'Erro inesperado.', problem?.details)
  }

  return response.status === 204 ? (undefined as T) : response.json()
}

export const api = {
  listEvents: () => request<EventSummary[]>('/api/events'),
  getEvent: (id: string) => request<EventDetails>(`/api/events/${id}`),
  getSeatMap: (id: string) => request<SeatMap>(`/api/events/${id}/seats`),
  createHold: (eventId: string, seatIds: string[]) =>
    request<Hold>('/api/holds', { method: 'POST', body: JSON.stringify({ eventId, seatIds }) }),
  getHold: (holdId: string) => request<Hold>(`/api/holds/${holdId}`),
  releaseHold: (holdId: string) => request<void>(`/api/holds/${holdId}`, { method: 'DELETE' }),
  createOrder: (holdId: string, customerName: string, customerEmail: string) =>
    request<Order>('/api/orders', { method: 'POST', body: JSON.stringify({ holdId, customerName, customerEmail }) }),
  payOrder: (orderId: string) => request<Order>(`/api/orders/${orderId}/pay`, { method: 'POST' }),
  getOrder: (orderId: string) => request<Order>(`/api/orders/${orderId}`),
}

// Pedidos feitos neste navegador (para a página "Meus ingressos").
const ordersKey = 'ticketrush:orders'
export const myOrders = {
  list: (): string[] => JSON.parse(localStorage.getItem(ordersKey) ?? '[]'),
  add: (id: string) => {
    const ids = myOrders.list().filter((x) => x !== id)
    localStorage.setItem(ordersKey, JSON.stringify([id, ...ids].slice(0, 20)))
  },
}
