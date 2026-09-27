import { useEffect, useMemo, useState } from 'react'
import axios from 'axios'
import { useNavigate } from 'react-router-dom'

const API = 'https://cromap.onrender.com'

/* ─────────────────────────────── tipovi ─────────────────────────────── */

interface User {
  id: number
  firstName: string
  lastName: string
  username: string
  email: string
  createdAt: string
  /** users.birth_date — null dok ga korisnik ne ispuni (Google prijava) */
  birthDate: string | null
  birthYear: number | null
  age: number | null
  /** zadnji dan sa sesijom, lajkom, komentarom ili objavom; null = nikad */
  lastActiveAt: string | null
  followersCount: number
  followingCount: number
  /** objave korisnika */
  totalPosts: number
  /** lajkovi PRIMLJENI na objavama korisnika */
  totalLikes: number
  /** komentari PRIMLJENI na objavama korisnika */
  totalComments: number
  totalSessionMinutes: number
}

interface AgeGroup {
  label: string
  count: number
}

interface Summary {
  totalUsers: number
  totalLikes: number
  totalComments: number
  totalMinutes: number
  activeLast7Days: number
  averageAge: number | null
  usersWithoutBirthDate: number
  ageGroups: AgeGroup[]
}

interface SupportReport {
  id: number
  type: string
  message: string
  userName: string
  userUsername: string
  createdAt: string
  isResolved?: boolean
}

interface PlanRating {
  id: number
  userName: string
  destination: string
  rating: number
  createdAt: string
}

interface UserActivity {
  date: string
  sessionMinutes: number
  likes: number
  comments: number
  posts: number
}

type Tab = 'users' | 'reports' | 'ratings'
type SortKey =
  | 'name'
  | 'age'
  | 'posts'
  | 'likes'
  | 'comments'
  | 'followers'
  | 'lastActive'
  | 'createdAt'
type SortDir = 'asc' | 'desc'
type ReportFilter = 'all' | 'open' | 'app' | 'map'

/* ──────────────────────────── pomoćne funkcije ──────────────────────── */

// Dobne skupine su iste kao na backendu (AdminRepository.GetAdminSummaryAsync),
// pa se stupac iz grafa i filtar u tablici uvijek slažu.
const AGE_BUCKETS: { label: string; match: (age: number | null) => boolean }[] =
  [
    { label: '<18', match: a => a !== null && a < 18 },
    { label: '18-24', match: a => a !== null && a >= 18 && a <= 24 },
    { label: '25-34', match: a => a !== null && a >= 25 && a <= 34 },
    { label: '35-44', match: a => a !== null && a >= 35 && a <= 44 },
    { label: '45-54', match: a => a !== null && a >= 45 && a <= 54 },
    { label: '55+', match: a => a !== null && a >= 55 },
    { label: 'Nepoznato', match: a => a === null },
  ]

// Backend šalje 30 dana; u grafu se prikazuje zadnjih 14 jer se dulji niz
// ionako ne može usporediti pogledom.
const CHART_DAYS = 14

// "2026-09-26" kroz new Date() postaje ponoć po UTC-u, pa u zonama zapadno od
// Greenwicha ispadne dan ranije. Čisti datum se zato parsira ručno.
const parseDay = (value?: string | null): Date | null => {
  if (!value) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (match) return new Date(+match[1], +match[2] - 1, +match[3])
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

const formatDate = (value?: string | null) => {
  const day = parseDay(value)
  return day ? day.toLocaleDateString('hr-HR') : '—'
}

const formatShortDay = (value: string) => {
  const day = parseDay(value)
  return day
    ? day.toLocaleDateString('hr-HR', { day: '2-digit', month: '2-digit' })
    : value
}

// "Prije 3 dana" je čitljivije od datuma: odmah se vidi tko se još vraća, a
// tko se registrirao pa nestao.
const lastSeenLabel = (value?: string | null) => {
  const day = parseDay(value)
  if (!day) return 'Nikad'
  const now = new Date()
  const diff = Math.round(
    (Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) -
      Date.UTC(day.getFullYear(), day.getMonth(), day.getDate())) /
      86400000,
  )
  if (diff <= 0) return 'Danas'
  if (diff === 1) return 'Jučer'
  if (diff < 30) return `Prije ${diff} d.`
  return day.toLocaleDateString('hr-HR')
}

const isRecent = (value?: string | null, days = 7) => {
  const day = parseDay(value)
  if (!day) return false
  const diff = (Date.now() - day.getTime()) / 86400000
  return diff <= days
}

const formatMinutes = (minutes: number) =>
  minutes >= 60
    ? `${Math.floor(minutes / 60)}h ${minutes % 60}min`
    : `${minutes}min`

const initials = (first: string, last: string) =>
  `${first?.[0] ?? ''}${last?.[0] ?? ''}`.toUpperCase()

const ageBucketOf = (age: number | null) =>
  AGE_BUCKETS.find(bucket => bucket.match(age))?.label ?? 'Nepoznato'

/* ──────────────────────────────── ekran ─────────────────────────────── */

