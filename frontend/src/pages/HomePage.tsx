import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type EventSummary } from '../api'
import { eventGradient, formatDate, formatMoney } from '../format'

export function HomePage() {
  const [events, setEvents] = useState<EventSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.listEvents().then(setEvents).catch((e) => setError(e.message))
  }, [])

  return (
    <>
      <section className="hero">
        <h1>Garanta seu lugar antes que acabe.</h1>
        <p>
          Escolha os assentos e eles ficam <strong>reservados só para você por 10 minutos</strong>. Ninguém mais
          consegue comprar o mesmo lugar, nem com mil pessoas clicando ao mesmo tempo.
        </p>
      </section>

      {error && <div className="alert error">{error}</div>}
      {!events && !error && <div className="grid">{[1, 2, 3].map((i) => <div key={i} className="card skeleton" />)}</div>}

      <div className="grid">
        {events?.map((event) => (
          <Link key={event.id} to={`/eventos/${event.id}`} className="card event-card">
            <div className="event-cover" style={{ background: eventGradient(event.category) }}>
              <span className="chip">{event.category}</span>
            </div>
            <div className="card-body">
              <h3>{event.title}</h3>
              <p className="muted">{formatDate(event.startsAt)}</p>
              <p className="muted">{event.venue} · {event.city}</p>
              <div className="card-footer">
                <span>a partir de <strong>{formatMoney(event.minPrice)}</strong></span>
                <span className="muted small">{event.totalSeats} lugares</span>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </>
  )
}
