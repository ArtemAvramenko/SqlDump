// <copyright file="Dumper.cs">
//   SqlDump - Simple SQL Server database dumper
//   (c) 2019 Artem Avramenko. https://github.com/ArtemAvramenko/SqlDump
//   License: MIT
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace SqlDumper
{
    internal class Dumper
    {
        private const string SchemaParam = "schema";

        private const string TableParam = "table";

        private readonly string TablesCommand = @"
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE='BASE TABLE'
";

        private readonly string ColumnsCommand = $@"
SELECT c.COLUMN_NAME, c.DATA_TYPE, k.ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k ON
  c.TABLE_SCHEMA = k.TABLE_SCHEMA AND
  c.TABLE_NAME = k.TABLE_NAME AND
  c.COLUMN_NAME = k.COLUMN_NAME
WHERE c.TABLE_SCHEMA = @{SchemaParam} AND c.TABLE_NAME = @{TableParam}
ORDER BY ISNULL(k.ORDINAL_POSITION, 30000), 1
";

        private SqlConnection _connection;

        private SqlCommand _columnsCommand;

        private readonly string _sqlString;

        public Dumper(string sqlString)
        {
            _sqlString = sqlString;
        }

        public bool UseGoStatements { get; set; } = true;

        public int StatementsInTransaction { get; set; } = 1000;

        public int RowsInStatement { get; set; } = 100;

        public string[] IgnoredTableNames { get; set; }

        public void Dump(TextWriter writer)
        {
            using (_connection = new SqlConnection(_sqlString))
            {
                _connection.Open();

                _columnsCommand = _connection.CreateCommand();
                _columnsCommand.CommandText = ColumnsCommand;
                _columnsCommand.Parameters.Add(SchemaParam, SqlDbType.NVarChar);
                _columnsCommand.Parameters.Add(TableParam, SqlDbType.NVarChar);

                var tables = new List<(string, string)>();
                var tablesCommand = _connection.CreateCommand();
                tablesCommand.CommandText = TablesCommand;
                using (var tableReader = tablesCommand.ExecuteReader())
                {
                    while (tableReader.Read())
                    {
                        tables.Add((tableReader.GetString(0), tableReader.GetString(1)));
                    }
                }
                tables = tables
                    .OrderBy(_ => _.Item1, StringComparer.InvariantCultureIgnoreCase)
                    .ThenBy(_ => _.Item2, StringComparer.InvariantCultureIgnoreCase)
                    .ToList();
                foreach (var (schemaName, tableName) in tables)
                {
                    if (IgnoredTableNames == null || !IgnoredTableNames.Contains(tableName))
                    {
                        DumpTable(writer, schemaName, tableName);
                    }
                }
            }
        }

        private void DumpTable(TextWriter writer, string schemaName, string tableName)
        {
            var sortColumns = new List<string>();
            _columnsCommand.Parameters[SchemaParam].Value = schemaName;
            _columnsCommand.Parameters[TableParam].Value = tableName;
            var columns = new List<string>();
            var selectList = new List<string>();
            using (var metaReader = _columnsCommand.ExecuteReader())
            {
                while (metaReader.Read())
                {
                    var columnName = metaReader.GetString(0);
                    var typeName = metaReader.GetString(1);
                    var isSorted = !metaReader.IsDBNull(2);
                    columns.Add(columnName);
                    if (isSorted)
                    {
                        sortColumns.Add(columnName);
                    }
                    columnName = QuoteName(columnName);
                    if (Formatters.IsSpecialType(typeName))
                    {
                        columnName = $"{columnName}.ToString() as {columnName}";
                    }
                    selectList.Add(columnName);
                }
                metaReader.Close();
            }

            var fullTableName = $"{QuoteName(schemaName)}.{QuoteName(tableName)}";
            var command = _connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", selectList)} FROM {fullTableName}";
            if (sortColumns.Count > 0)
            {
                command.CommandText += " ORDER BY " + string.Join(", ", sortColumns.Select(QuoteName));
            }

            var reader = command.ExecuteReader();

            var formatters = new List<Func<string>>();
            columns.RemoveAll(column =>
            {
                var ordinal = reader.GetOrdinal(column);
                var formatter = Formatters.GetFormatter(reader, ordinal);
                if (formatter != null)
                {
                    formatters.Add(formatter);
                }
                return formatter == null;
            });

            writer.WriteLine("-- cr lf");
            writer.WriteLine($"-- Table {fullTableName}");
            var columnList = string.Join(", ", columns.Select(QuoteName));

            void insertGoStatement()
            {
                if (UseGoStatements)
                {
                    writer.WriteLine("GO;");
                }
            }

            int statementRows = 0;
            while (reader.Read())
            {
                if (statementRows % RowsInStatement == 0)
                {
                    if (statementRows > 0)
                    {
                        writer.WriteLine(";");
                    }
                    if (StatementsInTransaction == 0)
                    {
                        if (statementRows > 0)
                        {
                            insertGoStatement();
                        }
                    }
                    else if (statementRows / RowsInStatement % StatementsInTransaction == 0)
                    {
                        if (statementRows > 0)
                        {
                            writer.WriteLine("COMMIT;");
                            insertGoStatement();
                            writer.WriteLine("-- cr lf");
                        }
                        writer.WriteLine("BEGIN TRANSACTION;");
                    }
                    writer.WriteLine($"INSERT INTO {fullTableName} ({columnList}) VALUES");
                }
                else
                {
                    writer.WriteLine(",");
                }
                var values = formatters.ConvertAll(x => x());
                writer.Write($"  ({string.Join(", ", values)})");
                statementRows++;
            }
            reader.Close();
            if (statementRows > 0) {
                writer.WriteLine(";");
                if (StatementsInTransaction > 0)
                {
                    writer.WriteLine("COMMIT;");
                }
                insertGoStatement();
            }
        }

        private string QuoteName(string name)
        {
            return $"[{name}]";
        }
    }
}
