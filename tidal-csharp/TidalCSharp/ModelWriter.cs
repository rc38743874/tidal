using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TidalCSharp {
	public class ModelWriter {
		public static void WriteDefsToFile (List<ModelDef> modelDefList, string fileName) {
			var outText = new StringBuilder ();
			outText.Append ("[");
			bool firstModel = true;
			foreach (var modelDef in modelDefList.OrderBy(x => x.ModelName)) {
				if (firstModel) firstModel = false; else outText.AppendLine (",");
				outText.AppendLine ("{\"name\":\"" + modelDef.ModelName + "\",");
				outText.AppendLine("\t\"namespace\":\"" + modelDef.Namespace + "\",");
				outText.Append("\t\"propertyDefList\":[");
				bool first = true;
				foreach (var propertyDef in modelDef.PropertyDefMap.Values) {
					if (first) first = false; else { outText.AppendLine (","); outText.Append ("\t\t"); }
					outText.Append ("{\"propertyName\":\"" + propertyDef.PropertyName + "\", ");
					outText.Append ("\"propertyTypeCode\":\"" + propertyDef.PropertyTypeCode + "\", ");
					outText.Append("\"propertyTypeNamespace\":\"" + propertyDef.PropertyTypeNamespace + "\", ");
					outText.Append("\"isEnum\":\"" + propertyDef.IsEnum.ToString().ToLower() + "\", ");
					outText.Append("\"isInterface\":\"" + propertyDef.IsInterface.ToString().ToLower() + "\", ");
					outText.Append("\"isReference\":" + propertyDef.IsReference.ToString().ToLower() + "}");
				}
				outText.Append("]}");
			}
			outText.AppendLine("]");
			File.WriteAllText (fileName, outText.ToString());
		}
	}
}
