using ApplyTrail.Application.Logic;

namespace ApplyTrail.UnitTests;

public class DuplicateDetectorTests
{
    // ---- links ----------------------------------------------------------------------------

    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/123456/?trackingId=abc&utm_source=share#details", "linkedin.com/jobs/view/123456")]
    [InlineData("http://linkedin.com/jobs/view/123456", "linkedin.com/jobs/view/123456")]
    [InlineData("https://www.indeed.com/viewjob?jk=ABC123&from=serp&vjs=3", "indeed.com/viewjob?jk=ABC123")]
    [InlineData("https://example.com/careers/", "example.com/careers")]
    [InlineData("https://example.com/", "example.com")]
    [InlineData("https://boards.example.com/jobs?b=2&a=1", "boards.example.com/jobs?a=1&b=2")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Links_are_reduced_to_the_part_that_identifies_the_job(string? url, string expected)
    {
        Assert.Equal(expected, DuplicateDetector.NormalizeUrl(url));
    }

    [Fact]
    public void The_same_job_reached_through_different_tracking_links_is_the_same_link()
    {
        var reason = DuplicateDetector.Compare(
            "Acme", "Engineer", "https://www.linkedin.com/jobs/view/999/?trackingId=x",
            "Totally different text", "Other", "https://linkedin.com/jobs/view/999?utm_medium=email");

        Assert.Equal(DuplicateReason.SameLink, reason);
    }

    [Fact]
    public void Query_parameters_that_identify_the_job_are_kept()
    {
        var reason = DuplicateDetector.Compare(
            "Acme", "Engineer", "https://www.indeed.com/viewjob?jk=AAA",
            "Other Co", "Other role", "https://www.indeed.com/viewjob?jk=BBB");

        Assert.Equal(DuplicateReason.None, reason);
    }

    [Fact]
    public void Two_blank_links_are_not_treated_as_the_same_link()
    {
        var reason = DuplicateDetector.Compare("Acme", "Engineer", null, "Globex", "Designer", "");

        Assert.Equal(DuplicateReason.None, reason);
    }

    // ---- companies ------------------------------------------------------------------------

    [Theory]
    [InlineData("Stripe Ireland Ltd.", "stripe")]
    [InlineData("ACME Inc.", "acme")]
    [InlineData("Acme Limited", "acme")]
    [InlineData("Ireland Ltd", "ireland")] // never strips a name down to nothing
    [InlineData("Johnson & Johnson", "johnson and johnson")]
    [InlineData("  Open   AI  ", "open ai")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Company_names_ignore_case_punctuation_and_legal_suffixes(string? company, string expected)
    {
        Assert.Equal(expected, DuplicateDetector.NormalizeCompany(company));
    }

    // ---- roles ----------------------------------------------------------------------------

    [Theory]
    [InlineData("Senior Software Engineer", "Software Engineer (Senior)", 1.0)]
    [InlineData("SOFTWARE   engineer", "software engineer", 1.0)]
    [InlineData("Engineer for the Platform", "Platform Engineer", 1.0)]
    [InlineData(".NET Developer", "Dotnet Developer", 1.0)]
    [InlineData("Software Engineer - .NET / Angular", "Software Engineer (.NET, Angular)", 1.0)]
    public void Equivalent_titles_are_fully_similar(string a, string b, double expected)
    {
        Assert.Equal(expected, DuplicateDetector.RoleSimilarity(a, b));
    }

    [Fact]
    public void Seniority_makes_a_different_job()
    {
        var similarity = DuplicateDetector.RoleSimilarity("Software Engineer", "Senior Software Engineer");

        Assert.Equal(0.6667, similarity, 4);
        Assert.True(similarity < DuplicateDetector.RoleThreshold);
    }

    [Fact]
    public void Different_languages_are_different_jobs()
    {
        // Punctuation matters here: C# and C++ must not collapse to the same word.
        Assert.True(DuplicateDetector.RoleSimilarity("C# Developer", "C++ Developer") < DuplicateDetector.RoleThreshold);
    }

    [Fact]
    public void Missing_titles_are_never_similar()
    {
        Assert.Equal(0, DuplicateDetector.RoleSimilarity("", "Engineer"));
        Assert.Equal(0, DuplicateDetector.RoleSimilarity(null, null));
    }

    // ---- company + role -------------------------------------------------------------------

    [Fact]
    public void Same_employer_and_near_identical_title_is_a_duplicate()
    {
        var reason = DuplicateDetector.Compare(
            "Acme Ltd", "Senior Full Stack Software Engineer", null,
            "ACME", "Senior Full Stack Software Engineer Dublin", null);

        // 5 shared words out of 6 = 0.83, above the 0.8 threshold.
        Assert.Equal(DuplicateReason.SameCompanyAndRole, reason);
    }

    [Fact]
    public void A_short_title_with_an_extra_word_is_not_close_enough()
    {
        var reason = DuplicateDetector.Compare(
            "Acme", "Full Stack Engineer", null,
            "Acme", "Full Stack Engineer Dublin", null);

        // 3 shared words out of 4 = 0.75, below the threshold: could be a genuinely separate opening.
        Assert.Equal(DuplicateReason.None, reason);
    }

    [Fact]
    public void Different_employers_with_the_same_title_are_not_duplicates()
    {
        var reason = DuplicateDetector.Compare("Acme", "Software Engineer", null, "Globex", "Software Engineer", null);

        Assert.Equal(DuplicateReason.None, reason);
    }

    [Fact]
    public void Same_employer_with_a_different_role_is_not_a_duplicate()
    {
        var reason = DuplicateDetector.Compare("Acme", "Software Engineer", null, "Acme", "Product Designer", null);

        Assert.Equal(DuplicateReason.None, reason);
    }

    [Fact]
    public void Blank_company_names_never_match()
    {
        var reason = DuplicateDetector.Compare("", "Engineer", null, "", "Engineer", null);

        Assert.Equal(DuplicateReason.None, reason);
    }
}
