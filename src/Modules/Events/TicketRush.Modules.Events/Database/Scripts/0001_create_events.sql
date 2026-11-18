CREATE TABLE events.events (
    id           uuid        PRIMARY KEY,
    title        text        NOT NULL,
    description  text        NOT NULL,
    category     text        NOT NULL,
    venue        text        NOT NULL,
    city         text        NOT NULL,
    starts_at    timestamptz NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_events_starts_at ON events.events (starts_at);

-- Os assentos não são materializados: são derivados de (code, rows, seats_per_row).
CREATE TABLE events.sections (
    id            uuid          PRIMARY KEY,
    event_id      uuid          NOT NULL REFERENCES events.events (id) ON DELETE CASCADE,
    code          text          NOT NULL CHECK (code ~ '^[A-Z0-9]{2,6}$'),
    name          text          NOT NULL,
    price         numeric(10,2) NOT NULL CHECK (price >= 0),
    rows          int           NOT NULL CHECK (rows BETWEEN 1 AND 26),
    seats_per_row int           NOT NULL CHECK (seats_per_row BETWEEN 1 AND 60),
    sort_order    int           NOT NULL,
    CONSTRAINT uq_sections_event_code UNIQUE (event_id, code)
);
