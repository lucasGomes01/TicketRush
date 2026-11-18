import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth'

export function LoginPage({ mode }: { mode: 'login' | 'register' }) {
  const { user, login, register } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const back = params.get('volta')
  const destination = back?.startsWith('/') && !back.startsWith('//') ? back : '/'

  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (user) return <Navigate to={destination} replace />

  const isRegister = mode === 'register'
  const otherMode = `${isRegister ? '/entrar' : '/cadastro'}${back ? `?volta=${encodeURIComponent(back)}` : ''}`

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (isRegister) await register(name, email, password)
      else await login(email, password)
      navigate(destination, { replace: true })
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <p className="eyebrow">{isRegister ? 'Primeira vez aqui' : 'Bem-vindo de volta'}</p>
      <h1>{isRegister ? 'Criar conta' : 'Entrar'}</h1>
      {back && <p className="muted auth-hint">Entre para reservar seus lugares. Depois você volta direto para onde estava.</p>}

      <form onSubmit={submit} className="form panel">
        {isRegister && (
          <label>
            Nome completo
            <input value={name} onChange={(e) => setName(e.target.value)} required minLength={3} autoComplete="name" />
          </label>
        )}
        <label>
          E-mail
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="email" />
        </label>
        <label>
          Senha
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={isRegister ? 8 : undefined}
            autoComplete={isRegister ? 'new-password' : 'current-password'}
          />
          {isRegister && <small className="field-hint">Pelo menos 8 caracteres.</small>}
        </label>
        {error && <div className="alert error">{error}</div>}
        <button className="btn primary" disabled={busy}>
          {busy ? 'Aguarde…' : isRegister ? 'Criar conta' : 'Entrar'}
        </button>
      </form>

      <p className="auth-switch">
        {isRegister ? 'Já tem conta?' : 'Ainda não tem conta?'} <Link to={otherMode}>{isRegister ? 'Entrar' : 'Criar conta'}</Link>
      </p>
    </div>
  )
}
