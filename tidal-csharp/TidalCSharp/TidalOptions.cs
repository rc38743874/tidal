using System;
using Mono.Options;
using System.Collections.Generic;

namespace TidalCSharp {
	public class TidalOptions {

		private OptionSet optionSet;

		public DatabaseTypeEnum DatabaseType { get; set; }
		public string DataAccessFileNameOut { get; set; }
		public string TableDefFileNameOut { get; set; }
		public string TableDefFileNameIn { get; set; }
		public string TableCreateScriptFileName { get; set; }
		public string TableDropScriptFileName { get; set; }
		public string SQLScriptFileNameOut { get; set; }
		public string SQLScriptFileNameIn { get; set; }
		public string ModelsPathOut { get; set; }
		public bool RemoveProcedures { get; set; }
		public bool CreateProcedures { get; set; }
		public string StoredProcDefFileNameOut { get; set; }
		public string StoredProcDefFileNameIn { get; set; }
		public bool PasswordPrompt { get; set; }
		public string Password { get; set; }
		public string ModelDefFileNameIn { get; set; }
		public string ModelDefFileNameOut { get; set; }
		public string ConnectionString { get; set; }

		public string TranslationFileName { get; set; }
		public string WhiteListFileName { get; set; }
		public string GenerateWhiteListFileName { get; set; }
		public bool CleanOracle { get; set; }

		/* TODO: Remove ModelsNamespace if we really didn't use it */
		public string ModelsNamespace { get; set; }

		public List<string> ModelsAssemblyFileNameList = new List<string> ();
		public List<string> IgnoreTableNameList = new List<string>();
		public string ModuleName { get; set; }
		public string ProjectNamespace { get; set; }
		public bool Verbose { get; set; }
		public bool ShouldShowHelp { get; set; }

		public bool ShouldMakeTableDefMap { get; set; }
		public bool ShouldMakeProcedureDefList { get; set; }

		public TidalOptions ()
		{
			this.optionSet = new OptionSet {
				{ "a|modelsdll=", "path/filename of external assembly containing models", a => this.ModelsAssemblyFileNameList.Add(a) },
				{ "b|tablecreatescript=", "path/filename to write out .sql table creation script ", b => this.TableCreateScriptFileName = b },
				{ "B|tabledropscript=", "path/filename to write out .sql table drop script ", B => this.TableDropScriptFileName = B },
				{ "c|createproc", "automatically execute stored procedure script", c => this.CreateProcedures = (c != null)},
				{ "C|conn=", "connection string to database", C => this.ConnectionString = C},
				{ "d|modelout=", "path/filename to save model descriptions .json file", d => this.ModelDefFileNameOut = d},
				{ "D|modelin=", "path/filename from which to read model descriptions", D => this.ModelDefFileNameIn = D},
				{ "h|help", "show this message and exit", h => this.ShouldShowHelp = (h != null) },
				{ "i|ignore=", "ignore these tables (multiple -i allowed)", i => this.IgnoreTableNameList.Add(i) },
				{ "m|makemodels=", "generate and save fresh models into this path.", m => this.ModelsPathOut = m},
				{ "M|namemapfile=", "path/filename containing mappings of names", M => this.TranslationFileName = M},
				{ "n|namespace=", "project namespace for output DataAccess class", n => this.ProjectNamespace = n},
				{ "N|modelsns=", "models namespace for generated and/or imported models", N => this.ModelsNamespace = N},
				{ "o|out=", "path/filename of the generated code DataAccess class.", o => this.DataAccessFileNameOut = o},
				{ "O|oracle", "process tables and column names that have been targeted to Oracle", O => this.CleanOracle = (O != null)},
				{ "p|prompt", "enter password via prompt", p => this.PasswordPrompt = (p!=null) },
				{ "P|password=", "password to connect to database", P => this.Password = P },
				{ "q|sqlout=", "path/filename to save optional copy of stored procedures.", q => this.SQLScriptFileNameOut = q },
				{ "Q|sqlin=", "path/filename from which to read stored procedure creation .sql script", Q => this.SQLScriptFileNameIn = Q},
				{ "r|removeproc", "remove existing Tidal stored procedures for this module in the database", r => this.RemoveProcedures = (r != null)},
				{ "s|storedprocout=", "path/filename to save stored procedure descriptions .json file", s => this.StoredProcDefFileNameOut = s},
				{ "S|storedprocin=", "path/filename from which to read stored procedure descriptions", S => this.StoredProcDefFileNameIn = S},
				{ "t|tableout=", "path/filename to save table schema definitions.", t => this.TableDefFileNameOut = t },
				{ "T|tablein=", "path/filename to read table schema definitions.", T => this.TableDefFileNameIn = T },
				{ "u|modulename=", "module name for tagging stored procedures", u => this.ModuleName = u},
				{ "v|verbose", "verbose messages", v => this.Verbose = (v != null) },
				{ "w|whitelist=", "path/filename of whitelist allowing only some stored procedures", w => this.WhiteListFileName = w },
				{ "W|genwhiteList=", "path/filename of list containing all possibilities to use for a white list", w => this.GenerateWhiteListFileName = w },

			};
		}

		/* returns true if successfully processed options */
		public bool BuildFromArguments(string[] args) {
			List<string> extraCommandList;
			try {
				extraCommandList = this.optionSet.Parse(args);

			}
			catch (OptionException e) {
				Console.WriteLine(e.Message);
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}



			if (this.ShouldShowHelp == true) {
				Console.WriteLine("Usage: tidal-csharp.exe database-type [OPTIONS]");
				Console.WriteLine("Create a DataAccess.cs class in C#, and/or intermediate definition files.");
				Console.WriteLine();


				Console.WriteLine("database-type:");
				Console.WriteLine("\tmssql: Microsoft SQL Server");
				Console.WriteLine("\tmysql: MySQL");

				// output the options
				Console.WriteLine("Options:");
				this.optionSet.WriteOptionDescriptions(Console.Out);

				Console.WriteLine("Examples:");
				Console.WriteLine("tidal-csharp.exe mssql [OPTIONS]");
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


			if (extraCommandList.Count == 0) {
				Console.WriteLine("Database type argument (mssql or mysql) required but not found.");
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
			}
			else if (extraCommandList.Count == 1) {
				string databaseTypeInput = extraCommandList[0].ToLowerInvariant();
				DatabaseTypeEnum type;
				if (Enum.TryParse<DatabaseTypeEnum>(databaseTypeInput, out type) == true) {
					this.DatabaseType = type;
				}
				else {
					Console.WriteLine($"Database type {extraCommandList[0]} not recognized.");
					Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
					return false;
				}
			}
			else {
				Console.WriteLine("Unrecognized information: ");
				foreach (string extraLine in extraCommandList) {
					Console.WriteLine("\t" + extraLine);
				}
				Console.WriteLine("Try 'tidal-csharp.exe --help' for more information.");
				return false;
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

