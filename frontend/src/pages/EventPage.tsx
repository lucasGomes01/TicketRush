import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type EventDetails, type Hold, type SeatMap as SeatMapData } from '../api'
import { SeatMap } from '../components/SeatMap'
import { eventGradient, formatDate, formatMoney, useCountdown } from '../format'

const MAX_SEATS = 6
const holdKey = (eventId: string) => `ticketrush:hold:${eventId}`

export function EventPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [event, setEvent] = useState<EventDetails | null>(null)
  const [seatMap, setSeatMap] = useState<SeatMapData>({ eventId: id, sold: [], held: [] })
  const [selected, setSelected] = useState<string[]>([])
  const [hold, setHold] = useState<Hold | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ kind: 'error' | 'info'; text: string } | null>(null)
  const { seconds, label } = useCountdown(hold?.expiresAt)

  const refreshSeats = useCallback(() => api.getSeatMap(id).then(setSeatMap).catch(() => {}), [id])

  useEffect(() => {
    api.getEvent(id).then(setEvent).catch((e) => setMessage({ kind: 'error', text: e.message }))
    refreshSeats()

    // Recupera a reserva ativa deste evento (se o usuário recarregou a página).
    const saved = sessionStorage.getItem(holdKey(id))
    if (saved) {
      api.getHold(saved).then(setHold).catch(() => sessionStorage.removeItem(holdKey(id)))
    }

    // Atualiza o mapa periodicamente para mostrar reservas de outras pessoas.
    const timer = setInterval(refreshSeats, 3000)
    return () => clearInterval(timer)
  }, [id, refreshSeats])

  useEffect(() => {
    if (hold && seconds === 0) {
      setHold(null)
      sessionStorage.removeItem(holdKey(id))
      setMessage({ kind: 'info', text: 'Sua reserva expirou e os assentos foram liberados.' })
      refreshSeats()
    }
  }, [hold, seconds, id, refreshSeats])

  const taken = useMemo(() => ({ sold: new Set(seatMap.sold), held: new Set(seatMap.held) }), [seatMap])
  const sets = useMemo(
    () => ({
      ...taken,
      mine: new Set(hold?.seats.map((s) => s.seatId) ?? []),
      selected: new Set(selected),
    }),
    [taken, hold, selected],
  )

  // Se alguém reservou um assento que estava só selecionado aqui, tira da seleção.
  useEffect(() => {
    setSelected((current) => {
      const stillFree = current.filter((s) => !taken.held.has(s) && !taken.sold.has(s))
      return stillFree.length === current.length ? current : stillFree
    })
  }, [taken])

  const priceOf = (seatId: string) => event?.sections.find((s) => seatId.startsWith(`${s.code}-`))?.price ?? 0
  const selectedTotal = selected.reduce((sum, s) => sum + priceOf(s), 0)

  function toggle(seatId: string) {
    setMessage(null)
    setSelected((current) =>
      current.includes(seatId)
        ? current.filter((s) => s !== seatId)
        : current.length >= MAX_SEATS
          ? (setMessage({ kind: 'info', text: `Máximo de ${MAX_SEATS} assentos por reserva.` }), current)
          : [...current, seatId],
    )
  }

  async function reserve() {
    setBusy(true)
    setMessage(null)
    try {
      const created = await api.createHold(id, selected)
      setHold(created)
      setSelected([])
      sessionStorage.setItem(holdKey(id), created.holdId)
    } catch (e) {
      const error = e as ApiError
      const seats = error.details?.seatIds?.join(', ')
      setMessage({ kind: 'error', text: seats ? `${error.message} (${seats})` : error.message })
    } finally {
      setBusy(false)
      refreshSeats()
    }
  }

  async function release() {
    if (!hold) return
    setBusy(true)
    await api.releaseHold(hold.holdId).catch(() => {})
    sessionStorage.removeItem(holdKey(id))
    setHold(null)
    setBusy(false)
    refreshSeats()
  }

  if (!event) {
    return message ? <div className="alert error">{message.text}</div> : <div className="card skeleton tall" />
  }

  const available = event.totalSeats - seatMap.sold.length - seatMap.held.length

  return (
    <>
      <Link to="/" className="back">← Todos os eventos</Link>
      <section className="event-hero" style={{ background: eventGradient(event.category) }}>
        <span className="chip">{event.category}</span>
        <h1>{event.title}</h1>
        <p>{formatDate(event.startsAt)} · {event.venue}, {event.city}</p>
        <p className="event-description">{event.description}</p>
      </section>

      <div className="event-layout">
        <div className="card pad">
          <div className="seatmap-header">
            <h2>Escolha seus assentos</h2>
            <span className="muted small">{available} de {event.totalSeats} disponíveis · atualiza a cada 3s</span>
          </div>
          <SeatMap
            sections={event.sections}
            {...sets}
            disabled={!!hold || busy}
            onToggle={toggle}
          />
        </div>

        <aside className="card pad summary">
          {hold ? (
            <>
              <div className={`timer ${seconds < 60 ? 'urgent' : ''}`}>
                <span>Reserva expira em</span>
                <strong>{label}</strong>
              </div>
              <ul className="seat-list">
                {hold.seats.map((s) => (
                  <li key={s.seatId}><span>{s.seatId} <small className="muted">{s.sectionName}</small></span><span>{formatMoney(s.price)}</span></li>
                ))}
              </ul>
              <div className="total"><span>Total</span><strong>{formatMoney(hold.total)}</strong></div>
              <button className="btn primary" onClick={() => navigate(`/checkout/${hold.holdId}`)}>Ir para o pagamento</button>
              <button className="btn ghost" onClick={release} disabled={busy}>Liberar assentos</button>
            </>
          ) : (
            <>
              <h3>Seu carrinho</h3>
              {selected.length === 0 ? (
                <p className="muted">Clique nos assentos livres para selecionar (até {MAX_SEATS}).</p>
              ) : (
                <ul className="seat-list">
                  {selected.map((s) => (
                    <li key={s}><span>{s}</span><span>{formatMoney(priceOf(s))}</span></li>
                  ))}
                </ul>
              )}
              <div className="total"><span>Total</span><strong>{formatMoney(selectedTotal)}</strong></div>
              <button className="btn primary" onClick={reserve} disabled={busy || selected.length === 0}>
                {busy ? 'Reservando…' : 'Reservar assentos'}
              </button>
            </>
          )}
          {message && <div className={`alert ${message.kind}`}>{message.text}</div>}
        </aside>
      </div>
    </>
  )
}
