using System;
using System.Data.Common;
using MySql.Data.MySqlClient;


namespace TidalCSharp {
	public class MySQLProcessor : IProcessor {
		
		private MySqlConnection connection;
		

		public DbConnection GetConnection(string connectionString, string password) {
			var builder = new DbConnectionStringBuilder();
			builder.ConnectionString = connectionString;
			if (password != null) {
				builder["password"] = password;
			}
			this.connection = new MySqlConnection(builder.ConnectionString);
			return this.connection;
		}
		
		public ITableExtractor GetTableExtractor() {
			return new MySQLTableExtractor(connection);
		}
		
		public ITableScriptWriter GetTableScriptWriter() {
			return new MySQLTableScriptWriter();
		}
		
		public ClassCreatorBase GetClassCreator() {
			return new MySQLClassCreator();
		}
		
		public IProcedureCreator GetProcedureCreator() {
			return new MySQLProcedureCreator();
		}
		
		public IProcedureReader GetProcedureReader() {
			return new MySQLProcedureReader(connection);
		}
		
		public IProcedureRemover GetProcedureRemover() {
			return new MySQLProcedureRemover(connection);
		}
		
		public IScriptExecutor GetScriptExecutor() {
			return new MySQLScriptExecutor(connection);
		}
		
	}
}

