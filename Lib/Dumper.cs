// <copyright file="Dumper.cs">
//   SqlDump - Simple SQL Server database dumper
//   (c) 2021 Artem Avramenko. https://github.com/ArtemAvramenko/SqlDump
//   License: MIT
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

#if SQL_CLIENT_LEGACY

using System.Data.SqlClient;

#else

using Microsoft.Data.SqlClient;

#endif

namespace SqlDumper
{
    internal class Dumper
    {
        private const string SchemaParam = "schema";

        private const string TableParam = "table";

        private const string SchemaAndTableParam = "schematable";

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
  c.COLUMN_NAME = k.COLUMN_NAME AND
  OBJECTPROPERTY(OBJECT_ID(k.CONSTRAINT_SCHEMA + '.' + QUOTENAME(k.CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
WHERE c.TABLE_SCHEMA = @{SchemaParam} AND c.TABLE_NAME = @{TableParam}
  AND COLUMNPROPERTY(OBJECT_ID(@{SchemaAndTableParam}), c.COLUMN_NAME, 'IsComputed') = 0
ORDER BY ISNULL(k.ORDINAL_POSITION, 30000), 1
";

        private readonly SqlConnection _connection;

        private SqlCommand _columnsCommand;

        public Dumper(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
        }

        public Dumper(SqlConnection connection)
        {
            _connection = connection;
        }

        public bool UseGoStatements { get; set; } = true;

        public int StatementsInTransaction { get; set; } = 1000;

        public int RowsInStatement { get; set; } = 100;

        public string[] IgnoredTableNames { get; set; }

        public void Dump(TextWriter writer)
        {
            if (_connection.State == ConnectionState.Closed)
            {
                _connection.Open();
            }

            writer.WriteLine("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
            _columnsCommand = _connection.CreateCommand();
            _columnsCommand.CommandText = ColumnsCommand;
            _columnsCommand.Parameters.Add(SchemaParam, SqlDbType.NVarChar);
            _columnsCommand.Parameters.Add(TableParam, SqlDbType.NVarChar);
            _columnsCommand.Parameters.Add(SchemaAndTableParam, SqlDbType.NVarChar);

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

            writer.WriteLine();
            writer.WriteLine("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
            InsertGoStatement(writer);
        }

        private void DumpTable(TextWriter writer, string schemaName, string tableName)
        {
            var fullTableName = $"{QuoteName(schemaName)}.{QuoteName(tableName)}";

            var sortColumns = new List<string>();
            _columnsCommand.Parameters[SchemaParam].Value = schemaName;
            _columnsCommand.Parameters[TableParam].Value = tableName;
            _columnsCommand.Parameters[SchemaAndTableParam].Value = fullTableName;
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

            var command = _connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", selectList)} FROM {fullTableName}";
            if (sortColumns.Count > 0)
            {
                command.CommandText += " ORDER BY " + string.Join(", ", sortColumns.Select(QuoteName));
            }

            void SetIdentityInsert(string value)
            {
                writer.WriteLine($"IF OBJECTPROPERTY(OBJECT_ID('{fullTableName}'), 'TableHasIdentity') = 1 SET IDENTITY_INSERT {fullTableName} {value};");
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

            writer.WriteLine();
            writer.WriteLine($"-- Table {fullTableName}");
            var columnList = string.Join(", ", columns.Select(QuoteName));

            int statementRows = 0;
            while (reader.Read())
            {
                if (statementRows % RowsInStatement == 0)
                {
                    if (statementRows == 0)
                    {
                        SetIdentityInsert("ON");
                    }
                    else
                    {
                        writer.WriteLine(";");
                    }
                    if (StatementsInTransaction == 0)
                    {
                        if (statementRows > 0)
                        {
                            InsertGoStatement(writer);
                        }
                    }
                    else if (statementRows / RowsInStatement % StatementsInTransaction == 0)
                    {
                        if (statementRows > 0)
                        {
                            writer.WriteLine("COMMIT;");
                            InsertGoStatement(writer);
                            writer.WriteLine();
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
            if (statementRows > 0)
            {
                writer.WriteLine(";");
                if (StatementsInTransaction > 0)
                {
                    writer.WriteLine("COMMIT;");
                }
                SetIdentityInsert("OFF");
                InsertGoStatement(writer);
            }
        }

        private void InsertGoStatement(TextWriter writer)
        {
            if (UseGoStatements)
            {
                writer.WriteLine("GO");
            }
        }

        private string QuoteName(string name)
        {
            return $"[{name}]";
        }
    }
}