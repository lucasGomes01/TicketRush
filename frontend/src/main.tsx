import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router-dom'
import './styles.css'
import { HomePage } from './pages/HomePage'
import { EventPage } from './pages/EventPage'
import { CheckoutPage } from './pages/CheckoutPage'
import { OrderPage } from './pages/OrderPage'
import { MyTicketsPage } from './pages/MyTicketsPage'

function App() {
  return (
    <BrowserRouter>
      <header className="topbar">
        <div className="container topbar-inner">
          <Link to="/" className="brand">
            Ticket<em>Rush</em>
          </Link>
          <nav>
            <NavLink to="/" end>Eventos</NavLink>
            <NavLink to="/meus-ingressos">Meus ingressos</NavLink>
          </nav>
        </div>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/eventos/:id" element={<EventPage />} />
          <Route path="/checkout/:holdId" element={<CheckoutPage />} />
          <Route path="/pedidos/:id" element={<OrderPage />} />
          <Route path="/meus-ingressos" element={<MyTicketsPage />} />
          <Route path="*" element={<p className="muted">Página não encontrada.</p>} />
        </Routes>
      </main>
      <footer className="footer container">
        <span>TicketRush</span>
        <a href="https://github.com/lucasGomes01/TicketRush" target="_blank" rel="noreferrer">código no GitHub</a>
      </footer>
    </BrowserRouter>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
