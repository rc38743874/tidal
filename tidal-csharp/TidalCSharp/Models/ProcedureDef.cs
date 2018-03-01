using System;
using System.Collections.Generic;
using System.Text;

namespace TidalCSharp {
	public class ProcedureDef {

		/* name of the stored proc, e.g. Web_Author_Create */
		public string ProcedureName { get; set; }

		/* name of the function, e.g. Create (in DataAccess.Author) */
		public string FunctionName { get; set; }

		public TableDef TableDef { get; set; }

		public Dictionary<string, ParameterDef> ParameterDefMap { get; set; }


		public string ReturnTypeCode { get; set; } /* e.g. "Integer" */

		public bool IsSingleRow { get; set; }
		public bool OutputsRows { get; set; }


		public bool ReadOnly { get; set; }

		/* fields returned by the query, if it contains a resultset */
		public Dictionary<string, FieldDef> FieldDefMap { get; set; }


		public string ToJSONString() {
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"{{procedureName:{this.ProcedureName},");
			sb.AppendLine($"\tfunctionName:{this.FunctionName},");
			sb.AppendLine($"\ttableName:{this.TableDef.TableName},");
			sb.AppendLine($"\treturnTypeCode:{this.ReturnTypeCode},");
			sb.AppendLine($"\tisSingleRow:{this.IsSingleRow.ToString().ToLowerInvariant()},");
			sb.AppendLine($"\tisOutputsRows:{this.OutputsRows.ToString().ToLowerInvariant()},");
			sb.Append("\tparameterArray:[");
			bool first = true;
			foreach (var parameterDef in this.ParameterDefMap.Values) {
				if (first == true) {
					first = false;
				}
				else {
					sb.AppendLine(",");
				}
				sb.Append($"\t\t{parameterDef.ToJSONString()}");
			}
			sb.AppendLine("],");

			sb.Append("\tfieldArray:[");
			first = true;
			foreach (var fieldDef in this.FieldDefMap.Values) {
				if (first == true) {
					first = false;
				}
				else {
					sb.AppendLine(",");
				}
				sb.Append($"\t\t{fieldDef.ToJSONString()}");
			}
			sb.AppendLine("\t]");
			sb.AppendLine("}");
			return sb.ToString();
		}
	}
}