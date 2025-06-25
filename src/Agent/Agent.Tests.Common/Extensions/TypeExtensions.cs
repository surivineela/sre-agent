// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace System
{
    /// <summary>
    /// Extension methods for the Type class
    /// </summary>
    internal static class TypeExtensions
    {
        private static readonly IReadOnlyDictionary<Type, string> KeywordTypes = new Dictionary<Type, string>
        {
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(bool)] = "bool",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",
            [typeof(char)] = "char",
            [typeof(string)] = "string",
            [typeof(object)] = "object",
            [typeof(void)] = "void",
        };

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        public static string FormatName(this Type type)
        {
            var keywordType = KeywordTypes.GetValueOrDefault(type);
            if (keywordType != null)
            {
                return keywordType;
            }
            else if (type.IsArray)
            {
                return type.GetElementType()?.FormatName() + "[]";
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0].FormatName() + "?";
            }
            else if (type.IsGenericType)
            {
                return type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(x => x.FormatName())) + ">";
            }
            else
            {
                return type.Name;
            }
        }
    }
}
