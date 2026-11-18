import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { api, session, type User } from './api'

type AuthContextValue = {
  user: User | null
  login: (email: string, password: string) => Promise<void>
  register: (name: string, email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => session.get()?.user ?? null)

  // Quando a API devolve 401 (token expirado), o cliente limpa a sessão e avisa por este evento.
  useEffect(() => {
    const onLogout = () => setUser(null)
    window.addEventListener('ticketrush:logout', onLogout)
    return () => window.removeEventListener('ticketrush:logout', onLogout)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api.login(email, password)
    session.save(auth)
    setUser(auth.user)
  }, [])

  const register = useCallback(async (name: string, email: string, password: string) => {
    const auth = await api.register(name, email, password)
    session.save(auth)
    setUser(auth.user)
  }, [])

  const logout = useCallback(() => session.clear(), [])

  return <AuthContext.Provider value={{ user, login, register, logout }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth precisa estar dentro de <AuthProvider>')
  return context
}

/** Caminho da tela de login que volta para a página atual depois de entrar. */
export const loginPath = (back: string) => `/entrar?volta=${encodeURIComponent(back)}`

export function RequireAuth({ children, admin = false }: { children: ReactNode; admin?: boolean }) {
  const { user } = useAuth()
  const location = useLocation()
  if (!user) return <Navigate to={loginPath(location.pathname)} replace />
  // A interface só esconde; quem garante a permissão é a API (política "admin" no POST /api/events).
  if (admin && !user.isAdmin) return <p className="muted">Esta área é restrita a administradores.</p>
  return children
}
