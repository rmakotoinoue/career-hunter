namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Shared language between CandidateProfile, JobCatalog and JobAssessment.
/// </summary>
public enum WorkModality
{
    Remote = 1,
    Hybrid = 2,
    Onsite = 3
}

public enum EmploymentContractType
{
    Clt = 1,
    Pj = 2
}

public enum SeniorityLevel
{
    Intern = 1,
    Junior = 2,
    MidLevel = 3,
    Senior = 4,
    Lead = 5,
    Principal = 6,
    Architect = 7
}

public static class EnumLabels
{
    public static string Describe(this WorkModality modality) => modality switch
    {
        WorkModality.Remote => "Remoto",
        WorkModality.Hybrid => "Híbrido",
        WorkModality.Onsite => "Presencial",
        _ => modality.ToString()
    };

    public static string Describe(this EmploymentContractType contract) => contract switch
    {
        EmploymentContractType.Clt => "CLT",
        EmploymentContractType.Pj => "PJ",
        _ => contract.ToString()
    };

    public static string Describe(this SeniorityLevel seniority) => seniority switch
    {
        SeniorityLevel.Intern => "Estágio",
        SeniorityLevel.Junior => "Júnior",
        SeniorityLevel.MidLevel => "Pleno",
        SeniorityLevel.Senior => "Sênior",
        SeniorityLevel.Lead => "Liderança técnica",
        SeniorityLevel.Principal => "Principal",
        SeniorityLevel.Architect => "Arquiteto",
        _ => seniority.ToString()
    };
}
