# SqlDump
Simple SQL Server database dumper. Shipped as source-only [NuGet package](https://www.nuget.org/packages/SqlDump.Sources).

## Installing
* Package Manager: `Install-Package SqlDump.Sources`
* .NET command line: `dotnet add package SqlDump.Sources`

## Example
``` csharp
private void GenerateBackupScript(string connectionString, string outputFile)
{
    var dumper = new SqlDumper.Dumper(connectionString);
    dumper.IgnoredTableNames = new[] { "__EFMigrationsHistory", "sysdiagrams" };
    using (var writer = File.CreateText(outputFile))
    {
        dumper.Dump(writer);
    }
}
```
See [result](https://raw.githubusercontent.com/ArtemAvramenko/SqlDump/master/Tests/Data.sql)

## Options
 Property                      | Type        | Default Value | Note
 ----------------------------- | ----------- | ------------- |-----
 **`StatementsInTransaction`** | `int`       | `1000`        | `0` - do not use `BEGIN TRANSACTION`
 **`RowsInStatement`**         | `int`       | `100`         | `1` - separate `INSERT` for each row of data
 **`UseGoStatements`**         | `bool`      | `true`        | `true` - add `GO` after each 'COMMIT' (after each `INSERT`, if transactions are not explicitly used)
 **`IgnoredTableNames`**       | `string[]?` | `null`        | List of tables that will not be included in the dump

## Support for sql_variant type
Support for the sql_variant type is still very limited and requires setting the RowsInStatement to 1.

## Lecacy System.Data.SqlClient
Add SQL_CLIENT_LEGACY to project defines.

## ProgressChanged Event
``` csharp
    dumper.ProgressChanged += (sender, e) =>
    {
        if (e.RowsDumped == 0)
        {
            logWriter.WriteLine($"Dumping {e.SchemaName}.{e.TableName}...");
        }
        if (e.RowsDumped > 0 && (e.IsCompleted || e.RowsDumped % 10000 == 0))
        {
            logWriter.WriteLine($"{e.RowsDumped} rows dumped...");
        }
        if (e.IsCompleted)
        {
            logWriter.WriteLine($"The table {e.SchemaName}.{e.TableName} has been dumped");
        }
    };
```
