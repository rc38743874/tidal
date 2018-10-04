
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TidalCSharp {
	public class ModelDef {

		public string ModelName {get; set;}
		public string Namespace { get; set; }

		public List<FunctionDef> FunctionDefList { get; set; }
		public Dictionary<string, PropertyDef> PropertyDefMap { get; set; }


		/* collect all fields in queries that this model drives, for use with FillField */
		public Dictionary<string, FieldDef> FieldDefMap { get; set; }


		public bool UsesBuildListFunction { get; set; }
		public bool UsesMakeObjectFunction { get; set; }

		/** if this model is just a table and doesn't exist as a matching class in our assembly */
		public bool IsJustTable { get; set; }

		public PropertyDef GetLikelyPropertyDef(string sqlName) {
			string possiblePropertyName = sqlName;
			if (this.PropertyDefMap.ContainsKey(possiblePropertyName) == true) {
				return this.PropertyDefMap[possiblePropertyName];
			}

			/* If a field is suffixed with Key it will be referring to the object if there is a foreign key constraint */
			if (possiblePropertyName.EndsWith("Key", false, CultureInfo.InvariantCulture)) {
				possiblePropertyName = StripKeySuffix(possiblePropertyName);

				if (this.PropertyDefMap.ContainsKey(possiblePropertyName)) {
					return this.PropertyDefMap[possiblePropertyName];
				}
			}

			return null;
		}

		public List<PropertyDef> ScanForLikelyPropertyDef(List<PropertyDef> incomingPropertyDefChain, string sqlName, ModelDef referencedModelDef, List<ModelDef> modelDefList) {
			/* used when we haven't found any property to use, but it might be that it is a property of a 
			 * referenced object (or of a reference to a reference etc.).  So drill down along the model map
			 * to see if we can find one that matches.
			 */
			var usedModelDefs = new List<ModelDef>() { this };

			foreach (var propertyDef in this.PropertyDefMap.Values) {
				if (propertyDef.IsReference) {
					var subModelDef = modelDefList.FirstOrDefault(x => x.Namespace == propertyDef.PropertyTypeNamespace && x.ModelName == propertyDef.PropertyTypeCode);
					if (usedModelDefs.Contains(subModelDef) == false) {
						usedModelDefs.Add(subModelDef);
						var outputPropertyDefChain = new List<PropertyDef>(incomingPropertyDefChain);
						outputPropertyDefChain.Add(propertyDef);
						// Shared.Info($"DEBUG:sqlName:{sqlName},referencedModelDef.ModelName={referencedModelDef.ModelName}, subModelDef.ModelName={subModelDef.ModelName}");
						if (subModelDef == referencedModelDef) {
							var newPropertyDef = subModelDef.GetLikelyPropertyDef(sqlName);
							if (newPropertyDef != null) {
								outputPropertyDefChain.Add(newPropertyDef);
								return outputPropertyDefChain;
							}
						}
						else {
							if (subModelDef != null) {
								var bestResult = subModelDef.ScanForLikelyPropertyDef(outputPropertyDefChain, sqlName, referencedModelDef, modelDefList);
								if (bestResult != null) return bestResult;
							}
						}
					}
				}
			}
			return null;					
		}

		/* TODO: Remove duplicated function */
		private string StripKeySuffix(string name) {
			if (name.EndsWith("Key", false, CultureInfo.InvariantCulture)) {
				return name.Substring(0, name.Length-3);
			}
			else {
				return name;
			}
		}


	}
}