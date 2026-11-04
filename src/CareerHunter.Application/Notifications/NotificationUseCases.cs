using System.Net;
using System.Text;
using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Application.Notifications;

public sealed record ConfigureNotificationPreferenceCommand(
    Guid CandidateProfileId,
    string Email,
    bool IsEnabled,
    int SendHourLocal,
    string TimeZoneId,
    int MinimumScore);

public sealed class ConfigureNotificationPreferenceUseCase(
    INotificationPreferenceRepository preferences,
    ICandidateProfileRepository candidates,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(
        ConfigureNotificationPreferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await candidates.ExistsAsync(command.CandidateProfileId, cancellationToken))
        {
            throw new NotFoundException("CandidateProfile", command.CandidateProfileId);
        }

        var email = Domain.SharedKernel.EmailAddress.Create(command.Email);
        var preference = await preferences.GetByCandidateAsync(command.CandidateProfileId, cancellationToken);

        if (preference is null)
        {
            preference = NotificationPreference.Create(
                command.CandidateProfileId,
                email,
                command.IsEnabled,
                command.SendHourLocal,
                command.TimeZoneId,
                command.MinimumScore,
                clock.UtcNow);

            preferences.Add(preference);
        }
        else
        {
            preference.Update(
                email,
                command.IsEnabled,
                command.SendHourLocal,
                command.TimeZoneId,
                command.MinimumScore,
                clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return preference.Id;
    }
}

public sealed record GenerateDailyDigestCommand(Guid CandidateProfileId, DateOnly? DigestDate = null);

/// <summary>
/// Builds the digest for one candidate. One digest per candidate per day: a second call returns
/// the existing digest instead of creating a duplicate notification.
/// </summary>
public sealed class GenerateDailyDigestUseCase(
    INotificationPreferenceRepository preferences,
    IDailyDigestRepository digests,
    IJobEvaluationQueries evaluations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<DigestGenerationResultDto> ExecuteAsync(
        GenerateDailyDigestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var preference = await preferences.GetByCandidateAsync(command.CandidateProfileId, cancellationToken)
                         ?? throw new NotFoundException("NotificationPreference", command.CandidateProfileId);

        var digestDate = command.DigestDate ?? clock.Today;
        var existing = await digests.GetForDateAsync(preference.CandidateProfileId, digestDate, cancellationToken);

        if (existing is not null)
        {
            return new DigestGenerationResultDto(
                existing.Id,
                existing.DigestDate,
                existing.Status,
                existing.Items.Count,
                AlreadyExisted: true);
        }

        var since = new DateTimeOffset(digestDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var top = await evaluations.ListForDigestAsync(
            preference.CandidateProfileId,
            since,
            preference.MinimumScore,
            take: 20,
            cancellationToken);

        var digest = DailyDigest.Create(
            preference.CandidateProfileId,
            digestDate,
            top.Select(item => new DigestItemInput(
                item.Id,
                item.JobOpportunityId,
                item.JobTitle,
                item.EmployerName,
                item.OverallScore,
                item.Recommendation,
                item.JobUrl)),
            clock.Today,
            clock.UtcNow);

        digests.Add(digest);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DigestGenerationResultDto(
            digest.Id,
            digest.DigestDate,
            digest.Status,
            digest.Items.Count,
            AlreadyExisted: false);
    }
}

public sealed record SendDailyDigestCommand(Guid DailyDigestId);

public sealed class SendDailyDigestUseCase(
    IDailyDigestRepository digests,
    INotificationPreferenceRepository preferences,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<SendDailyDigestUseCase> logger)
{
    public async Task<DigestDeliveryResultDto> ExecuteAsync(
        SendDailyDigestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var digest = await digests.GetAsync(command.DailyDigestId, cancellationToken)
                     ?? throw new NotFoundException("DailyDigest", command.DailyDigestId);

        if (digest.Status == DigestStatus.Sent)
        {
            // Idempotent: never send the same digest twice.
            return new DigestDeliveryResultDto(digest.Id, digest.Status, true, digest.AttemptCount, null);
        }

        if (!digest.CanBeSent)
        {
            return new DigestDeliveryResultDto(
                digest.Id,
                digest.Status,
                false,
                digest.AttemptCount,
                digest.LastError ?? "The digest cannot be sent anymore.");
        }

        var preference = await preferences.GetByCandidateAsync(digest.CandidateProfileId, cancellationToken)
                         ?? throw new NotFoundException("NotificationPreference", digest.CandidateProfileId);

        if (!preference.IsEnabled)
        {
            throw new BusinessRuleException("E-mail notifications are disabled for this candidate.");
        }

        var message = DailyDigestEmailBuilder.Build(digest, preference);

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
            digest.RecordDeliveryAttempt(emailSender.Provider, succeeded: true, error: null, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Digest {DigestId} delivered using {Provider}.", digest.Id, emailSender.Provider);
            return new DigestDeliveryResultDto(digest.Id, digest.Status, true, digest.AttemptCount, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Delivery failure must never affect evaluations or digest items.
            logger.LogWarning(exception, "Digest {DigestId} delivery failed.", digest.Id);
            digest.RecordDeliveryAttempt(emailSender.Provider, succeeded: false, exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new DigestDeliveryResultDto(
                digest.Id,
                digest.Status,
                false,
                digest.AttemptCount,
                digest.LastError);
        }
    }
}

/// <summary>
/// Renders the digest e-mail exactly as it would be delivered, without sending anything.
/// It reuses the same builder as the sender, so the preview cannot drift from the real message.
/// </summary>
public sealed record DigestEmailPreviewDto(
    Guid DailyDigestId,
    string Subject,
    IReadOnlyList<string> To,
    string TextBody,
    string HtmlBody);

public sealed class PreviewDailyDigestUseCase(
    IDailyDigestRepository digests,
    INotificationPreferenceRepository preferences)
{
    public async Task<DigestEmailPreviewDto> ExecuteAsync(
        Guid dailyDigestId,
        CancellationToken cancellationToken = default)
    {
        var digest = await digests.GetAsync(dailyDigestId, cancellationToken)
                     ?? throw new NotFoundException("DailyDigest", dailyDigestId);

        var preference = await preferences.GetByCandidateAsync(digest.CandidateProfileId, cancellationToken)
                         ?? throw new NotFoundException("NotificationPreference", digest.CandidateProfileId);

        var message = DailyDigestEmailBuilder.Build(digest, preference);

        return new DigestEmailPreviewDto(
            digest.Id,
            message.Subject,
            message.To,
            message.TextBody,
            message.HtmlBody);
    }
}

/// <summary>
/// Renders the digest e-mail. Every value that comes from an external job source is HTML-encoded
/// and newlines are stripped from the subject to prevent header/content injection.
/// </summary>
public static class DailyDigestEmailBuilder
{
    public static EmailMessage Build(DailyDigest digest, NotificationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentNullException.ThrowIfNull(preference);

        var subject = $"Career Hunter — {digest.Items.Count} oportunidade(s) em {digest.DigestDate:dd/MM/yyyy}";

        var text = new StringBuilder();
        text.AppendLine($"Relatório diário de {digest.DigestDate:dd/MM/yyyy}");
        text.AppendLine();

        if (digest.Items.Count == 0)
        {
            text.AppendLine("Nenhuma oportunidade atingiu a pontuação mínima configurada hoje.");
        }

        foreach (var item in digest.Items.OrderBy(item => item.Rank))
        {
            text.AppendLine($"{item.Rank}. {item.JobTitle} — {item.EmployerName} (score {item.Score}, {item.Recommendation})");
            text.AppendLine($"   Link original: {item.JobUrl}");
            text.AppendLine();
        }

        text.AppendLine("Revise cada oportunidade e candidate-se manualmente pelo link original. Nenhuma candidatura é enviada automaticamente.");

        var html = new StringBuilder();
        html.Append("<h2>Relatório diário do Career Hunter</h2>");
        html.Append($"<p>Data: {digest.DigestDate:dd/MM/yyyy}</p>");

        if (digest.Items.Count == 0)
        {
            html.Append("<p>Nenhuma oportunidade atingiu a pontuação mínima configurada hoje.</p>");
        }
        else
        {
            html.Append("<ol>");
            foreach (var item in digest.Items.OrderBy(item => item.Rank))
            {
                html.Append("<li>");
                html.Append($"<strong>{WebUtility.HtmlEncode(item.JobTitle)}</strong> — {WebUtility.HtmlEncode(item.EmployerName)}");
                html.Append($"<br/>Score: {item.Score} ({WebUtility.HtmlEncode(item.Recommendation.ToString())})");
                html.Append($"<br/><a href=\"{WebUtility.HtmlEncode(item.JobUrl)}\">Abrir vaga original</a>");
                html.Append("</li>");
            }

            html.Append("</ol>");
        }

        html.Append("<p><em>Revise e candidate-se manualmente. Nenhuma candidatura é enviada automaticamente.</em></p>");

        return new EmailMessage(
            [preference.Email.Value],
            subject.ReplaceLineEndings(" "),
            text.ToString(),
            html.ToString());
    }
}
