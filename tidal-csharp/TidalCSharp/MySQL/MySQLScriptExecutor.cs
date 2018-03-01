using System;
using System.Data.Common;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace TidalCSharp {

	public class MySQLScriptExecutor : IScriptExecutor {
		
		
		private MySqlConnection mySqlConnection { get; set; }

		public MySQLScriptExecutor(MySqlConnection connection) {
			this.mySqlConnection = connection;
		}

		
		
		public void ExecuteTidalProcedureScript(string scriptText) {
			
			MySqlConnection conn = this.mySqlConnection;

			using (MySqlCommand command = new MySqlCommand(scriptText, conn)) {
				command.ExecuteNonQuery();
			}
		}
	}
}

