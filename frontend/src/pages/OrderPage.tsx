import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type Order } from '../api'
import { formatDate, formatMoney } from '../format'

const steps = [
  { status: 'Pending', label: 'Pedido criado' },
  { status: 'Paid', label: 'Pagamento aprovado' },
  { status: 'Completed', label: 'Ingressos emitidos' },
] as const

export function OrderPage() {
  const { id = '' } = useParams()
  const [order, setOrder] = useState<Order | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>
    const load = async () => {
      try {
        const current = await api.getOrder(id)
        setOrder(current)
        // Emissão é assíncrona: continua consultando até um status final.
        if (current.status === 'Paid' || current.status === 'Pending') timer = setTimeout(load, 1000)
      } catch (e) {
        setError((e as Error).message)
      }
    }
    load()
    return () => clearTimeout(timer)
  }, [id])

  if (error) return <div className="alert error">{error}</div>
  if (!order) return <div className="card skeleton tall" />

  const reached = steps.findIndex((s) => s.status === order.status)
  const failed = order.status === 'Refunded' || order.status === 'Expired'

  return (
    <div className="narrow">
      <Link to="/meus-ingressos" className="back">← Meus ingressos</Link>
      <div className="card pad">
        <h2>{order.eventTitle}</h2>
        <p className="muted">{formatDate(order.eventStartsAt)} · {order.eventVenue}</p>

        <ol className="steps">
          {steps.map((step, i) => (
            <li key={step.status} className={failed ? '' : i <= reached || order.status === 'Completed' ? 'done' : i === reached + 1 ? 'active' : ''}>
              {step.label}
            </li>
          ))}
        </ol>

        {failed && (
          <div className="alert error">
            {order.status === 'Refunded' ? 'Pagamento estornado: ' : 'Pedido expirado: '}
            {order.failureReason}
          </div>
        )}

        {order.status === 'Completed' ? (
          <div className="tickets">
            {order.tickets.map((t) => (
              <div key={t.id} className="ticket">
                <div>
                  <span className="muted small">Assento</span>
                  <strong className="ticket-seat">{t.seatId}</strong>
                </div>
                <div className="ticket-code">
                  <span className="muted small">Código</span>
                  <code>{t.code}</code>
                </div>
              </div>
            ))}
            <p className="muted small">Enviado para {order.customerEmail} (simulado).</p>
          </div>
        ) : (
          !failed && <p className="muted">Emitindo seus ingressos…</p>
        )}

        <div className="total"><span>Total pago</span><strong>{formatMoney(order.total)}</strong></div>
      </div>
    </div>
  )
}
