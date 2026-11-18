import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, type Section } from '../api'
import { SeatMap } from '../components/SeatMap'
import { formatMoney } from '../format'

const CATEGORIES = ['Show', 'Teatro', 'Comédia', 'Concerto', 'Cinema', 'eSports', 'Dança']

type SectionDraft = { code: string; name: string; price: string; rows: string; seatsPerRow: string }

const emptySection = (): SectionDraft => ({ code: '', name: '', price: '', rows: '', seatsPerRow: '' })
const empty = new Set<string>()

/** Converte o rascunho em seção válida (ou null), usado na prévia e no envio. */
function toSection(s: SectionDraft): Section | null {
  const code = s.code.trim().toUpperCase()
  const rows = Number(s.rows)
  const seatsPerRow = Number(s.seatsPerRow)
  const price = Number(s.price.replace(',', '.'))
  if (!/^[A-Z0-9]{2,6}$/.test(code) || !s.name.trim()) return null
  if (!Number.isInteger(rows) || rows < 1 || rows > 26) return null
  if (!Number.isInteger(seatsPerRow) || seatsPerRow < 1 || seatsPerRow > 60) return null
  if (!Number.isFinite(price) || price < 0) return null
  return { code, name: s.name.trim(), price, rows, seatsPerRow }
}

export function AdminEventPage() {
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('Show')
  const [venue, setVenue] = useState('')
  const [city, setCity] = useState('')
  const [startsAt, setStartsAt] = useState('')
  const [sections, setSections] = useState<SectionDraft[]>([
    { code: 'PLA', name: 'Plateia', price: '120', rows: '8', seatsPerRow: '16' },
  ])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const valid = useMemo(() => sections.map(toSection), [sections])
  const preview = valid.filter((s): s is Section => s !== null)
  const totalSeats = preview.reduce((sum, s) => sum + s.rows * s.seatsPerRow, 0)
  const duplicated = new Set(preview.map((s) => s.code)).size !== preview.length

  const update = (i: number, field: keyof SectionDraft, value: string) =>
    setSections((current) => current.map((s, j) => (j === i ? { ...s, [field]: value } : s)))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    if (valid.some((s) => s === null)) return setError('Revise os setores: há campos inválidos.')
    if (duplicated) return setError('Cada setor precisa de um código diferente.')
    if (!startsAt || new Date(startsAt) <= new Date()) return setError('Escolha uma data no futuro.')

    setBusy(true)
    try {
      const { id } = await api.createEvent({
        title: title.trim(),
        description: description.trim(),
        category,
        venue: venue.trim(),
        city: city.trim(),
        startsAt: new Date(startsAt).toISOString(),
        sections: preview,
      })
      navigate(`/eventos/${id}`)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <>
      <section className="intro intro-compact">
        <p className="eyebrow">Administração</p>
        <h1>Novo evento</h1>
      </section>

      <form onSubmit={submit} className="admin-layout">
        <div className="panel form admin-form">
          <h2>Informações</h2>
          <label>
            Título
            <input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={120} />
          </label>
          <label>
            Descrição
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} maxLength={500} />
          </label>
          <div className="form-row">
            <label>
              Categoria
              <select value={category} onChange={(e) => setCategory(e.target.value)}>
                {CATEGORIES.map((c) => <option key={c}>{c}</option>)}
              </select>
            </label>
            <label>
              Data e hora
              <input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} required />
            </label>
          </div>
          <div className="form-row">
            <label>
              Local
              <input value={venue} onChange={(e) => setVenue(e.target.value)} required placeholder="Teatro Municipal" />
            </label>
            <label>
              Cidade
              <input value={city} onChange={(e) => setCity(e.target.value)} required placeholder="São Paulo" />
            </label>
          </div>

          <h2 className="admin-subtitle">Setores</h2>
          <p className="muted small">Do palco para o fundo. Código de 2 a 6 letras/números; até 26 fileiras e 60 assentos por fileira.</p>
          <div className="section-editor">
            <div className="section-editor-head">
              <span>Código</span><span>Nome</span><span>Preço</span><span>Fileiras</span><span>Por fileira</span><span />
            </div>
            {sections.map((s, i) => (
              <div key={i} className={`section-editor-row ${valid[i] ? '' : 'invalid'}`}>
                <input aria-label="Código" value={s.code} onChange={(e) => update(i, 'code', e.target.value.toUpperCase())} placeholder="VIP" maxLength={6} />
                <input aria-label="Nome" value={s.name} onChange={(e) => update(i, 'name', e.target.value)} placeholder="Camarote" />
                <input aria-label="Preço" value={s.price} onChange={(e) => update(i, 'price', e.target.value)} inputMode="decimal" placeholder="150" />
                <input aria-label="Fileiras" value={s.rows} onChange={(e) => update(i, 'rows', e.target.value)} inputMode="numeric" placeholder="5" />
                <input aria-label="Assentos por fileira" value={s.seatsPerRow} onChange={(e) => update(i, 'seatsPerRow', e.target.value)} inputMode="numeric" placeholder="12" />
                <button type="button" className="icon-btn" aria-label="Remover setor" disabled={sections.length === 1}
                  onClick={() => setSections((current) => current.filter((_, j) => j !== i))}>×</button>
              </div>
            ))}
          </div>
          <button type="button" className="btn ghost" onClick={() => setSections((c) => [...c, emptySection()])} disabled={sections.length >= 6}>
            Adicionar setor
          </button>
        </div>

        <aside className="panel admin-preview">
          <div className="seatmap-header">
            <h2>Prévia</h2>
            <span className="muted small"><b>{totalSeats}</b> lugares</span>
          </div>
          {preview.length > 0 ? (
            <SeatMap sections={preview} sold={empty} held={empty} mine={empty} selected={empty} disabled onToggle={() => {}} />
          ) : (
            <p className="muted small">Preencha um setor para ver o mapa.</p>
          )}
          {preview.length > 0 && (
            <p className="muted small">A partir de {formatMoney(Math.min(...preview.map((s) => s.price)))}</p>
          )}
          {error && <div className="alert error">{error}</div>}
          <button className="btn primary" disabled={busy}>{busy ? 'Publicando…' : 'Publicar evento'}</button>
        </aside>
      </form>
    </>
  )
}
