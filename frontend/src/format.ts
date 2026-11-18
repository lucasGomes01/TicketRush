import { useEffect, useState } from 'react'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const date = new Intl.DateTimeFormat('pt-BR', { weekday: 'short', day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })

export const formatMoney = (value: number) => money.format(value)
export const formatDate = (iso: string) => date.format(new Date(iso))

export const categoryHue: Record<string, number> = {
  Show: 280,
  Comédia: 35,
  Concerto: 200,
  eSports: 150,
  Cinema: 340,
}

export function eventGradient(category: string) {
  const hue = categoryHue[category] ?? 250
  return `linear-gradient(135deg, hsl(${hue} 80% 55%), hsl(${(hue + 50) % 360} 75% 40%))`
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
