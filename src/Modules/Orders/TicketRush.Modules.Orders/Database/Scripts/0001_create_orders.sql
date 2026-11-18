CREATE TABLE orders.orders (
    id              uuid          PRIMARY KEY,
    hold_id         uuid          NOT NULL,
    event_id        uuid          NOT NULL,
    event_title     text          NOT NULL,
    event_venue     text          NOT NULL,
    event_starts_at timestamptz   NOT NULL,
    client_id       text          NOT NULL,
    customer_name   text          NOT NULL,
    customer_email  text          NOT NULL,
    status          text          NOT NULL,
    total           numeric(10,2) NOT NULL,
    created_at      timestamptz   NOT NULL,
    paid_at         timestamptz   NULL,
    completed_at    timestamptz   NULL,
    failure_reason  text          NULL,
    -- Uma reserva gera no máximo um pedido (idempotência do checkout).
    CONSTRAINT uq_orders_hold_id UNIQUE (hold_id)
);

CREATE TABLE orders.order_items (
    order_id     uuid          NOT NULL REFERENCES orders.orders (id) ON DELETE CASCADE,
    seat_id      text          NOT NULL,
    section_name text          NOT NULL,
    price        numeric(10,2) NOT NULL,
    PRIMARY KEY (order_id, seat_id)
);

CREATE TABLE orders.outbox_messages (
    id           uuid        PRIMARY KEY,
    type         text        NOT NULL,
    content      jsonb       NOT NULL,
    occurred_at  timestamptz NOT NULL,
    processed_at timestamptz NULL,
    error        text        NULL
);

CREATE INDEX ix_orders_outbox_pending ON orders.outbox_messages (occurred_at) WHERE processed_at IS NULL;
