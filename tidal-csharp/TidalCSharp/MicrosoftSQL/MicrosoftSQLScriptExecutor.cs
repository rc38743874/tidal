using System;
using System.Data.Common;
using System.Data.SqlClient;

namespace TidalCSharp {

	public class MicrosoftSQLScriptExecutor : IScriptExecutor {
		
		
		private SqlConnection SqlConnection { get; set; }

		public MicrosoftSQLScriptExecutor(SqlConnection connection) {
			this.SqlConnection = connection;
		}

		
		
		public void ExecuteTidalProcedureScript(string scriptText) {
            string[] array = scriptText.Split(new string[] { "\nGO\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string execText in array) {
                SqlConnection conn = this.SqlConnection;

                using (SqlCommand command = new SqlCommand(execText, conn)) {
                    command.ExecuteNonQuery();
                }
            }
		}
	}
}

