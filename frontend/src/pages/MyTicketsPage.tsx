import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, myOrders, type Order } from '../api'
import { formatDate, formatMoney } from '../format'

const statusLabel: Record<Order['status'], string> = {
  Pending: 'Aguardando pagamento',
  Paid: 'Emitindo',
  Completed: 'Confirmado',
  Refunded: 'Estornado',
  Expired: 'Expirado',
}

export function MyTicketsPage() {
  const [orders, setOrders] = useState<Order[] | null>(null)

  useEffect(() => {
    Promise.all(myOrders.list().map((id) => api.getOrder(id).catch(() => null))).then((list) =>
      setOrders(list.filter((o): o is Order => o !== null)),
    )
  }, [])

  return (
    <div className="narrow">
      <h1>Meus ingressos</h1>
      {orders?.length === 0 && (
        <p className="muted">
          Nenhum pedido neste navegador ainda. <Link to="/">Escolha um evento</Link>.
        </p>
      )}
      {orders?.map((o) => (
        <Link key={o.id} to={`/pedidos/${o.id}`} className="card pad order-row">
          <div>
            <strong>{o.eventTitle}</strong>
            <p className="muted small">{formatDate(o.eventStartsAt)} · {o.items.map((i) => i.seatId).join(', ')}</p>
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
