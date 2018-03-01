using System.Text;

namespace TidalCSharp {
	public class ParameterDef {

		public string ParameterName {get; set; }

		/* Parent procedure def */
		public ProcedureDef ProcedureDef {get; set; }


		/* cross lookup */
		public ArgumentDef ArgumentDef {get; set; }
		public PropertyDef PropertyDef {get; set; }
		public ColumnDef ColumnDef {get; set; }


		/* TODO: is this redundant of IsOutParameter */
		public string ParameterMode {get; set; }

		public string ParameterDataTypeCode {get; set;}

		/* MethodSuffix */

		/* TODO: is this redundant of CharLength? */
		public int ParameterSize {get; set;}

		public bool IsIdentity {get; set; }
		public bool IsOutParameter {get; set; }
		public bool IsNullable {get; set; }


		public int? CharLength {get; set;}
		public ulong? Precision {get; set;}
		public int? Scale {get; set;}
		public int OrdinalPosition {get; set;}


		public string ToJSONString() {
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"{{parameterName:\"{this.ParameterName}\",");
			sb.AppendLine($"{{parameterMode:\"{this.ParameterMode}\",");
			sb.AppendLine($"parameterDataTypeCode:\"{this.ParameterDataTypeCode}\",");
			sb.AppendLine($"parameterSize:{this.ParameterSize},");
			sb.AppendLine($"isIdentity:{this.IsIdentity.ToString().ToLowerInvariant()},");
			sb.AppendLine($"isOutParameter:{this.IsOutParameter.ToString().ToLowerInvariant()},");
			sb.AppendLine($"isNullable:{this.IsNullable.ToString().ToLowerInvariant()},");
			if (this.CharLength != null) sb.AppendLine($"charLength:{this.CharLength.Value},");
			if (this.Precision != null) sb.AppendLine($"precision:{this.Precision.Value},");
			if (this.Scale != null) sb.AppendLine($"scale:{this.Scale.Value},");
			sb.Append($"ordinalPosition:{this.OrdinalPosition}}}");
			return sb.ToString();
		}
	}
}