-- ============================================================================
--  MIGRACIJA ZA SUSTAV OBAVIJESTI (in-app + email)
-- ----------------------------------------------------------------------------
--  Do sada su prekidači "Obavijesti u aplikaciji" i "Email obavijesti" u
--  postavkama samo javljali da opcija još nije dostupna — nije postojala ni
--  jedna tablica u koju bi se obavijest ili korisnikov izbor kategorija
--  mogao spremiti. Ova migracija dodaje:
--
--    notifications             — same obavijesti (jedan red = jedna obavijest
--                                za jednog korisnika)
--    notification_preferences  — po korisniku: jesu li app/email obavijesti
--                                uključene, na koji email idu i koje su
--                                kategorije (i dobne skupine) odabrane
--
--  Skripta je idempotentna (sve je IF NOT EXISTS) — smije se pokrenuti više
--  puta i sigurno je pokrenuti je prije samog deploya backenda.
--
--  Pokretanje:
--      psql "<Render External Database URL>" -f migracija_obavijesti.sql
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS notifications (
    id            SERIAL PRIMARY KEY,
    user_id       INTEGER      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    category      VARCHAR(64)  NOT NULL DEFAULT 'general',
    title         VARCHAR(200) NOT NULL,
    body          TEXT,
    place_name    VARCHAR(200),
    latitude      DOUBLE PRECISION,
    longitude     DOUBLE PRECISION,
    -- Za podsjetnike koje korisnik sam kreira ("javi mi u subotu u 19h").
    scheduled_at  TIMESTAMP,
    is_read       BOOLEAN      NOT NULL DEFAULT FALSE,
    -- 'user'   = korisnik ju je sam kreirao (podsjetnik)
    -- 'system' = nastala iz aktivnosti u aplikaciji / kategorije
    source        VARCHAR(20)  NOT NULL DEFAULT 'system',
    created_by    INTEGER      REFERENCES users(id) ON DELETE SET NULL,
    email_sent    BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Popis obavijesti se uvijek čita "moje, najnovije prvo", a broj nepročitanih
-- se čita na svakom otvaranju karte — oba idu preko ova dva indeksa.
CREATE INDEX IF NOT EXISTS idx_notifications_user_created
    ON notifications(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
    ON notifications(user_id) WHERE is_read = FALSE;

CREATE TABLE IF NOT EXISTS notification_preferences (
    user_id       INTEGER      PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    app_enabled   BOOLEAN      NOT NULL DEFAULT FALSE,
    email_enabled BOOLEAN      NOT NULL DEFAULT FALSE,
    email         VARCHAR(255),
    -- Kategorije i dobne skupine spremaju se kao CSV jer ih klijent i tako
    -- uvijek čita i piše u cijelosti (jedan "Spremi" u postavkama prepiše
    -- cijeli izbor), pa zasebna tablica ne bi ništa dobila.
    categories    TEXT         NOT NULL DEFAULT '',
    age_groups    TEXT         NOT NULL DEFAULT '',
    updated_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

COMMIT;

-- ─── Provjera nakon pokretanja ──────────────────────────────────────────────
-- SELECT COUNT(*) FROM notifications;              -- mora vratiti 0, ne grešku
-- SELECT COUNT(*) FROM notification_preferences;   -- mora vratiti 0, ne grešku
