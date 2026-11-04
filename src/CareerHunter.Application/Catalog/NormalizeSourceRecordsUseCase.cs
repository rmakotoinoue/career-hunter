using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Application.Discovery;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Application.Catalog;

/// <summary>
/// Converts pending raw records into normalized opportunities.
/// Rules: employer must be identifiable, provenance is preserved, duplicate opportunities are
/// merged instead of duplicated, and a record that cannot be normalized is rejected with a reason.
/// </summary>
public sealed class NormalizeSourceRecordsUseCase(
    IEmployerRepository employers,
    IJobOpportunityRepository opportunities,
    ISourceJobRecordRepository records,
    IJobSourceRepository jobSources,
    IUnitOfWork unitOfWork,
    IClock clock,
    JobNormalizer normalizer,
    ILogger<NormalizeSourceRecordsUseCase> logger)
{
    private const int MaxReasonLength = 480;

    public async Task<NormalizationResultDto> ExecuteAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var pending = await records.ListPendingAsync(Math.Clamp(take, 1, 500), cancellationToken);
        if (pending.Count == 0)
        {
            return new NormalizationResultDto(0, 0, 0, []);
        }

        var sources = (await jobSources.ListAsync(onlyEnabled: false, cancellationToken: cancellationToken))
            .ToDictionary(source => source.Id);
        var rejectionReasons = new List<string>();
        var normalized = 0;
        var rejected = 0;

        foreach (var record in pending)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.RawCompany))
                {
                    rejected += await RejectAsync(record, "Employer could not be identified from the source content.", cancellationToken);
                    continue;
                }

                var now = clock.UtcNow;
                var data = normalizer.Normalize(record.RawTitle, record.RawLocation, record.RawDescription);
                var employerName = TextNormalizer.Company(record.RawCompany);

                var employer = await employers.GetByNormalizedNameAsync(employerName, cancellationToken);
                if (employer is null)
                {
                    employer = Employer.Create(record.RawCompany, null, now);
                    employers.Add(employer);
                }

                var deduplicationKey = JobOpportunity.BuildDeduplicationKey(employer.NormalizedName, data.Title, data.Location);
                var opportunity = await opportunities.GetByDeduplicationKeyAsync(deduplicationKey, cancellationToken);

                if (opportunity is null)
                {
                    opportunity = JobOpportunity.Create(
                        employer.Id,
                        employer.NormalizedName,
                        data.Title,
                        data.Location,
                        data.Description,
                        now,
                        data.Modality,
                        data.ContractType,
                        data.Seniority,
                        data.Compensation,
                        record.PublishedAt);

                    opportunities.Add(opportunity);
                }
                else
                {
                    // Never replace known data with unknown data.
                    opportunity.UpdateNormalizedData(
                        data.Title,
                        data.Location,
                        data.Modality ?? opportunity.Modality,
                        data.ContractType ?? opportunity.ContractType,
                        data.Seniority ?? opportunity.Seniority,
                        data.Compensation ?? opportunity.Compensation,
                        data.Description,
                        record.PublishedAt ?? opportunity.PublishedAt,
                        now);
                }

                if (data.Requirements.Count > 0)
                {
                    opportunity.ReplaceRequirements(data.Requirements, now);
                }

                opportunity.AddSourceReference(
                    new JobSourceReferenceInput(
                        record.Id,
                        sources.TryGetValue(record.JobSourceId, out var source) ? source.Name : "unknown",
                        record.SourceUrl,
                        record.PublishedAt,
                        record.CollectedAt),
                    now);

                record.MarkNormalized(opportunity.Id, now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                normalized++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DomainException exception)
            {
                rejected += await RejectAsync(record, exception.Message, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure normalizing source record {RecordId}.", record.Id);
                rejected += await RejectAsync(record, "Unexpected failure while normalizing this record.", cancellationToken);
            }
        }

        return new NormalizationResultDto(pending.Count, normalized, rejected, rejectionReasons);

        async Task<int> RejectAsync(Domain.Discovery.SourceJobRecord target, string reason, CancellationToken ct)
        {
            var bounded = reason.Length > MaxReasonLength ? reason[..MaxReasonLength] : reason;
            target.MarkRejected(bounded, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            rejectionReasons.Add(bounded);
            return 1;
        }
    }
}
