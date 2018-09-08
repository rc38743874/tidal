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
			string newLineString;
			if (scriptText.Contains("\r\n")) {
				newLineString = "\r\n";
			}
			else {
				newLineString = "\n";
			}
			
			string[] array = scriptText.Split(new string[] { newLineString + "GO" + newLineString }, StringSplitOptions.RemoveEmptyEntries);

			for (int index = 0; index < array.Length; index ++) {
				string execText = array[index];
				Console.Write("Executing script command #" + (index + 1) + " of " + array.Length + "...");
				SqlConnection conn = this.SqlConnection;

                using (SqlCommand command = new SqlCommand(execText, conn)) {
					try {
						command.ExecuteNonQuery();
					} 
					catch {
						Console.WriteLine("Command Text: " + execText);
						throw;
					}
                }
				Console.WriteLine(" complete.");
            }
		}
	}
}

