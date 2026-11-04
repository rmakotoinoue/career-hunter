using System.Net;
using System.Text;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.SharedKernel;
using CareerHunter.Infrastructure.Adapters;
using CareerHunter.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareerHunter.Infrastructure.Tests;

public sealed class JobProviderAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Adzuna_sends_configured_credentials_and_maps_the_search_results()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "count": 2,
              "results": [
                {
                  "id": "123",
                  "title": "Desenvolvedor .NET Sênior",
                  "redirect_url": "https://www.adzuna.com.br/details/123",
                  "company": { "display_name": "Acme" },
                  "location": { "display_name": "São Paulo" },
                  "description": "<p>Vaga para pessoa desenvolvedora .NET com C# e Azure.</p>",
                  "created": "2026-11-01T10:00:00Z"
                },
                {
                  "id": "456",
                  "title": "Analista Comercial",
                  "redirect_url": "https://www.adzuna.com.br/details/456",
                  "company": { "display_name": "Acme" },
                  "location": { "display_name": "São Paulo" },
                  "description": "Vaga para atuar com vendas consultivas de software.",
                  "created": "2026-11-01T10:00:00Z"
                }
              ]
            }
            """));

        var adapter = new AdzunaJobSourceAdapter(
            handler.Factory,
            Options(appId: "app-id", appKey: "app-key"),
            new JobPostingFilter(new JobProviderFilterOptions { TitleKeywords = [".net"] }),
            NullLogger<AdzunaJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Adzuna BR - .NET",
            "https://api.adzuna.com/v1/api/jobs/br/search/1?what=.net&where=S%C3%A3o%20Paulo",
            JobSourceKind.Adzuna,
            null,
            360,
            Now);

        var jobs = await adapter.CollectAsync(source);

        var job = Assert.Single(jobs);
        Assert.Equal("Desenvolvedor .NET Sênior", job.Title);
        Assert.Equal("Acme", job.Company);
        Assert.Equal("São Paulo", job.Location);
        Assert.Equal("https://www.adzuna.com.br/details/123", job.SourceUrl);
        Assert.Equal("123", job.ExternalId);
        Assert.Contains("C# e Azure", job.Description);
        Assert.DoesNotContain("<p>", job.Description);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), job.PublishedAt);

        var requestUri = Assert.IsType<Uri>(handler.LastRequest?.RequestUri);
        Assert.Contains("app_id=app-id", requestUri.Query);
        Assert.Contains("app_key=app-key", requestUri.Query);
        Assert.Contains("results_per_page=50", requestUri.Query);
    }

    [Fact]
    public async Task Adzuna_without_credentials_fails_with_actionable_message()
    {
        var adapter = new AdzunaJobSourceAdapter(
            new StubHttpHandler(_ => throw new InvalidOperationException("must not call the network")).Factory,
            Options(appId: null, appKey: null),
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<AdzunaJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Adzuna BR",
            "https://api.adzuna.com/v1/api/jobs/br/search/1",
            JobSourceKind.Adzuna,
            null,
            360,
            Now);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.CollectAsync(source));
        Assert.Contains("JobProviders__Adzuna__AppId", exception.Message);
    }

    [Fact]
    public async Task Adzuna_rejects_a_source_url_outside_the_documented_host()
    {
        var adapter = new AdzunaJobSourceAdapter(
            new StubHttpHandler(_ => throw new InvalidOperationException("must not call the network")).Factory,
            Options(appId: "app-id", appKey: "app-key"),
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<AdzunaJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Adzuna falsa",
            "https://evil.example/v1/api/jobs/br/search/1",
            JobSourceKind.Adzuna,
            null,
            360,
            Now);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.CollectAsync(source));
        Assert.Contains("api.adzuna.com", exception.Message);
    }

    [Fact]
    public async Task Jooble_posts_the_search_from_the_source_url_and_maps_the_jobs()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "totalCount": 1,
              "jobs": [
                {
                  "id": "abc",
                  "title": "Engenheiro de Software .NET",
                  "location": "São Paulo, SP",
                  "snippet": "<b>Vaga</b> para atuar com .NET, C# e mensageria.",
                  "company": "Acme",
                  "link": "https://br.jooble.org/jdp/abc",
                  "updated": "2026-11-02T00:00:00.0000000"
                }
              ]
            }
            """));

        var adapter = new JoobleJobSourceAdapter(
            handler.Factory,
            new JobProviderOptions { Jooble = new JoobleOptions { ApiKey = "test-key" } },
            new JobPostingFilter(new JobProviderFilterOptions { TitleKeywords = [".net"] }),
            NullLogger<JoobleJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Jooble BR - .NET",
            "https://br.jooble.org/api/?keywords=desenvolvedor%20.net&location=Brasil",
            JobSourceKind.Jooble,
            null,
            360,
            Now);

        var jobs = await adapter.CollectAsync(source);

        var job = Assert.Single(jobs);
        Assert.Equal("Engenheiro de Software .NET", job.Title);
        Assert.Equal("São Paulo, SP", job.Location);
        Assert.Equal("Acme", job.Company);
        Assert.Contains(".NET", job.Description);
        Assert.DoesNotContain("<b>", job.Description);

        Assert.Equal("https://br.jooble.org/api/test-key", handler.LastRequest?.RequestUri?.ToString());
        Assert.Contains("\"keywords\":\"desenvolvedor .net\"", handler.LastBody);
        Assert.Contains("\"location\":\"Brasil\"", handler.LastBody);
    }

    [Fact]
    public async Task Jooble_without_a_keyword_fails_with_guidance()
    {
        var adapter = new JoobleJobSourceAdapter(
            new StubHttpHandler(_ => throw new InvalidOperationException("must not call the network")).Factory,
            new JobProviderOptions { Jooble = new JoobleOptions { ApiKey = "test-key" } },
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<JoobleJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Jooble BR",
            "https://jooble.org/api/?location=Brasil",
            JobSourceKind.Jooble,
            null,
            360,
            Now);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.CollectAsync(source));
        Assert.Contains("keywords", exception.Message);
    }

    [Fact]
    public async Task Greenhouse_decodes_escaped_html_uses_company_name_and_respects_the_location_filter()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "jobs": [
                {
                  "id": 4749401005,
                  "title": "Pessoa Desenvolvedora .NET Sênior",
                  "absolute_url": "https://job-boards.greenhouse.io/c6bank/jobs/4749401005",
                  "company_name": "C6 Bank",
                  "location": { "name": "São Paulo, Brazil" },
                  "content": "&lt;div class=&quot;description&quot;&gt;&lt;p&gt;Vaga para atuar com .NET e Azure.&lt;/p&gt;&lt;/div&gt;",
                  "first_published": "2026-10-30T14:43:18-04:00",
                  "updated_at": "2026-11-01T09:00:00-03:00"
                },
                {
                  "id": 4749401006,
                  "title": "Backend Engineer .NET",
                  "absolute_url": "https://job-boards.greenhouse.io/c6bank/jobs/4749401006",
                  "company_name": "C6 Bank",
                  "location": { "name": "Lisbon, Portugal" },
                  "content": "&lt;p&gt;Vaga para atuar com .NET em Lisboa.&lt;/p&gt;",
                  "first_published": "2026-10-30T14:43:18-04:00"
                }
              ]
            }
            """));

        var adapter = new GreenhouseJobSourceAdapter(
            handler.Factory,
            new JobPostingFilter(new JobProviderFilterOptions
            {
                TitleKeywords = [".net"],
                LocationKeywords = ["brazil"]
            }),
            NullLogger<GreenhouseJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Greenhouse - C6 Bank",
            "https://boards-api.greenhouse.io/v1/boards/c6bank/jobs",
            JobSourceKind.Greenhouse,
            null,
            360,
            Now);

        var jobs = await adapter.CollectAsync(source);

        var job = Assert.Single(jobs);
        Assert.Equal("C6 Bank", job.Company);
        Assert.Equal("São Paulo, Brazil", job.Location);
        Assert.Equal("4749401005", job.ExternalId);
        Assert.Contains(".NET e Azure", job.Description);
        Assert.DoesNotContain("<p>", job.Description);
        Assert.DoesNotContain("&lt;", job.Description);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 18, 43, 18, TimeSpan.Zero), job.PublishedAt);
        Assert.Contains("content=true", handler.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task Greenhouse_falls_back_to_the_board_token_when_company_name_is_missing()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "jobs": [
                {
                  "id": 1,
                  "title": "Tech Lead .NET",
                  "absolute_url": "https://job-boards.greenhouse.io/acme/jobs/1",
                  "location": { "name": "Remote - Brazil" },
                  "content": "<p>Vaga para liderar um time .NET.</p>",
                  "updated_at": "2026-11-01T09:00:00-03:00"
                }
              ]
            }
            """));

        var adapter = new GreenhouseJobSourceAdapter(
            handler.Factory,
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<GreenhouseJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Greenhouse - Acme",
            "https://boards-api.greenhouse.io/v1/boards/acme/jobs",
            JobSourceKind.Greenhouse,
            null,
            360,
            Now);

        var job = Assert.Single(await adapter.CollectAsync(source));
        Assert.Equal("acme", job.Company);
    }

    [Fact]
    public async Task Lever_maps_postings_using_the_epoch_date_and_the_site_token()
    {
        var handler = new StubHttpHandler(_ => Json("""
            [
              {
                "id": "lev-1",
                "text": "Senior Backend Engineer",
                "hostedUrl": "https://jobs.lever.co/acme/lev-1",
                "descriptionPlain": "We are hiring a backend engineer to work with .NET and Azure.",
                "categories": { "location": "Remote - Brazil", "team": "Platform" },
                "createdAt": 1762000000000
              }
            ]
            """));

        var adapter = new LeverJobSourceAdapter(
            handler.Factory,
            new JobPostingFilter(new JobProviderFilterOptions { LocationKeywords = ["brazil"] }),
            NullLogger<LeverJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Lever - Acme",
            "https://api.lever.co/v0/postings/acme",
            JobSourceKind.Lever,
            null,
            360,
            Now);

        var job = Assert.Single(await adapter.CollectAsync(source));
        Assert.Equal("Senior Backend Engineer", job.Title);
        Assert.Equal("acme", job.Company);
        Assert.Equal("Remote - Brazil", job.Location);
        Assert.Equal("https://jobs.lever.co/acme/lev-1", job.SourceUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1762000000000), job.PublishedAt);
        Assert.Contains("mode=json", handler.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task Lever_uses_the_company_label_from_the_source_url_when_provided()
    {
        var handler = new StubHttpHandler(_ => Json("""
            [
              {
                "id": "lev-2",
                "text": "Pessoa Desenvolvedora .NET",
                "hostedUrl": "https://jobs.lever.co/acme/lev-2",
                "descriptionPlain": "Vaga para atuar com .NET em São Paulo.",
                "categories": { "location": "São Paulo, Brazil" },
                "createdAt": 1762000000000
              }
            ]
            """));

        var adapter = new LeverJobSourceAdapter(
            handler.Factory,
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<LeverJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Lever - Acme Brasil",
            "https://api.lever.co/v0/postings/acme?company=Acme%20Brasil",
            JobSourceKind.Lever,
            null,
            360,
            Now);

        var job = Assert.Single(await adapter.CollectAsync(source));
        Assert.Equal("Acme Brasil", job.Company);
    }

    private static JobProviderOptions Options(string? appId, string? appKey) => new()
    {
        Adzuna = new AdzunaOptions { AppId = appId, AppKey = appKey }
    };

    [Fact]
    public async Task Adzuna_rebuilds_the_search_from_the_candidate_plan()
    {
        var handler = new StubHttpHandler(_ => Json("""{ "count": 0, "results": [] }"""));

        var adapter = new AdzunaJobSourceAdapter(
            handler.Factory,
            Options(appId: "app-id", appKey: "app-key"),
            new JobPostingFilter(new JobProviderFilterOptions { TitleKeywords = [".net"] }),
            NullLogger<AdzunaJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Adzuna BR - .NET",
            "https://api.adzuna.com/v1/api/jobs/br/search/1?what=desenvolvedor%20.net&where=S%C3%A3o%20Paulo",
            JobSourceKind.Adzuna,
            null,
            360,
            Now);

        await adapter.CollectAsync(source, CandidatePlan(acceptsRemote: true));

        var query = Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.Query);
        var parameters = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => pair[0],
                pair => pair.Length > 1 ? pair[1] : string.Empty,
                StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Tech Lead", parameters["what"]);
        Assert.Equal(".NET C#", parameters["what_or"]);
        Assert.False(parameters.ContainsKey("where"));
        Assert.DoesNotContain("desenvolvedor", query);
        Assert.Equal("app-id", parameters["app_id"]);
        Assert.Equal("app-key", parameters["app_key"]);
    }

    [Fact]
    public async Task Jooble_posts_the_plan_keywords_and_location_instead_of_the_static_url()
    {
        var handler = new StubHttpHandler(_ => Json("""{ "totalCount": 0, "jobs": [] }"""));

        var adapter = new JoobleJobSourceAdapter(
            handler.Factory,
            new JobProviderOptions { Jooble = new JoobleOptions { ApiKey = "test-key" } },
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<JoobleJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Jooble BR - .NET",
            "https://br.jooble.org/api/?keywords=desenvolvedor%20.net&location=Brasil",
            JobSourceKind.Jooble,
            null,
            360,
            Now);

        await adapter.CollectAsync(source, CandidatePlan(acceptsRemote: true));

        Assert.Equal("https://br.jooble.org/api/test-key", handler.LastRequest?.RequestUri?.ToString());
        Assert.Contains("\"keywords\":\"Tech Lead .NET C#\"", handler.LastBody);
        Assert.Contains("\"location\":\"Brasil\"", handler.LastBody);
    }

    [Fact]
    public async Task Greenhouse_uses_the_plan_keywords_instead_of_the_global_filter()
    {
        var handler = new StubHttpHandler(_ => Json("""
            {
              "jobs": [
                {
                  "id": 1,
                  "title": "Pessoa Desenvolvedora .NET Sênior",
                  "absolute_url": "https://job-boards.greenhouse.io/acme/jobs/1",
                  "company_name": "Acme",
                  "location": { "name": "São Paulo, Brazil" },
                  "content": "<p>Vaga para atuar com .NET.</p>"
                },
                {
                  "id": 2,
                  "title": "Analista de Vendas",
                  "absolute_url": "https://job-boards.greenhouse.io/acme/jobs/2",
                  "company_name": "Acme",
                  "location": { "name": "São Paulo, Brazil" },
                  "content": "<p>Vaga comercial.</p>"
                }
              ]
            }
            """));

        // The global filter accepts everything; only the plan may trim the board.
        var adapter = new GreenhouseJobSourceAdapter(
            handler.Factory,
            new JobPostingFilter(new JobProviderFilterOptions()),
            NullLogger<GreenhouseJobSourceAdapter>.Instance);

        var source = JobSource.Create(
            "Greenhouse - Acme",
            "https://boards-api.greenhouse.io/v1/boards/acme/jobs",
            JobSourceKind.Greenhouse,
            null,
            360,
            Now);

        var jobs = await adapter.CollectAsync(source, CandidatePlan(acceptsRemote: true));

        var job = Assert.Single(jobs);
        Assert.Equal("Pessoa Desenvolvedora .NET Sênior", job.Title);
    }

    private static JobSearchPlan CandidatePlan(bool acceptsRemote)
    {
        var profile = CandidateProfile.Create(
            "Rafael Makoto Inoue",
            EmailAddress.Create("rafael.inoue@example.com"),
            "Tech Lead .NET",
            null,
            CandidateLocation.Create("São Paulo", "SP", "Brasil"),
            yearsOfExperience: 19,
            linkedInUrl: null,
            Now);

        profile.AddCareerTarget("Tech Lead", SeniorityLevel.Lead, isPrimary: true, notes: null, Now);
        profile.AddSkill(".NET", SkillCategory.Technical, 5, 15, Now);
        profile.AddSkill("C#", SkillCategory.Technical, 5, 15, Now);
        profile.UpdateWorkPreferences(WorkPreferences.Create(acceptsRemote, true, false, true, true), Now);

        return JobSearchPlan.FromProfile(profile);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        public IHttpClientFactory Factory => new StubHttpClientFactory(this);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
