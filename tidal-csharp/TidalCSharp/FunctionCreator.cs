
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TidalCSharp {
	public class FunctionCreator {


		public static List<ModelDef> CreateModelDefList (string modelNamespace, 
				string moduleName, 
				Dictionary<string, ModelDef> modelDefMap, 
				List<ProcedureDef> procedureDefList, 
				List<string> ignoreTableNameList,
				List<TableMapping> tableMappingList,
				bool cleanOracle) {
	//		List<ModelDef> modelDefList = new List<ModelDef>();

			foreach (ProcedureDef procedureDef in procedureDefList) {
				string procedureName = procedureDef.ProcedureName;
				int firstUnderscoreIndex = procedureName.IndexOf('_');
				int secondUnderscoreIndex = procedureName.IndexOf('_', firstUnderscoreIndex + 1);
				// int lastUnderscoreIndex = procedureName.LastIndexOf('_');

				// string modelName = procedureName.Substring(firstUnderscoreIndex + 1, lastUnderscoreIndex - firstUnderscoreIndex - 1);
				// string functionName = procedureName.Substring(lastUnderscoreIndex + 1);
				/* This assumes that no tables have underscores in their names */
				/* TODO: probably need an table name underscore removal method elsewhere. */
				string modelName = procedureName.Substring(firstUnderscoreIndex + 1, secondUnderscoreIndex - firstUnderscoreIndex - 1);				
				string functionName = procedureName.Substring(secondUnderscoreIndex + 1);

				/* skip tables we are ignoring */
				if (ignoreTableNameList.Contains(modelName)) continue;

				ModelDef modelDef = null;
				if (modelDefMap.ContainsKey(modelName)) {
					modelDef = modelDefMap[modelName];
				}
				else {
					Console.WriteLine("Adding a virtual model after table named " + modelName + " from procedure " + procedureName + " which did not have a matching model in the models collection.");
					modelDef = new ModelDef {
						ModelName = modelName,
						FieldDefMap = new Dictionary<string, FieldDef>(),
						FunctionDefList = new List<FunctionDef>(),
						Namespace = "",
						PropertyDefMap = new Dictionary<string, PropertyDef>(),
						UsesBuildListFunction = false,
						UsesMakeObjectFunction = false, 
						IsJustTable = true
					};
					modelDefMap[modelName] = modelDef;
				}
				
				FunctionDef functionDef = new FunctionDef {
					FunctionName = functionName,
					ProcedureDef = procedureDef,
					ArgumentDefList = new List<ArgumentDef>()
				};

				modelDef.FunctionDefList.Add(functionDef);

				bool isSingleRow = (functionName.StartsWith("Read"));

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
							// Console.WriteLine($"DEBUG: Found propertyDef of {propertyDef.PropertyName} for parameterName:{parameterDef.ParameterName} in function {functionName}.");
						}
						else {
							/* TODO: display only if warning level = x */
							// Console.WriteLine($"Warning:  Could not find a propertyDef for parameterName:{parameterDef.ParameterName} in function {functionName} of {modelName}.");
						}

						functionDef.ArgumentDefList.Add(argumentDef);	
					}
				}

				if (procedureDef.OutputsRows == true) {
					functionDef.OutputPropertyDefList = new List<PropertyDef>();
					foreach (FieldDef fieldDef in procedureDef.FieldDefMap.Values) {
						string fieldName = fieldDef.FieldName;

						string convertedFieldName = NameMapping.MakeCleanColumnName(tableMappingList, fieldDef.BaseTableName, modelDef.ModelName, fieldName, cleanOracle); 

						PropertyDef propertyDef = modelDef.GetLikelyPropertyDef(convertedFieldName);

						/* We can easily get a field that is several layers back from our root object from joins
						 * in the query.
						 * for example Book.Author.City.State.Country.CountryName, and the query returns CountryName.
						 * We need to work down through the object graph to find the model that matches the table
						 * which that field is using.
						 * It may be that in the initial procedure read when we query the first recordset, with a browse value that 
						 * returns join columns, that we can use that information to traverse the model tree more
						 * directly.
						 */
						List<PropertyDef> propertyDefChain = null;
						if (propertyDef == null) {

							var referencedModelName = NameMapping.MakeCleanTableName(tableMappingList, fieldDef.BaseTableName, cleanOracle);

							// Console.WriteLine($"DEBUG:convertedFieldName:{convertedFieldName}, fieldDef.BaseTableName={fieldDef.BaseTableName}, referencedModelName={referencedModelName}");
							if (modelDefMap.ContainsKey(referencedModelName) == true) {
							
								var referencedModelDef = modelDefMap[referencedModelName];

								var usedModelDefList = new List<ModelDef>() { modelDef };
								propertyDefChain = modelDef.ScanForLikelyPropertyDef(new List<PropertyDef>(), convertedFieldName, referencedModelDef, modelDefMap.Values.ToList<ModelDef>(), usedModelDefList);
								if (propertyDefChain != null) {
									//Console.WriteLine($"DEBUG: Found propertydef chain! fieldName:{convertedFieldName} in procedure {procedureDef.ProcedureName}");
									//propertyDefChain.ForEach(x => {
									//	Console.WriteLine($"{x.PropertyTypeNamespace}.{x.PropertyTypeCode} {x.PropertyName}");
									//});
								}
							}
						}

						/* if we didn't find a propertydef nor a propertydefchain, then it belongs as part of a function-specific Result output */
						if (propertyDef == null && propertyDefChain == null) {
							if (functionDef.UsesResult == false) {
								functionDef.UsesResult = true;
								functionDef.ResultPropertyDefList = new List<ResultPropertyDef>();
							}
							functionDef.ResultPropertyDefList.Add(new ResultPropertyDef { 
								PropertyName = CleanPropertyName(convertedFieldName),
								PropertyTypeCode = fieldDef.DataTypeCode,
								FieldDef = fieldDef});
							Console.WriteLine($"Warning:  Could not find a propertyDef for fieldName \"{convertedFieldName}\" in procedure \"{procedureDef.ProcedureName}\".  " + 
							                  $"The base table name for this field at the SQL level was \"{fieldDef.BaseTableName}\".  " + 
							                  $"The converted model name was computed as \"{NameMapping.MakeCleanTableName(tableMappingList, fieldDef.BaseTableName, cleanOracle)}\".  " + 
							                  $"This field will be included as a property in a result class labeled \"{functionDef.FunctionName}Result\" created just for the output of this function.");
						}
					


	/* TODO: Commented the type check because it couldn't resolve object v. key value.  May not be necessary really.
	/*

						string propertyTypeCode = TypeConvertor.ConvertNullableSQLToCSharp(fieldDef.DataTypeCode, fieldDef.IsNullable);

						if (propertyDef.PropertyTypeCode != propertyTypeCode) {	
							throw new ApplicationException("PropertyTypeCode for " + modelDef.ModelName + "." + propertyDef.PropertyName + " found " + propertyDef.PropertyTypeCode + " but wanted " + propertyTypeCode + " based on field " + fieldDef.FieldName + " with data type " +  fieldDef.DataTypeCode + " and IsNullable=" + fieldDef.IsNullable);
						}
	*/


						// propertyDef.FieldDefList.Add(fieldDef);
						fieldDef.PropertyDef = propertyDef;
						fieldDef.PropertyDefChain = propertyDefChain;
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