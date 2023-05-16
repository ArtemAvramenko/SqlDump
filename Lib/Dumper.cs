// <copyright file="Dumper.cs">
//   SqlDump - Simple SQL Server database dumper
//   (c) 2023 Artem Avramenko. https://github.com/ArtemAvramenko/SqlDump
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
    partial class Dumper : IDisposable
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

        private bool _shouldDisposed;

        public Dumper(SqlConnection connection)
        {
            _connection = connection;
        }

        public Dumper(string connectionString) : this(new SqlConnection(connectionString))
        {
            _shouldDisposed = true;
        }

        public bool UseGoStatements { get; set; } = true;

        public int StatementsInTransaction { get; set; } = 1000;

        public int RowsInStatement { get; set; } = 100;

        public string[] IgnoredTableNames { get; set; }

        public event EventHandler<ProgressData> ProgressChanged;

        public void Dump(TextWriter writer)
        {
            if (_connection.State == ConnectionState.Closed)
            {
                _connection.Open();
            }

            writer.WriteLine("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

            var tables = new List<(string, string)>();
            using (var tablesCommand = _connection.CreateCommand())
            {
                tablesCommand.CommandText = TablesCommand;
                using (var tableReader = tablesCommand.ExecuteReader())
                {
                    while (tableReader.Read())
                    {
                        tables.Add((tableReader.GetString(0), tableReader.GetString(1)));
                    }
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_shouldDisposed)
            {
                if (disposing)
                {
                    _connection.Dispose();
                }
                _shouldDisposed = false;
            }
        }

        private TableInfo GetTableInfo(string schemaName, string tableName)
        {
            var fullTableName = $"{QuoteName(schemaName)}.{QuoteName(tableName)}";
            var table = new TableInfo(fullTableName);

            using (var columnsCommand = _connection.CreateCommand())
            {
                columnsCommand.CommandText = ColumnsCommand;
                columnsCommand.Parameters.Add(SchemaParam, SqlDbType.NVarChar);
                columnsCommand.Parameters.Add(TableParam, SqlDbType.NVarChar);
                columnsCommand.Parameters.Add(SchemaAndTableParam, SqlDbType.NVarChar);
                columnsCommand.Parameters[SchemaParam].Value = schemaName;
                columnsCommand.Parameters[TableParam].Value = tableName;
                columnsCommand.Parameters[SchemaAndTableParam].Value = fullTableName;

                using (var metaReader = columnsCommand.ExecuteReader())
                {
                    while (metaReader.Read())
                    {
                        var columnName = metaReader.GetString(0);
                        var typeName = metaReader.GetString(1);
                        var isSorted = !metaReader.IsDBNull(2);
                        table.Columns.Add(columnName);
                        if (isSorted)
                        {
                            table.SortColumns.Add(columnName);
                        }
                        columnName = QuoteName(columnName);
                        if (Formatters.IsSpecialType(typeName))
                        {
                            columnName = $"{columnName}.ToString() as {columnName}";
                        }
                        table.SelectList.Add(columnName);
                    }
                }
            }
            return table;
        }

        private void DumpTable(TextWriter writer, string schemaName, string tableName)
        {
            var table = GetTableInfo(schemaName, tableName);
            var fullTableName = table.FullTableName;

            int statementRows = 0;

            void SetIdentityInsert(string value)
            {
                writer.WriteLine($"IF OBJECTPROPERTY(OBJECT_ID('{fullTableName}'), 'TableHasIdentity') = 1 SET IDENTITY_INSERT {fullTableName} {value};");
            }

            void DoProgress(bool isCompleted)
            {
                ProgressChanged?.Invoke(this, new ProgressData(schemaName, tableName, statementRows, isCompleted));
            }

            using (var dataCommand = _connection.CreateCommand())
            {
                dataCommand.CommandText = $"SELECT {string.Join(", ", table.SelectList)} FROM {fullTableName}";
                if (table.SortColumns.Count > 0)
                {
                    dataCommand.CommandText += " ORDER BY " + string.Join(", ", table.SortColumns.Select(QuoteName));
                }

                using (var reader = dataCommand.ExecuteReader())
                {
                    var formatters = table.Columns
                        .Select(column =>
                        {
                            var ordinal = reader.GetOrdinal(column);
                            var formatter = Formatters.GetFormatter(reader, ordinal);
                            return new { column, formatter };
                        })
                        .Where(_ => _.formatter != null)
                        .ToList();

                    writer.WriteLine();
                    writer.WriteLine($"-- Table {fullTableName}");
                    var columnList = string.Join(", ", formatters.Select(f => QuoteName(f.column)));

                    while (reader.Read())
                    {
                        DoProgress(false);
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
                        var values = formatters.ConvertAll(x => x.formatter());
                        writer.Write($"  ({string.Join(", ", values)})");
                        statementRows++;
                    }
                }
            }
            DoProgress(true);
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

        private class TableInfo
        {
            public TableInfo(string fullTableName) => FullTableName = fullTableName;

            public string FullTableName { get; }
            public List<string> SortColumns { get; } = new List<string>();
            public List<string> Columns { get; } = new List<string>();
            public List<string> SelectList { get; } = new List<string>();
        }

        public class ProgressData
        {
            public ProgressData(string schemaName, string tableName, int rowsDumped, bool isCompleted)
            {
                SchemaName = schemaName;
                TableName = tableName;
                RowsDumped = rowsDumped;
                IsCompleted = isCompleted;
            }

            public string SchemaName { get; }

            public string TableName { get; }

            public int RowsDumped { get; }

            public bool IsCompleted { get; }
        }
    }
}