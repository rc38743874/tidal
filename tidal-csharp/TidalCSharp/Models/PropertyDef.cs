using System.Collections.Generic;

namespace TidalCSharp {

	/* represents a property of a class in the model */
	public class PropertyDef {

		public string PropertyName {get; set; }
		public string PropertyTypeCode {get; set; }


		/* does this property refer to an object class rather than a native type? */
		public bool IsReference { get; set; }

		/* cross lookups */
		/* for when it is an input */
		public ParameterDef ParameterDef {get; set; }
		public ArgumentDef ArgumentDef {get; set; }

		/* for when it is an output */
	//	public List<FieldDef> FieldDefList {get; set; }

	}

}