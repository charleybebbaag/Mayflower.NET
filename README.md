# Mayflower.NET

Mayflower is a simple, forward-only, database migrator for SQL Server based on the migrator which Stack Overflow uses.

This is a fork of [bretcope / Mayflower.NET](https://github.com/bretcope/Mayflower.NET). This fork adds the possibility to include a file in a migration (see [this issue](https://github.com/bretcope/Mayflower.NET/issues/11))

## Usage

### Creating Migrations

A migration is just plain T-SQL saved in a .sql file. Individual commands are separated with the `GO` keyword, just like when using [SSMS](https://msdn.microsoft.com/en-us/library/mt238290.aspx). For example:

```sql
CREATE TABLE One
(
  Id int not null identity(1,1),
  Name nvarchar(50) not null,
  
  constraint PK_One primary key clustered (Id)
)
GO

INSERT INTO One (Name) VALUES ('Wystan')
GO
```

> Migrations are run in a transaction by default, which allows them to be rolled back if any command fails. You can disable this transaction for a specific migration by beginning the file with `-- no transaction --`.

We recommend prefixing migration file names with a zero-padded number so that the migrations are listed in chronological order. For example, a directory of migrations might look like:

```
0001 - Add Users table.sql
0002 - Add Posts.sql
0003 - Insert default users.sql
0004 - Add auth columns to Users.sql
...
```

### Including a File in a Migration

It is possible to include one or more files in a migration using the special string `:r`. Example:

```
:r procedures\proc1.sql
:r functions\func2.sql
```

The first`:r` must appear at the begining of the migration, after some spaces or after the special comment `-- no transaction --`.

So it's ok to include files like this:
```


   :r procedures\proc1.sql
```

or like this:
```
-- no transaction --
:r procedures\proc1.sql
-- comment. An empty line or a line with spaces is accepted.
:r functions\func2.sql
```

This approach for importing a file is inspired from the tool `sqlcmd` that supports the command `:r`

Use case:
The procedures / functions / triggers / views are mantained in their own files. When such an object is released, a new migration is created that includes the file that contains the object.
Advantage: when I debug an issue and I want to see the changes for a procedure, it is easy to do so by using the history option of svn/github/hg.

### Running Migrations

#### Command Line

The easiest way to run migrations is with `mayflower.exe` . You obtain it from the Downloads section of [GitHub releases](https://github.com/bretcope/Mayflower.NET/releases) or installable via [nuget](https://www.nuget.org/packages/Mayflower/). It requires .NET Framework 4.5.2 or above.

Typical usage is simply:

```
mayflower --folder="c:\path\to\migrations" --connection="Persist Security Info=False;Integrated Security=true;Initial Catalog=MyDatabase;server=localhost"
```

If you use integrated auth, you can use the `--database` and `--server` arguments instead of supplying a connection string (server defaults to "localhost").

```
mayflower --folder="c:\path\to\migrations" --database=MyLocalDatabase
```

Use `mayflower --help` to show the complete set of options:

```
Usage: mayflower [OPTIONS]+
  Runs all *.sql files in the directory --dir=<directory>.
  The databse connection can be specified using a full connection string
  with --connection, or Mayflower can generate an integrated auth connection
  string using the --database and optional --server arguments.

  -h, --help                 Shows this help message.
  -c, --connection=VALUE     A SQL Server connection string. For integrated
                               auth, you can use --database and --server
                               instead.
  -d, --database=VALUE       Generates an integrated auth connection string
                               for the specified database.
  -s, --server=VALUE         Generates an integrated auth connection string
                               with the specified server (default: localhost).
  -f, --folder=VALUE         The folder containing your .sql migration files
                               (defaults to current working directory).
      --timeout=VALUE        Command timeout duration in seconds (default: 30)
      --preview              Run outstanding migrations, but roll them back.
      --global               Run all outstanding migrations in a single
                               transaction, if possible.
      --table=VALUE          Name of the table used to track migrations
                               (default: Migrations)
      --force                Will rerun modified migrations.
      --version              Print version number.
      --count                Print the number of outstanding migrations.
```

#### Programmatic

If you'd prefer, Mayflower can be called via code. Mayflower.dll is included in the [nuget package](https://www.nuget.org/packages/Mayflower/).

```csharp
var options = new Options
{
    Database = "MyLocalDatabase",
    MigrationsFolder = @"c:\path\to\migrations",
    Output = Console.Out,
};

var result = Migrator.RunOutstandingMigrations(options);
// result.Success indicates success or failure
```

The `Options` class has equivalent properties to most of the command line options.

### Reverting Migrations

Many migration systems have a notion of reversing a migration or "downgrading" in some sense. Mayflower has no such concept. If you want to reverse the effects of one migration, then you write a new migration to do so. Mayflower lives in a forward-only world.

## License

Mayflower is available under the [MIT License](https://github.com/bretcope/Mayflower.NET/blob/master/LICENSE.MIT).

#### Why not just open source the actual Stack Overflow migrator?

Nick Craver put it pretty well [in his blog post](https://nickcraver.com/blog/2016/05/03/stack-overflow-how-we-do-deployment-2016-edition/#database-migrations):

> The database migrator we use is a very simple repo we could open source, but honestly there are dozens out there and the “same migration against n databases” is fairly specific. The others are probably much better and ours is very specific to *only* our needs. The migrator connects to the Sites database, gets the list of databases to run against, and executes all migrations against every one (running multiple databases in parallel). This is done by looking at the passed-in migrations folder and loading it (once) as well as hashing the contents of every file. Each database has a `Migrations` table that keeps track of what has already been run.

Mayflower uses the same basic technique described in the last two sentences, but doesn't have any of the Stack Overflow-specific functionality. Additionally, it was built from the ground-up as a public migrator, rather than trying to adapt our internal codebase, which means it focuses on usability for third parties.

It's true that there are lots of other database migrators out there, but I personally love the extremely simple way we do migrations, so I thought it was worth having a public implementation. And, selfishly, I wanted to be able to use it for my own projects.
