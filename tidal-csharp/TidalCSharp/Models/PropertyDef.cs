using System.Collections.Generic;
using System.Linq;

namespace TidalCSharp {

	/* represents a property of a class in the model */
	public class PropertyDef {

		public string PropertyName {get; set; }
		public string PropertyTypeCode {get; set; }
		public string PropertyTypeNamespace { get; set; }

		/* does this property refer to an object class rather than a native type? */
		public bool IsReference { get; set; }

		/* does this property refer to an enumeration? */
		public bool IsEnum { get; set; }

		/* does this property point to an interface instead of a class? */
		public bool IsInterface { get; set; }

		/* cross lookups */
		/* for when it is an input */
		public ParameterDef ParameterDef {get; set; }
		public ArgumentDef ArgumentDef {get; set; }

		/* for when it is an output */
	//	public List<FieldDef> FieldDefList {get; set; }

		public ModelDef GetModelDef(List<ModelDef> modelDefList) {
			/* TODO: perhaps this should be set at creation time? */
			return modelDefList.FirstOrDefault(def => def.Namespace==this.PropertyTypeNamespace && def.ModelName==this.PropertyTypeCode);
		}

	}

}