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
        // Aggregation
        Add("SUM", "Adds all numbers in a column", "Decimal", ("Column", "Column", false));
        Add("SUMX", "Returns the sum of an expression evaluated for each row", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("AVERAGE", "Returns the average of all numbers in a column", "Decimal", ("Column", "Column", false));
        Add("AVERAGEX", "Calculates the average of an expression over a table", "Decimal",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("MIN", "Returns the minimum value in a column", "Variant", ("Column", "Column", false));
        Add("MINX", "Returns the minimum of an expression evaluated for each row", "Variant",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("MAX", "Returns the maximum value in a column", "Variant", ("Column", "Column", false));
        Add("MAXX", "Returns the maximum of an expression evaluated for each row", "Variant",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("COUNT", "Counts the number of cells in a column that contain numbers", "Integer", ("Column", "Column", false));
        Add("COUNTA", "Counts the number of cells in a column that are not empty", "Integer", ("Column", "Column", false));
        Add("COUNTAX", "Counts non-blank results of an expression over a table", "Integer",
            ("Table", "Table", false), ("Expression", "Scalar", false));
        Add("COUNTBLANK", "Counts the number of blank cells in a column", "Integer", ("Column", "Column", false));
        Add("COUNTROWS", "Counts the number of rows in a table", "Integer", ("Table", "Table", false));
        Add("DISTINCTCOUNT", "Counts the number of distinct values in a column", "Integer", ("Column", "Column", false));
        Add("DISTINCTCOUNTNOBLANK", "Counts distinct non-blank values in a column", "Integer", ("Column", "Column", false));
        Add("RANKX", "Returns the rank of an expression in a table", "Integer",
            ("Table", "Table", false), ("Expression", "Scalar", false), ("Value", "Scalar", true),
            ("Order", "Enum", true), ("Ties", "Enum", true));

        // Filter
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

        // Table
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

        // Relationship
        Add("RELATED", "Returns a related value from another table", "Variant", ("Column", "Column", false));
        Add("RELATEDTABLE", "Returns the related table filtered by context", "Table", ("Table", "Table", false));
        Add("LOOKUPVALUE", "Returns a value from a table based on search", "Variant",
            ("ResultColumn", "Column", false), ("SearchColumn", "Column", false), ("SearchValue", "Scalar", false));
        Add("PATH", "Returns a delimited path string", "String",
            ("IDColumn", "Column", false), ("ParentIDColumn", "Column", false));
        Add("PATHITEM", "Returns an item from a path string", "String",
            ("Path", "String", false), ("Position", "Integer", false));
        Add("PATHCONTAINS", "Checks if a path contains an item", "Boolean",
            ("Path", "String", false), ("Item", "String", false));
        Add("PATHLENGTH", "Returns the number of items in a path", "Integer", ("Path", "String", false));

        // Logical
        Add("IF", "Checks a condition and returns one of two values", "Variant",
            ("Condition", "Boolean", false), ("ThenValue", "Scalar", false), ("ElseValue", "Scalar", true));
        Add("IFERROR", "Returns a value if an error occurs", "Variant",
            ("Value", "Scalar", false), ("ValueIfError", "Scalar", false));
        Add("SWITCH", "Evaluates an expression against a list of values", "Variant",
            ("Expression", "Scalar", false), ("Value1", "Scalar", false), ("Result1", "Scalar", false));
        Add("ISBLANK", "Checks if a value is blank", "Boolean", ("Value", "Scalar", false));
        Add("ISERROR", "Checks if a value is an error", "Boolean", ("Value", "Scalar", false));
        Add("ISLOGICAL", "Checks if a value is logical", "Boolean", ("Value", "Scalar", false));
        Add("ISNONTEXT", "Checks if a value is not text", "Boolean", ("Value", "Scalar", false));
        Add("ISNUMBER", "Checks if a value is a number", "Boolean", ("Value", "Scalar", false));
        Add("ISTEXT", "Checks if a value is text", "Boolean", ("Value", "Scalar", false));
        Add("COALESCE", "Returns the first non-blank argument", "Variant",
            ("Value1", "Scalar", false), ("Value2", "Scalar", true));

        // Text
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
        Add("SUBSTITUTE", "Replaces occurrences of a substring", "String",
            ("Text", "String", false), ("OldText", "String", false), ("NewText", "String", false),
            ("InstanceNum", "Integer", true));
        Add("REPLACE", "Replaces part of a text string", "String",
            ("OldText", "String", false), ("StartNum", "Integer", false),
            ("NumChars", "Integer", false), ("NewText", "String", false));
        Add("FORMAT", "Formats a value with a format string", "String",
            ("Value", "Scalar", false), ("FormatString", "String", false));
        Add("COMBINEVALUES", "Combines values with a delimiter", "String",
            ("Delimiter", "String", false), ("Value1", "Scalar", false), ("Value2", "Scalar", true));
        Add("BLANK", "Returns a blank value", "Blank");
        Add("EXACT", "Checks if two strings are identical", "Boolean",
            ("Text1", "String", false), ("Text2", "String", false));
        Add("REPT", "Repeats text a given number of times", "String",
            ("Text", "String", false), ("NumTimes", "Integer", false));
        Add("UNICHAR", "Returns the Unicode character", "String", ("Number", "Integer", false));

        // Math
        Add("ABS", "Returns the absolute value", "Decimal", ("Number", "Decimal", false));
        Add("ROUND", "Rounds a number to the specified digits", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("ROUNDUP", "Rounds a number up", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("ROUNDDOWN", "Rounds a number down", "Decimal",
            ("Number", "Decimal", false), ("NumDigits", "Integer", false));
        Add("CEILING", "Rounds up to the nearest significance", "Decimal",
            ("Number", "Decimal", false), ("Significance", "Decimal", false));
        Add("FLOOR", "Rounds down to the nearest significance", "Decimal",
            ("Number", "Decimal", false), ("Significance", "Decimal", false));
        Add("INT", "Rounds down to the nearest integer", "Integer", ("Number", "Decimal", false));
        Add("MOD", "Returns the remainder after division", "Decimal",
            ("Number", "Decimal", false), ("Divisor", "Decimal", false));
        Add("POWER", "Returns a number raised to a power", "Decimal",
            ("Number", "Decimal", false), ("Power", "Decimal", false));
        Add("SQRT", "Returns the square root", "Decimal", ("Number", "Decimal", false));
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
        Add("GCD", "Returns the greatest common divisor", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("LCM", "Returns the least common multiple", "Integer",
            ("Number1", "Integer", false), ("Number2", "Integer", false));
        Add("QUOTIENT", "Returns the integer portion of a division", "Integer",
            ("Numerator", "Decimal", false), ("Denominator", "Decimal", false));
        Add("CONVERT", "Converts an expression to a specified data type", "Variant",
            ("Expression", "Scalar", false), ("DataType", "Type", false));
        Add("CURRENCY", "Converts to currency data type", "Currency", ("Value", "Scalar", false));

        // Date/Time
        Add("DATE", "Returns a date from year, month, day", "DateTime",
            ("Year", "Integer", false), ("Month", "Integer", false), ("Day", "Integer", false));
        Add("DATEVALUE", "Converts a date string to a date", "DateTime", ("DateText", "String", false));
        Add("NOW", "Returns the current date and time", "DateTime");
        Add("TODAY", "Returns the current date", "DateTime");
        Add("YEAR", "Returns the year of a date", "Integer", ("Date", "DateTime", false));
        Add("MONTH", "Returns the month of a date", "Integer", ("Date", "DateTime", false));
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

        // Time Intelligence
        Add("DATESYTD", "Returns year-to-date dates", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("DATESMTD", "Returns month-to-date dates", "Table", ("Dates", "Column", false));
        Add("DATESQTD", "Returns quarter-to-date dates", "Table", ("Dates", "Column", false));
        Add("TOTALYTD", "Evaluates year-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true),
            ("YearEndDate", "String", true));
        Add("TOTALMTD", "Evaluates month-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("TOTALQTD", "Evaluates quarter-to-date total", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("SAMEPERIODLASTYEAR", "Returns dates shifted one year back", "Table", ("Dates", "Column", false));
        Add("PREVIOUSYEAR", "Returns dates for the previous year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("PREVIOUSQUARTER", "Returns dates for the previous quarter", "Table", ("Dates", "Column", false));
        Add("PREVIOUSMONTH", "Returns dates for the previous month", "Table", ("Dates", "Column", false));
        Add("PREVIOUSDAY", "Returns dates for the previous day", "Table", ("Dates", "Column", false));
        Add("NEXTYEAR", "Returns dates for the next year", "Table",
            ("Dates", "Column", false), ("YearEndDate", "String", true));
        Add("NEXTQUARTER", "Returns dates for the next quarter", "Table", ("Dates", "Column", false));
        Add("NEXTMONTH", "Returns dates for the next month", "Table", ("Dates", "Column", false));
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
        Add("OPENINGBALANCEYEAR", "Evaluates at the start of the year", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));
        Add("CLOSINGBALANCEYEAR", "Evaluates at the end of the year", "Variant",
            ("Expression", "Scalar", false), ("Dates", "Column", false), ("Filter", "Boolean", true));

        // Info
        Add("USERNAME", "Returns the current user name", "String");
        Add("USERPRINCIPALNAME", "Returns the UPN of the current user", "String");
        Add("CONTAINS", "Checks if a table contains specified values", "Boolean",
            ("Table", "Table", false), ("Column", "Column", false), ("Value", "Scalar", false));
        Add("CONTAINSROW", "Checks if a row exists in a table", "Boolean",
            ("Table", "Table", false), ("Value", "Scalar", false));
        Add("HASONEFILTER", "Returns TRUE if exactly one filter on column", "Boolean", ("Column", "Column", false));
        Add("ISCROSSFILTERED", "Returns TRUE if column is cross-filtered", "Boolean", ("Column", "Column", false));
        Add("ISFILTERED", "Returns TRUE if column is directly filtered", "Boolean", ("Column", "Column", false));
        Add("ISINSCOPE", "Returns TRUE if column is at level of granularity", "Boolean", ("Column", "Column", false));
        Add("SELECTEDVALUE", "Returns the value if only one in the filter", "Variant",
            ("Column", "Column", false), ("AlternateResult", "Scalar", true));
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
