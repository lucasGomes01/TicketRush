CREATE TABLE identity.users (
    id            uuid        PRIMARY KEY,
    name          text        NOT NULL,
    email         text        NOT NULL,
    password_hash text        NOT NULL,
    created_at    timestamptz NOT NULL
);

-- E-mail é único sem diferenciar maiúsculas/minúsculas.
CREATE UNIQUE INDEX uq_users_email ON identity.users (lower(email));
