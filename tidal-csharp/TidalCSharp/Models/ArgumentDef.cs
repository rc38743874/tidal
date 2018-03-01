

namespace TidalCSharp {
	public class ArgumentDef {


		public string ArgumentName { get; set; }
		public string ArgumentTypeCode {get; set; }

		public bool IsNullable { get; set; }

		public ParameterDef ParameterDef {get; set; }
		public PropertyDef PropertyDef {get; set; }


	}
}
