using System;
using System.Text.RegularExpressions;

namespace TidalCSharp {

	public class TypeConvertor {

		public static string ConvertNullableSQLToCSharp(string sqlTypeCode, bool isNullable) {
			string typeCode = TypeConvertor.ConvertSQLToCSharp(sqlTypeCode);
			if (isNullable && typeCode!="string") typeCode += "?";
			return typeCode;
		}



		public static string ConvertSQLToCSharp (string sqlTypeCode) {
			switch (sqlTypeCode) {
				case "binary":
				case "varbinary":
					return "Byte[]";
				case "tinyint":
					return "byte";
				case "smallint":
					return "short";
				case "int":
					return "int";
				case "bigint":
					return "long";
				case "smallmoney":
				case "money":
				case "numeric":
				case "decimal":
					return "decimal";
				case "bit":
					return "bool";
				case "varchar":
				case "char":
				case "nvarchar":
				case "nchar":
				case "xml":
					return "string";
				case "real":
					return "single";
				case "float":
					return "double";
				case "date":
				case "smalldatetime":
				case "datetime":
				case "datetime2":
					return "DateTime";
	/*
				case "binary":
				case "varbinary":
					return "Byte[]";
				case "tinyint":
					return "byte";
				case "smallint":
					return "short";
				case "int":
				case "System.Int32":
					return "int";
				case "bigint":
				case "System.Int64":
					return "long";
				case "System.UInt64":
					return "ulong";
				case "smallmoney":
				case "money":
				case "numeric":
				case "decimal":
				case "System.Decimal":
					return "decimal";
				case "bit":
				case "System.Boolean":
					return "bool";
				case "varchar":
				case "char":
				case "nvarchar":
				case "nchar":
				case "System.String":
					return "string";
				case "real":
				case "System.Single":
					return "single";
				case "float":
				case "System.Double":
					return "double";
				case "smalldatetime":
				case "datetime":
				case "System.DateTime":
					return "DateTime";
	*/
				default:
					throw new ApplicationException("Unrecognized SQL type code passed to TypeConvertor: " + sqlTypeCode);
			}


		}

		/* use more vernacular names such as long instead of System.Int64 e.g. */
		public static string ConvertCLRToVernacular(string clrTypeCode) {
			if (clrTypeCode.StartsWith("System.Nullable`1[", StringComparison.InvariantCulture)) {
				string innerCLRCode = clrTypeCode.Substring(18,clrTypeCode.Length - 19);
				string innerVernacularCode = ConvertCLRToVernacular(innerCLRCode);
				return innerVernacularCode + "?";
			}
			switch (clrTypeCode) {
				case "System.Int32":
					return "int";
				case "System.Int64":
					return "long";
				case "System.UInt64":
					return "ulong";
				case "System.Decimal":
					return "decimal";
				case "System.Boolean":
					return "bool";
				case "System.String":
					return "string";
				case "System.Single":
					return "single";
				case "System.Double":
					return "double";
				case "System.DateTime":
					return "DateTime";
				default:
					Console.WriteLine("Warning: Unrecognized CLR type code passed to TypeConvertor: " + clrTypeCode);
					return null;
					// throw new ApplicationException("Unrecognized CLR type code passed to TypeConvertor: " + clrTypeCode);
			}
		}





	}
}