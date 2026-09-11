using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    /// <summary>Kraj u kojem se korisnik pojavljuje, s težinom.</summary>
    public class UserArea
    {
        public double CellLat { get; set; }
        public double CellLon { get; set; }
        public int Weight { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public interface IUserAreaRepository
    {
        /// <summary>
        /// Zabilježi da je korisnik bio u okolici zadane točke. Zove se kad
        /// aplikacija ionako zna lokaciju (karta), ne u pozadini.
        /// </summary>
        Task RecordAsync(int userId, double latitude, double longitude);

        /// <summary>Krajevi u kojima se korisnik najčešće pojavljuje.</summary>
        Task<IEnumerable<UserArea>> GetTopAreasAsync(int userId, int limit = 3);
    }

    public class UserAreaRepository : IUserAreaRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public UserAreaRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        /// <summary>
        /// Zaokruživanje na ~11 km. Time se ne pamti gdje je netko bio nego
        /// samo u kojem je kraju — dovoljno za "objave iz mog kraja", a ne
        /// trag kretanja.
        /// </summary>
        private static double ToCell(double value) => Math.Round(value, 1);

        public async Task RecordAsync(int userId, double latitude, double longitude)
        {
            using var connection = _dbConnection.CreateConnection();

            // Težina raste sa svakim javljanjem; selidba se vidi sama od sebe
            // jer novi kraj raste, a stari ostaje na svom broju.
            var sql = @"
                INSERT INTO user_areas (user_id, cell_lat, cell_lon, weight, last_seen)
                VALUES (@UserId, @CellLat, @CellLon, 1, CURRENT_TIMESTAMP)
                ON CONFLICT (user_id, cell_lat, cell_lon) DO UPDATE SET
                    weight    = user_areas.weight + 1,
                    last_seen = CURRENT_TIMESTAMP";

            await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                CellLat = ToCell(latitude),
                CellLon = ToCell(longitude)
            });
        }

        public async Task<IEnumerable<UserArea>> GetTopAreasAsync(int userId, int limit = 3)
        {
            using var connection = _dbConnection.CreateConnection();

            // Stariji od pola godine se ne broje: tko se preselio ne treba
            // zauvijek dobivati objave iz grada iz kojeg je otišao.
            var sql = @"
                SELECT cell_lat AS CellLat, cell_lon AS CellLon, weight, last_seen AS LastSeen
                FROM user_areas
                WHERE user_id = @UserId
                  AND last_seen > CURRENT_TIMESTAMP - INTERVAL '180 days'
                ORDER BY weight DESC, last_seen DESC
                LIMIT @Limit";

            return await connection.QueryAsync<UserArea>(sql, new { UserId = userId, Limit = limit });
        }
    }
}
