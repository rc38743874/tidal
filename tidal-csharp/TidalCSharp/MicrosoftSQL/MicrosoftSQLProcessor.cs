using System;
using System.Data.Common;
using System.Data.SqlClient;


namespace TidalCSharp {
	public class MicrosoftSQLProcessor : IProcessor {
		
		private SqlConnection connection;
		

		public DbConnection GetConnection(string connectionString, string password) {
			var builder = new DbConnectionStringBuilder();
			builder.ConnectionString = connectionString;
			if (password != null) {
				builder["password"] = password;
			}
			this.connection = new SqlConnection(builder.ConnectionString);
			return this.connection;
		}
		
		public ITableExtractor GetTableExtractor() {
			return new MicrosoftSQLTableExtractor(connection);
		}
		
		public ITableScriptWriter GetTableScriptWriter() {
			return new MicrosoftSQLTableScriptWriter();
		}
		
		public IClassCreator GetClassCreator() {
			return new MicrosoftSQLClassCreator();
		}
		
		public IProcedureCreator GetProcedureCreator() {
			return new MicrosoftSQLProcedureCreator();
		}
		
		public IProcedureReader GetProcedureReader() {
			return new MicrosoftSQLProcedureReader(connection);
		}
		
		public IProcedureRemover GetProcedureRemover() {
			return new MicrosoftSQLProcedureRemover(connection);
		}
		
		public IScriptExecutor GetScriptExecutor() {
			return new MicrosoftSQLScriptExecutor(connection);
		}
		
	}
}

