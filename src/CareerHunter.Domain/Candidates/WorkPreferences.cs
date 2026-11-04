using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

/// <summary>
/// Declared work arrangements and contract types. Missing information is never
/// assumed: the candidate must accept at least one modality and one contract type.
/// </summary>
public sealed class WorkPreferences
{
    private WorkPreferences()
    {
    }

    private WorkPreferences(
        bool acceptsRemote,
        bool acceptsHybrid,
        bool acceptsOnsite,
        bool acceptsClt,
        bool acceptsPj)
    {
        AcceptsRemote = acceptsRemote;
        AcceptsHybrid = acceptsHybrid;
        AcceptsOnsite = acceptsOnsite;
        AcceptsClt = acceptsClt;
        AcceptsPj = acceptsPj;
    }

    public bool AcceptsRemote { get; private set; }

    public bool AcceptsHybrid { get; private set; }

    public bool AcceptsOnsite { get; private set; }

    public bool AcceptsClt { get; private set; }

    public bool AcceptsPj { get; private set; }

    public static WorkPreferences Create(
        bool acceptsRemote,
        bool acceptsHybrid,
        bool acceptsOnsite,
        bool acceptsClt,
        bool acceptsPj)
    {
        if (!acceptsRemote && !acceptsHybrid && !acceptsOnsite)
        {
            throw new DomainValidationException("workPreferences", "At least one work modality must be accepted.");
        }

        if (!acceptsClt && !acceptsPj)
        {
            throw new DomainValidationException("workPreferences", "At least one contract type (CLT or PJ) must be accepted.");
        }

        return new WorkPreferences(acceptsRemote, acceptsHybrid, acceptsOnsite, acceptsClt, acceptsPj);
    }

    public bool Accepts(WorkModality modality) => modality switch
    {
        WorkModality.Remote => AcceptsRemote,
        WorkModality.Hybrid => AcceptsHybrid,
        WorkModality.Onsite => AcceptsOnsite,
        _ => false
    };

    public bool Accepts(EmploymentContractType contract) => contract switch
    {
        EmploymentContractType.Clt => AcceptsClt,
        EmploymentContractType.Pj => AcceptsPj,
        _ => false
    };

    public IReadOnlyList<WorkModality> Modalities()
    {
        var modalities = new List<WorkModality>(3);
        if (AcceptsRemote)
        {
            modalities.Add(WorkModality.Remote);
        }

        if (AcceptsHybrid)
        {
            modalities.Add(WorkModality.Hybrid);
        }

        if (AcceptsOnsite)
        {
            modalities.Add(WorkModality.Onsite);
        }

        return modalities;
    }
}
