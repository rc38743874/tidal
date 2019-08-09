using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Loader;

namespace TidalCSharp {
	
	public class ModelReader {
			
		/* returns new model defs added to map.  If an assembly B depends on another
			assembly A, then that assembly A should appear first in the list */
		public static List<ModelDef> AddToFromFile (Dictionary<string, ModelDef> modelDefMap, string fileName, string requiredNamespace) {
			List<ModelDef> newModelDefList = new List<ModelDef> ();
			
			/* load this assembly into our default context.  Additional dependent 
				assemblies will then find the parent assemblies loaded. */
 			var modelsAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fileName);
			foreach (Type modelType in modelsAssembly.GetTypes()) {

				bool wasCompilerGenerated =
					Attribute.GetCustomAttribute(modelType, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)) != null;

				if (wasCompilerGenerated == false) {
					ModelDef modelDef = new ModelDef {
						ModelName = modelType.Name,
						Namespace = modelType.Namespace,
						FunctionDefList = new List<FunctionDef>(),
						PropertyDefMap = new Dictionary<string, PropertyDef>(),
						FieldDefMap = new Dictionary<string, FieldDef>(), 
						IsJustTable = false,
						IsAbstract = modelType.IsAbstract
					};

					/* okay this is a little confusing because FieldDefMap we are using to store
					 * fields from the stored procedure results that match those in this class.  Fields
					 * in .NET's Reflection are all the class-level declarations that are not
					 * properties.  For our purposes we store them both like Properties.
					 */

					foreach (FieldInfo info in modelType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
						/* ignore private fields */
						if ((info.Attributes & System.Reflection.FieldAttributes.Private) != System.Reflection.FieldAttributes.Private) {
							AddMember(modelDef, info.Name, info.FieldType, requiredNamespace);
						}
					}

					foreach (PropertyInfo info in modelType.GetProperties()) {
						AddMember(modelDef, info.Name, info.PropertyType, requiredNamespace);
					}


					/* if we have multiple models with the same name, only use the first one */
					if (modelDefMap.ContainsKey(modelDef.ModelName) == false) {
						newModelDefList.Add(modelDef);
						modelDefMap[modelDef.ModelName] = modelDef;
					}

					//				var typeInfo = info.GetTypeInfo();

					//				Console.WriteLine(typeInfo.Name);

				}
			}
			return newModelDefList;


		}

		private static void AddMember(ModelDef modelDef, string memberName, Type type, string requiredNamespace) {
			string typeCode = type.Name;
			string typeNamespace = type.Namespace;
			if (type.Namespace != requiredNamespace) {
				string vernacularCode = TypeConvertor.ConvertCLRToVernacular(type.ToString());
				if (vernacularCode != null) {
					typeCode = vernacularCode;
					typeNamespace = null;
				}
			}

			/* TODO: better to use IsClass or IsByRef ? */
			bool isReference = type.IsClass || type.IsInterface;
			if (memberName == "State") {
				Console.WriteLine("State");
			}
			if (typeCode == "string") isReference = false;



			/* TODO: not sure if this should rewrite the type here or not */
			/* TODO: the Interface rewriting should be an option */
			if (type.IsInterface && typeCode.StartsWith("I")) {
				typeCode = typeCode.Substring(1);
			}

			PropertyDef propertyDef = new PropertyDef {
				PropertyName = memberName,
				PropertyTypeCode = typeCode,
				PropertyTypeNamespace = typeNamespace,
				IsInterface = type.IsInterface,
				IsReference = isReference,
				IsEnum = type.IsEnum
			};

			modelDef.PropertyDefMap[memberName] = propertyDef;

		}

	}
}