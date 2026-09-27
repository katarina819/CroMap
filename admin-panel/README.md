# Web admin panel

`Dashboard.tsx` je izvor glavnog ekrana web admin panela koji se spaja na ovaj
backend (`https://cromap.onrender.com`). Panel je zasebna React aplikacija
(react-router-dom + axios) i **ne gradi se zajedno s backendom** — ovdje stoji
zato što je jedini potrošač admin API-ja, pa se promjena API-ja i promjena
ekrana vide na istom mjestu.

Datoteka se ne koristi izravno: kopira se u projekt admin panela preko
postojećeg `Dashboard.tsx`. Ako se ekran mijenja tamo, neka se promjena vrati i
ovamo — inače ove dvije kopije razbježe.

## Što ekran očekuje od API-ja

| Ruta | Što koristi |
| --- | --- |
| `GET /api/admin/users` | `birthDate`, `birthYear`, `age`, `lastActiveAt` uz postojeće brojače |
| `GET /api/admin/stats/summary` | `activeLast7Days`, `averageAge`, `usersWithoutBirthDate`, `ageGroups[{ label, count }]` |
| `GET /api/admin/users/{id}/daily-activity?days=30` | niz od 30 dana uzlazno, s retkom i za dane bez aktivnosti |
| `GET /api/support/reports`, `GET /api/plan-ratings` | nepromijenjeno |

Sva polja koja mogu biti prazna (`birthDate`, `birthYear`, `age`,
`lastActiveAt`) ekran prikazuje kao "nepoznato" / "Nikad", pa panel radi i nad
starijim odgovorima backenda.

## Na što paziti u ekranu

- **Lajkovi i komentari u tablici korisnika su PRIMLJENI** — oni koje su drugi
  ostavili na objavama tog korisnika. Lajkovi i komentari u grafu aktivnosti su
  ono što je korisnik SAM napravio. Zato su i različito nazvani.
- Dobne skupine (`AGE_BUCKETS`) moraju odgovarati onima koje računa
  `AdminRepository.GetAdminSummaryAsync`; inače filtar u tablici i stupci u
  grafu pokazuju različite brojeve.
- Riješene prijave čuvaju se u `localStorage` (`resolvedReports`), ne na
  backendu — vide se samo u pregledniku u kojem su označene.
