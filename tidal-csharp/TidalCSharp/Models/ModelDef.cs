
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
		
		/* abstract classes require Func<> arguments passed in for object creation */
		public bool IsAbstract { get; set; }

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

		public List<PropertyDef> ScanForLikelyPropertyDef(List<PropertyDef> incomingPropertyDefChain, string sqlName, ModelDef referencedModelDef, List<ModelDef> modelDefList, List<ModelDef> usedModelDefList) {
			/* used when we haven't found any property to use, but it might be that it is a property of a 
			 * referenced object (or of a reference to a reference etc.).  So drill down along the model map
			 * to see if we can find one that matches.
			 */

			foreach (var propertyDef in this.PropertyDefMap.Values) {
				if (propertyDef.PropertyTypeNamespace == "System.Collections.Generic") {
					/* skip lists and dictionaries */
				} 
				else if (propertyDef.IsReference) {
					/* gets type model def with same namespace+modelname */
					var subModelDef = modelDefList.FirstOrDefault(x => x.Namespace == propertyDef.PropertyTypeNamespace && x.ModelName == propertyDef.PropertyTypeCode);
					if (subModelDef == null) {
						// Console.WriteLine("Warning: Property Def " + propertyDef.PropertyName + " had Model Def that was not found for Namespace==" + propertyDef.PropertyTypeNamespace + " and ModelName==" + propertyDef.PropertyTypeCode + ".  Skipping.");
					}
					else {
						if (usedModelDefList.Contains(subModelDef) == false) {
							usedModelDefList.Add(subModelDef);
							var outputPropertyDefChain = new List<PropertyDef>(incomingPropertyDefChain);
							outputPropertyDefChain.Add(propertyDef);
							// Console.WriteLine($"DEBUG:sqlName:{sqlName},referencedModelDef.ModelName={referencedModelDef.ModelName}, subModelDef.ModelName={subModelDef.ModelName}");
							if (subModelDef == referencedModelDef) {
								var newPropertyDef = subModelDef.GetLikelyPropertyDef(sqlName);
								if (newPropertyDef != null) {
									outputPropertyDefChain.Add(newPropertyDef);

									return outputPropertyDefChain;
								}
							}
							else {
								var bestResult = subModelDef.ScanForLikelyPropertyDef(outputPropertyDefChain, sqlName, referencedModelDef, modelDefList, usedModelDefList);
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