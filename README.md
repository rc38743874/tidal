# tidal
Tightly-Integrated Data Access Layer
------------------------------------
What is Tidal:
Tool that will generate a Data Access Layer for you from a database.

When to use Tidal:
You have a database that you need to access from programs but have none of the data access programming written yet.

What you must have to use Tidal:
1) Access to the database to create stored procedures
2) Code in a supported language (currently C#, soon VB.NET, Java and Javascript)
3) Use a database that is supported by Tidal (currently MySQL and MSSQL)

What Tidal does:
1) Takes a table structure from a database and creates Create, Read, Update, Delete, and List functions.  You then run the SQL script to add these stored procedures to the database.
2) Wires the stored procedures to functions within your programming language, and connects them to any object models you have in place.
3) Generates a DataAccess class that you can use from the rest of your code to manipulate data stored in the database

Some constraints/conventions:
- Tables should have a primary key, integer autonumber/identity, labeled TableName + "ID", e.g. AuthorID.
- Foreign keys use Key as their name, not ID.  For example, if the Book table references the Author table, Book table would have a column named AuthorKey.  
- Lists are generated based on indices.  So for example, if you have indexed the Book table by AuthorKey, Tidal will create a stored procedure (and a function in the DataAccess class) for Book called ListForAuthor.
- Table names should match object class names in your object model.
- Table names should be singular, not plural.
- Tables should be named with Pascal Case, not all caps, camel case or underscores.  e.g. they should be named InterstateHighwayRoad, not INTERSTATE_HIGHWAY_ROAD or interstateHighwayRoad.


Output structures:

Tidal generates a single file to put into your project called DataAccess.  DataAccess then has subclasses (or further namespaces depending on the language) for each of the tables in the database.  These should match the names in the object model.  These classes are static classes, with static methods for each of the procedures.

For example, in our Book-Author model, your webpage wants to list all the books from an author.  In pseudo-code you would write: 
  var list = DataAccess.Book.ListForAuthor(authorKey);

Normally we pass the connection to the database as well, so in C# this might appear as:
  List<Book> list = DataAccess.Book.ListForAuthor(connection, authorKey);


