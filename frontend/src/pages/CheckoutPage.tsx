import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, myOrders, type Hold } from '../api'
import { formatMoney, useCountdown } from '../format'

export function CheckoutPage() {
  const { holdId = '' } = useParams()
  const navigate = useNavigate()
  const [hold, setHold] = useState<Hold | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const { seconds, label } = useCountdown(hold?.expiresAt)

  useEffect(() => {
    api.getHold(holdId).then(setHold).catch((e) => setError(e.message))
  }, [holdId])

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      // Cria o pedido (idempotente por reserva) e simula o pagamento.
      const order = await api.createOrder(holdId, name, email)
      myOrders.add(order.id)
      await api.payOrder(order.id)
      navigate(`/pedidos/${order.id}`)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  if (!hold) {
    return (
      <div className="narrow">
        {error ? (
          <div className="alert error">
            {error} <Link to="/">Voltar aos eventos</Link>
          </div>
        ) : (
          <div className="card skeleton tall" />
        )}
      </div>
    )
  }

  const expired = seconds === 0

  return (
    <div className="narrow">
      <Link to={`/eventos/${hold.eventId}`} className="back">← Voltar ao mapa de assentos</Link>
      <div className="card pad">
        <div className={`timer ${seconds < 60 ? 'urgent' : ''}`}>
          <span>{expired ? 'Reserva expirada' : 'Finalize em'}</span>
          <strong>{label}</strong>
        </div>

        <h2>Checkout</h2>
        <ul className="seat-list">
          {hold.seats.map((s) => (
            <li key={s.seatId}><span>{s.seatId} <small className="muted">{s.sectionName}</small></span><span>{formatMoney(s.price)}</span></li>
          ))}
        </ul>
        <div className="total"><span>Total</span><strong>{formatMoney(hold.total)}</strong></div>

        <form onSubmit={submit} className="form">
          <label>
            Nome completo
            <input value={name} onChange={(e) => setName(e.target.value)} required minLength={3} autoComplete="name" />
          </label>
          <label>
            E-mail para receber os ingressos
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" />
          </label>
          <div className="payment-note">
            💳 Pagamento <strong>simulado</strong>: nenhum dado de cartão é solicitado. Ao confirmar, o pedido é marcado como
            pago e os ingressos são emitidos de forma assíncrona (Outbox → módulo Ticketing).
          </div>
          <button className="btn primary" disabled={busy || expired}>
            {busy ? 'Processando…' : `Pagar ${formatMoney(hold.total)}`}
          </button>
        </form>
        {error && <div className="alert error">{error}</div>}
      </div>
    </div>
  )
}
