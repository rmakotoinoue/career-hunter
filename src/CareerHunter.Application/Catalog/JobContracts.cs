using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Catalog;

public sealed record JobRequirementDto(string Name, JobRequirementCategory Category, bool IsRequired);

public sealed record JobSourceReferenceDto(
    Guid SourceJobRecordId,
    string JobSourceName,
    string SourceUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CollectedAt);

public sealed record JobCompensationDto(
    decimal? MinAmount,
    decimal? MaxAmount,
    string Currency,
    SalaryPeriod Period,
    bool IsEstimated,
    bool IsKnown);

public sealed record JobOpportunitySummaryDto(
    Guid Id,
    string Title,
    string EmployerName,
    string Location,
    WorkModality? Modality,
    EmploymentContractType? ContractType,
    SeniorityLevel? Seniority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Currency,
    JobOpportunityStatus Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset LastSeenAt);

public sealed record JobOpportunityDto(
    Guid Id,
    string Title,
    Guid EmployerId,
    string EmployerName,
    string? EmployerWebsite,
    string City,
    string? State,
    string Country,
    string Location,
    WorkModality? Modality,
    EmploymentContractType? ContractType,
    SeniorityLevel? Seniority,
    JobCompensationDto? Compensation,
    string Description,
    DateTimeOffset? PublishedAt,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    JobOpportunityStatus Status,
    DateTimeOffset? ExpiredAt,
    IReadOnlyList<JobRequirementDto> Requirements,
    IReadOnlyList<JobSourceReferenceDto> Sources);

public sealed record JobSearchFilter(
    string? Query = null,
    WorkModality? Modality = null,
    EmploymentContractType? ContractType = null,
    SeniorityLevel? Seniority = null,
    bool OnlyActive = true,
    int Page = 1,
    int PageSize = 20);
