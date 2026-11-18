import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type EventSummary } from '../api'
import { dateParts, formatMoney } from '../format'

export function HomePage() {
  const [events, setEvents] = useState<EventSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.listEvents().then(setEvents).catch((e) => setError(e.message))
  }, [])

  return (
    <>
      <section className="intro">
        <p className="eyebrow">Temporada 2027</p>
        <h1>Próximas datas</h1>
        <p className="lede">
          Escolha o lugar no mapa e ele fica separado para você por dez minutos enquanto finaliza a compra.
        </p>
      </section>

      {error && <div className="alert error">{error}</div>}

      <ol className="listing">
        {!events && !error && [1, 2, 3].map((i) => <li key={i} className="listing-row placeholder" />)}
        {events?.map((event) => {
          const d = dateParts(event.startsAt)
          return (
            <li key={event.id}>
              <Link to={`/eventos/${event.id}`} className="listing-row">
                <div className="date-block">
                  <span className="date-day">{d.day}</span>
                  <span className="date-month">{d.month}</span>
                </div>
                <div className="listing-main">
                  <span className="tag">{event.category}</span>
                  <h2>{event.title}</h2>
                  <p className="muted">
                    {event.venue}, {event.city} · {d.weekday}, {d.time}
                  </p>
                </div>
                <div className="listing-price">
                  <span className="muted small">a partir de</span>
                  <strong>{formatMoney(event.minPrice)}</strong>
                  <span className="muted small">{event.totalSeats} lugares</span>
                </div>
                <span className="listing-cta">Ingressos</span>
              </Link>
            </li>
          )
        })}
      </ol>
    </>
  )
}
