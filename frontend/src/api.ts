// Cliente da API. Rotas de reserva e pedido exigem o JWT do usuário logado (header Authorization).

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

export type OrderSummary = {
  id: string
  status: OrderStatus
  eventTitle: string
  eventVenue: string
  eventStartsAt: string
  total: number
  createdAt: string
  seatIds: string[]
}

export type User = { id: string; name: string; email: string; isAdmin: boolean }

export type AuthResponse = { token: string; expiresAt: string; user: User }

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public details?: { seatIds?: string[] },
  ) {
    super(message)
  }
}

// Sessão: o token fica no localStorage e expira em 8 horas (o servidor também valida).
const sessionKey = 'ticketrush:session'

export const session = {
  get(): AuthResponse | null {
    try {
      const saved = JSON.parse(localStorage.getItem(sessionKey) ?? 'null') as AuthResponse | null
      return saved && new Date(saved.expiresAt) > new Date() ? saved : null
    } catch {
      return null
    }
  },
  save(auth: AuthResponse) {
    localStorage.setItem(sessionKey, JSON.stringify(auth))
  },
  clear() {
    localStorage.removeItem(sessionKey)
    window.dispatchEvent(new Event('ticketrush:logout'))
  },
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = session.get()?.token
  const response = await fetch(path, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    // Token expirado ou inválido: encerra a sessão para a interface pedir login de novo.
    if (response.status === 401 && token) session.clear()
    const problem = await response.json().catch(() => null)
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : null
    throw new ApiError(response.status, validation || problem?.title || 'Erro inesperado.', problem?.details)
  }

  return response.status === 204 ? (undefined as T) : response.json()
}

export const api = {
  register: (name: string, email: string, password: string) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify({ name, email, password }) }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),

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
  listMyOrders: () => request<OrderSummary[]>('/api/orders'),
}
