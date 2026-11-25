using ApplyTrail.Application.Logic;

namespace ApplyTrail.UnitTests;

public class CsvExporterTests
{
    [Fact]
    public void Writes_a_header_and_rows_with_crlf_line_endings()
    {
        var csv = CsvExporter.Write(["a", "b"], [["1", "2"], ["3", "4"]]);

        Assert.Equal("a,b\r\n1,2\r\n3,4\r\n", csv);
    }

    [Fact]
    public void No_rows_still_produces_the_header()
    {
        Assert.Equal("a,b\r\n", CsvExporter.Write(["a", "b"], []));
    }

    [Fact]
    public void Null_and_empty_cells_are_blank()
    {
        Assert.Equal("x,y,z\r\na,,c\r\n", CsvExporter.Write(["x", "y", "z"], [["a", null, "c"]]));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("Hello, World", "\"Hello, World\"")]
    [InlineData("She said \"hi\"", "\"She said \"\"hi\"\"\"")]
    [InlineData("line one\nline two", "\"line one\nline two\"")]
    [InlineData("Café, Dublin", "\"Café, Dublin\"")]
    [InlineData("a=b", "a=b")] // only a LEADING '=' is dangerous
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Cells_are_quoted_only_when_they_have_to_be(string? value, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(value));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\",\"click\")", "\"'=HYPERLINK(\"\"http://evil\"\",\"\"click\"\")\"")]
    [InlineData("+44 20 7946 0000", "'+44 20 7946 0000")]
    [InlineData("-5 years experience", "'-5 years experience")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tindented", "'\tindented")]
    public void Cells_that_a_spreadsheet_would_run_as_formulas_are_neutralised(string value, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(value));
    }

    [Fact]
    public void A_neutralised_cell_that_also_contains_a_comma_is_still_quoted()
    {
        Assert.Equal("\"'=SUM(A1,A2)\"", CsvExporter.Escape("=SUM(A1,A2)"));
    }
}
