// <copyright file="Formatters.cs">
//   SqlDump - Simple SQL Server database dumper
//   (c) 2023 Artem Avramenko. https://github.com/ArtemAvramenko/SqlDump
//   License: MIT
// </copyright>

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;

namespace SqlDumper
{
    internal static class DictionaryExtensions
    {
        public static void Add<TKey, TValue>(this Dictionary<Type, Func<object, TValue>> dictionary, Func<TKey, TValue> f)
        {
            dictionary.Add(typeof(TKey), x => f((TKey)x));
        }
    }

    partial class Dumper
    {
        private static class Formatters
        {
            private static readonly Dictionary<string, Func<object, string>> _sqlTypeFormatters =
                new Dictionary<string, Func<object, string>>()
                {
                { "date", x => string.Format("'{0:yyyy-MM-dd}'", x) },
                { "datetime", x => FormatDateTime(((SqlDateTime)x).Value, precision:"fff")},
                { "decimal", x => x.ToString() },
                { "xml", x => FormatString(((SqlXml)x).Value)}
                };

            private static readonly Dictionary<Type, Func<object, string>> _clrTypeFormatters =
                new Dictionary<Type, Func<object, string>>()
                {
                (string x) => FormatString(x),
                (DateTime x) => FormatDateTime(x),
                (DateTimeOffset x) =>
                    $"'{FormatDateTime(x.DateTime, quote:false)}" +
                    $"{x.ToString("zzz", CultureInfo.InvariantCulture)}'",
                (TimeSpan x) => FormatDateTime(new DateTime(x.Ticks), date: false),
                (bool x) => x ? "1" : "0",
                (byte[] x) => "0x" + BitConverter.ToString(x).Replace("-",""),
                (Guid x) => $"'{x}'"
                };

            private static readonly string[] _ignoredSqlTypes = new[] { "timestamp" };

            private static readonly string[] _specialDataTypes = new[] { "geometry", "geography", "hierarchyid" };

            public static bool IsSpecialType(string type) => _specialDataTypes.Contains(type);

            public static Func<string> GetFormatter(DbDataReader reader, int ordinal)
            {
                if (_ignoredSqlTypes.Contains(reader.GetDataTypeName(ordinal)))
                {
                    return null;
                }
                if (_sqlTypeFormatters.TryGetValue(reader.GetDataTypeName(ordinal), out var sqlFormatter))
                {
                    return () =>
                    {
                        var value = reader.GetProviderSpecificValue(ordinal);
                        if (value is DBNull || value is INullable nullable && nullable.IsNull)
                        {
                            return "NULL";
                        }
                        return sqlFormatter(value);
                    };
                }
                if (!_clrTypeFormatters.TryGetValue(reader.GetFieldType(ordinal), out var clrFormatter))
                {
                    clrFormatter = value =>
                    {
                        if (value is IFormattable formattable)
                        {
                            return formattable.ToString(null, CultureInfo.InvariantCulture);
                        }
                        return value.ToString();
                    };
                }
                return () =>
                {
                    var value = reader.GetValue(ordinal);
                    if (value == null || value is DBNull)
                    {
                        return "NULL";
                    }
                    return clrFormatter(value);
                };
            }


            private static string FormatDateTime(
                DateTime x,
                bool quote = true,
                bool date = true,
                string precision = "fffffff")
            {
                var result = x.ToString((date ? "yyyy-MM-ddT" : "") + "HH:mm:ss", CultureInfo.InvariantCulture);
                var ms = x.ToString(precision, CultureInfo.InvariantCulture);
                if (ms.Any(c => c != '0'))
                {
                    result += "." + ms;
                }
                return quote ? $"'{result}'" : result;
            }

            private static string FormatString(string value)
            {
                var prefix = value.Any(c => c > 127) ? "N" : "";
                return $"{prefix}'{value.Replace("'", "''")}'";
            }
        }
    }
}