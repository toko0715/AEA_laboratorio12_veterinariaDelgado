using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace VeterinariaApiDelgado.Data;

internal static class ErroresSql
{
    public static bool EsDuplicado(DbUpdateException error) =>
        error.InnerException is SqlException { Number: 2601 or 2627 };

    public static bool EsRestriccion(DbUpdateException error) =>
        error.InnerException is SqlException { Number: 547 };
}
