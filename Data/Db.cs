using System.Data.SqlClient;

namespace ChudoObuvApp
{
    public static class Db
    {
        // ВНИМАНИЕ: У тебя база называется Chudo_obuv (с подчеркиванием)
        private const string ConnectionString =
            "Data Source=localhost;Initial Catalog=Chudo_obuv;Integrated Security=True;TrustServerCertificate=True;";

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}