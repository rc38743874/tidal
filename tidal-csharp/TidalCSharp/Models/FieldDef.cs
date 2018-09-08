using System;
using System.Collections.Generic;
using System.Text;

/* represents a field in the results of a stored procedure query.
	Not every field will always translate to a PropertyDef, but usually they do.
	Also not every field is necessarily a ColumnDef, because some fields may be computed in the output.
 */
namespace TidalCSharp {
	public class FieldDef {

		public string FieldName {get; set;}
		public ProcedureDef ProcedureDef {get; set; }

		/* The vernacular of the data type for this field (e.g. short, int, string) */
		public string DataTypeCode {get; set; }

		public bool IsNullable {get; set; }

		public string BaseTableName {get; set; }
		public string BaseColumnName {get; set; }


		/* cross lookup for a single property */
		public PropertyDef PropertyDef {get; set; }

		/* cross lookup if the property is a sub-property of another referenced model */
		public List<PropertyDef> PropertyDefChain { get; set; }

		/* TODO: these aren't really part of FieldDef, should ref PropertyDef I think */
		public string PropertyTypeCode {get; set; }
		public bool IsReference {get; set; }

		public string ToJSONString() {
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"{{fieldName:\"{this.FieldName}\",");
			sb.AppendLine($"dataTypeCode:\"{this.DataTypeCode}\",");
			sb.AppendLine($"isNullable:{this.IsNullable.ToString().ToLowerInvariant()},");
			sb.AppendLine($"baseTableName:\"{this.BaseTableName}\",");
			sb.Append($"baseColumnName:\"{this.BaseColumnName}\"}}");
			return sb.ToString();
		}
	}
}