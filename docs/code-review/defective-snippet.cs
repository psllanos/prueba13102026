// ---------------------------------------------------------------------------------------------
// DEFECTIVE SNIPPET UNDER REVIEW  (Entregable 4 - Code Review)
//
// This file reproduces the defective C# snippet supplied with the exercise. It is intentionally
// wrong and is NEVER compiled: it is excluded from the solution and kept only as review material.
//
// The exercise statement lists exactly eight defects the reviewer must find:
//   1. falta de asincronia real          (no real asynchrony)
//   2. ausencia de transacciones         (no transaction)
//   3. error de concurrencia             (concurrency bug)
//   4. logica incorrecta                 (wrong logic)
//   5. no hay validaciones               (no validation)
//   6. no cumple SOLID                   (SOLID violation)
//   7. no hay DTOs                       (no DTOs)
//   8. no hay politica de seleccion      (no selection policy)
//
// Every one of the eight is present, and each is traced to a line in docs/code-review/review.md.
// ---------------------------------------------------------------------------------------------

using System.Data.SqlClient;

namespace Asisya.Assistance.Api;

public class ProviderController : ControllerBase
{
    // DEFECT 6 (SOLID): the controller news up its own SqlConnection and holds the selection
    // rules inline. It is a fat, multi-responsibility class that cannot be tested without SQL.
    private readonly SqlConnection _connection = new SqlConnection(
        "Server=prod-as-isya.database.windows.net;Database=Assistance;User Id=sa;Password=As1sya2024!;");

    // DEFECT 8 (no selection policy): the weights (0.5 / 0.2 / 0.3) are magic numbers inline.
    // No ScoringPolicy abstraction exists, so changing the business rule means editing a controller.
    [HttpPost]
    [Route("api/provider/optimize")]
    public Provider Optimize([FromBody] dynamic request)
    {
        // DEFECT 5 (no validation): request is `dynamic`, so nothing is validated. A missing or
        // malformed field surfaces later as a NullReferenceException inside the processing loop.
        var serviceType = (string)request.serviceType;
        var latitude = (double)request.latitude;
        var longitude = (double)request.longitude;

        // DEFECT 1 (no real asynchrony): async/await is faked. .Result and .Wait() block the thread
        // and defeat the async pipeline; on ASP.NET Core this is thread-pool starvation.
        _connection.OpenAsync().Wait();
        var command = new SqlCommand(
            "SELECT Id, Name, EtaMinutes, DistanceKm, IsAvailable, Rating FROM Providers WHERE ServiceType = '"
            + serviceType + "'", _connection);

        var reader = command.ExecuteReaderAsync().Result;

        // DEFECT 3 (concurrency): a shared mutable list is filled from a blocked read on a
        // controller field. Two concurrent requests share _candidates and interleave their writes.
        _candidates.Clear();
        while (reader.Read())
        {
            _candidates.Add(new Provider
            {
                Id = (int)reader["Id"],
                Name = (string)reader["Name"],
                EtaMinutes = (int)reader["EtaMinutes"],
                DistanceKm = (double)reader["DistanceKm"],
                IsAvailable = (bool)reader["IsAvailable"],
                Rating = (double)reader["Rating"]
            });
        }

        Provider best = null;
        var bestScore = 0.0;

        // DEFECT 4 (wrong logic): the score is computed the wrong way round. Lower ETA is better,
        // yet ETA is added directly; a longer ETA raises the score. `bestScore` starts at 0.0, so a
        // provider whose score is negative (impossible after adding ETA) is never selected, and the
        // availability flag is read but never used. Distance is treated as higher-is-better too.
        foreach (var p in _candidates)
        {
            var score = (p.EtaMinutes * 0.5) + (p.DistanceKm * 0.2) + (p.Rating * 0.3);

            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        // DEFECT 2 (no transaction): the case is inserted and the provider is then marked as busy in
        // two independent statements. If the UPDATE fails the case exists with no assigned provider,
        // and there is no rollback of the first write. Under load this double-books a provider.
        var insert = new SqlCommand(
            "INSERT INTO AssistanceCases (ServiceType, ProviderId, CreatedAt) VALUES ('"
            + serviceType + "', " + best.Id + ", GETDATE())", _connection);
        insert.ExecuteNonQuery();

        var update = new SqlCommand(
            "UPDATE Providers SET IsAvailable = 0 WHERE Id = " + best.Id, _connection);
        update.ExecuteNonQuery();

        // DEFECT 7 (no DTOs): the persistence entity `Provider` is serialised straight back to the
        // client, leaking the internal schema and coupling the API contract to the database.
        return best;
    }

    // DEFECT 3 (concurrency), continued: a static list mutated by every request with no lock.
    private static readonly List<Provider> _candidates = new List<Provider>();
}
