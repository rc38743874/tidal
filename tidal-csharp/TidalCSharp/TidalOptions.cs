using System;
using CommandLine;
using System.Collections.Generic;
 
namespace TidalCSharp {
	public class TidalOptions {


		public DatabaseTypeEnum DatabaseType { get; set; }

        [Option('o', "out", Required = false, HelpText = "path/filename of the generated code DataAccess class")]
		public string DataAccessFileNameOut { get; set; }
		
		[Option('t', "tableout", Required = false, HelpText = "path/filename to save table schema definitions")]
		public string TableDefFileNameOut { get; set; }

		[Option('T', "tablein", Required = false, HelpText = "path/filename to read table schema definitions")]
		public string TableDefFileNameIn { get; set; }

        [Option('b', "tablecreatescript", Required = false, HelpText = "path/filename to write out .sql table creation script")]
		public string TableCreateScriptFileName { get; set; }
	
		[Option('B', "tabledropscript", Required = false, HelpText = "path/filename to write out .sql table drop script")]
		public string TableDropScriptFileName { get; set; }

		[Option('q', "sqlout", Required = false, HelpText = "path/filename to save optional copy of stored procedures")]
		public string SQLScriptFileNameOut { get; set; }

		[Option('Q', "sqlin", Required = false, HelpText = "path/filename from which to read stored procedure creation .sql script")]
		public string SQLScriptFileNameIn { get; set; }

		[Option('m', "makemodels", Required = false, HelpText = "generate and save fresh models into this path")]
		public string ModelsPathOut { get; set; }

		[Option('r', "removeproc", Required = false, HelpText = "remove existing Tidal stored procedures for this module in the database")]
		public bool RemoveProcedures { get; set; }

		[Option('c', "createproc", Required = false, HelpText = "automatically execute stored procedure script")]
		public bool CreateProcedures { get; set; }
				
		[Option('s', "storedprocout", Required = false, HelpText = "path/filename to save stored procedure descriptions .json file")]
		public string StoredProcDefFileNameOut { get; set; }

		[Option('S', "storedprocin", Required = false, HelpText = "path/filename from which to read stored procedure descriptions")]
		public string StoredProcDefFileNameIn { get; set; }

		[Option('p', "prompt", Required = false, HelpText = "enter password via prompt")]
		public bool PasswordPrompt { get; set; }
		
		[Option('P', "password", Required = false, HelpText = "password to connect to database")]
		public string Password { get; set; }

		[Option('D', "modelin", Required = false, HelpText = "path/filename from which to read model descriptions")]
		public string ModelDefFileNameIn { get; set; }
		
		[Option('d', "modelout", Required = false, HelpText = "path/filename to save model descriptions .json file")]
		public string ModelDefFileNameOut { get; set; }
		
	    [Option('C', "conn", Required = false, HelpText = "connection string to database")]
		public string ConnectionString { get; set; }

		[Option('M', "namemapfile", Required = false, HelpText = "path/filename containing mappings of names")]
		public string TranslationFileName { get; set; }

		[Option('w', "whitelist", Required = false, HelpText = "path/filename of whitelist allowing only some stored procedures")]
		public string WhiteListFileName { get; set; }
						
		[Option('W', "genwhiteList", Required = false, HelpText = "path/filename of list containing all possibilities to use for a white list")]
		public string GenerateWhiteListFileName { get; set; }

		[Option('O', "oracle", Required = false, HelpText = "process tables and column names that have been targeted to Oracle")]
		public bool CleanOracle { get; set; }

		/* TODO: Remove ModelsNamespace if we really didn't use it */
		[Option('N', "modelsns", Required = false, HelpText = "models namespace for generated and/or imported models")]
		public string ModelsNamespace { get; set; }

		
		[Option('a', "modelsdll", Required = false, HelpText = "path/filename of external assembly containing models")]
		// public List<string> ModelsAssemblyFileNameList { get; set; }
		public string ModelsAssemblyFileNameString { set => this.ModelsAssemblyFileNameList = new List<string>(value.Split(',')); }
		public List<string> ModelsAssemblyFileNameList { get; set; }
			
