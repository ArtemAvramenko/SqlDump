# SqlDump
Simple SQL Server database dumper. Shipped as source-only [NuGet package](https://www.nuget.org/packages/SqlDump.Sources).

# Installing
* Package Manager: `Install-Package SqlDump.Sources`
* .NET command line: `dotnet add package SqlDump.Sources`

# Example
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

# Lecacy System.Data.SqlClient
Add SQL_CLIENT_LEGACY to project defines.

# ProgressChanged event
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
