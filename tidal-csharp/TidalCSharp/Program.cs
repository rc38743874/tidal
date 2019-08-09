using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.Serialization.Json;
using System.Data.SqlClient;
using System.Text;
using CommandLine;

namespace TidalCSharp {

    class MainClass {

        public static void Main(string[] args) {

            try {
                Parser.Default.ParseArguments<TidalOptions>(args)
                   			.WithParsed<TidalOptions>(tidalOptions => {

					bool proceed = tidalOptions.BuildFromArguments(args);
					if (proceed == true) {
						Shared.Verbose = tidalOptions.Verbose;
						Shared.Info("Starting Tidal " + DateTime.Now);

						Shared.Info("looking for password");
						if (tidalOptions.PasswordPrompt == true) {
							Console.Write("Password:");
							tidalOptions.Password = Console.ReadLine();
						}
						
						/* Although we could hide the connection within IProcessor, it's very convenient to put the
						* connection in a using block here. */
						//                IProcessor processor = new MySQLProcessor();
						Shared.Info("creating processor");


						IProcessor processor = null;
						switch (tidalOptions.DatabaseType) {
							case DatabaseTypeEnum.mssql:
								processor = new MicrosoftSQLProcessor();
								break;
							case DatabaseTypeEnum.mysql:
								processor = new MySQLProcessor();
								break;
						}
						Shared.Info("creating connection string");

						DbConnection conn = null;
						string databaseName = null;
						if (tidalOptions.ConnectionString != null) {
							DbConnectionStringBuilder dbcs = new DbConnectionStringBuilder();
							dbcs.ConnectionString = tidalOptions.ConnectionString;
							Shared.Info("checking for database name");
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

							Shared.Info("making connection");
							conn = processor.GetConnection(tidalOptions.ConnectionString, tidalOptions.Password);
						}


						Shared.Info("***" + tidalOptions.TranslationFileName);
						List<TableMapping> tableMappingList = null;
						if (tidalOptions.TranslationFileName != null) {
							tableMappingList = MappingFileReader.ReadFromFile(tidalOptions.TranslationFileName);
						}

						using (conn) {
							if (conn != null) {
								Shared.Info("opening connection");
								conn.Open();
							}



							List<TableDef> tableDefList = null;
							TableDefMap tableDefMap = null;

							if (tidalOptions.ShouldMakeTableDefMap == true) {



								Shared.Info("getting table extractor");

								ITableExtractor extractor = processor.GetTableExtractor();

								if (tidalOptions.ShouldMakeTableDefMap == true) {
									Shared.Info("extracting table data with " + extractor.GetType());
									tableDefMap = extractor.ExtractTableData(tableMappingList, tidalOptions.CleanOracle);

									if (tidalOptions.TableDefFileNameOut != null) {
										Shared.Info("writing table def file");
										File.WriteAllText(tidalOptions.TableDefFileNameOut, tableDefMap.ToJSONString());

									}
								}

								tableDefList = tableDefMap.Values.ToList<TableDef>();

								if (tidalOptions.TableCreateScriptFileName != null) {
									Shared.Info("writing create table script");
									ITableScriptWriter creationWriter = processor.GetTableScriptWriter();
									string tableCreationScriptText = creationWriter.GetTableCreateScriptText(databaseName, tableDefList);
									File.WriteAllText(tidalOptions.TableCreateScriptFileName, tableCreationScriptText);
								}

								if (tidalOptions.TableDropScriptFileName != null) {
									Shared.Info("writing drop table script");
									ITableScriptWriter dropWriter = processor.GetTableScriptWriter();
									string tableCreationScriptText = dropWriter.GetTableDropScriptText(databaseName, tableDefList);
									File.WriteAllText(tidalOptions.TableDropScriptFileName, tableCreationScriptText);
								}

								if (tidalOptions.ModelsPathOut != null) {
									Shared.Info("making models");
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
									Shared.Info("reading models from assembly " + fileName);
									/* read object model */
									ModelReader.AddToFromFile(modelDefMap, fileName, tidalOptions.ModelsNamespace);
								}
							}

							if (tidalOptions.ModelDefFileNameOut != null) {
								Shared.Info("Writing model defs to file...");
								ModelWriter.WriteDefsToFile (modelDefMap.Values.ToList(), tidalOptions.ModelDefFileNameOut);
								Shared.Info("Model defs written.");
							}

							/* if we will create stored procs */
							if (tidalOptions.ModuleName != null) {
								string storedProcedureSQLText = "";
								if (tidalOptions.SQLScriptFileNameOut != null || tidalOptions.CreateProcedures == true) {
									Shared.Info("calcing stored proc script");
									IProcedureCreator procCreator = processor.GetProcedureCreator();
									storedProcedureSQLText = procCreator.GetStoredProcedureScriptText(tidalOptions.ModuleName, tableDefList, 1, tidalOptions.IgnoreTableNameList);
								}
								// Shared.Info(storedProcedureSQLText);

								if (tidalOptions.SQLScriptFileNameOut != null) { 
									Shared.Info("writing stored proc script file");
									File.WriteAllText(tidalOptions.SQLScriptFileNameOut, storedProcedureSQLText);
								}

								if (tidalOptions.RemoveProcedures == true) {
									Shared.Info("removing old procedures");
									/* remove Tidal generated procedures for this module (in case the tables are gone, e.g.) */
									IProcedureRemover procRemover = processor.GetProcedureRemover();
									procRemover.RemoveTidalStoredProcs(databaseName, tidalOptions.ModuleName);
								}

								if (tidalOptions.CreateProcedures == true) {
									Shared.Info("executing stored proc script");
									/* execute Tidal sql script */
									IScriptExecutor scriptExecutor = processor.GetScriptExecutor();
									scriptExecutor.ExecuteTidalProcedureScript(storedProcedureSQLText);
								}

							}

							List<ProcedureDef> procedureDefList = null;
							if (tidalOptions.ShouldMakeProcedureDefList == true) {
								Shared.Info("absorbing stored proc definitions");

								/* read stored procedures and generate stored procedure defs */
								IProcedureReader procReader = processor.GetProcedureReader();
								procedureDefList = procReader.MakeProcedureDefList(databaseName, tidalOptions.ModuleName, tableDefMap);


								if (tidalOptions.StoredProcDefFileNameOut != null) {
									Shared.Info("writing stored proc definition json file");
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
				});
            }
            catch (SqlException ex) {
                Shared.Info("SQL Error: " + ex.ToString());
                Shared.Info("Line number: " + ex.LineNumber);
                throw;
            }
        }
    }
}