export default function Dashboard() {
  const [users, setUsers] = useState<User[]>([])
  const [summary, setSummary] = useState<Summary | null>(null)
  const [reports, setReports] = useState<SupportReport[]>([])
  const [ratings, setRatings] = useState<PlanRating[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [activeTab, setActiveTab] = useState<Tab>('users')
  const [ageFilter, setAgeFilter] = useState<string | null>(null)
  const [sort, setSort] = useState<{ key: SortKey; dir: SortDir }>({
    key: 'createdAt',
    dir: 'desc',
  })
  const [reportFilter, setReportFilter] = useState<ReportFilter>('all')
  const [selectedUser, setSelectedUser] = useState<User | null>(null)
  const [userActivity, setUserActivity] = useState<UserActivity[]>([])
  const [activityLoading, setActivityLoading] = useState(false)
  const [showModal, setShowModal] = useState(false)
  const navigate = useNavigate()

  const token = localStorage.getItem('adminToken')
  const headers = { Authorization: `Bearer ${token}` }

  useEffect(() => {
    if (!token) {
      navigate('/login')
      return
    }
    loadAll()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const getResolvedIds = (): number[] => {
    try {
      return JSON.parse(localStorage.getItem('resolvedReports') || '[]')
    } catch {
      return []
    }
  }

  const handleToggleResolved = (id: number) => {
    setReports(prev => {
      const updated = prev.map(r =>
        r.id === id ? { ...r, isResolved: !r.isResolved } : r,
      )
      const resolvedIds = updated.filter(r => r.isResolved).map(r => r.id)
      localStorage.setItem('resolvedReports', JSON.stringify(resolvedIds))
      return updated
    })
  }

  const loadAll = async () => {
    setRefreshing(true)
    try {
      const [usersRes, summaryRes, reportsRes, ratingsRes] = await Promise.all([
        axios.get(`${API}/api/admin/users`, { headers }),
        axios.get(`${API}/api/admin/stats/summary`, { headers }),
        axios.get(`${API}/api/support/reports`, { headers }),
        axios.get(`${API}/api/plan-ratings`, { headers }),
      ])
      setUsers(usersRes.data)
      setSummary(summaryRes.data)

      const resolvedIds = getResolvedIds()
      setReports(
        reportsRes.data.map((r: SupportReport) => ({
          ...r,
          isResolved: resolvedIds.includes(r.id),
        })),
      )

      setRatings(ratingsRes.data)
    } catch {
      navigate('/login')
    } finally {
      setLoading(false)
      setRefreshing(false)
    }
  }

  const handleUserClick = async (user: User) => {
    setSelectedUser(user)
    setUserActivity([])
    setShowModal(true)
    setActivityLoading(true)
    try {
      const res = await axios.get(
        `${API}/api/admin/users/${user.id}/daily-activity?days=30`,
        { headers },
      )
      setUserActivity(res.data)
    } catch {
      setUserActivity([])
    } finally {
      setActivityLoading(false)
    }
  }

  const handleLogout = () => {
    localStorage.removeItem('adminToken')
    navigate('/login')
  }

  const toggleSort = (key: SortKey) =>
    setSort(prev =>
      prev.key === key
        ? { key, dir: prev.dir === 'asc' ? 'desc' : 'asc' }
        : { key, dir: key === 'name' || key === 'age' ? 'asc' : 'desc' },
    )

  /* ── popis korisnika: pretraga → dobni filtar → sortiranje ── */

  const visibleUsers = useMemo(() => {
    const query = search.trim().toLowerCase()

    const filtered = users.filter(user => {
      // Uz ime, korisničko ime i e-mail pretražuje se i godište ("1998").
      const haystack = [
        user.firstName,
        user.lastName,
        user.username,
        user.email,
        user.birthYear ? String(user.birthYear) : '',
      ]
        .join(' ')
        .toLowerCase()
      if (query && !haystack.includes(query)) return false
      if (ageFilter && ageBucketOf(user.age ?? null) !== ageFilter) return false
      return true
    })

    const value = (user: User): string | number => {
      switch (sort.key) {
        case 'name':
          return `${user.firstName} ${user.lastName}`.toLowerCase()
        case 'age':
          return user.age ?? 0
        case 'posts':
          return user.totalPosts
        case 'likes':
          return user.totalLikes
        case 'comments':
          return user.totalComments
        case 'followers':
          return user.followersCount
        case 'lastActive':
          return user.lastActiveAt ?? ''
        default:
          return user.createdAt
      }
    }

    return [...filtered].sort((a, b) => {
      // Korisnici bez godišta idu na kraj bez obzira na smjer sortiranja —
      // inače bi pri sortiranju po dobi zauzeli cijeli vrh tablice.
      if (sort.key === 'age') {
        if (a.age == null && b.age == null) return 0
        if (a.age == null) return 1
        if (b.age == null) return -1
      }
      const av = value(a)
      const bv = value(b)
      const result =
        typeof av === 'number' && typeof bv === 'number'
          ? av - bv
          : String(av).localeCompare(String(bv), 'hr')
      return sort.dir === 'asc' ? result : -result
    })
  }, [users, search, ageFilter, sort])

  /* ── podaci za graf aktivnosti ── */

  const chartData = useMemo(
    () => [...userActivity].slice(-CHART_DAYS).reverse(),
    [userActivity],
  )

  const periodTotals = useMemo(
    () =>
      userActivity.reduce(
        (acc, day) => ({
          sessionMinutes: acc.sessionMinutes + day.sessionMinutes,
          likes: acc.likes + day.likes,
          comments: acc.comments + day.comments,
          posts: acc.posts + day.posts,
        }),
        { sessionMinutes: 0, likes: 0, comments: 0, posts: 0 },
      ),
    [userActivity],
  )

  const hasActivity =
    periodTotals.sessionMinutes > 0 ||
    periodTotals.likes > 0 ||
    periodTotals.comments > 0 ||
    periodTotals.posts > 0

  // Stupci se skaliraju prema najvećoj vrijednosti u prikazanom prozoru, a ne
  // prema fiksnoj granici — inače su svi stupci ili maksimalni ili nevidljivi.
  const maxMinutes = Math.max(...chartData.map(a => a.sessionMinutes), 1)
  const maxLikes = Math.max(...chartData.map(a => a.likes), 1)
  const maxComments = Math.max(...chartData.map(a => a.comments), 1)
  const barWidth = (value: number, max: number) =>
    value <= 0 ? 0 : Math.max(4, (value / max) * 200)

  const maxAgeGroup = Math.max(
    ...(summary?.ageGroups ?? []).map(g => g.count),
    1,
  )

  const visibleReports = reports.filter(r => {
    if (reportFilter === 'open') return !r.isResolved
    if (reportFilter === 'app') return r.type === 'app'
    if (reportFilter === 'map') return r.type !== 'app'
    return true
  })
  const openReports = reports.filter(r => !r.isResolved).length

  const averageRating = ratings.length
    ? (ratings.reduce((sum, r) => sum + r.rating, 0) / ratings.length).toFixed(
        1,
      )
    : null

  if (loading)
    return (
      <div style={styles.center}>
        <div style={styles.spinner} />
        <p style={{ color: '#fff', marginTop: 16 }}>Učitavanje...</p>
      </div>
    )

  const sortArrow = (key: SortKey) =>
    sort.key === key ? (sort.dir === 'asc' ? ' ▲' : ' ▼') : ''

  return (
    <div style={styles.container}>
      {/* Header */}
      <div style={styles.header}>
        <div>
          <h1 style={styles.headerTitle}>VARA Admin Panel</h1>
          <p style={styles.headerSub}>Upravljanje korisnicima i aktivnostima</p>
        </div>
        <div style={{ display: 'flex', gap: 10 }}>
          <button
            style={styles.refreshBtn}
            onClick={loadAll}
            disabled={refreshing}
          >
            {refreshing ? 'Osvježavanje…' : '↻ Osvježi'}
          </button>
          <button style={styles.logoutBtn} onClick={handleLogout}>
            Odjava
          </button>
        </div>
      </div>

      {/* Summary Cards */}
      {summary && (
        <div style={styles.summaryGrid}>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>👥</div>
            <div style={styles.summaryNumber}>{summary.totalUsers}</div>
            <div style={styles.summaryLabel}>Korisnici</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>⚡</div>
            <div style={styles.summaryNumber}>
              {summary.activeLast7Days ?? 0}
            </div>
            <div style={styles.summaryLabel}>Aktivni (7 dana)</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>🎂</div>
            <div style={styles.summaryNumber}>
              {summary.averageAge != null ? summary.averageAge : '—'}
            </div>
            <div style={styles.summaryLabel}>Prosječna dob</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>❤️</div>
            <div style={styles.summaryNumber}>{summary.totalLikes}</div>
            <div style={styles.summaryLabel}>Lajkovi</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>💬</div>
            <div style={styles.summaryNumber}>{summary.totalComments}</div>
            <div style={styles.summaryLabel}>Komentari</div>
          </div>
          <div style={styles.summaryCard}>
            <div style={styles.summaryIcon}>⏱️</div>
            <div style={styles.summaryNumber}>
              {Math.floor(summary.totalMinutes / 60)}h
            </div>
            <div style={styles.summaryLabel}>Ukupno vremena</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={styles.tabs}>
        {(['users', 'reports', 'ratings'] as const).map(tab => (
          <button
            key={tab}
            style={{
              ...styles.tab,
              ...(activeTab === tab ? styles.activeTab : {}),
            }}
            onClick={() => setActiveTab(tab)}
          >
            {tab === 'users'
              ? `👥 Korisnici (${users.length})`
              : tab === 'reports'
                ? `🐛 Prijave (${openReports}/${reports.length})`
                : `⭐ Ocjene (${ratings.length})`}
          </button>
        ))}
      </div>

      {/* Content */}
      <div style={styles.content}>
        {/* Users Tab */}
        {activeTab === 'users' && (
          <>
            {/* Raspodjela po dobi — klik na stupac filtrira tablicu */}
            {summary?.ageGroups && summary.ageGroups.length > 0 && (
              <div style={styles.panel}>
                <div style={styles.panelHeader}>
                  <span style={styles.panelTitle}>Dob korisnika</span>
                  <span style={styles.panelNote}>
                    {summary.usersWithoutBirthDate > 0
                      ? `bez datuma rođenja: ${summary.usersWithoutBirthDate}`
                      : 'svi korisnici imaju datum rođenja'}
                  </span>
                </div>
                <div style={styles.ageChart}>
                  {summary.ageGroups.map(group => {
                    const selected = ageFilter === group.label
                    return (
                      <button
                        key={group.label}
                        style={{
                          ...styles.ageColumn,
                          ...(selected ? styles.ageColumnActive : {}),
                        }}
                        onClick={() =>
                          setAgeFilter(selected ? null : group.label)
                        }
                        title={`Prikaži samo skupinu ${group.label}`}
                      >
                        <span style={styles.ageCount}>{group.count}</span>
                        <span
                          style={{
                            ...styles.ageBar,
                            height: Math.max(
                              4,
                              (group.count / maxAgeGroup) * 70,
                            ),
                            backgroundColor: selected ? '#1B3F0E' : '#8fae7e',
                          }}
                        />
                        <span style={styles.ageBucket}>{group.label}</span>
                      </button>
                    )
                  })}
                </div>
              </div>
            )}

            <div style={styles.toolbar}>
              <input
                style={styles.search}
                placeholder="Pretraži po imenu, e-mailu ili godištu..."
                value={search}
                onChange={e => setSearch(e.target.value)}
              />
              {ageFilter && (
                <button
                  style={styles.clearFilter}
                  onClick={() => setAgeFilter(null)}
                >
                  Dob: {ageFilter} ✕
                </button>
              )}
              <span style={styles.resultCount}>
                {visibleUsers.length} od {users.length}
              </span>
            </div>

            <table style={styles.table}>
              <thead>
                <tr style={styles.tableHeader}>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('name')}
                  >
                    Korisnik{sortArrow('name')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('age')}
                  >
                    Godište / dob{sortArrow('age')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('posts')}
                  >
                    Objave{sortArrow('posts')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('likes')}
                  >
                    Lajkovi ✱{sortArrow('likes')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('comments')}
                  >
                    Komentari ✱{sortArrow('comments')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('followers')}
                  >
                    Pratitelji{sortArrow('followers')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('lastActive')}
                  >
                    Zadnja aktivnost{sortArrow('lastActive')}
                  </th>
                  <th
                    style={styles.thSortable}
                    onClick={() => toggleSort('createdAt')}
                  >
                    Registriran{sortArrow('createdAt')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {visibleUsers.length === 0 && (
                  <tr>
                    <td style={styles.emptyCell} colSpan={8}>
                      Nema korisnika koji odgovaraju pretrazi.
                    </td>
                  </tr>
                )}
                {visibleUsers.map(user => (
                  <tr
                    key={user.id}
                    style={{ ...styles.tr, cursor: 'pointer' }}
                    onClick={() => handleUserClick(user)}
                    onMouseEnter={e =>
                      (e.currentTarget.style.backgroundColor = '#f0f7f0')
                    }
                    onMouseLeave={e =>
                      (e.currentTarget.style.backgroundColor = '')
                    }
                  >
                    <td style={styles.td}>
                      <div style={styles.userCell}>
                        <div style={styles.avatar}>
                          {initials(user.firstName, user.lastName)}
                        </div>
                        <div>
                          <div style={styles.userName}>
                            {user.firstName} {user.lastName}
                          </div>
                          <div style={styles.userUsername}>
                            @{user.username} · {user.email}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td style={styles.td}>
                      {user.birthYear ? (
                        <>
                          <div style={styles.birthYear}>{user.birthYear}.</div>
                          <div style={styles.birthAge}>
                            {user.age != null ? `${user.age} g.` : ''}
                          </div>
                        </>
                      ) : (
                        <span style={styles.unknown}>nepoznato</span>
                      )}
                    </td>
                    <td style={styles.tdCenter}>{user.totalPosts}</td>
                    <td style={styles.tdCenter}>{user.totalLikes}</td>
                    <td style={styles.tdCenter}>{user.totalComments}</td>
                    <td style={styles.tdCenter}>{user.followersCount}</td>
                    <td style={styles.td}>
                      <span
                        style={{
                          ...styles.lastSeen,
                          ...(isRecent(user.lastActiveAt)
                            ? styles.lastSeenActive
                            : user.lastActiveAt
                              ? {}
                              : styles.lastSeenNever),
                        }}
                      >
                        {lastSeenLabel(user.lastActiveAt)}
                      </span>
                    </td>
                    <td style={styles.td}>{formatDate(user.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p style={styles.footnote}>
              ✱ Lajkovi i komentari u tablici su oni koje su drugi ostavili na
              objavama korisnika. Što je korisnik sam napravio vidi se u
              njegovom profilu, u grafu aktivnosti.
            </p>
          </>
        )}

        {/* Reports Tab */}
        {activeTab === 'reports' && (
          <>
            <div style={styles.toolbar}>
              {(
                [
                  ['all', `Sve (${reports.length})`],
                  ['open', `Neriješene (${openReports})`],
                  [
                    'app',
                    `🐛 App (${reports.filter(r => r.type === 'app').length})`,
                  ],
                  [
                    'map',
                    `🗺 Karta (${reports.filter(r => r.type !== 'app').length})`,
                  ],
                ] as [ReportFilter, string][]
              ).map(([key, label]) => (
                <button
                  key={key}
                  style={{
                    ...styles.filterChip,
                    ...(reportFilter === key ? styles.filterChipActive : {}),
                  }}
                  onClick={() => setReportFilter(key)}
                >
                  {label}
                </button>
              ))}
            </div>

            <table style={styles.table}>
              <thead>
                <tr style={styles.tableHeader}>
                  <th style={styles.th}>Riješeno</th>
                  <th style={styles.th}>Tip</th>
                  <th style={styles.th}>Korisnik</th>
                  <th style={styles.th}>Poruka</th>
                  <th style={styles.th}>Datum</th>
                </tr>
              </thead>
              <tbody>
                {visibleReports.length === 0 && (
                  <tr>
                    <td style={styles.emptyCell} colSpan={5}>
                      Nema prijava za prikaz.
                    </td>
                  </tr>
                )}
                {visibleReports.map(r => (
                  <tr
                    key={r.id}
                    style={{
                      ...styles.tr,
                      opacity: r.isResolved ? 0.5 : 1,
                      textDecoration: r.isResolved ? 'line-through' : 'none',
                    }}
                  >
                    <td style={styles.tdCenter}>
                      <input
                        type="checkbox"
                        checked={!!r.isResolved}
                        onChange={() => handleToggleResolved(r.id)}
                        style={{ width: 18, height: 18, cursor: 'pointer' }}
                      />
                    </td>
                    <td style={styles.td}>
                      <span
                        style={{
                          ...styles.badge,
                          backgroundColor:
                            r.type === 'app' ? '#fff3e0' : '#e8f5e9',
                          color: r.type === 'app' ? '#ff9500' : '#34c759',
                        }}
                      >
                        {r.type === 'app' ? '🐛 App' : '🗺 Karta'}
                      </span>
                    </td>
                    <td style={styles.td}>
                      <div>{r.userName}</div>
                      <div style={{ color: '#999', fontSize: 12 }}>
                        @{r.userUsername}
                      </div>
                    </td>
                    <td style={styles.td}>{r.message}</td>
                    <td style={styles.td}>{formatDate(r.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}

        {/* Ratings Tab */}
        {activeTab === 'ratings' && (
          <>
            {averageRating && (
              <div style={styles.panel}>
                <div style={styles.panelHeader}>
                  <span style={styles.panelTitle}>Prosječna ocjena plana</span>
                  <span style={styles.panelNote}>{ratings.length} ocjena</span>
                </div>
                <div style={styles.avgRating}>
                  <span style={{ color: '#f39c12', fontSize: 22 }}>
                    {'★'.repeat(Math.round(Number(averageRating)))}
                    <span style={{ color: '#ddd' }}>
                      {'★'.repeat(5 - Math.round(Number(averageRating)))}
                    </span>
                  </span>
                  <span style={styles.avgRatingNumber}>{averageRating}</span>
                </div>
              </div>
            )}

            <table style={styles.table}>
              <thead>
                <tr style={styles.tableHeader}>
                  <th style={styles.th}>Korisnik</th>
                  <th style={styles.th}>Destinacija</th>
                  <th style={styles.th}>Ocjena</th>
                  <th style={styles.th}>Datum</th>
                </tr>
              </thead>
              <tbody>
                {ratings.length === 0 && (
                  <tr>
                    <td style={styles.emptyCell} colSpan={4}>
                      Nema ocjena za prikaz.
                    </td>
                  </tr>
                )}
                {ratings.map(r => (
                  <tr key={r.id} style={styles.tr}>
                    <td style={styles.td}>{r.userName}</td>
                    <td style={styles.td}>{r.destination}</td>
                    <td style={styles.td}>
                      <span style={{ color: '#f39c12' }}>
                        {'★'.repeat(r.rating)}
                      </span>
                      <span style={{ color: '#ddd' }}>
                        {'★'.repeat(5 - r.rating)}
                      </span>
                    </td>
                    <td style={styles.td}>{formatDate(r.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>

      {/* USER MODAL */}
      {showModal && selectedUser && (
        <div style={modal.overlay} onClick={() => setShowModal(false)}>
          <div style={modal.panel} onClick={e => e.stopPropagation()}>
            {/* Modal Header */}
            <div style={modal.header}>
              <div style={modal.avatar}>
                {initials(selectedUser.firstName, selectedUser.lastName)}
              </div>
              <div style={{ flex: 1 }}>
                <div style={modal.name}>
                  {selectedUser.firstName} {selectedUser.lastName}
                </div>
                <div style={modal.username}>@{selectedUser.username}</div>
                <div style={modal.email}>{selectedUser.email}</div>
              </div>
              <button
                style={modal.closeBtn}
                onClick={() => setShowModal(false)}
              >
                ✕
              </button>
            </div>

            {/* Osnovni podaci */}
            <div style={modal.facts}>
              <div style={modal.fact}>
                <div style={modal.factLabel}>Godina rođenja</div>
                <div style={modal.factValue}>
                  {selectedUser.birthYear ? `${selectedUser.birthYear}.` : '—'}
                </div>
              </div>
              <div style={modal.fact}>
                <div style={modal.factLabel}>Dob</div>
                <div style={modal.factValue}>
                  {selectedUser.age != null ? `${selectedUser.age} g.` : '—'}
                </div>
              </div>
              <div style={modal.fact}>
                <div style={modal.factLabel}>Registriran</div>
                <div style={modal.factValue}>
                  {formatDate(selectedUser.createdAt)}
                </div>
              </div>
              <div style={modal.fact}>
                <div style={modal.factLabel}>Zadnja aktivnost</div>
                <div style={modal.factValue}>
                  {lastSeenLabel(selectedUser.lastActiveAt)}
                </div>
              </div>
            </div>

            {/* Stats Grid */}
            <div style={modal.statsGrid}>
              {[
                {
                  label: 'Pratitelji',
                  value: selectedUser.followersCount,
                  icon: '👥',
                },
                {
                  label: 'Praćeni',
                  value: selectedUser.followingCount,
                  icon: '➕',
                },
                { label: 'Objave', value: selectedUser.totalPosts, icon: '📸' },
                {
                  label: 'Lajkovi na objavama',
                  value: selectedUser.totalLikes,
                  icon: '❤️',
                },
                {
                  label: 'Komentari na objavama',
                  value: selectedUser.totalComments,
                  icon: '💬',
                },
                {
                  label: 'Sati u aplikaciji',
                  value: Math.floor(selectedUser.totalSessionMinutes / 60),
                  icon: '⏱️',
                },
              ].map(s => (
                <div key={s.label} style={modal.statCard}>
                  <div style={{ fontSize: 20 }}>{s.icon}</div>
                  <div style={modal.statNumber}>{s.value}</div>
                  <div style={modal.statLabel}>{s.label}</div>
                </div>
              ))}
            </div>

            {/* Activity Chart */}
            <div style={modal.section}>
              <div style={modal.sectionTitle}>📊 Aktivnost korisnika</div>

              {activityLoading ? (
                <div
                  style={{ textAlign: 'center', padding: 24, color: '#999' }}
                >
                  Učitavanje...
                </div>
              ) : !hasActivity ? (
                <div
                  style={{ textAlign: 'center', padding: 24, color: '#999' }}
                >
                  Nema aktivnosti u zadnjih 30 dana
                </div>
              ) : (
                <>
                  {/* Zbroj za cijeli prozor — iz samih stupaca se nije dalo
                      očitati koliko je korisnik ukupno napravio. */}
                  <div style={modal.totals}>
                    <div style={modal.totalCard}>
                      <div style={modal.totalValue}>
                        {formatMinutes(periodTotals.sessionMinutes)}
                      </div>
                      <div style={modal.totalLabel}>u aplikaciji</div>
                    </div>
                    <div style={modal.totalCard}>
                      <div style={modal.totalValue}>{periodTotals.likes}</div>
                      <div style={modal.totalLabel}>lajkova dao</div>
                    </div>
                    <div style={modal.totalCard}>
                      <div style={modal.totalValue}>
                        {periodTotals.comments}
                      </div>
                      <div style={modal.totalLabel}>komentara napisao</div>
                    </div>
                    <div style={modal.totalCard}>
                      <div style={modal.totalValue}>{periodTotals.posts}</div>
                      <div style={modal.totalLabel}>objava</div>
                    </div>
                  </div>
                  <div style={modal.totalsNote}>zbroj za zadnjih 30 dana</div>

                  {/* Legend */}
                  <div style={modal.legend}>
                    <span
                      style={{ ...modal.legendDot, backgroundColor: '#ff9500' }}
                    />{' '}
                    Minute
                    <span
                      style={{
                        ...modal.legendDot,
                        backgroundColor: '#ff4757',
                        marginLeft: 16,
                      }}
                    />{' '}
                    Lajkovi
                    <span
                      style={{
                        ...modal.legendDot,
                        backgroundColor: '#1B3F0E',
                        marginLeft: 16,
                      }}
                    />{' '}
                    Komentari
                    <span style={modal.legendNote}>
                      zadnjih {CHART_DAYS} dana
                    </span>
                  </div>

                  {/* Bars */}
                  <div style={modal.chartContainer}>
                    {chartData.map(a => (
                      <div key={a.date} style={modal.chartRow}>
                        <div style={modal.chartDate}>
                          {formatShortDay(a.date)}
                        </div>
                        <div style={modal.bars}>
                          <div style={modal.barLine}>
                            <div
                              style={{
                                width: barWidth(a.sessionMinutes, maxMinutes),
                                height: 12,
                                backgroundColor: '#ff9500',
                                borderRadius: 4,
                                transition: 'width 0.3s',
                              }}
                            />
                            {a.sessionMinutes > 0 && (
                              <span style={modal.barValue}>
                                {a.sessionMinutes}min
                              </span>
                            )}
                          </div>
                          <div style={modal.barLine}>
                            <div
                              style={{
                                width: barWidth(a.likes, maxLikes),
                                height: 12,
                                backgroundColor: '#ff4757',
                                borderRadius: 4,
                              }}
                            />
                            {a.likes > 0 && (
                              <span style={modal.barValue}>{a.likes}</span>
                            )}
                          </div>
                          <div style={modal.barLine}>
                            <div
                              style={{
                                width: barWidth(a.comments, maxComments),
                                height: 12,
                                backgroundColor: '#1B3F0E',
                                borderRadius: 4,
                              }}
                            />
                            {a.comments > 0 && (
                              <span style={modal.barValue}>{a.comments}</span>
                            )}
                          </div>
                        </div>
                        {a.posts > 0 && (
                          <div style={modal.postsBadge}>📸 {a.posts}</div>
                        )}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

/* ─────────────────────────────── stilovi ────────────────────────────── */

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100vh',
    backgroundColor: '#f5f5f5',
    fontFamily: 'sans-serif',
  },
  center: {
    minHeight: '100vh',
    backgroundColor: '#1B3F0E',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
  },
  spinner: {
    width: 40,
    height: 40,
    border: '4px solid rgba(255,255,255,0.3)',
    borderTop: '4px solid #fff',
    borderRadius: '50%',
    animation: 'spin 1s linear infinite',
  },
  header: {
    backgroundColor: '#1B3F0E',
    padding: '20px 32px',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  headerTitle: { color: '#fff', margin: 0, fontSize: 24, fontWeight: 'bold' },
  headerSub: {
    color: 'rgba(255,255,255,0.6)',
    margin: '4px 0 0',
    fontSize: 13,
  },
  refreshBtn: {
    backgroundColor: 'rgba(255,255,255,0.15)',
    color: '#fff',
    border: '1px solid rgba(255,255,255,0.3)',
    borderRadius: 8,
    padding: '8px 16px',
    cursor: 'pointer',
    fontSize: 14,
  },
  logoutBtn: {
    backgroundColor: '#ff4757',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '8px 20px',
    cursor: 'pointer',
    fontSize: 14,
    fontWeight: 600,
  },
  summaryGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
    gap: 16,
    padding: 24,
  },
  summaryCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    textAlign: 'center',
    boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
  },
  summaryIcon: { fontSize: 28, marginBottom: 8 },
  summaryNumber: { fontSize: 28, fontWeight: 'bold', color: '#1B3F0E' },
  summaryLabel: { fontSize: 13, color: '#999', marginTop: 4 },
  tabs: { display: 'flex', gap: 8, padding: '0 24px 16px' },
  tab: {
    padding: '10px 20px',
    borderRadius: 8,
    border: '1px solid #e0e0e0',
    backgroundColor: '#fff',
    cursor: 'pointer',
    fontSize: 14,
  },
  activeTab: {
    backgroundColor: '#1B3F0E',
    color: '#fff',
    border: '1px solid #1B3F0E',
  },
  content: { padding: '0 24px 24px' },
  panel: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    marginBottom: 16,
    boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
  },
  panelHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'baseline',
    marginBottom: 14,
  },
  panelTitle: { fontSize: 15, fontWeight: 600, color: '#333' },
  panelNote: { fontSize: 12, color: '#bbb' },
  ageChart: { display: 'flex', alignItems: 'flex-end', gap: 10, height: 110 },
  ageColumn: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'flex-end',
    gap: 4,
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    padding: '4px 0',
    borderRadius: 8,
  },
  ageColumnActive: { backgroundColor: '#f0f7f0' },
  ageCount: { fontSize: 13, fontWeight: 600, color: '#333' },
  ageBar: { width: '60%', borderRadius: 4, display: 'block' },
  ageBucket: { fontSize: 11, color: '#999' },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    gap: 10,
    marginBottom: 16,
    flexWrap: 'wrap',
  },
  search: {
    flex: 1,
    minWidth: 240,
    padding: '12px 16px',
    borderRadius: 8,
    border: '1px solid #e0e0e0',
    fontSize: 15,
    boxSizing: 'border-box',
  },
  clearFilter: {
    padding: '8px 14px',
    borderRadius: 16,
    border: '1px solid #1B3F0E',
    backgroundColor: '#1B3F0E',
    color: '#fff',
    cursor: 'pointer',
    fontSize: 13,
  },
  resultCount: { fontSize: 13, color: '#999' },
  filterChip: {
    padding: '8px 14px',
    borderRadius: 16,
    border: '1px solid #e0e0e0',
    backgroundColor: '#fff',
    cursor: 'pointer',
    fontSize: 13,
  },
  filterChipActive: {
    backgroundColor: '#1B3F0E',
    color: '#fff',
    borderColor: '#1B3F0E',
  },
  table: {
    width: '100%',
    backgroundColor: '#fff',
    borderRadius: 12,
    borderCollapse: 'collapse',
    overflow: 'hidden',
    boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
  },
  tableHeader: { backgroundColor: '#f8f9fa' },
  th: {
    padding: '12px 16px',
    textAlign: 'left',
    fontSize: 12,
    color: '#999',
    fontWeight: 600,
    textTransform: 'uppercase',
  },
  thSortable: {
    padding: '12px 16px',
    textAlign: 'left',
    fontSize: 12,
    color: '#777',
    fontWeight: 600,
    textTransform: 'uppercase',
    cursor: 'pointer',
    userSelect: 'none',
    whiteSpace: 'nowrap',
  },
  tr: { borderTop: '1px solid #f0f0f0' },
  td: {
    padding: '14px 16px',
    fontSize: 14,
    color: '#333',
    verticalAlign: 'middle',
  },
  tdCenter: {
    padding: '14px 16px',
    fontSize: 14,
    color: '#333',
    textAlign: 'center',
    verticalAlign: 'middle',
  },
  emptyCell: {
    padding: '28px 16px',
    fontSize: 14,
    color: '#999',
    textAlign: 'center',
  },
  userCell: { display: 'flex', alignItems: 'center', gap: 12 },
  avatar: {
    width: 36,
    height: 36,
    borderRadius: '50%',
    backgroundColor: '#1B3F0E',
    color: '#fff',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: 13,
    fontWeight: 600,
    flexShrink: 0,
  },
  userName: { fontWeight: 600, fontSize: 14 },
  userUsername: { color: '#999', fontSize: 12 },
  birthYear: { fontSize: 14, fontWeight: 600, color: '#333' },
  birthAge: { fontSize: 12, color: '#999' },
  unknown: { fontSize: 13, color: '#ccc', fontStyle: 'italic' },
  lastSeen: { fontSize: 13, color: '#666' },
  lastSeenActive: { color: '#1B3F0E', fontWeight: 600 },
  lastSeenNever: { color: '#ccc', fontStyle: 'italic' },
  footnote: { fontSize: 12, color: '#999', marginTop: 12, lineHeight: 1.5 },
  badge: {
    padding: '4px 10px',
    borderRadius: 12,
    fontSize: 12,
    fontWeight: 600,
  },
  avgRating: { display: 'flex', alignItems: 'center', gap: 12 },
  avgRatingNumber: { fontSize: 20, fontWeight: 'bold', color: '#1B3F0E' },
}

const modal: Record<string, React.CSSProperties> = {
  overlay: {
    position: 'fixed',
    inset: 0,
    backgroundColor: 'rgba(0,0,0,0.5)',
    display: 'flex',
    justifyContent: 'flex-end',
    zIndex: 1000,
  },
  panel: {
    backgroundColor: '#fff',
    width: '100%',
    maxWidth: 560,
    height: '100vh',
    overflowY: 'auto',
    boxShadow: '-4px 0 24px rgba(0,0,0,0.15)',
  },
  header: {
    backgroundColor: '#1B3F0E',
    padding: 24,
    display: 'flex',
    gap: 16,
    alignItems: 'flex-start',
  },
  avatar: {
    width: 56,
    height: 56,
    borderRadius: '50%',
    backgroundColor: 'rgba(255,255,255,0.2)',
    color: '#fff',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: 20,
    fontWeight: 700,
    flexShrink: 0,
  },
  name: { color: '#fff', fontSize: 18, fontWeight: 700 },
  username: { color: 'rgba(255,255,255,0.7)', fontSize: 13, marginTop: 2 },
  email: { color: 'rgba(255,255,255,0.6)', fontSize: 12, marginTop: 4 },
  closeBtn: {
    background: 'none',
    border: 'none',
    color: '#fff',
    fontSize: 20,
    cursor: 'pointer',
    marginLeft: 'auto',
    padding: 4,
  },
  facts: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, 1fr)',
    gap: 12,
    padding: '20px 20px 0',
  },
  fact: { backgroundColor: '#f0f7f0', borderRadius: 10, padding: '10px 14px' },
  factLabel: {
    fontSize: 10,
    color: '#7a8c72',
    textTransform: 'uppercase',
    letterSpacing: 0.4,
  },
  factValue: { fontSize: 14, fontWeight: 600, color: '#1B3F0E', marginTop: 3 },
  statsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(3, 1fr)',
    gap: 12,
    padding: 20,
  },
  statCard: {
    backgroundColor: '#f8f9fa',
    borderRadius: 10,
    padding: 16,
    textAlign: 'center',
  },
  statNumber: {
    fontSize: 22,
    fontWeight: 'bold',
    color: '#1B3F0E',
    marginTop: 6,
  },
  statLabel: { fontSize: 11, color: '#999', marginTop: 4 },
  section: { padding: '0 20px 24px' },
  sectionTitle: {
    fontSize: 15,
    fontWeight: 600,
    color: '#333',
    marginBottom: 16,
  },
  totals: { display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8 },
  totalCard: {
    backgroundColor: '#f8f9fa',
    borderRadius: 10,
    padding: '10px 6px',
    textAlign: 'center',
  },
  totalValue: { fontSize: 15, fontWeight: 700, color: '#1B3F0E' },
  totalLabel: { fontSize: 10, color: '#999', marginTop: 2 },
  totalsNote: { fontSize: 11, color: '#bbb', margin: '6px 0 16px' },
  legend: {
    display: 'flex',
    alignItems: 'center',
    fontSize: 12,
    color: '#666',
    marginBottom: 16,
  },
  legendDot: {
    display: 'inline-block',
    width: 10,
    height: 10,
    borderRadius: '50%',
    marginRight: 4,
  },
  legendNote: { marginLeft: 'auto', color: '#bbb' },
  chartContainer: { display: 'flex', flexDirection: 'column', gap: 8 },
  chartRow: { display: 'flex', alignItems: 'flex-start', gap: 12 },
  chartDate: {
    width: 40,
    fontSize: 11,
    color: '#999',
    paddingTop: 2,
    flexShrink: 0,
  },
  bars: { flex: 1 },
  barLine: { display: 'flex', alignItems: 'center', gap: 4, marginBottom: 3 },
  barValue: { fontSize: 10, color: '#999' },
  postsBadge: {
    fontSize: 11,
    color: '#1B3F0E',
    backgroundColor: '#eaf3e7',
    borderRadius: 8,
    padding: '2px 6px',
    whiteSpace: 'nowrap',
  },
}
