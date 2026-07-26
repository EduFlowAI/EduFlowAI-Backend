namespace EduFlowAI.Admission.Infrastructure.Seeding;

internal sealed record AdmissionTrackSeedDefinition(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> PrerequisiteTopics);

internal static class AdmissionTrackSeedCatalog
{
    internal static IReadOnlyList<AdmissionTrackSeedDefinition> All { get; } =
    [
        new(
            AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
            "Open Source Applications Development",
            "Build and maintain applications using open-source platforms, collaborative tooling, and modern software-development practices.",
            [
                "Programming Fundamentals",
                "Git",
                "Linux",
                "Web Fundamentals"
            ]),
        new(
            AdmissionSeedIds.AiAndMachineLearningTrackId,
            "AI and Machine Learning",
            "Develop applied machine-learning solutions using data preparation, statistical reasoning, model training, and evaluation.",
            [
                "Python",
                "Statistics",
                "Linear Algebra",
                "Machine Learning Fundamentals"
            ]),
        new(
            AdmissionSeedIds.SystemsAdministrationTrackId,
            "Systems Administration",
            "Operate, automate, monitor, and troubleshoot computing infrastructure across Linux and networked environments.",
            [
                "Operating Systems",
                "Linux",
                "Networking Fundamentals",
                "Scripting"
            ]),
        new(
            AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
            "Professional Development & BI-infused CRM",
            "Build business applications and CRM solutions that combine .NET development, data analysis, and business-process automation.",
            [
                "C#",
                "SQL",
                "Business Analysis",
                "CRM Fundamentals"
            ]),
        new(
            AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
            "Cloud Platform Development",
            "Develop cloud-ready services and applications using APIs, containers, automation, and managed platform capabilities.",
            [
                "Programming Fundamentals",
                "REST APIs",
                "Containers",
                "Cloud Fundamentals"
            ]),
        new(
            AdmissionSeedIds.CloudArchitectureTrackId,
            "Cloud Architecture",
            "Design resilient cloud systems by applying networking, infrastructure, security, and distributed-system principles.",
            [
                "Networking Fundamentals",
                "Linux",
                "Containers",
                "Distributed Systems"
            ]),
        new(
            AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
            "Integrated Software Development & Architecture",
            "Design and implement maintainable enterprise applications using layered architecture, integration patterns, and modern APIs.",
            [
                "Object-Oriented Programming",
                "Databases",
                "Design Patterns",
                "REST APIs"
            ]),
        new(
            AdmissionSeedIds.CyberSecurityTrackId,
            "Cyber Security",
            "Protect systems and networks through security analysis, secure configuration, vulnerability awareness, and incident fundamentals.",
            [
                "Networking Fundamentals",
                "Linux",
                "Operating Systems",
                "Security Fundamentals"
            ]),
        new(
            AdmissionSeedIds.EnterpriseJavaTrackId,
            "Enterprise & Web Applications Development (Java)",
            "Build enterprise and web applications using Java, object-oriented design, relational data, and service-based architectures.",
            [
                "Java",
                "Object-Oriented Programming",
                "SQL",
                "Web Fundamentals"
            ]),
        new(
            AdmissionSeedIds.TelecomApplicationDevelopmentTrackId,
            "Telecom Application Development",
            "Develop software for connected and telecom-oriented systems using networking, Linux, data structures, and reliable services.",
            [
                "Programming Fundamentals",
                "Computer Networks",
                "Linux",
                "Data Structures"
            ])
    ];
}
