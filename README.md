# tidal
Tightly-Integrated Data Access Layer
------------------------------------
What is Tidal:
Tool that will generate a Data Access Layer for you from a database.

When to use Tidal:
You have a database that you need to access from programs but have none of the data access programming written yet.

What you must have to use Tidal:
1) Access rights to the database to create stored procedures
2) Code in a supported language (currently C#, soon VB.NET, Java and Javascript)
3) Use a database type that is supported by Tidal (currently MySQL and MSSQL)
4) .NET Core installed

What Tidal does:
1) Takes a table structure from a database and creates Create, Read, Update, Delete, and List functions.  You then run the SQL script to add these stored procedures to the database.
2) Wires the stored procedures to functions within your programming language, and connects them to any object models you have in place.
3) Generates a DataAccess class that you can use from the rest of your code to manipulate data stored in the database

Some constraints/conventions:
- Tables should have a primary key, int or bigint (long) as autonumber/identity, labeled TableName + "ID", e.g. AuthorID.
- Foreign keys use Key as their name, not ID.  For example, if the Book table references the Author table, Book table would have a column named AuthorKey.  
- Lists are generated based on indices.  So for example, if you have indexed the Book table by AuthorKey, Tidal will create a stored procedure (and a function in the DataAccess class) for Book called ListForAuthor.
- Table names should match object class names in your object model.
- Table names should be singular, not plural.
- Tables should be named with Pascal Case, not all caps, camel case or underscores.  e.g. they should be named InterstateHighwayRoad, not INTERSTATE_HIGHWAY_ROAD or interstateHighwayRoad.
- Functionality not covered by the standard Create, Read, etc. should be written as custom stored procedures, named using a <ModuleName>_<TableName>_<FunctionName> convention.  Tidal will create C# functions for these automatically in DataAccess.cs.


Output structures:

Tidal generates a single file to put into your project called DataAccess.  DataAccess then has subclasses (or further namespaces depending on the language) for each of the tables in the database.  These should match the names in the object model.  These classes are static classes, with static methods for each of the procedures.

To allow for tests there is an interface created called IDataAccess.  Test stand-ins shouls use this interface.

To use the DataAccess functions, within your project, instantiate a DataAccess object, then call functions off of that.  For example:
	var DA = new DataAccess();
	DA.Book.ListForAuthor(...);

For example, in our Book-Author model, your webpage wants to list all the books from an author.  The first parameter is the ADO Connection, the second is any current transaction.  So if there was no transaction, the above code should read:
	var bookList = DA.Book.ListForAuthor(conn, null, authorKey);  // bookList will be a List<Book>

Command line:

Here is an example of running from the command-line, with parameters:
dotnet run -- --modelsns=YourSolution.ModelsNamespace \
--modelsdll=/path/to/a/models/dll/ModelsDLL.dll,/path/to/a/second/models/dll/SecondModelsDLL.dll \
--modulename=NameOfModule \
--conn="server=localhost;user=OpenUser;database=MyDB;password=_badpassword" \
--sqlout=/path/to/save/stored/proc/file/NameOfModule-tidal.sql \
--out=/path/to/project/where/DataAccess/lives/DataAccess.cs \
--namespace=YourSolution.NameOfModule \
--tableout=/optional/path/to/export/table/definitions/tables-tidal.json \
--storedprocout=/optional/path/to/export/stored/proc/definitions/stored-proc.json \
--modelout=/optional/path/to/export/model/definitions/models.json \
--namemapfile=/path/for/rewriting/table/and/column/names/to/match/models/NameOfDatabase-tidal-mappings.json \
--createproc \
--removeproc \
--ignore=UnusedTable,AnotherUnusedTable,AnUnusedView \
--dblibrary=mssql

The above call will read the database and create all standard CRUDL procedures, removing any old Tidal-created procedures for this module first.  It will save a .sql file with the stored procedures it has created.  It will generate a DataAccess.cs file in your project.



