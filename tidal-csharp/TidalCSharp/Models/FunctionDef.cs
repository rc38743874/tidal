
using System.Collections.Generic;

namespace TidalCSharp {
	public class FunctionDef {

		public string FunctionName { get; set; }
	//	public string ObjectName {get; set; }

		public bool OutputsList {get; set; }
		public bool OutputsObject {get; set; }

		public string ReturnTypeCode { get; set; }
		
		public List<ArgumentDef> ArgumentDefList { get; set; }
		public List<PropertyDef> OutputPropertyDefList { get; set; }
		
		public bool UsesResult { get; set; }
		public List<ResultPropertyDef> ResultPropertyDefList {get; set;}
		

		/* reference for ease of cross lookups */
		public ProcedureDef ProcedureDef {get; set; }



	}
}