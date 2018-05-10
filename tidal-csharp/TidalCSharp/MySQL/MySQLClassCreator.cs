using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TidalCSharp {
	public class MySQLClassCreator : ClassCreatorBase {

		protected override void OutputSpecificUsingLines(StringBuilder buildText) {
			buildText.AppendLine("using MySql.Data;");
			buildText.AppendLine("using MySql.Data.MySqlClient;");
		}

		protected override string GetSqlCommandText() {
			return "MySqlCommand";
		}

		protected override string GetSqlConnectionText() {
			return "MySqlConnection";
		}

		protected override string GetSqlTransactionText() {
			return "MySqlTransaction";
		}

		protected override string GetSqlParameterText() {
			return "MySqlParameter";
		}

		protected override string GetSqlDbTypeText() {
			return "MySqlDbType";
		}

		protected override string GetSqlDbTypeCode(string inputCode) {
			switch (inputCode) {
				case "bigint":
					return "Int64";
				case "int":
					return "Int32";
				default:
					throw new ApplicationException("Unknown input code for GetSqlDbTypeCode: " + inputCode);
			}
		}
	}

}


