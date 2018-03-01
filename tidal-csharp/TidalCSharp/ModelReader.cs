using System;
using System.Reflection;
using System.Collections.Generic;

namespace TidalCSharp {
	
	public class ModelReader {
			
		public static Dictionary<string, ModelDef> ReadFromFile (string fileName, string requiredNamespace) {
			Dictionary<string, ModelDef> modelDefMap = new Dictionary<string, ModelDef>();

			Assembly modelsAssembly = Assembly.LoadFile(fileName);

		

			foreach (Type modelType in modelsAssembly.GetTypes()) {
		
				if (modelType.Namespace == requiredNamespace) {

					ModelDef modelDef = new ModelDef {
						ModelName = modelType.Name,
						FunctionDefList = new List<FunctionDef>(),
						PropertyDefMap = new Dictionary<string, PropertyDef>(),
						FieldDefMap = new Dictionary<string, FieldDef>()};

					foreach (PropertyInfo info in modelType.GetProperties()) {
				
						string typeCode = info.PropertyType.Name;
						if (info.PropertyType.Namespace != requiredNamespace) {
							typeCode = TypeConvertor.ConvertCLRToVernacular(info.PropertyType.ToString());
						}

						bool isReference = info.PropertyType.IsClass;
						if (typeCode == "string") isReference = false;

						PropertyDef propertyDef = new PropertyDef {
								PropertyName = info.Name,
								PropertyTypeCode = typeCode,
								IsReference = isReference
	/* TODO: better to use IsClass or IsByRef ? */

						};

						modelDef.PropertyDefMap[info.Name] = propertyDef;


					}

					modelDefMap[modelDef.ModelName] = modelDef;

	//				var typeInfo = info.GetTypeInfo();

	//				Console.WriteLine(typeInfo.Name);

				}

			}

			return modelDefMap;


		}



	}
}