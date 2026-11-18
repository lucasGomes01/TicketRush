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

const rowLetter = (i: number) => String.fromCharCode(65 + i)

export function SeatMap({ sections, sold, held, mine, selected, disabled, onToggle }: Props) {
  return (
    <div className="seatmap">
      <div className="stage">PALCO</div>
      {sections.map((section) => (
        <div key={section.code} className="section">
          <div className="section-header">
            <strong>{section.name}</strong>
            <span>{formatMoney(section.price)}</span>
          </div>
          <div className="section-rows">
            {Array.from({ length: section.rows }, (_, row) => (
              <div key={row} className="seat-row">
                <span className="row-label">{rowLetter(row)}</span>
                {Array.from({ length: section.seatsPerRow }, (_, i) => {
                  const seatId = `${section.code}-${rowLetter(row)}${i + 1}`
                  const state = sold.has(seatId)
                    ? 'sold'
                    : mine.has(seatId)
                      ? 'mine'
                      : held.has(seatId)
                        ? 'held'
                        : selected.has(seatId)
                          ? 'selected'
                          : 'available'
                  const clickable = !disabled && (state === 'available' || state === 'selected')
                  return (
                    <button
                      key={seatId}
                      type="button"
                      className={`seat ${state}`}
                      disabled={!clickable}
                      title={`${section.name} · ${seatId}`}
                      aria-label={`Assento ${seatId}, ${state}`}
                      onClick={() => onToggle(seatId)}
                    >
                      {i + 1}
                    </button>
                  )
                })}
              </div>
            ))}
          </div>
        </div>
      ))}
      <div className="legend">
        <span><i className="seat available" /> Livre</span>
        <span><i className="seat selected" /> Selecionado</span>
        <span><i className="seat mine" /> Sua reserva</span>
        <span><i className="seat held" /> Reservado</span>
        <span><i className="seat sold" /> Vendido</span>
      </div>
    </div>
  )
}
