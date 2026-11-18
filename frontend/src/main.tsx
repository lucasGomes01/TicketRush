import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router-dom'
import './styles.css'
import { AuthProvider, RequireAuth, useAuth } from './auth'
import { HomePage } from './pages/HomePage'
import { EventPage } from './pages/EventPage'
import { CheckoutPage } from './pages/CheckoutPage'
import { OrderPage } from './pages/OrderPage'
import { MyTicketsPage } from './pages/MyTicketsPage'
import { LoginPage } from './pages/LoginPage'

function Header() {
  const { user, logout } = useAuth()
  return (
    <header className="topbar">
      <div className="container topbar-inner">
        <Link to="/" className="brand">
          Ticket<em>Rush</em>
        </Link>
        <nav>
          <NavLink to="/" end>Eventos</NavLink>
          {user ? (
            <>
              <NavLink to="/meus-ingressos">Meus ingressos</NavLink>
              <span className="nav-user" title={user.email}>{user.name.split(' ')[0]}</span>
              <button type="button" className="nav-link" onClick={logout}>Sair</button>
            </>
          ) : (
            <NavLink to="/entrar">Entrar</NavLink>
          )}
        </nav>
      </div>
    </header>
  )
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Header />
        <main className="container">
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/eventos/:id" element={<EventPage />} />
            <Route path="/entrar" element={<LoginPage mode="login" />} />
            <Route path="/cadastro" element={<LoginPage mode="register" />} />
            <Route path="/checkout/:holdId" element={<RequireAuth><CheckoutPage /></RequireAuth>} />
            <Route path="/pedidos/:id" element={<RequireAuth><OrderPage /></RequireAuth>} />
            <Route path="/meus-ingressos" element={<RequireAuth><MyTicketsPage /></RequireAuth>} />
            <Route path="*" element={<p className="muted">Página não encontrada.</p>} />
          </Routes>
        </main>
        <footer className="footer container">
          <span>TicketRush</span>
          <a href="https://github.com/lucasGomes01/TicketRush" target="_blank" rel="noreferrer">código no GitHub</a>
        </footer>
      </AuthProvider>
    </BrowserRouter>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
