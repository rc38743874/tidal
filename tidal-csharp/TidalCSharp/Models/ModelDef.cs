
using System.Collections.Generic;

namespace TidalCSharp {
	public class ModelDef {

		public string ModelName {get; set;}

		public List<FunctionDef> FunctionDefList { get; set; }
		public Dictionary<string, PropertyDef> PropertyDefMap { get; set; }


		/* collect all fields in queries that this model drives, for use with FillField */
		public Dictionary<string, FieldDef> FieldDefMap { get; set; }


		public bool UsesBuildListFunction { get; set; }
		public bool UsesMakeObjectFunction { get; set; }


		public PropertyDef GetLikelyPropertyDef(string sqlName) {
			string possiblePropertyName = sqlName;
			if (this.PropertyDefMap.ContainsKey(possiblePropertyName) == true) {
				return this.PropertyDefMap[possiblePropertyName];
			}

			/* If a field is suffixed with Key it will be referring to the object if there is a foreign key constraint */
			if (possiblePropertyName.EndsWith("Key")) {
				possiblePropertyName = StripKeySuffix(possiblePropertyName);

				if (this.PropertyDefMap.ContainsKey(possiblePropertyName)) {
					return this.PropertyDefMap[possiblePropertyName];
				}
			}
			
			return null;
		}

	/* TODO: Duplicate function */
		private string StripKeySuffix(string name) {
			if (name.EndsWith("Key")) {
				return name.Substring(0, name.Length-3);
			}
			else {
				return name;
			}
		}


	}
}