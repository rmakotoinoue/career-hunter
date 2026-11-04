using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Notifications;

public enum DigestStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

/// <summary>
/// E-mail delivery preference for one candidate. Delivery infrastructure is not referenced
/// here: the domain only knows *what* to send and *when*, not *how*.
/// </summary>
public sealed class NotificationPreference : AggregateRoot
{
    private NotificationPreference()
    {
        TimeZoneId = string.Empty;
    }

    private NotificationPreference(
        Guid candidateProfileId,
        EmailAddress email,
        bool isEnabled,
        int sendHourLocal,
        string timeZoneId,
        int minimumScore,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        CandidateProfileId = candidateProfileId != Guid.Empty
            ? candidateProfileId
            : throw new DomainValidationException("candidateProfileId", "Candidate profile is required.");

        Email = email ?? throw new DomainValidationException("email", "E-mail is required.");
        IsEnabled = isEnabled;
        SendHourLocal = Guard.InRange(sendHourLocal, "sendHourLocal", 0, 23);
        TimeZoneId = ValidateTimeZone(timeZoneId);
        MinimumScore = Guard.InRange(minimumScore, "minimumScore", 0, 100);
    }

    public Guid CandidateProfileId { get; private set; }

    public EmailAddress Email { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Local hour (0-23) at which the digest should be produced.
    /// </summary>
    public int SendHourLocal { get; private set; }

    /// <summary>
    /// IANA or Windows time zone identifier, validated against the host time zone database.
    /// </summary>
    public string TimeZoneId { get; private set; }

    /// <summary>
    /// Evaluations below this score are not included in the digest.
    /// </summary>
    public int MinimumScore { get; private set; }

    public static NotificationPreference Create(
        Guid candidateProfileId,
        EmailAddress email,
        bool isEnabled,
        int sendHourLocal,
        string timeZoneId,
        int minimumScore,
        DateTimeOffset now)
        => new(candidateProfileId, email, isEnabled, sendHourLocal, timeZoneId, minimumScore, now);

    public void Update(
        EmailAddress email,
        bool isEnabled,
        int sendHourLocal,
        string timeZoneId,
        int minimumScore,
        DateTimeOffset now)
    {
        Email = email ?? throw new DomainValidationException("email", "E-mail is required.");
        IsEnabled = isEnabled;
        SendHourLocal = Guard.InRange(sendHourLocal, "sendHourLocal", 0, 23);
        TimeZoneId = ValidateTimeZone(timeZoneId);
        MinimumScore = Guard.InRange(minimumScore, "minimumScore", 0, 100);
        Touch(now);
    }

    private static string ValidateTimeZone(string? timeZoneId)
    {
        var normalized = Guard.NotBlank(timeZoneId, "timeZoneId", 100);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainValidationException("timeZoneId", "Unknown time zone identifier.");
        }

        return normalized;
    }
}

public sealed record DigestItemInput(
    Guid JobEvaluationId,
    Guid JobOpportunityId,
    string JobTitle,
    string EmployerName,
    int Score,
    EvaluationRecommendation Recommendation,
    string JobUrl);

/// <summary>
/// One daily report. Owns its items and every delivery attempt so a failing provider can
/// be retried without losing the evaluations it references.
/// </summary>
public sealed class DailyDigest : AggregateRoot
{
    public const int MaxDeliveryAttempts = 3;

    private readonly List<DailyDigestItem> _items = [];
    private readonly List<NotificationDelivery> _deliveries = [];

    private DailyDigest()
    {
        DigestDate = default;
    }

