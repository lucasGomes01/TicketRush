import { useEffect, useMemo, useRef } from 'react'
import type { Section } from '../api'
import { formatMoney } from '../format'

type Props = {
  sections: Section[]
  sold: Set<string>
  held: Set<string>
  mine: Set<string>
  selected: Set<string>
  disabled: boolean
  onToggle: (seatId: string) => void
}

type SeatState = 'available' | 'selected' | 'mine' | 'held' | 'sold'

type PlacedSeat = { id: string; x: number; y: number; section: Section; number: number }
type RowLabel = { text: string; x: number; y: number }

// Geometria da plateia: as fileiras são arcos concêntricos cujo centro fica no palco.
const SEAT_GAP = 22 // distância entre centros de assentos ao longo do arco
const ROW_GAP = 24 // distância entre fileiras
const SECTION_GAP = 26 // corredor entre setores
const FIRST_RADIUS = 150
const MAX_SPAN = 2.2 // abertura máxima do arco, em radianos (~126°)
const SEAT_R = 7.5

const rowLetter = (i: number) => String.fromCharCode(65 + i)

function layout(sections: Section[]) {
  const seats: PlacedSeat[] = []
  const labels: RowLabel[] = []
  const sectionMarks: { section: Section; y: number }[] = []
  let radius = FIRST_RADIUS

  sections.forEach((section, s) => {
    if (s > 0) radius += SECTION_GAP
    sectionMarks.push({ section, y: radius })

    for (let row = 0; row < section.rows; row++) {
      const n = section.seatsPerRow
      const span = Math.min(((n - 1) * SEAT_GAP) / radius, MAX_SPAN)
      const step = n > 1 ? span / (n - 1) : 0
      for (let i = 0; i < n; i++) {
        const angle = -span / 2 + i * step
        seats.push({
          id: `${section.code}-${rowLetter(row)}${i + 1}`,
          x: radius * Math.sin(angle),
          y: radius * Math.cos(angle),
          section,
          number: i + 1,
        })
      }
      const edge = -span / 2 - (SEAT_GAP * 1.1) / radius
      labels.push({ text: rowLetter(row), x: radius * Math.sin(edge), y: radius * Math.cos(edge) })
      radius += ROW_GAP
    }
  })

  const xs = seats.map((p) => p.x)
  const ys = seats.map((p) => p.y)
  const pad = 28
  const minX = Math.min(...xs, ...labels.map((l) => l.x)) - pad
  const maxX = Math.max(...xs) + pad
  // As pontas das fileiras sobem por causa da curva: o palco fica acima do assento mais alto.
  const top = Math.min(...ys, ...labels.map((l) => l.y)) - 64
  const maxY = Math.max(...ys) + pad

  return { seats, labels, sectionMarks, viewBox: `${minX} ${top} ${maxX - minX} ${maxY - top}`, top }
}

export function SeatMap({ sections, sold, held, mine, selected, disabled, onToggle }: Props) {
  const plan = useMemo(() => layout(sections), [sections])
  const scroller = useRef<HTMLDivElement>(null)

  // No celular o mapa é mais largo que a tela: começa centralizado no palco.
  useEffect(() => {
    const el = scroller.current
    if (el) el.scrollLeft = (el.scrollWidth - el.clientWidth) / 2
  }, [plan])

  const stateOf = (id: string): SeatState =>
    sold.has(id) ? 'sold' : mine.has(id) ? 'mine' : held.has(id) ? 'held' : selected.has(id) ? 'selected' : 'available'

  return (
    <div className="seatmap">
      <div className="auditorium-scroll" ref={scroller}>
      <svg className="auditorium" viewBox={plan.viewBox} role="group" aria-label="Plateia">
        <path className="stage-shape" d={`M -120 ${plan.top + 18} L 120 ${plan.top + 18} Q 0 ${plan.top + 62} -120 ${plan.top + 18} Z`} />
        <text className="stage-label" x={0} y={plan.top + 34} textAnchor="middle">PALCO</text>

        {plan.labels.map((l, i) => (
          <text key={i} className="row-label" x={l.x} y={l.y + 3.5} textAnchor="middle">{l.text}</text>
        ))}

        {plan.seats.map((seat) => {
          const state = stateOf(seat.id)
          const clickable = !disabled && (state === 'available' || state === 'selected')
          return (
            <g
              key={seat.id}
              className={`seat ${state}${clickable ? ' clickable' : ''}`}
              role="button"
              tabIndex={clickable ? 0 : -1}
              aria-label={`Assento ${seat.id}, ${state}`}
              aria-disabled={!clickable}
              onClick={() => clickable && onToggle(seat.id)}
              onKeyDown={(e) => {
                if (clickable && (e.key === 'Enter' || e.key === ' ')) {
                  e.preventDefault()
                  onToggle(seat.id)
                }
              }}
            >
              <title>{`${seat.section.name} · ${seat.id} · ${formatMoney(seat.section.price)}`}</title>
              <circle cx={seat.x} cy={seat.y} r={SEAT_R} />
              {state === 'sold' && (
                <path d={`M ${seat.x - 3.5} ${seat.y - 3.5} L ${seat.x + 3.5} ${seat.y + 3.5} M ${seat.x + 3.5} ${seat.y - 3.5} L ${seat.x - 3.5} ${seat.y + 3.5}`} />
              )}
            </g>
          )
        })}
      </svg>
      </div>

      <ul className="sections">
        {sections.map((s) => (
          <li key={s.code}>
            <span className="section-name">{s.name}</span>
            <span className="section-dots" />
            <span className="section-price">{formatMoney(s.price)}</span>
          </li>
        ))}
      </ul>

      <div className="legend">
        <span><i className="dot available" /> Livre</span>
        <span><i className="dot selected" /> Selecionado</span>
        <span><i className="dot mine" /> Seu lugar</span>
        <span><i className="dot held" /> Em reserva</span>
        <span><i className="dot sold" /> Vendido</span>
      </div>
    </div>
  )
}
