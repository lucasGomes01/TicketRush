CREATE TABLE ticketing.tickets (
    id           uuid          PRIMARY KEY,
    code         text          NOT NULL UNIQUE,
    event_id     uuid          NOT NULL,
    seat_id      text          NOT NULL,
    order_id     uuid          NOT NULL,
    holder_name  text          NOT NULL,
    holder_email text          NOT NULL,
    price        numeric(10,2) NOT NULL,
    issued_at    timestamptz   NOT NULL,
    -- Última linha de defesa contra overselling: mesmo que a reserva no Redis falhe,
    -- o banco nunca aceita dois ingressos para o mesmo assento.
    CONSTRAINT uq_tickets_event_seat UNIQUE (event_id, seat_id)
);

CREATE INDEX ix_tickets_order_id ON ticketing.tickets (order_id);

CREATE TABLE ticketing.outbox_messages (
    id           uuid        PRIMARY KEY,
    type         text        NOT NULL,
    content      jsonb       NOT NULL,
    occurred_at  timestamptz NOT NULL,
    processed_at timestamptz NULL,
    error        text        NULL
);

CREATE INDEX ix_ticketing_outbox_pending ON ticketing.outbox_messages (occurred_at) WHERE processed_at IS NULL;