    private DailyDigest(
        Guid candidateProfileId,
        DateOnly digestDate,
        IEnumerable<DigestItemInput> items,
        DateOnly today,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        CandidateProfileId = candidateProfileId != Guid.Empty
            ? candidateProfileId
            : throw new DomainValidationException("candidateProfileId", "Candidate profile is required.");

        if (digestDate == default)
        {
            throw new DomainValidationException("digestDate", "Digest date is required.");
        }

        if (digestDate > today)
        {
            throw new DomainValidationException("digestDate", "A digest cannot be created for a future date.");
        }

        DigestDate = digestDate;
        GeneratedAt = Guard.UtcTimestamp(now, "generatedAt");
        Status = DigestStatus.Pending;

        var rank = 1;
        foreach (var item in items
                     .Where(item => item.JobEvaluationId != Guid.Empty && item.JobOpportunityId != Guid.Empty)
                     .OrderByDescending(item => item.Score)
                     .ThenBy(item => item.JobTitle, StringComparer.Ordinal))
        {
            _items.Add(DailyDigestItem.Create(Id, item, rank++, now));
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public DateOnly DigestDate { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }

    public DigestStatus Status { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public int AttemptCount => _deliveries.Count;

    public IReadOnlyList<DailyDigestItem> Items => _items;

    public IReadOnlyList<NotificationDelivery> Deliveries => _deliveries;

    public bool CanBeSent => Status == DigestStatus.Pending && AttemptCount < MaxDeliveryAttempts;

    public static DailyDigest Create(
        Guid candidateProfileId,
        DateOnly digestDate,
        IEnumerable<DigestItemInput> items,
        DateOnly today,
        DateTimeOffset now)
        => new(candidateProfileId, digestDate, items, today, now);

    public void RecordDeliveryAttempt(string provider, bool succeeded, string? error, DateTimeOffset now)
    {
        if (Status == DigestStatus.Sent)
        {
            throw new DomainException("The digest was already delivered. Duplicate notifications are not allowed.");
        }

        if (AttemptCount >= MaxDeliveryAttempts)
        {
            throw new DomainException("The digest reached the maximum number of delivery attempts.");
        }

        var delivery = NotificationDelivery.Create(Id, provider, succeeded, error, now);
        _deliveries.Add(delivery);

        if (succeeded)
        {
            Status = DigestStatus.Sent;
            SentAt = delivery.AttemptedAt;
            LastError = null;
        }
        else
        {
            LastError = delivery.ErrorMessage;
            Status = AttemptCount >= MaxDeliveryAttempts ? DigestStatus.Failed : DigestStatus.Pending;
        }

        Touch(now);
    }
}

public sealed class DailyDigestItem : Entity
{
    private DailyDigestItem()
    {
        JobTitle = string.Empty;
        EmployerName = string.Empty;
        JobUrl = string.Empty;
    }

    private DailyDigestItem(Guid dailyDigestId, DigestItemInput input, int rank, DateTimeOffset now)
    {
        DailyDigestId = dailyDigestId;
        JobEvaluationId = input.JobEvaluationId;
        JobOpportunityId = input.JobOpportunityId;
        JobTitle = Guard.NotBlank(input.JobTitle, "jobTitle", 200);
        EmployerName = Guard.NotBlank(input.EmployerName, "employerName", 200);
        Score = Guard.InRange(input.Score, "score", 0, 100);
        Recommendation = input.Recommendation;
        JobUrl = Guard.AbsoluteHttpUrl(input.JobUrl, "jobUrl");
        Rank = Guard.InRange(rank, "rank", 1, 500);
        CreatedAt = Guard.UtcTimestamp(now, "createdAt");
    }

    public Guid DailyDigestId { get; private set; }

    public Guid JobEvaluationId { get; private set; }

    public Guid JobOpportunityId { get; private set; }

    public string JobTitle { get; private set; }

    public string EmployerName { get; private set; }

    public int Score { get; private set; }

    public EvaluationRecommendation Recommendation { get; private set; }

    /// <summary>
    /// Original application URL, preserved for the candidate.
    /// </summary>
    public string JobUrl { get; private set; }

    public int Rank { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static DailyDigestItem Create(Guid dailyDigestId, DigestItemInput input, int rank, DateTimeOffset now)
        => new(dailyDigestId, input, rank, now);
}

public sealed class NotificationDelivery : Entity
{
    private NotificationDelivery()
    {
        Provider = string.Empty;
    }

    private NotificationDelivery(
        Guid dailyDigestId,
        string provider,
        bool succeeded,
        string? errorMessage,
        DateTimeOffset attemptedAt)
    {
        DailyDigestId = dailyDigestId;
        Provider = Guard.NotBlank(provider, "provider", 80);
        Succeeded = succeeded;
        // Provider errors are external text: keep it bounded and on a single line.
        ErrorMessage = errorMessage is null
            ? null
            : Guard.Optional(errorMessage.Replace('\r', ' ').Replace('\n', ' '), "errorMessage", 2000);
        AttemptedAt = Guard.UtcTimestamp(attemptedAt, "attemptedAt");
    }

    public Guid DailyDigestId { get; private set; }

    public string Provider { get; private set; }

    public bool Succeeded { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    internal static NotificationDelivery Create(
        Guid dailyDigestId,
        string provider,
        bool succeeded,
        string? errorMessage,
        DateTimeOffset attemptedAt)
        => new(dailyDigestId, provider, succeeded, errorMessage, attemptedAt);
}
