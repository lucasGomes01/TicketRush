import { useEffect, useState } from 'react'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const full = new Intl.DateTimeFormat('pt-BR', { weekday: 'long', day: '2-digit', month: 'long', hour: '2-digit', minute: '2-digit' })
const time = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' })
const weekday = new Intl.DateTimeFormat('pt-BR', { weekday: 'short' })
const month = new Intl.DateTimeFormat('pt-BR', { month: 'short' })

export const formatMoney = (value: number) => money.format(value)
export const formatDate = (iso: string) => full.format(new Date(iso))

/** Partes da data para o bloco de calendário da listagem. */
export function dateParts(iso: string) {
  const d = new Date(iso)
  return {
    day: String(d.getDate()).padStart(2, '0'),
    month: month.format(d).replace('.', ''),
    weekday: weekday.format(d).replace('.', ''),
    time: time.format(d),
  }
}

/** Segundos restantes até `expiresAt` (atualiza a cada segundo). */
export function useCountdown(expiresAt: string | null | undefined) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    setNow(Date.now())
    if (!expiresAt) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [expiresAt])

  // Calculado a cada render: nunca fica "0" por um frame logo depois que a reserva chega.
  const seconds = expiresAt ? Math.max(0, Math.floor((new Date(expiresAt).getTime() - now) / 1000)) : 0

  const label = `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
  return { seconds, label }
}
