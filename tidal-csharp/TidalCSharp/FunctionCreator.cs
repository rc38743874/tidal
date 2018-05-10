
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TidalCSharp {
	public class FunctionCreator {


		public static List<ModelDef> CreateModelDefList (string modelNamespace, string moduleName, Dictionary<string, ModelDef> modelDefMap, List<ProcedureDef> procedureDefList) {
	//		List<ModelDef> modelDefList = new List<ModelDef>();

			foreach (ProcedureDef procedureDef in procedureDefList) {
				string procedureName = procedureDef.ProcedureName;
				int firstUnderscoreIndex = procedureName.IndexOf('_');
				int lastUnderscoreIndex = procedureName.LastIndexOf('_');

				string modelName = procedureName.Substring(firstUnderscoreIndex + 1, lastUnderscoreIndex - firstUnderscoreIndex - 1);
				string functionName = procedureName.Substring(lastUnderscoreIndex + 1);

				ModelDef modelDef = null;
				if (modelDefMap.ContainsKey(modelName)) {
					modelDef = modelDefMap[modelName];
				}
				else {
					Console.WriteLine("Procedure " + procedureName + " coded for table named " + modelName + " did not have a matching model in the models collection.");
					Console.WriteLine("Available models follow:");
					foreach (var modelDefTest in modelDefMap.Keys) {
						Console.WriteLine(modelDefTest);
					}
					/* TODO: process for creating procedures without needing an underlying class */
					/* TODO: remove hack continue */
					continue; 
					// throw new ApplicationException("Procedure " + procedureName + " coded for table named " + modelName + " did not have a matching model in the models collection.");
				}
				
				FunctionDef functionDef = new FunctionDef {
					FunctionName = functionName,
					ProcedureDef = procedureDef,
					ArgumentDefList = new List<ArgumentDef>()
				};

				modelDef.FunctionDefList.Add(functionDef);

				bool isSingleRow =  (functionName.StartsWith("Read"));

				if (procedureDef.OutputsRows == true) {
					functionDef.OutputsList = true;
					modelDef.UsesMakeObjectFunction = true;
					functionDef.ReturnTypeCode = modelDef.ModelName;
					functionDef.ReturnTypeNamespace = modelDef.Namespace;
					if (isSingleRow == true) { 
						functionDef.OutputsList = false;
						functionDef.OutputsObject = true;	
					}
					else {
						functionDef.OutputsList = true;
						functionDef.OutputsObject = false;			
						modelDef.UsesBuildListFunction = true;
					}
				}
				else {
					functionDef.OutputsList = false;
					functionDef.OutputsObject = false;			
				}


				foreach (ParameterDef parameterDef in procedureDef.ParameterDefMap.Values) {
					string typeCode = TypeConvertor.ConvertNullableSQLToCSharp(parameterDef.ParameterDataTypeCode, parameterDef.IsNullable);

					if (parameterDef.IsOutParameter == true) {

						if (functionDef.ReturnTypeCode != null) {
							throw new ApplicationException("Stored procedure " + procedureDef.ProcedureName + " returns row data but also has an out parameter: " + parameterDef.ParameterName);
						}
						functionDef.ReturnTypeCode = typeCode;
						functionDef.ReturnTypeNamespace = null;
					}			
					else {

						/* we assume everything does not allow nulls unless it actually does */
						bool isNullable = false;
						if (typeCode != "string") {
							if (parameterDef.ColumnDef != null) {
								if (parameterDef.ColumnDef.IsNullable == true) {
									isNullable = true;
								}
							}					
						}

						ArgumentDef argumentDef = new ArgumentDef {
							ArgumentName = Char.ToLowerInvariant(parameterDef.ParameterName[1]) + parameterDef.ParameterName.Substring(2),
							ArgumentTypeCode = typeCode,
							ParameterDef = parameterDef,
							IsNullable = isNullable
						};

						parameterDef.ArgumentDef = argumentDef;

						string parameterName = parameterDef.ParameterName;
						PropertyDef propertyDef = modelDef.GetLikelyPropertyDef(parameterDef.ParameterName.Substring(1));

						if (propertyDef != null) {
							argumentDef.PropertyDef = propertyDef;
							parameterDef.PropertyDef = propertyDef;
						}

						functionDef.ArgumentDefList.Add(argumentDef);	
					}
				}

				if (procedureDef.OutputsRows == true) {
					functionDef.OutputPropertyDefList = new List<PropertyDef>();
					foreach (FieldDef fieldDef in procedureDef.FieldDefMap.Values) {
						string fieldName = fieldDef.FieldName;

						PropertyDef propertyDef = modelDef.GetLikelyPropertyDef(fieldName);

						if (propertyDef == null) {
							if (functionDef.UsesResult == false) {
								functionDef.UsesResult = true;
								functionDef.ResultPropertyDefList = new List<ResultPropertyDef>();
							}
							functionDef.ResultPropertyDefList.Add(new ResultPropertyDef { 
								PropertyName = CleanPropertyName(fieldName),
								PropertyTypeCode = fieldDef.DataTypeCode,
								FieldDef = fieldDef});
						}

			/* TODO: we can easily have fields that are not part of the model class (as well as parameters that don't match either).
					These fields would go in a separate special result class. For now we assume they ARE in the model. */



	/* TODO: Commented the type check because it couldn't resolve object v. key value.  May not be necessary really.
	/*

						string propertyTypeCode = TypeConvertor.ConvertNullableSQLToCSharp(fieldDef.DataTypeCode, fieldDef.IsNullable);

						if (propertyDef.PropertyTypeCode != propertyTypeCode) {	
							throw new ApplicationException("PropertyTypeCode for " + modelDef.ModelName + "." + propertyDef.PropertyName + " found " + propertyDef.PropertyTypeCode + " but wanted " + propertyTypeCode + " based on field " + fieldDef.FieldName + " with data type " +  fieldDef.DataTypeCode + " and IsNullable=" + fieldDef.IsNullable);
						}
	*/


						// propertyDef.FieldDefList.Add(fieldDef);
						fieldDef.PropertyDef = propertyDef;
						functionDef.OutputPropertyDefList.Add(propertyDef);

						if (modelDef.FieldDefMap.ContainsKey(fieldDef.FieldName)) {
							FieldDef foundFieldDef = modelDef.FieldDefMap[fieldDef.FieldName];
							if (foundFieldDef.BaseTableName != fieldDef.BaseTableName) {
								throw new ApplicationException("A stored procedure (" + procedureDef.ProcedureName + ") based on model " + modelDef.ModelName +" returned a field pointing to a base table named " + fieldDef.BaseTableName + ", but another procedure (" + foundFieldDef.ProcedureDef.ProcedureName + " had produced a field with the same name based on table " + foundFieldDef.BaseTableName + ".  Consider using two different names for this value in the two procedures.");
							}
						}
						else {
							modelDef.FieldDefMap[fieldDef.FieldName] = fieldDef;
						}


					}

				}


			}

			return modelDefMap.Values.ToList<ModelDef>();
		}

		public static string CleanPropertyName(string fieldName) {
			return new Regex("[^A-Za-z0-9_]").Replace(fieldName, "_");
		}

		/* TODO: Duplicate of MySQLClassCreator, figure out where this belongs */	
		private static string GetModelNamespaceText(string modelNamespace) {
			if (modelNamespace == null) return "";
			return modelNamespace + ".";
		}
		



	}
}