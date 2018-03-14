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

					IProcessor processor;
					switch (tidalOptions.DatabaseType) {
						case DatabaseTypeEnum.mssql:
							processor = new MicrosoftSQLProcessor();
							break;
						case DatabaseTypeEnum.mysql:
							processor = new MySQLProcessor();
							break;
						default:
							throw new ApplicationException("Database type was not specified: " + tidalOptions.DatabaseType);
					}
                    Console.WriteLine("creating connection string");

                    DbConnectionStringBuilder dbcs = new DbConnectionStringBuilder();
                    dbcs.ConnectionString = tidalOptions.ConnectionString;
                    Console.WriteLine("checking for database name");
					string databaseName;
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
                    using (DbConnection conn = processor.GetConnection(tidalOptions.ConnectionString, tidalOptions.Password)) {
                        Console.WriteLine("opening connection");
                        conn.Open();

                        Console.WriteLine("getting table extractor");
                        /* TODO: selectively do/skip these based on options */
                        ITableExtractor extractor = processor.GetTableExtractor();

                        Console.WriteLine("extracting table data with " + extractor.GetType());
                        TableDefMap tableDefMap = extractor.ExtractTableData();

                        if (tidalOptions.TableDefFileNameOut != null) {
                            Console.WriteLine("writing table def file");
                            File.WriteAllText(tidalOptions.TableDefFileNameOut, tableDefMap.ToJSONString());

                        }


                        if (tidalOptions.TableCreateScriptFileName != null) {
                            Console.WriteLine("writing create table script");
                            ITableScriptWriter creationWriter = processor.GetTableScriptWriter();
                            string tableCreationScriptText = creationWriter.GetTableCreateScriptText(databaseName, tableDefMap.Values.ToList<TableDef>());
                            File.WriteAllText(tidalOptions.TableCreateScriptFileName, tableCreationScriptText);
                        }

                        if (tidalOptions.TableDropScriptFileName != null) {
                            Console.WriteLine("writing drop table script");
                            ITableScriptWriter dropWriter = processor.GetTableScriptWriter();
                            string tableCreationScriptText = dropWriter.GetTableDropScriptText(databaseName, tableDefMap.Values.ToList<TableDef>());
                            File.WriteAllText(tidalOptions.TableDropScriptFileName, tableCreationScriptText);
                        }

                        List<TableDef> tableDefList = tableDefMap.Values.ToList<TableDef>();

                        if (tidalOptions.ModelsPathOut != null) {
                            Console.WriteLine("making models");
                            ModelCreator.MakeModels(tableDefList, tidalOptions.ModelsNamespace, tidalOptions.ModelsPathOut);
                        }



                        Console.WriteLine("reading models");
                        /* read object model */
                        Dictionary<string, ModelDef> modelDefMap = ModelReader.ReadFromFile(tidalOptions.ModelsAssemblyFileName, tidalOptions.ModelsNamespace);

                        Console.WriteLine("calcing stored proc script");
                        IProcedureCreator procCreator = processor.GetProcedureCreator();
                        string text = procCreator.GetStoredProcedureScriptText(tidalOptions.ModuleName, tableDefList, 1);

                        // Console.WriteLine(text);
                        if (tidalOptions.SQLScriptFileNameOut != null) {

                            Console.WriteLine("writing stored proc script file");
                            File.WriteAllText(tidalOptions.SQLScriptFileNameOut, text);
                        }


                        Console.WriteLine("removing old procedures");
                        /* remove Tidal generated procedures for this module (in case the tables are gone, e.g.) */
                        IProcedureRemover procRemover = processor.GetProcedureRemover();
                        procRemover.RemoveTidalStoredProcs(databaseName, tidalOptions.ModuleName);


                        Console.WriteLine("executing stored proc script");
                        /* execute Tidal sql script */
                        IScriptExecutor scriptExecutor = processor.GetScriptExecutor();
                        scriptExecutor.ExecuteTidalProcedureScript(text);

                        Console.WriteLine("absorbing stored proc definitions");
                        /* read stored procedures and generate stored procedure defs */
                        IProcedureReader procReader = processor.GetProcedureReader();
                        List<ProcedureDef> procedureDefList = procReader.MakeProcedureDefList(databaseName, tidalOptions.ModuleName, tableDefMap);

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

                        /* convert the procedures, parameters, and outputs into function calls and arguments */
                        List<ModelDef> modelDefList = FunctionCreator.CreateModelDefList(tidalOptions.ModelsNamespace, tidalOptions.ModuleName, modelDefMap, procedureDefList);

                        /* combine with stored proc defs to create DataAccess class */
                        IClassCreator classCreator = processor.GetClassCreator();
                        string classText = classCreator.GetDataAccessClassText(tidalOptions.ProjectNamespace, tidalOptions.ModelsNamespace, modelDefList);

                        File.WriteAllText(tidalOptions.DataAccessFileNameOut, classText);

                        conn.Close();
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
