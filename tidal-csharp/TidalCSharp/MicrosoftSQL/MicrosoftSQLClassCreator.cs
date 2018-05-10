using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TidalCSharp {
	public class MicrosoftSQLClassCreator : ClassCreatorBase {

		protected override void OutputSpecificUsingLines(StringBuilder buildText) {
			buildText.AppendLine("using System.Data.SqlClient;");
		}

		protected override string GetSqlCommandText() {
			return "SqlCommand";
		}

		protected override string GetSqlConnectionText() {
			return "SqlConnection";
		}

		protected override string GetSqlTransactionText() {
			return "SqlTransaction";
		}

		protected override string GetSqlParameterText() {
			return "SqlParameter";
		}

		protected override string GetSqlDbTypeText() {
			return "SqlDbType";
		}

		protected override string GetSqlDbTypeCode(string inputCode) {
			switch (inputCode) {
			case "bigint":
				return "BigInt";
			case "int":
				return "Int";
			case "tinyint":
				return "TinyInt";
			case "smallint":
				return "SmallInt";
			default:
				throw new ApplicationException("Unknown input code for GetSqlDbTypeCode: " + inputCode);
			}
		}

	}

}


