using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Data.Common;
using Mono.Options;
using System.Runtime.Serialization.Json;
using System.Data.SqlClient;
using System.Text;

namespace TidalCSharp {

    class MainClass {

        public static void Main(string[] args) {

            try {
                var tidalOptions = new TidalOptions();

                bool proceed = tidalOptions.BuildFromArguments(args);
                if (proceed == true) {
                    Console.WriteLine("looking for password");
                    if (tidalOptions.PasswordPrompt == true) {
                        Console.Write("Password:");
                        tidalOptions.Password = Console.ReadLine();
                    }

                    if (tidalOptions.Verbose) Console.WriteLine("Starting Tidal " + DateTime.Now);

                    /* Although we could hide the connection within IProcessor, it's very convenient to put the
                     * connection in a using block here. */
                    //                IProcessor processor = new MySQLProcessor();
                    Console.WriteLine("creating processor");


					IProcessor processor = null;
					switch (tidalOptions.DatabaseType) {
						case DatabaseTypeEnum.mssql:
							processor = new MicrosoftSQLProcessor();
							break;
						case DatabaseTypeEnum.mysql:
							processor = new MySQLProcessor();
							break;
					}
                    Console.WriteLine("creating connection string");

					DbConnection conn = null;
					string databaseName = null;
					if (tidalOptions.ConnectionString != null) {
						DbConnectionStringBuilder dbcs = new DbConnectionStringBuilder();
						dbcs.ConnectionString = tidalOptions.ConnectionString;
						Console.WriteLine("checking for database name");
						if (dbcs.ContainsKey("database")) {
							databaseName = (string)dbcs["database"];
						}
						else {
							if (dbcs.ContainsKey("Initial Catalog")) {
								databaseName = (string)dbcs["Initial Catalog"];
							}
							else {
								throw new ApplicationException("A database name is required in the connection string.  Please use either Database=VALUE or Initial Catalog=VALUE.");
							}
						}

						Console.WriteLine("making connection");
						conn = processor.GetConnection(tidalOptions.ConnectionString, tidalOptions.Password);
					}


					Console.WriteLine("***" + tidalOptions.TranslationFileName);
					List<TableMapping> tableMappingList = null;
					if (tidalOptions.TranslationFileName != null) {
						tableMappingList = MappingFileReader.ReadFromFile(tidalOptions.TranslationFileName);
					}

					using (conn) {
						if (conn != null) {
							Console.WriteLine("opening connection");
							conn.Open();
						}



						List<TableDef> tableDefList = null;
						TableDefMap tableDefMap = null;

						if (tidalOptions.ShouldMakeTableDefMap == true) {



							Console.WriteLine("getting table extractor");

							ITableExtractor extractor = processor.GetTableExtractor();

							if (tidalOptions.ShouldMakeTableDefMap == true) {
								Console.WriteLine("extracting table data with " + extractor.GetType());
								tableDefMap = extractor.ExtractTableData(tableMappingList, tidalOptions.CleanOracle);

								if (tidalOptions.TableDefFileNameOut != null) {
									Console.WriteLine("writing table def file");
									File.WriteAllText(tidalOptions.TableDefFileNameOut, tableDefMap.ToJSONString());

								}
							}

							tableDefList = tableDefMap.Values.ToList<TableDef>();

							if (tidalOptions.TableCreateScriptFileName != null) {
								Console.WriteLine("writing create table script");
								ITableScriptWriter creationWriter = processor.GetTableScriptWriter();
								string tableCreationScriptText = creationWriter.GetTableCreateScriptText(databaseName, tableDefList);
								File.WriteAllText(tidalOptions.TableCreateScriptFileName, tableCreationScriptText);
							}

							if (tidalOptions.TableDropScriptFileName != null) {
								Console.WriteLine("writing drop table script");
								ITableScriptWriter dropWriter = processor.GetTableScriptWriter();
								string tableCreationScriptText = dropWriter.GetTableDropScriptText(databaseName, tableDefList);
								File.WriteAllText(tidalOptions.TableDropScriptFileName, tableCreationScriptText);
							}

							if (tidalOptions.ModelsPathOut != null) {
								Console.WriteLine("making models");
								ModelCreator.MakeModels(tableDefList, 
											tidalOptions.ModelsNamespace, 
											tidalOptions.ModelsPathOut,
											tableMappingList,
											tidalOptions.CleanOracle);
							}
						}

						Dictionary<string, ModelDef> modelDefMap = null;

						/* TODO: allow create model by only reading database */
						if (tidalOptions.ModelsAssemblyFileNameList.Count > 0) {
							modelDefMap = new Dictionary<string, ModelDef> ();
							foreach (string fileName in tidalOptions.ModelsAssemblyFileNameList) {
								Console.WriteLine ("reading models from assembly " + fileName);
								/* read object model */
								ModelReader.AddToFromFile(modelDefMap, fileName, tidalOptions.ModelsNamespace);
							}
						}

						if (tidalOptions.ModelDefFileNameOut != null) {
							Console.WriteLine ("Writing model defs to file...");
							ModelWriter.WriteDefsToFile (modelDefMap.Values.ToList(), tidalOptions.ModelDefFileNameOut);
							Console.WriteLine ("Model defs written.");
						}

						/* if we will create stored procs */
						if (tidalOptions.ModuleName != null) {
							Console.WriteLine("calcing stored proc script");
							IProcedureCreator procCreator = processor.GetProcedureCreator();
							string storedProcedureSQLText = procCreator.GetStoredProcedureScriptText(tidalOptions.ModuleName, tableDefList, 1, tidalOptions.IgnoreTableNameList);


							// Console.WriteLine(storedProcedureSQLText);
							if (tidalOptions.SQLScriptFileNameOut != null) {

								Console.WriteLine("writing stored proc script file");
								File.WriteAllText(tidalOptions.SQLScriptFileNameOut, storedProcedureSQLText);
							}

							if (tidalOptions.RemoveProcedures == true) {
								Console.WriteLine("removing old procedures");
								/* remove Tidal generated procedures for this module (in case the tables are gone, e.g.) */
								IProcedureRemover procRemover = processor.GetProcedureRemover();
								procRemover.RemoveTidalStoredProcs(databaseName, tidalOptions.ModuleName);
							}

							if (tidalOptions.CreateProcedures == true) {
								Console.WriteLine("executing stored proc script");
								/* execute Tidal sql script */
								IScriptExecutor scriptExecutor = processor.GetScriptExecutor();
								scriptExecutor.ExecuteTidalProcedureScript(storedProcedureSQLText);
							}

						}

						List<ProcedureDef> procedureDefList = null;
						if (tidalOptions.ShouldMakeProcedureDefList == true) {
							Console.WriteLine("absorbing stored proc definitions");

							/* read stored procedures and generate stored procedure defs */
							IProcedureReader procReader = processor.GetProcedureReader();
							procedureDefList = procReader.MakeProcedureDefList(databaseName, tidalOptions.ModuleName, tableDefMap);


							if (tidalOptions.StoredProcDefFileNameOut != null) {
								Console.WriteLine("writing stored proc definition json file");
								var sbSPJson = new StringBuilder("[");
								bool first = true;
								foreach (var procedureDef in procedureDefList) {
									if (first == true) first = false; else sbSPJson.AppendLine(",");
									sbSPJson.Append(procedureDef.ToJSONString());
								}
								sbSPJson.Append("]");
								File.WriteAllText(tidalOptions.StoredProcDefFileNameOut, sbSPJson.ToString());
							}
						}


						if (tidalOptions.DataAccessFileNameOut != null) {
							/* convert the procedures, parameters, and outputs into function calls and arguments */
							List<ModelDef> modelDefList = FunctionCreator.CreateModelDefList(
								tidalOptions.ModelsNamespace, 
								tidalOptions.ModuleName, 
								modelDefMap, 
								procedureDefList, 
								tidalOptions.IgnoreTableNameList,
								tableMappingList,
								tidalOptions.CleanOracle);

							/* combine with stored proc defs to create DataAccess class */
							ClassCreatorBase classCreator = processor.GetClassCreator();
							string allText = classCreator.GetAllText(tidalOptions.ProjectNamespace, modelDefList);

							File.WriteAllText(tidalOptions.DataAccessFileNameOut, allText);
						}

						if (conn != null) conn.Close();

                    }
                }
            }
            catch (SqlException ex) {
                Console.WriteLine("SQL Error: " + ex.ToString());
                Console.WriteLine("Line number: " + ex.LineNumber);
                throw;
            }
        }
    }
}