		// i => this.IgnoreTableNameList.Add(i) 
		[Option('i', "ignore", Required = false, HelpText = "ignore these tables (multiple -i allowed)")]
		public string IgnoreTableNameString { set => this.IgnoreTableNameList.AddRange(value.Split(',')); }
		public List<string> IgnoreTableNameList = new List<string>();		
		
		[Option('u', "modulename", Required = false, HelpText = "module name for tagging stored procedures")]
		public string ModuleName { get; set; }
		
		[Option('n', "namespace", Required = false, HelpText = "project namespace for output DataAccess class")]
		public string ProjectNamespace { get; set; }
		
		[Option('v', "verbose", Required = false, HelpText = "verbose messages")]
		public bool Verbose { get; set; }
		
		[Option('h', "help", Required = false, HelpText = "show this message and exit")]
		public bool ShouldShowHelp { get; set; }


		[Option('l', "dblibrary", Required = false, HelpText = "database library (mssql or mysql)")]
		public string DatabaseLibrary {get; set; }

		public bool ShouldMakeTableDefMap { get; set; }
		public bool ShouldMakeProcedureDefList { get; set; }


		/* returns true if successfully processed options */
		public bool BuildFromArguments(string[] args) {
			// Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
			
			if (this.ShouldShowHelp == true) {
				// Console.WriteLine("Usage: tidal-csharp.exe database-type [OPTIONS]");
				Console.WriteLine("Usage: tidal-csharp.exe [OPTIONS]");
				Console.WriteLine("Create a DataAccess.cs class in C#, and/or intermediate definition files.");
				Console.WriteLine();


				// Console.WriteLine("database-type:");
				// Console.WriteLine("\tmssql: Microsoft SQL Server");
				// Console.WriteLine("\tmysql: MySQL");

				// output the options
				// Console.WriteLine("Options:");
				/* TODO: write options */
				// this.WriteOptionDescriptions(Console.Out);

				// Console.WriteLine("Examples:");
				// Console.WriteLine("tidal-csharp.exe [OPTIONS]");
				return false;
			}

			this.ShouldMakeProcedureDefList = false;
			List<string> tableDefRequirerList = new List<string>();
			bool wasOkay = true;
			// bool tableDefMapRequired = false;
			// bool moduleNameRequired = false;

			// bool hasCorrectDatabaseInputs = false;
			/* first step in the chain is reading table defs from either a file or database
			 * 		if we set TableDefFileNameIn, then we are reading from a file
			 * 		if we set TableDefFileNameOut, then we are reading from a db, and exporting to that file
			 * 		if we set neither, then we will read from the database, but not write out to a file
			 * 		if we set both, it's an error 
			 
			 */
			if (this.TableDefFileNameOut != null && this.TableDefFileNameIn != null) {
				/* TODO: if we set both, perhaps it should check the two against each other? */
				wasOkay = false;
				Console.WriteLine("Cannot use a table schema definition input file and a table schema definition output file simultaneously.  Please use either -t or -T exclusively.");
			}

			/* this side track provides a table drop script for convenience.
			 * 	this is switched on if there is a TableDropScriptFileName provided.
			 * 	it requires a tableDefMap, either from file or DB
			 */
			if (this.TableDropScriptFileName != null) {
				tableDefRequirerList.Add("-B");
			}

			/* this side track provides a table create script for convenience.
			 * 	this is switched on if there is a TableCreateScriptFileName provided.
			 * 	it requires a tableDefMap, either from file or DB
			 */
			if (this.TableCreateScriptFileName != null) {
				tableDefRequirerList.Add("-b");
			}


			/* Write .cs model files if desired.
			 * this is switched by ModelsPathOut being provided
			 * it requires a tableDefMap, either from file or DB
			 */
			if (this.ModelsPathOut != null) {
				tableDefRequirerList.Add("-m");
				if (this.ModelsNamespace == null) {
					wasOkay = false;
					Console.WriteLine("Writing models out with the -m option requires a models namespace to be specified using -N.");
				}
			}

			if (this.SQLScriptFileNameOut != null) {
				if (this.SQLScriptFileNameIn != null) {
					wasOkay = false;
					Console.WriteLine("Cannot use a .sql script input file and a .sql script output file simultaneously.  Please use either -q or -Q exclusively.");
				}

				if (this.ModuleName == null) {
					wasOkay = false;
					Console.WriteLine("Option -q to write out a stored procedure script requires a module name specified by -u.");
				}
				tableDefRequirerList.Add("-q");

			}

			if (this.CreateProcedures == true) {
				if (this.SQLScriptFileNameIn == null) {
					tableDefRequirerList.Add("-Q");
				}
				if (this.ConnectionString == null) {
					wasOkay = false;
					Console.WriteLine("Automatically creating procedures with -c requires a database connection specified by -C.");					
				}
			}

			if (this.StoredProcDefFileNameOut != null) {
				tableDefRequirerList.Add("-s");
				if (this.ModuleName == null) {
					wasOkay = false;
					Console.WriteLine("Option -s to write out stored procedure parameter definitions .json file requires a module name specified by -u.");
				}
			}


			if (tableDefRequirerList.Count == 0) {
				this.ShouldMakeTableDefMap = false;
			}
			else {
				this.ShouldMakeTableDefMap = true;
				if (this.ConnectionString == null
				    && this.TableDefFileNameIn == null) {
					wasOkay = false;

					string paramsNeeding = "";
					if (tableDefRequirerList.Count == 1) {
						paramsNeeding = "Parameter " + tableDefRequirerList[0] + " requires";
					}
					else {
						paramsNeeding = "Parameters " + String.Join(", ", tableDefRequirerList.ToArray()) + " require";
					}

					/* TODO: may want a list of parameters that are causing a tableDefMapRequired */
					Console.WriteLine(paramsNeeding + " reading table schema definitions either directly from the database specified using -C, or a static .json file using -T.");
				}
			}

			if (this.RemoveProcedures == true) {
				if (this.ConnectionString == null) {
					wasOkay = false;

					/* TODO: may want a list of parameters that are causing a tableDefMapRequired */
					Console.WriteLine("Removing procedures with -r requires a database connection specified by -C.");					
				}
			}

			if (this.DataAccessFileNameOut != null) {
				this.ShouldMakeProcedureDefList = true;
				if (this.ProjectNamespace  ==  null) {
					wasOkay = false;
					Console.WriteLine("Option -o to output a DataAccess file requires a project namespace to be specified with -n.");
				}

			}


			if (DatabaseLibrary == null) {
				Console.WriteLine("Database type argument (mssql or mysql) required but not found.");
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}
			else {
				string databaseTypeInput = this.DatabaseLibrary.ToLowerInvariant();
				DatabaseTypeEnum type;
				if (Enum.TryParse<DatabaseTypeEnum>(databaseTypeInput, out type) == true) {
					this.DatabaseType = type;
				}
				else {
					Console.WriteLine($"Database library type {this.DatabaseLibrary} not recognized.");
					Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
					return false;
				}
			}


			if (this.StoredProcDefFileNameOut != null && this.StoredProcDefFileNameIn != null) {
				wasOkay = false;
				Console.WriteLine("Cannot use a stored procedure definition input file and a stored procedure definition output file simultaneously.  Please use either -s or -S exclusively.");
			}

			if (this.ModelDefFileNameIn != null && this.ModelDefFileNameOut != null) {
				wasOkay = false;
				Console.WriteLine("Cannot use a model descriptions input file and a model descriptions output file simultaneously.  Please use either -d or -D exclusively.");
			}

			if (this.PasswordPrompt == true && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine("Option -p or --prompt to prompt for a password requires a connection string (-C or --conn).");
			}

			if (this.Password != null && this.ConnectionString == null) {
				wasOkay = false;
				Console.WriteLine("Option -P or --password to supply a password requires a connection string (-C or --conn).");
			}

			if (this.ConnectionString != null && this.Password == null && this.PasswordPrompt == false) {
				/* TODO: what if the password is part of the connection string? */
				// wasOkay = false;
				// Console.WriteLine("A connection specified by -C requires either a password with -P or a password prompt with -p.");
			}


			return wasOkay;

		}
	}
}

