namespace TabularForge.DAXParser.Semantics;

public sealed class DaxFunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<DaxFunctionParameter> Parameters { get; set; } = new();

    public string GetSignatureText()
    {
        var parms = string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type}"));
        return $"{Name}({parms}) -> {ReturnType}";
    }
}

public sealed class DaxFunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string Description { get; set; } = string.Empty;
}

public static class DaxFunctionCatalog
{
    private static readonly Dictionary<string, DaxFunctionSignature> Functions
        = new(StringComparer.OrdinalIgnoreCase);

    static DaxFunctionCatalog()
    {
        // ============================================================
        // AGGREGATION FUNCTIONS
        // ============================================================
        Add("SUM", "Adds all numbers in a column", "Decimal", ("Column", "Column", false));
        Add("SUMX", "Returns the sum of an expression evaluated for each row", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("AVERAGE", "Returns the average of all numbers in a column", "Decimal", ("Column", "Column", false));
        Add("AVERAGEX", "Calculates the average of an expression over a table", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("AVERAGEA", "Returns the average of values in a column, treating text and FALSE as 0, TRUE as 1", "Decimal",
            ("Column", "Column", false));
        Add("MIN", "Returns the minimum value in a column", "Variant", ("Column", "Column", false));
        Add("MINX", "Returns the minimum of an expression evaluated for each row", "Variant",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("MINA", "Returns the minimum value in a column, treating TRUE/FALSE as 1/0", "Variant",
            ("Column", "Column", false));
        Add("MAX", "Returns the maximum value in a column", "Variant", ("Column", "Column", false));
        Add("MAXX", "Returns the maximum of an expression evaluated for each row", "Variant",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("MAXA", "Returns the maximum value in a column, treating TRUE/FALSE as 1/0", "Variant",
            ("Column", "Column", false));
        Add("COUNT", "Counts the number of cells in a column that contain numbers", "Integer", ("Column", "Column", false));
        Add("COUNTA", "Counts the number of cells in a column that are not empty", "Integer", ("Column", "Column", false));
        Add("COUNTAX", "Counts non-blank results of an expression over a table", "Integer",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("COUNTX", "Counts the number of rows that contain a non-blank value or an expression that evaluates to non-blank", "Integer",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("COUNTBLANK", "Counts the number of blank cells in a column", "Integer", ("Column", "Column", false));
        Add("COUNTROWS", "Counts the number of rows in a table", "Integer", ("Table", "Table", false));
        Add("DISTINCTCOUNT", "Counts the number of distinct values in a column", "Integer", ("Column", "Column", false));
        Add("DISTINCTCOUNTNOBLANK", "Counts distinct non-blank values in a column", "Integer", ("Column", "Column", false));
        Add("APPROXIMATEDISTINCTCOUNT", "Returns an approximate count of distinct values using HyperLogLog", "Integer",
            ("Column", "Column", false));
        Add("PRODUCT", "Returns the product of numbers in a column", "Decimal", ("Column", "Column", false));
        Add("PRODUCTX", "Returns the product of an expression evaluated for each row", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("RANKX", "Returns the rank of an expression in a table", "Integer",
            ("Table", "Table", false), ("Expression", "Scalar", false), ("Value", "Scalar", true),
            ("Order", "Enum", true), ("Ties", "Enum", true));

        // ============================================================
        // FILTER FUNCTIONS
        // ============================================================
        Add("CALCULATE", "Evaluates an expression in a modified filter context", "Variant",
            ("Expression", "Scalar", false), ("Filter1", "Boolean/Table", true),
            ("Filter2", "Boolean/Table", true), ("Filter3", "Boolean/Table", true));
        Add("CALCULATETABLE", "Evaluates a table expression in a modified filter context", "Table",
            ("Expression", "Table", false), ("Filter1", "Boolean/Table", true),
            ("Filter2", "Boolean/Table", true));
        Add("FILTER", "Returns a table filtered by an expression", "Table",
            ("Table", "Table", false), ("FilterExpression", "Boolean", false));
        Add("ALL", "Returns all rows, ignoring filters", "Table", ("TableOrColumn", "Table/Column", false));
        Add("ALLEXCEPT", "Returns all rows except the specified columns", "Table",
            ("Table", "Table", false), ("Column1", "Column", false), ("Column2", "Column", true));
        Add("ALLSELECTED", "Returns all rows in the current query context", "Table",
            ("TableOrColumn", "Table/Column", true));
        Add("ALLCROSSFILTERED", "Returns all rows from a table ignoring any cross-filters", "Table",
            ("Table", "Table", false));
        Add("ALLNOBLANKROW", "Returns all rows except the blank row, ignoring filters", "Table",
            ("TableOrColumn", "Table/Column", false));
        Add("VALUES", "Returns a one-column table of distinct values", "Table", ("Column", "Column", false));
        Add("DISTINCT", "Returns a one-column table of distinct values", "Table", ("Column", "Column", false));
        Add("KEEPFILTERS", "Modifies how filters are applied", "Table", ("Expression", "Boolean/Table", false));
        Add("REMOVEFILTERS", "Clears filters from specified tables or columns", "Void",
            ("TableOrColumn", "Table/Column", true));
        Add("SELECTEDVALUE", "Returns a value when only one distinct value", "Variant",
            ("Column", "Column", false), ("AlternateResult", "Scalar", true));
        Add("HASONEVALUE", "Returns TRUE if only one value in the column", "Boolean", ("Column", "Column", false));
        Add("USERELATIONSHIP", "Specifies a relationship to use in CALCULATE", "Void",
            ("Column1", "Column", false), ("Column2", "Column", false));
        Add("INDEX", "Returns a row at an absolute position in a table", "Table",
            ("Position", "Integer", false), ("Table", "Table", false), ("OrderBy", "OrderBy", true),
            ("Blanks", "Enum", true), ("PartitionBy", "PartitionBy", true));
        Add("OFFSET", "Returns a row at a relative offset from the current row", "Table",
            ("Delta", "Integer", false), ("Table", "Table", false), ("OrderBy", "OrderBy", true),
            ("Blanks", "Enum", true), ("PartitionBy", "PartitionBy", true));
        Add("ORDERBY", "Defines the sort order for window functions", "OrderBy",
            ("Column", "Column", false), ("Order", "Enum", true));
        Add("PARTITIONBY", "Defines the partition for window functions", "PartitionBy",
            ("Column", "Column", false));
        Add("MATCHBY", "Defines the columns to match for window functions", "MatchBy",
            ("Column", "Column", false));
        Add("WINDOW", "Returns multiple rows from a table within a specified window", "Table",
            ("From", "Integer", false), ("FromType", "Enum", false), ("To", "Integer", false),
            ("ToType", "Enum", false), ("Table", "Table", false), ("OrderBy", "OrderBy", true),
            ("Blanks", "Enum", true), ("PartitionBy", "PartitionBy", true));

        // ============================================================
        // TABLE FUNCTIONS
        // ============================================================
        Add("ADDCOLUMNS", "Adds calculated columns to a table", "Table",
            ("Table", "Table", false), ("Name", "String", false), ("Expression", "Scalar", false));
        Add("SELECTCOLUMNS", "Returns a table with selected columns", "Table",
            ("Table", "Table", false), ("Name", "String", false), ("Expression", "Scalar", false));
        Add("SUMMARIZE", "Returns a summary table", "Table",
            ("Table", "Table", false), ("GroupBy", "Column", false));
        Add("SUMMARIZECOLUMNS", "Returns a summary table with grouping", "Table",
            ("GroupBy", "Column", false), ("FilterTable", "Table", true));
        Add("CROSSJOIN", "Returns the Cartesian product of tables", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("UNION", "Creates a union of two tables", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("INTERSECT", "Returns the intersection of two tables", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("EXCEPT", "Returns rows in the first table not in the second", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("TOPN", "Returns the top N rows", "Table",
            ("N", "Integer", false), ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("GENERATEALL", "Returns a table with combinations of rows", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("GENERATE", "Returns a table from Cartesian product", "Table",
            ("Table1", "Table", false), ("Table2", "Table", false));
        Add("GENERATESERIES", "Returns a single column table of values", "Table",
            ("StartValue", "Decimal", false), ("EndValue", "Decimal", false), ("Increment", "Decimal", true));
        Add("ROW", "Returns a single-row table", "Table",
            ("Name", "String", false), ("Expression", "Scalar", false));
        Add("DATATABLE", "Creates a table with specified data", "Table",
            ("Name", "String", false), ("Type", "Type", false), ("Data", "Variant", false));
        Add("TREATAS", "Applies a filter from one table to another", "Table",
            ("Expression", "Table", false), ("Column", "Column", false));
        Add("NATURALINNERJOIN", "Joins two tables using common columns", "Table",
            ("Left", "Table", false), ("Right", "Table", false));
        Add("NATURALLEFTOUTERJOIN", "Left outer joins two tables", "Table",
            ("Left", "Table", false), ("Right", "Table", false));
        Add("ADDMISSINGITEMS", "Adds rows with missing item combinations to a table", "Table",
            ("ShowAll_ColumnName", "Column", false), ("Table", "Table", false),
            ("GroupBy_ColumnName", "Column", true));
        Add("CURRENTGROUP", "Returns the current group from GROUPBY", "Table");
        Add("DETAILROWS", "Returns the detail rows for a measure", "Table",
            ("Measure", "Measure", false));
        Add("FILTERS", "Returns a table of filter values for a column", "Table",
            ("Column", "Column", false));
        Add("GROUPBY", "Groups a table by columns with aggregations", "Table",
            ("Table", "Table", false), ("GroupBy_ColumnName", "Column", false),
            ("Name", "String", true), ("Expression", "Scalar", true));
        Add("IGNORE", "Marks a measure to be ignored in SUMMARIZECOLUMNS", "Table",
            ("Measure", "Measure", false));
        Add("NONVISUAL", "Marks filters as non-visual in SUMMARIZECOLUMNS", "Table",
            ("Expression", "Table", false));
        Add("ROLLUP", "Identifies columns for subtotals in SUMMARIZECOLUMNS", "Table",
            ("Column", "Column", false));
        Add("ROLLUPADDISSUBTOTAL", "Adds a column to identify subtotal rows", "Table",
            ("Column", "Column", false), ("Name", "String", false));
        Add("ROLLUPGROUP", "Groups columns for ROLLUP", "Table",
            ("Column", "Column", false));
        Add("ROLLUPISSUBTOTAL", "Checks if a row is a subtotal row", "Boolean",
            ("Column", "Column", false));
        Add("SUBSTITUTEWITHINDEX", "Returns a table with an index column replacing another column", "Table",
            ("Table", "Table", false), ("Name", "String", false), ("SemiJoinIndexColumn", "Table", false),
            ("Expression", "Scalar", false));
        Add("TOPNPERLEVEL", "Returns the top N rows per grouping level", "Table",
            ("N", "Integer", false), ("Table", "Table", false), ("GroupBy_ColumnName", "Column", false),
            ("OrderBy_Expression", "Scalar", false), ("Order", "Enum", true));
        Add("TOPNSKIP", "Returns the top N rows after skipping some rows", "Table",
            ("N_Value", "Integer", false), ("Skip", "Integer", false), ("Table", "Table", false),
            ("OrderBy_Expression", "Scalar", false), ("Order", "Enum", true));
        Add("SAMPLE", "Returns a sample of N rows from a table", "Table",
            ("N", "Integer", false), ("Table", "Table", false), ("OrderBy", "Scalar", true));

        // ============================================================
        // RELATIONSHIP FUNCTIONS
        // ============================================================
        Add("RELATED", "Returns a related value from another table", "Variant", ("Column", "Column", false));
        Add("RELATEDTABLE", "Returns the related table filtered by context", "Table", ("Table", "Table", false));
        Add("LOOKUPVALUE", "Returns a value from a table based on search", "Variant",
            ("ResultColumn", "Column", false), ("SearchColumn", "Column", false), ("SearchValue", "Scalar", false));
        Add("CROSSFILTER", "Specifies the cross-filter direction for a relationship", "Void",
            ("LeftColumn", "Column", false), ("RightColumn", "Column", false), ("Direction", "Enum", false));
        Add("PATH", "Returns a delimited path string", "String",
            ("IDColumn", "Column", false), ("ParentIDColumn", "Column", false));
        Add("PATHITEM", "Returns an item from a path string", "String",
            ("Path", "String", false), ("Position", "Integer", false), ("Type", "Enum", true));
        Add("PATHITEMREVERSE", "Returns an item from a path string counting from the end", "String",
            ("Path", "String", false), ("Position", "Integer", false), ("Type", "Enum", true));
        Add("PATHCONTAINS", "Checks if a path contains an item", "Boolean",
            ("Path", "String", false), ("Item", "String", false));
        Add("PATHLENGTH", "Returns the number of items in a path", "Integer", ("Path", "String", false));

        // ============================================================
        // LOGICAL FUNCTIONS
        // ============================================================
        Add("IF", "Checks a condition and returns one of two values", "Variant",
            ("Condition", "Boolean", false), ("ThenValue", "Scalar", false), ("ElseValue", "Scalar", true));
        Add("IF.EAGER", "Checks a condition and returns one of two values (evaluates both branches)", "Variant",
            ("Condition", "Boolean", false), ("ThenValue", "Scalar", false), ("ElseValue", "Scalar", true));
        Add("IFERROR", "Returns a value if an error occurs", "Variant",
            ("Value", "Scalar", false), ("ValueIfError", "Scalar", false));
        Add("SWITCH", "Evaluates an expression against a list of values", "Variant",
            ("Expression", "Scalar", false), ("Value1", "Scalar", false), ("Result1", "Scalar", false));
        Add("AND", "Returns TRUE if all arguments are TRUE", "Boolean",
            ("Logical1", "Boolean", false), ("Logical2", "Boolean", false));
        Add("OR", "Returns TRUE if any argument is TRUE", "Boolean",
            ("Logical1", "Boolean", false), ("Logical2", "Boolean", false));
        Add("NOT", "Reverses the logic of its argument", "Boolean",
            ("Logical", "Boolean", false));
        Add("TRUE", "Returns the boolean value TRUE", "Boolean");
        Add("FALSE", "Returns the boolean value FALSE", "Boolean");
        Add("COALESCE", "Returns the first non-blank argument", "Variant",
            ("Value1", "Scalar", false), ("Value2", "Scalar", true));
        Add("BITAND", "Returns a bitwise AND of two numbers", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("BITOR", "Returns a bitwise OR of two numbers", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("BITXOR", "Returns a bitwise XOR of two numbers", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("BITLSHIFT", "Returns a number shifted left by the specified bits", "Integer",
            ("Number", "Integer", false), ("ShiftAmount", "Integer", false));
        Add("BITRSHIFT", "Returns a number shifted right by the specified bits", "Integer",
            ("Number", "Integer", false), ("ShiftAmount", "Integer", false));

        // ============================================================
        // INFORMATION FUNCTIONS
        // ============================================================
        Add("ISBLANK", "Checks if a value is blank", "Boolean", ("Value", "Scalar", false));
        Add("ISERROR", "Checks if a value is an error", "Boolean", ("Value", "Scalar", false));
        Add("ISLOGICAL", "Checks if a value is logical", "Boolean", ("Value", "Scalar", false));
        Add("ISNONTEXT", "Checks if a value is not text", "Boolean", ("Value", "Scalar", false));
        Add("ISNUMBER", "Checks if a value is a number", "Boolean", ("Value", "Scalar", false));
        Add("ISTEXT", "Checks if a value is text", "Boolean", ("Value", "Scalar", false));
        Add("ISBOOLEAN", "Checks if a value is a boolean", "Boolean", ("Value", "Scalar", false));
        Add("ISDATETIME", "Checks if a value is a datetime", "Boolean", ("Value", "Scalar", false));
        Add("ISDECIMAL", "Checks if a value is decimal", "Boolean", ("Value", "Scalar", false));
        Add("ISDOUBLE", "Checks if a value is double precision", "Boolean", ("Value", "Scalar", false));
        Add("ISEMPTY", "Checks if a table is empty", "Boolean", ("Table", "Table", false));
        Add("ISEVEN", "Checks if a number is even", "Boolean", ("Number", "Decimal", false));
        Add("ISINT64", "Checks if a value is a 64-bit integer", "Boolean", ("Value", "Scalar", false));
        Add("ISNUMERIC", "Checks if a value is numeric", "Boolean", ("Value", "Scalar", false));
        Add("ISODD", "Checks if a number is odd", "Boolean", ("Number", "Decimal", false));
        Add("ISSTRING", "Checks if a value is a string", "Boolean", ("Value", "Scalar", false));
        Add("ISSUBTOTAL", "Checks if a column contains a subtotal", "Boolean", ("Column", "Column", false));
        Add("ISAFTER", "Returns TRUE if current row is after specified values", "Boolean",
            ("Value1", "Scalar", false), ("Value2", "Scalar", false), ("Order", "Enum", true));
        Add("ISONORAFTER", "Returns TRUE if current row is on or after specified values", "Boolean",
            ("Value1", "Scalar", false), ("Value2", "Scalar", false), ("Order", "Enum", true));
        Add("ISSELECTEDMEASURE", "Checks if a specific measure is being evaluated", "Boolean",
            ("Measure", "Measure", false));
        Add("USERNAME", "Returns the current user name", "String");
        Add("USERPRINCIPALNAME", "Returns the UPN of the current user", "String");
        Add("USERCULTURE", "Returns the locale for the current user", "String");
        Add("USEROBJECTID", "Returns the Object ID of the current user", "String");
        Add("CUSTOMDATA", "Returns the custom data string passed to the connection", "String");
        Add("CONTAINS", "Checks if a table contains specified values", "Boolean",
            ("Table", "Table", false), ("Column", "Column", false), ("Value", "Scalar", false));
        Add("CONTAINSROW", "Checks if a row exists in a table", "Boolean",
            ("Table", "Table", false), ("Value", "Scalar", false));
        Add("HASONEFILTER", "Returns TRUE if exactly one filter on column", "Boolean", ("Column", "Column", false));
        Add("ISCROSSFILTERED", "Returns TRUE if column is cross-filtered", "Boolean", ("Column", "Column", false));
        Add("ISFILTERED", "Returns TRUE if column is directly filtered", "Boolean", ("Column", "Column", false));
        Add("ISINSCOPE", "Returns TRUE if column is at level of granularity", "Boolean", ("Column", "Column", false));
        Add("COLUMNSTATISTICS", "Returns statistics about columns in the model", "Table");
        Add("EVALUATEANDLOG", "Evaluates an expression and logs it", "Variant",
            ("Value", "Scalar", false), ("Label", "String", true));
        Add("NAMEOF", "Returns the name of a column", "String", ("Column", "Column", false));
        Add("SELECTEDMEASURE", "Returns the currently selected measure", "Variant");
        Add("SELECTEDMEASUREFORMATSTRING", "Returns the format string of the selected measure", "String");
        Add("SELECTEDMEASURENAME", "Returns the name of the selected measure", "String");

        // ============================================================
        // TEXT FUNCTIONS
        // ============================================================
        Add("CONCATENATE", "Joins two text strings", "String",
            ("Text1", "String", false), ("Text2", "String", false));
        Add("CONCATENATEX", "Concatenates the result of an expression for each row", "String",
            ("Table", "Table", false), ("Expression", "String", false), ("Delimiter", "String", true));
        Add("UPPER", "Converts text to uppercase", "String", ("Text", "String", false));
        Add("LOWER", "Converts text to lowercase", "String", ("Text", "String", false));
        Add("TRIM", "Removes leading/trailing spaces", "String", ("Text", "String", false));
        Add("LEFT", "Returns characters from the start of a string", "String",
            ("Text", "String", false), ("NumChars", "Integer", true));
        Add("RIGHT", "Returns characters from the end of a string", "String",
            ("Text", "String", false), ("NumChars", "Integer", true));
        Add("MID", "Returns characters from the middle of a string", "String",
            ("Text", "String", false), ("StartPosition", "Integer", false), ("NumChars", "Integer", false));
        Add("LEN", "Returns the length of a string", "Integer", ("Text", "String", false));
        Add("FIND", "Returns the position of a substring", "Integer",
            ("FindText", "String", false), ("WithinText", "String", false), ("StartPosition", "Integer", true));
        Add("SEARCH", "Returns the position of a substring (case-insensitive)", "Integer",
            ("FindText", "String", false), ("WithinText", "String", false), ("StartPosition", "Integer", true));
        Add("CONTAINSSTRING", "Returns TRUE if one string contains another (case-insensitive)", "Boolean",
            ("WithinText", "String", false), ("FindText", "String", false));
        Add("CONTAINSSTRINGEXACT", "Returns TRUE if one string contains another (case-sensitive)", "Boolean",
            ("WithinText", "String", false), ("FindText", "String", false));
        Add("SUBSTITUTE", "Replaces occurrences of a substring", "String",
            ("Text", "String", false), ("OldText", "String", false), ("NewText", "String", false),
            ("InstanceNum", "Integer", true));
        Add("REPLACE", "Replaces part of a text string", "String",
            ("OldText", "String", false), ("StartNum", "Integer", false),
            ("NumChars", "Integer", false), ("NewText", "String", false));
        Add("FORMAT", "Formats a value with a format string", "String",
            ("Value", "Scalar", false), ("FormatString", "String", false));
        Add("FIXED", "Formats a number as text with a fixed number of decimals", "String",
            ("Number", "Decimal", false), ("Decimals", "Integer", true), ("NoCommas", "Boolean", true));
        Add("COMBINEVALUES", "Combines values with a delimiter", "String",
            ("Delimiter", "String", false), ("Value1", "Scalar", false), ("Value2", "Scalar", true));
        Add("BLANK", "Returns a blank value", "Blank");
        Add("EXACT", "Checks if two strings are identical", "Boolean",
            ("Text1", "String", false), ("Text2", "String", false));
        Add("REPT", "Repeats text a given number of times", "String",
            ("Text", "String", false), ("NumTimes", "Integer", false));
        Add("UNICHAR", "Returns the Unicode character", "String", ("Number", "Integer", false));
        Add("UNICODE", "Returns the Unicode code point of the first character", "Integer",
            ("Text", "String", false));
        Add("TOCSV", "Converts a table to CSV format", "String",
            ("Table", "Table", false), ("MaxRows", "Integer", true));
        Add("TOJSON", "Converts a table to JSON format", "String",
            ("Table", "Table", false), ("MaxRows", "Integer", true));

        // ============================================================
        // MATH AND TRIG FUNCTIONS
        // ============================================================
        Add("ABS", "Returns the absolute value", "Decimal", ("Number", "Decimal", false));
        Add("ROUND", "Rounds a number to the specified digits", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("ROUNDUP", "Rounds a number up", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("ROUNDDOWN", "Rounds a number down", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("CEILING", "Rounds up to the nearest significance", "Decimal",
            ("Number", "Decimal", false), ("Significance", "Decimal", false));
        Add("ISO.CEILING", "Rounds up to the nearest integer or significance (ISO compliant)", "Decimal",
            ("Number", "Decimal", false), ("Significance", "Decimal", true));
        Add("FLOOR", "Rounds down to the nearest significance", "Decimal",
            ("Number", "Decimal", false), ("Significance", "Decimal", false));
        Add("INT", "Rounds down to the nearest integer", "Integer", ("Number", "Decimal", false));
        Add("MOD", "Returns the remainder after division", "Decimal",
            ("Number", "Decimal", false), ("Divisor", "Decimal", false));
        Add("MROUND", "Rounds a number to a specified multiple", "Decimal",
            ("Number", "Decimal", false), ("Multiple", "Decimal", false));
        Add("POWER", "Returns a number raised to a power", "Decimal",
            ("Number", "Decimal", false), ("Power", "Decimal", false));
        Add("SQRT", "Returns the square root", "Decimal", ("Number", "Decimal", false));
        Add("SQRTPI", "Returns the square root of a number multiplied by PI", "Decimal",
            ("Number", "Decimal", false));
        Add("DIVIDE", "Performs division with alternate result for zero", "Decimal",
            ("Numerator", "Decimal", false), ("Denominator", "Decimal", false), ("AlternateResult", "Scalar", true));
        Add("SIGN", "Returns the sign of a number", "Integer", ("Number", "Decimal", false));
        Add("LOG", "Returns the logarithm", "Decimal",
            ("Number", "Decimal", false), ("Base", "Decimal", true));
        Add("LOG10", "Returns the base-10 logarithm", "Decimal", ("Number", "Decimal", false));
        Add("LN", "Returns the natural logarithm", "Decimal", ("Number", "Decimal", false));
        Add("EXP", "Returns e raised to a power", "Decimal", ("Number", "Decimal", false));
        Add("PI", "Returns the value of Pi", "Decimal");
        Add("RAND", "Returns a random number between 0 and 1", "Decimal");
        Add("RANDBETWEEN", "Returns a random integer in a range", "Integer",
            ("Bottom", "Integer", false), ("Top", "Integer", false));
        Add("TRUNC", "Truncates a number to an integer", "Integer",
            ("Number", "Decimal", false), ("NumDigits", "Integer", true));
        Add("EVEN", "Rounds up to the nearest even integer", "Integer", ("Number", "Decimal", false));
        Add("ODD", "Rounds up to the nearest odd integer", "Integer", ("Number", "Decimal", false));
        Add("FACT", "Returns the factorial of a number", "Decimal", ("Number", "Integer", false));
        Add("GCD", "Returns the greatest common divisor", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("LCM", "Returns the least common multiple", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("QUOTIENT", "Returns the integer portion of a division", "Integer",
            ("Numerator", "Decimal", false), ("Denominator", "Decimal", false));
        Add("CONVERT", "Converts an expression to a specified data type", "Variant",
            ("Expression", "Scalar", false), ("DataType", "Type", false));
        Add("CURRENCY", "Converts to currency data type", "Currency", ("Value", "Scalar", false));
        Add("VALUE", "Converts a text string to a number", "Decimal", ("Text", "String", false));
        Add("DEGREES", "Converts radians to degrees", "Decimal", ("Angle", "Decimal", false));
        Add("RADIANS", "Converts degrees to radians", "Decimal", ("Angle", "Decimal", false));

        // Trigonometric functions
        Add("SIN", "Returns the sine of an angle", "Decimal", ("Number", "Decimal", false));
        Add("COS", "Returns the cosine of an angle", "Decimal", ("Number", "Decimal", false));
        Add("TAN", "Returns the tangent of an angle", "Decimal", ("Number", "Decimal", false));
        Add("COT", "Returns the cotangent of an angle", "Decimal", ("Number", "Decimal", false));
        Add("ASIN", "Returns the arcsine of a number", "Decimal", ("Number", "Decimal", false));
        Add("ACOS", "Returns the arccosine of a number", "Decimal", ("Number", "Decimal", false));
        Add("ATAN", "Returns the arctangent of a number", "Decimal", ("Number", "Decimal", false));
        Add("ACOT", "Returns the arccotangent of a number", "Decimal", ("Number", "Decimal", false));
        Add("SINH", "Returns the hyperbolic sine of a number", "Decimal", ("Number", "Decimal", false));
        Add("COSH", "Returns the hyperbolic cosine of a number", "Decimal", ("Number", "Decimal", false));
        Add("TANH", "Returns the hyperbolic tangent of a number", "Decimal", ("Number", "Decimal", false));
        Add("COTH", "Returns the hyperbolic cotangent of a number", "Decimal", ("Number", "Decimal", false));
        Add("ASINH", "Returns the inverse hyperbolic sine of a number", "Decimal", ("Number", "Decimal", false));
        Add("ACOSH", "Returns the inverse hyperbolic cosine of a number", "Decimal", ("Number", "Decimal", false));
        Add("ATANH", "Returns the inverse hyperbolic tangent of a number", "Decimal", ("Number", "Decimal", false));
        Add("ACOTH", "Returns the inverse hyperbolic cotangent of a number", "Decimal", ("Number", "Decimal", false));

        // ============================================================
        // DATE/TIME FUNCTIONS
        // ============================================================
        Add("DATE", "Returns a date from year, month, day", "DateTime",
            ("Year", "Integer", false), ("Month", "Integer", false), ("Day", "Integer", false));
        Add("DATEVALUE", "Converts a date string to a date", "DateTime", ("DateText", "String", false));
        Add("NOW", "Returns the current date and time", "DateTime");
        Add("TODAY", "Returns the current date", "DateTime");
        Add("UTCNOW", "Returns the current UTC date and time", "DateTime");
        Add("UTCTODAY", "Returns the current UTC date", "DateTime");
        Add("YEAR", "Returns the year of a date", "Integer", ("Date", "DateTime", false));
        Add("MONTH", "Returns the month of a date", "Integer", ("Date", "DateTime", false));
        Add("QUARTER", "Returns the quarter of a date (1-4)", "Integer", ("Date", "DateTime", false));
        Add("DAY", "Returns the day of a date", "Integer", ("Date", "DateTime", false));
        Add("HOUR", "Returns the hour of a datetime", "Integer", ("DateTime", "DateTime", false));
        Add("MINUTE", "Returns the minute of a datetime", "Integer", ("DateTime", "DateTime", false));
        Add("SECOND", "Returns the second of a datetime", "Integer", ("DateTime", "DateTime", false));
        Add("WEEKDAY", "Returns the day of the week", "Integer",
            ("Date", "DateTime", false), ("ReturnType", "Integer", true));
        Add("WEEKNUM", "Returns the week number", "Integer",
            ("Date", "DateTime", false), ("ReturnType", "Integer", true));
        Add("EOMONTH", "Returns the last day of the month", "DateTime",
            ("StartDate", "DateTime", false), ("Months", "Integer", false));
        Add("EDATE", "Returns a date that is a number of months before or after", "DateTime",
            ("StartDate", "DateTime", false), ("Months", "Integer", false));
        Add("DATEDIFF", "Returns the difference between two dates", "Integer",
            ("Date1", "DateTime", false), ("Date2", "DateTime", false), ("Interval", "Enum", false));
        Add("CALENDAR", "Returns a single-column table of dates", "Table",
            ("StartDate", "DateTime", false), ("EndDate", "DateTime", false));
        Add("CALENDARAUTO", "Returns a table of dates based on model data", "Table",
            ("FiscalYearEndMonth", "Integer", true));
        Add("TIME", "Returns a time value", "DateTime",
            ("Hour", "Integer", false), ("Minute", "Integer", false), ("Second", "Integer", false));
        Add("TIMEVALUE", "Converts a time string to a time value", "DateTime", ("TimeText", "String", false));
        Add("NETWORKDAYS", "Returns the number of working days between two dates", "Integer",
            ("StartDate", "DateTime", false), ("EndDate", "DateTime", false), ("Holidays", "Table", true));
        Add("YEARFRAC", "Returns the fraction of a year between two dates", "Decimal",
            ("StartDate", "DateTime", false), ("EndDate", "DateTime", false), ("Basis", "Integer", true));

        // ============================================================
        // TIME INTELLIGENCE FUNCTIONS
        // ============================================================
        Add("DATESYTD", "Returns year-to-date dates", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("DATESMTD", "Returns month-to-date dates", "Table", ("Dates", "Column", false));
        Add("DATESQTD", "Returns quarter-to-date dates", "Table", ("Dates", "Column", false));
        Add("DATESWTD", "Returns week-to-date dates", "Table",
            ("Dates", "Column", false), ("FirstDayOfWeek", "Integer", true));
        Add("TOTALYTD", "Evaluates year-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("YearEndDate", "String", true));
        Add("TOTALMTD", "Evaluates month-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("TOTALQTD", "Evaluates quarter-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("TOTALWTD", "Evaluates week-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("FirstDayOfWeek", "Integer", true));
        Add("SAMEPERIODLASTYEAR", "Returns dates shifted one year back", "Table", ("Dates", "Column", false));
        Add("PREVIOUSYEAR", "Returns dates for the previous year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("PREVIOUSQUARTER", "Returns dates for the previous quarter", "Table", ("Dates", "Column", false));
        Add("PREVIOUSMONTH", "Returns dates for the previous month", "Table", ("Dates", "Column", false));
        Add("PREVIOUSWEEK", "Returns dates for the previous week", "Table",
            ("Dates", "Column", false), ("FirstDayOfWeek", "Integer", true));
        Add("PREVIOUSDAY", "Returns dates for the previous day", "Table", ("Dates", "Column", false));
        Add("NEXTYEAR", "Returns dates for the next year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("NEXTQUARTER", "Returns dates for the next quarter", "Table", ("Dates", "Column", false));
        Add("NEXTMONTH", "Returns dates for the next month", "Table", ("Dates", "Column", false));
        Add("NEXTWEEK", "Returns dates for the next week", "Table",
            ("Dates", "Column", false), ("FirstDayOfWeek", "Integer", true));
        Add("NEXTDAY", "Returns dates for the next day", "Table", ("Dates", "Column", false));
        Add("DATEADD", "Shifts dates by an interval", "Table",
            ("Dates", "Column", false), ("NumberOfIntervals", "Integer", false), ("Interval", "Enum", false));
        Add("DATESBETWEEN", "Returns dates between two dates", "Table",
            ("Dates", "Column", false), ("StartDate", "DateTime", false), ("EndDate", "DateTime", false));
        Add("DATESINPERIOD", "Returns dates in a period", "Table",
            ("Dates", "Column", false), ("StartDate", "DateTime", false),
            ("NumberOfIntervals", "Integer", false), ("Interval", "Enum", false));
        Add("PARALLELPERIOD", "Returns a parallel period of dates", "Table",
            ("Dates", "Column", false), ("NumberOfIntervals", "Integer", false), ("Interval", "Enum", false));

        // Opening/Closing Balance functions
        Add("OPENINGBALANCEYEAR", "Evaluates at the start of the year", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("YearEndDate", "String", true));
        Add("OPENINGBALANCEQUARTER", "Evaluates at the start of the quarter", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("OPENINGBALANCEMONTH", "Evaluates at the start of the month", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("OPENINGBALANCEWEEK", "Evaluates at the start of the week", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("FirstDayOfWeek", "Integer", true));
        Add("CLOSINGBALANCEYEAR", "Evaluates at the end of the year", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("YearEndDate", "String", true));
        Add("CLOSINGBALANCEQUARTER", "Evaluates at the end of the quarter", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("CLOSINGBALANCEMONTH", "Evaluates at the end of the month", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("CLOSINGBALANCEWEEK", "Evaluates at the end of the week", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("FirstDayOfWeek", "Integer", true));

        // Start/End of period functions
        Add("STARTOFYEAR", "Returns the first date of the year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("STARTOFQUARTER", "Returns the first date of the quarter", "Table", ("Dates", "Column", false));
        Add("STARTOFMONTH", "Returns the first date of the month", "Table", ("Dates", "Column", false));
        Add("STARTOFWEEK", "Returns the first date of the week", "Table",
            ("Dates", "Column", false), ("FirstDayOfWeek", "Integer", true));
        Add("ENDOFYEAR", "Returns the last date of the year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("ENDOFQUARTER", "Returns the last date of the quarter", "Table", ("Dates", "Column", false));
        Add("ENDOFMONTH", "Returns the last date of the month", "Table", ("Dates", "Column", false));
        Add("ENDOFWEEK", "Returns the last date of the week", "Table",
            ("Dates", "Column", false), ("FirstDayOfWeek", "Integer", true));

        // First/Last functions
        Add("FIRSTDATE", "Returns the first date in a column", "Table", ("Dates", "Column", false));
        Add("LASTDATE", "Returns the last date in a column", "Table", ("Dates", "Column", false));
        Add("FIRSTNONBLANK", "Returns the first value that is not blank", "Table",
            ("Column", "Column", false), ("Expression", "Scalar", false));
        Add("LASTNONBLANK", "Returns the last value that is not blank", "Table",
            ("Column", "Column", false), ("Expression", "Scalar", false));
        Add("FIRSTNONBLANKVALUE", "Returns the first non-blank value of an expression", "Variant",
            ("Column", "Column", false), ("Expression", "Scalar", false));
        Add("LASTNONBLANKVALUE", "Returns the last non-blank value of an expression", "Variant",
            ("Column", "Column", false), ("Expression", "Scalar", false));

        // ============================================================
        // STATISTICAL FUNCTIONS
        // ============================================================

        // Standard deviation and variance
        Add("STDEV.P", "Returns the standard deviation of a population", "Decimal", ("Column", "Column", false));
        Add("STDEV.S", "Returns the standard deviation of a sample", "Decimal", ("Column", "Column", false));
        Add("STDEVX.P", "Returns the standard deviation of a population for an expression", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("STDEVX.S", "Returns the standard deviation of a sample for an expression", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("VAR.P", "Returns the variance of a population", "Decimal", ("Column", "Column", false));
        Add("VAR.S", "Returns the variance of a sample", "Decimal", ("Column", "Column", false));
        Add("VARX.P", "Returns the variance of a population for an expression", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("VARX.S", "Returns the variance of a sample for an expression", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));

        // Median and percentile
        Add("MEDIAN", "Returns the median of numbers in a column", "Decimal", ("Column", "Column", false));
        Add("MEDIANX", "Returns the median of an expression over a table", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("PERCENTILE.EXC", "Returns the k-th percentile (exclusive)", "Decimal",
            ("Column", "Column", false), ("K", "Decimal", false));
        Add("PERCENTILE.INC", "Returns the k-th percentile (inclusive)", "Decimal",
            ("Column", "Column", false), ("K", "Decimal", false));
        Add("PERCENTILEX.EXC", "Returns the k-th percentile of an expression (exclusive)", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false), ("K", "Decimal", false));
        Add("PERCENTILEX.INC", "Returns the k-th percentile of an expression (inclusive)", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false), ("K", "Decimal", false));

        // Geometric mean
        Add("GEOMEAN", "Returns the geometric mean of a column", "Decimal", ("Column", "Column", false));
        Add("GEOMEANX", "Returns the geometric mean of an expression over a table", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));

        // Ranking
        Add("RANK.EQ", "Returns the rank of a number in a list", "Integer",
            ("Value", "Decimal", false), ("Column", "Column", false), ("Order", "Enum", true));

        // Combinatorics
        Add("COMBIN", "Returns the number of combinations", "Integer",
            ("Number", "Integer", false), ("NumberChosen", "Integer", false));
        Add("COMBINA", "Returns the number of combinations with repetitions", "Integer",
            ("Number", "Integer", false), ("NumberChosen", "Integer", false));
        Add("PERMUT", "Returns the number of permutations", "Integer",
            ("Number", "Integer", false), ("NumberChosen", "Integer", false));

        // Distribution functions
        Add("BETA.DIST", "Returns the beta cumulative distribution", "Decimal",
            ("X", "Decimal", false), ("Alpha", "Decimal", false), ("Beta", "Decimal", false),
            ("Cumulative", "Boolean", false), ("A", "Decimal", true), ("B", "Decimal", true));
        Add("BETA.INV", "Returns the inverse of the beta cumulative distribution", "Decimal",
            ("Probability", "Decimal", false), ("Alpha", "Decimal", false), ("Beta", "Decimal", false),
            ("A", "Decimal", true), ("B", "Decimal", true));
        Add("CHISQ.DIST", "Returns the chi-squared distribution", "Decimal",
            ("X", "Decimal", false), ("DegFreedom", "Integer", false), ("Cumulative", "Boolean", false));
        Add("CHISQ.DIST.RT", "Returns the right-tailed chi-squared distribution", "Decimal",
            ("X", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("CHISQ.INV", "Returns the inverse of the chi-squared distribution", "Decimal",
            ("Probability", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("CHISQ.INV.RT", "Returns the inverse of the right-tailed chi-squared distribution", "Decimal",
            ("Probability", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("EXPON.DIST", "Returns the exponential distribution", "Decimal",
            ("X", "Decimal", false), ("Lambda", "Decimal", false), ("Cumulative", "Boolean", false));
        Add("NORM.DIST", "Returns the normal distribution", "Decimal",
            ("X", "Decimal", false), ("Mean", "Decimal", false), ("StandardDev", "Decimal", false),
            ("Cumulative", "Boolean", false));
        Add("NORM.INV", "Returns the inverse of the normal distribution", "Decimal",
            ("Probability", "Decimal", false), ("Mean", "Decimal", false), ("StandardDev", "Decimal", false));
        Add("NORM.S.DIST", "Returns the standard normal distribution", "Decimal",
            ("Z", "Decimal", false), ("Cumulative", "Boolean", false));
        Add("NORM.S.INV", "Returns the inverse of the standard normal distribution", "Decimal",
            ("Probability", "Decimal", false));
        Add("POISSON.DIST", "Returns the Poisson distribution", "Decimal",
            ("X", "Decimal", false), ("Mean", "Decimal", false), ("Cumulative", "Boolean", false));
        Add("T.DIST", "Returns the Student's t-distribution", "Decimal",
            ("X", "Decimal", false), ("DegFreedom", "Integer", false), ("Cumulative", "Boolean", false));
        Add("T.DIST.2T", "Returns the two-tailed Student's t-distribution", "Decimal",
            ("X", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("T.DIST.RT", "Returns the right-tailed Student's t-distribution", "Decimal",
            ("X", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("T.INV", "Returns the inverse of the Student's t-distribution", "Decimal",
            ("Probability", "Decimal", false), ("DegFreedom", "Integer", false));
        Add("T.INV.2T", "Returns the inverse of the two-tailed Student's t-distribution", "Decimal",
            ("Probability", "Decimal", false), ("DegFreedom", "Integer", false));

        // Confidence intervals
        Add("CONFIDENCE.NORM", "Returns the confidence interval using normal distribution", "Decimal",
            ("Alpha", "Decimal", false), ("StandardDev", "Decimal", false), ("Size", "Integer", false));
        Add("CONFIDENCE.T", "Returns the confidence interval using Student's t-distribution", "Decimal",
            ("Alpha", "Decimal", false), ("StandardDev", "Decimal", false), ("Size", "Integer", false));

        // Linear regression
        Add("LINEST", "Returns a table of linear regression statistics", "Table",
            ("KnownY", "Column", false), ("KnownX", "Column", false), ("ConstVar", "Boolean", true),
            ("Stats", "Boolean", true));
        Add("LINESTX", "Returns a table of linear regression statistics over expressions", "Table",
            ("Table", "Table", false), ("KnownY", "Scalar", false), ("KnownX", "Scalar", false),
            ("ConstVar", "Boolean", true), ("Stats", "Boolean", true));

        // ============================================================
        // FINANCIAL FUNCTIONS
        // ============================================================

        // Interest accrued
        Add("ACCRINT", "Returns the accrued interest for a security that pays periodic interest", "Decimal",
            ("Issue", "DateTime", false), ("FirstInterest", "DateTime", false), ("Settlement", "DateTime", false),
            ("Rate", "Decimal", false), ("Par", "Decimal", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true), ("CalcMethod", "Integer", true));
        Add("ACCRINTM", "Returns the accrued interest for a security that pays interest at maturity", "Decimal",
            ("Issue", "DateTime", false), ("Settlement", "DateTime", false), ("Rate", "Decimal", false),
            ("Par", "Decimal", false), ("Basis", "Integer", true));

        // Depreciation
        Add("AMORDEGRC", "Returns the depreciation for each accounting period using degressive method", "Decimal",
            ("Cost", "Decimal", false), ("DatePurchased", "DateTime", false), ("FirstPeriod", "DateTime", false),
            ("Salvage", "Decimal", false), ("Period", "Integer", false), ("Rate", "Decimal", false),
            ("Basis", "Integer", true));
        Add("AMORLINC", "Returns the depreciation for each accounting period using linear method", "Decimal",
            ("Cost", "Decimal", false), ("DatePurchased", "DateTime", false), ("FirstPeriod", "DateTime", false),
            ("Salvage", "Decimal", false), ("Period", "Integer", false), ("Rate", "Decimal", false),
            ("Basis", "Integer", true));
        Add("DB", "Returns the depreciation using the fixed-declining balance method", "Decimal",
            ("Cost", "Decimal", false), ("Salvage", "Decimal", false), ("Life", "Integer", false),
            ("Period", "Integer", false), ("Month", "Integer", true));
        Add("DDB", "Returns the depreciation using the double-declining balance method", "Decimal",
            ("Cost", "Decimal", false), ("Salvage", "Decimal", false), ("Life", "Integer", false),
            ("Period", "Integer", false), ("Factor", "Decimal", true));
        Add("SLN", "Returns the straight-line depreciation of an asset", "Decimal",
            ("Cost", "Decimal", false), ("Salvage", "Decimal", false), ("Life", "Integer", false));
        Add("SYD", "Returns the sum-of-years digits depreciation of an asset", "Decimal",
            ("Cost", "Decimal", false), ("Salvage", "Decimal", false), ("Life", "Integer", false),
            ("Per", "Integer", false));
        Add("VDB", "Returns the depreciation using a variable declining balance method", "Decimal",
            ("Cost", "Decimal", false), ("Salvage", "Decimal", false), ("Life", "Integer", false),
            ("StartPeriod", "Decimal", false), ("EndPeriod", "Decimal", false), ("Factor", "Decimal", true),
            ("NoSwitch", "Boolean", true));

        // Coupon functions
        Add("COUPDAYBS", "Returns the number of days from the beginning of a coupon period to settlement", "Integer",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("COUPDAYS", "Returns the number of days in a coupon period that contains the settlement date", "Integer",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("COUPDAYSNC", "Returns the number of days from the settlement date to the next coupon date", "Integer",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("COUPNCD", "Returns the next coupon date after the settlement date", "DateTime",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("COUPNUM", "Returns the number of coupons payable between settlement and maturity", "Integer",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("COUPPCD", "Returns the previous coupon date before the settlement date", "DateTime",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));

        // Cumulative interest and principal
        Add("CUMIPMT", "Returns the cumulative interest paid between two periods", "Decimal",
            ("Rate", "Decimal", false), ("NPer", "Integer", false), ("PV", "Decimal", false),
            ("StartPeriod", "Integer", false), ("EndPeriod", "Integer", false), ("Type", "Integer", false));
        Add("CUMPRINC", "Returns the cumulative principal paid between two periods", "Decimal",
            ("Rate", "Decimal", false), ("NPer", "Integer", false), ("PV", "Decimal", false),
            ("StartPeriod", "Integer", false), ("EndPeriod", "Integer", false), ("Type", "Integer", false));

        // Discount and interest rate
        Add("DISC", "Returns the discount rate for a security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Pr", "Decimal", false),
            ("Redemption", "Decimal", false), ("Basis", "Integer", true));
        Add("INTRATE", "Returns the interest rate for a fully invested security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Investment", "Decimal", false),
            ("Redemption", "Decimal", false), ("Basis", "Integer", true));
        Add("RECEIVED", "Returns the amount received at maturity for a fully invested security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Investment", "Decimal", false),
            ("Discount", "Decimal", false), ("Basis", "Integer", true));

        // Dollar conversion
        Add("DOLLARDE", "Converts a dollar price from fractional to decimal", "Decimal",
            ("FractionalDollar", "Decimal", false), ("Fraction", "Integer", false));
        Add("DOLLARFR", "Converts a dollar price from decimal to fractional", "Decimal",
            ("DecimalDollar", "Decimal", false), ("Fraction", "Integer", false));

        // Duration
        Add("DURATION", "Returns the Macauley duration for a security with periodic interest", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Coupon", "Decimal", false),
            ("Yld", "Decimal", false), ("Frequency", "Integer", false), ("Basis", "Integer", true));
        Add("MDURATION", "Returns the modified Macauley duration for a security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Coupon", "Decimal", false),
            ("Yld", "Decimal", false), ("Frequency", "Integer", false), ("Basis", "Integer", true));
        Add("PDURATION", "Returns the number of periods for an investment to reach a specified value", "Decimal",
            ("Rate", "Decimal", false), ("PV", "Decimal", false), ("FV", "Decimal", false));

        // Effective and nominal rates
        Add("EFFECT", "Returns the effective annual interest rate", "Decimal",
            ("NominalRate", "Decimal", false), ("NPery", "Integer", false));
        Add("NOMINAL", "Returns the annual nominal interest rate", "Decimal",
            ("EffectRate", "Decimal", false), ("NPery", "Integer", false));

        // Future value, present value, payments
        Add("FV", "Returns the future value of an investment", "Decimal",
            ("Rate", "Decimal", false), ("NPer", "Integer", false), ("Pmt", "Decimal", false),
            ("PV", "Decimal", true), ("Type", "Integer", true));
        Add("PV", "Returns the present value of an investment", "Decimal",
            ("Rate", "Decimal", false), ("NPer", "Integer", false), ("Pmt", "Decimal", false),
            ("FV", "Decimal", true), ("Type", "Integer", true));
        Add("NPER", "Returns the number of periods for an investment", "Decimal",
            ("Rate", "Decimal", false), ("Pmt", "Decimal", false), ("PV", "Decimal", false),
            ("FV", "Decimal", true), ("Type", "Integer", true));
        Add("PMT", "Returns the periodic payment for an annuity", "Decimal",
            ("Rate", "Decimal", false), ("NPer", "Integer", false), ("PV", "Decimal", false),
            ("FV", "Decimal", true), ("Type", "Integer", true));
        Add("IPMT", "Returns the interest payment for a given period", "Decimal",
            ("Rate", "Decimal", false), ("Per", "Integer", false), ("NPer", "Integer", false),
            ("PV", "Decimal", false), ("FV", "Decimal", true), ("Type", "Integer", true));
        Add("PPMT", "Returns the principal payment for a given period", "Decimal",
            ("Rate", "Decimal", false), ("Per", "Integer", false), ("NPer", "Integer", false),
            ("PV", "Decimal", false), ("FV", "Decimal", true), ("Type", "Integer", true));
        Add("ISPMT", "Returns the interest paid during a specific period", "Decimal",
            ("Rate", "Decimal", false), ("Per", "Integer", false), ("NPer", "Integer", false),
            ("PV", "Decimal", false));
        Add("RATE", "Returns the interest rate per period", "Decimal",
            ("NPer", "Integer", false), ("Pmt", "Decimal", false), ("PV", "Decimal", false),
            ("FV", "Decimal", true), ("Type", "Integer", true), ("Guess", "Decimal", true));
        Add("RRI", "Returns an equivalent interest rate for the growth of an investment", "Decimal",
            ("NPer", "Integer", false), ("PV", "Decimal", false), ("FV", "Decimal", false));

        // Price functions
        Add("PRICE", "Returns the price per $100 face value of a security that pays periodic interest", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Rate", "Decimal", false),
            ("Yld", "Decimal", false), ("Redemption", "Decimal", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("PRICEDISC", "Returns the price per $100 face value of a discounted security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Discount", "Decimal", false),
            ("Redemption", "Decimal", false), ("Basis", "Integer", true));
        Add("PRICEMAT", "Returns the price per $100 face value of a security that pays interest at maturity", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Issue", "DateTime", false),
            ("Rate", "Decimal", false), ("Yld", "Decimal", false), ("Basis", "Integer", true));
        Add("ODDFPRICE", "Returns the price of a security with an odd first period", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Issue", "DateTime", false),
            ("FirstCoupon", "DateTime", false), ("Rate", "Decimal", false), ("Yld", "Decimal", false),
            ("Redemption", "Decimal", false), ("Frequency", "Integer", false), ("Basis", "Integer", true));
        Add("ODDLPRICE", "Returns the price of a security with an odd last period", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("LastInterest", "DateTime", false),
            ("Rate", "Decimal", false), ("Yld", "Decimal", false), ("Redemption", "Decimal", false),
            ("Frequency", "Integer", false), ("Basis", "Integer", true));

        // Yield functions
        Add("YIELD", "Returns the yield on a security that pays periodic interest", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Rate", "Decimal", false),
            ("Pr", "Decimal", false), ("Redemption", "Decimal", false), ("Frequency", "Integer", false),
            ("Basis", "Integer", true));
        Add("YIELDDISC", "Returns the annual yield for a discounted security", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Pr", "Decimal", false),
            ("Redemption", "Decimal", false), ("Basis", "Integer", true));
        Add("YIELDMAT", "Returns the annual yield of a security that pays interest at maturity", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Issue", "DateTime", false),
            ("Rate", "Decimal", false), ("Pr", "Decimal", false), ("Basis", "Integer", true));
        Add("ODDFYIELD", "Returns the yield of a security with an odd first period", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Issue", "DateTime", false),
            ("FirstCoupon", "DateTime", false), ("Rate", "Decimal", false), ("Pr", "Decimal", false),
            ("Redemption", "Decimal", false), ("Frequency", "Integer", false), ("Basis", "Integer", true));
        Add("ODDLYIELD", "Returns the yield of a security with an odd last period", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("LastInterest", "DateTime", false),
            ("Rate", "Decimal", false), ("Pr", "Decimal", false), ("Redemption", "Decimal", false),
            ("Frequency", "Integer", false), ("Basis", "Integer", true));

        // Treasury bill functions
        Add("TBILLEQ", "Returns the bond-equivalent yield for a Treasury bill", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Discount", "Decimal", false));
        Add("TBILLPRICE", "Returns the price per $100 face value for a Treasury bill", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Discount", "Decimal", false));
        Add("TBILLYIELD", "Returns the yield for a Treasury bill", "Decimal",
            ("Settlement", "DateTime", false), ("Maturity", "DateTime", false), ("Pr", "Decimal", false));

        // Internal rate of return
        Add("XIRR", "Returns the internal rate of return for non-periodic cash flows", "Decimal",
            ("Table", "Table", false), ("Values", "Scalar", false), ("Dates", "Scalar", false),
            ("Guess", "Decimal", true));
        Add("XNPV", "Returns the net present value for non-periodic cash flows", "Decimal",
            ("Rate", "Decimal", false), ("Table", "Table", false), ("Values", "Scalar", false),
            ("Dates", "Scalar", false));

        // ============================================================
        // OTHER / MISCELLANEOUS FUNCTIONS
        // ============================================================
        Add("EARLIER", "Returns the value of a column in an outer row context", "Variant",
            ("Column", "Column", false), ("Number", "Integer", true));
        Add("EARLIEST", "Returns the value of a column in the earliest row context", "Variant",
            ("Column", "Column", false));
        Add("ERROR", "Raises an error with a specified message", "Void",
            ("ErrorText", "String", false));
        Add("HASH", "Returns a hash value for a list of values", "Integer",
            ("Value1", "Scalar", false), ("Value2", "Scalar", true));
        Add("RANK", "Returns the rank of a value in a list", "Integer",
            ("Value", "Decimal", false), ("ColumnName", "Column", false), ("Order", "Enum", true));
        Add("ROWNUMBER", "Returns the current row number", "Integer",
            ("Table", "Table", true), ("OrderBy", "OrderBy", true), ("Blanks", "Enum", true),
            ("PartitionBy", "PartitionBy", true));
        Add("EXTERNALMEASURE", "References an external measure", "Variant",
            ("MeasureName", "String", false));
        Add("KEYWORDMATCH", "Returns TRUE if the specified keyword is found", "Boolean",
            ("Text", "String", false), ("Keyword", "String", false));

        // ============================================================
        // VISUAL CALCULATIONS (Power BI specific)
        // ============================================================
        Add("COLLAPSE", "Collapses the current hierarchy level", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true));
        Add("COLLAPSEALL", "Collapses all hierarchy levels", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true));
        Add("EXPAND", "Expands to the next hierarchy level", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true));
        Add("EXPANDALL", "Expands all hierarchy levels", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true));
        Add("FIRST", "Returns the first row in a visual calculation", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true), ("Reset", "Boolean", true));
        Add("LAST", "Returns the last row in a visual calculation", "Variant",
            ("Expression", "Scalar", false), ("Axis", "Column", true), ("Reset", "Boolean", true));
        Add("NEXT", "Returns the next row in a visual calculation", "Variant",
            ("Expression", "Scalar", false), ("Steps", "Integer", true), ("Axis", "Column", true),
            ("Reset", "Boolean", true));
        Add("PREVIOUS", "Returns the previous row in a visual calculation", "Variant",
            ("Expression", "Scalar", false), ("Steps", "Integer", true), ("Axis", "Column", true),
            ("Reset", "Boolean", true));
        Add("MOVINGAVERAGE", "Returns a moving average", "Decimal",
            ("Expression", "Scalar", false), ("WindowSize", "Integer", false), ("Axis", "Column", true),
            ("Reset", "Boolean", true));
        Add("RUNNINGSUM", "Returns a running sum", "Decimal",
            ("Expression", "Scalar", false), ("Axis", "Column", true), ("Reset", "Boolean", true));
        Add("RANGE", "Returns a range of rows in a visual calculation", "Table",
            ("StartOffset", "Integer", false), ("EndOffset", "Integer", false), ("Axis", "Column", true),
            ("Reset", "Boolean", true));
    }

    private static void Add(string name, string description, string returnType,
        params (string Name, string Type, bool IsOptional)[] parms)
    {
        var sig = new DaxFunctionSignature
        {
            Name = name,
            Description = description,
            ReturnType = returnType,
            Parameters = parms.Select(p => new DaxFunctionParameter
            {
                Name = p.Name,
                Type = p.Type,
                IsOptional = p.IsOptional
            }).ToList()
        };
        Functions[name] = sig;
    }

    public static bool TryGetFunction(string name, out DaxFunctionSignature? signature)
    {
        return Functions.TryGetValue(name, out signature);
    }

    public static IEnumerable<DaxFunctionSignature> GetAllFunctions() => Functions.Values;

    public static IEnumerable<DaxFunctionSignature> SearchFunctions(string prefix)
    {
        return Functions.Values.Where(f =>
            f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
