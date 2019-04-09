# SqlDump
Simple SQL Server database dumper. Shipped as source-only NuGet package.

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
See [result](Tests/Data.sql)