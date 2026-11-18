import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type OrderStatus, type OrderSummary } from '../api'
import { useAuth } from '../auth'
import { formatDate, formatMoney } from '../format'

const statusLabel: Record<OrderStatus, string> = {
  Pending: 'Aguardando pagamento',
  Paid: 'Emitindo',
  Completed: 'Confirmado',
  Refunded: 'Estornado',
  Expired: 'Expirado',
}

export function MyTicketsPage() {
  const { user } = useAuth()
  const [orders, setOrders] = useState<OrderSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.listMyOrders().then(setOrders).catch((e) => setError(e.message))
  }, [])

  return (
    <div className="narrow">
      <p className="eyebrow">{user?.name}</p>
      <h1>Meus ingressos</h1>
      {error && <div className="alert error">{error}</div>}
      {!orders && !error && <p className="muted">Carregando…</p>}
      {orders?.length === 0 && (
        <p className="muted">
          Você ainda não tem pedidos. <Link to="/">Escolha um evento</Link>.
        </p>
      )}
      {orders?.map((o) => (
        <Link key={o.id} to={`/pedidos/${o.id}`} className="panel order-row">
          <div>
            <strong>{o.eventTitle}</strong>
            <p className="muted small">{formatDate(o.eventStartsAt)} · {o.seatIds.join(', ')}</p>
          </div>
          <div className="order-row-right">
            <span className={`status ${o.status.toLowerCase()}`}>{statusLabel[o.status]}</span>
            <span>{formatMoney(o.total)}</span>
          </div>
        </Link>
      ))}
    </div>
  )
}
