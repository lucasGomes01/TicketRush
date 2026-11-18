-- Dados de demonstração (eventos e locais fictícios).
INSERT INTO events.events (id, title, description, category, venue, city, starts_at) VALUES
('0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a01', 'Noite do Rock Nacional',
 'Três bandas, um palco e quatro horas de clássicos do rock brasileiro.',
 'Show', 'Arena Horizonte', 'São Paulo', '2027-03-20 23:00:00+00'),
('0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a02', 'Riso Solto: Stand-up',
 'Os melhores comediantes da nova geração em uma noite só.',
 'Comédia', 'Teatro Aurora', 'Rio de Janeiro', '2027-02-12 23:30:00+00'),
('0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a03', 'Sinfônica: Trilhas de Cinema',
 'Orquestra completa interpretando trilhas sonoras que marcaram gerações.',
 'Concerto', 'Sala Lumière', 'Belo Horizonte', '2027-04-08 22:00:00+00'),
('0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a04', 'Final Regional de eSports',
 'As quatro melhores equipes da região disputam a vaga no nacional.',
 'eSports', 'Ginásio Vértice', 'Curitiba', '2027-05-15 18:00:00+00'),
('0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05', 'Pré-estreia Secreta',
 'Sessão surpresa com apenas 10 lugares. Ideal para testar a disputa por assentos.',
 'Cinema', 'Cine Estação', 'Porto Alegre', '2027-01-30 22:30:00+00');

INSERT INTO events.sections (id, event_id, code, name, price, rows, seats_per_row, sort_order) VALUES
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a01', 'VIP', 'VIP', 480.00, 3, 12, 1),
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a01', 'PLA', 'Plateia', 220.00, 6, 16, 2),
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a01', 'ARQ', 'Arquibancada', 120.00, 5, 20, 3),

(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a02', 'PLT', 'Plateia', 150.00, 8, 14, 1),
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a02', 'BAL', 'Balcão', 90.00, 3, 14, 2),

(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a03', 'CAM', 'Camarote', 350.00, 2, 8, 1),
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a03', 'PLA', 'Plateia', 180.00, 7, 18, 2),

(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a04', 'VIP', 'Pista VIP', 200.00, 2, 12, 1),
(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a04', 'STD', 'Arquibancada', 60.00, 5, 24, 2),

(gen_random_uuid(), '0b6f1e0a-1c1d-4b8e-9a51-0c6a5b2f7a05', 'SAL', 'Sala', 40.00, 2, 5, 1);
