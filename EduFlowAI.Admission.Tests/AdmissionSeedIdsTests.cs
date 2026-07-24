using EduFlowAI.Admission.Infrastructure.Seeding;

namespace EduFlowAI.Admission.Tests;

public sealed class AdmissionSeedIdsTests
{
    [Fact]
    public void Stable_seed_ids_are_nonempty_and_unique()
    {
        Guid[] ids =
        [
            AdmissionSeedIds.ItiInstitutionId,
            AdmissionSeedIds.NineMonthProgramId,
            AdmissionSeedIds.NationalIdRequirementId,
            AdmissionSeedIds.BirthCertificateRequirementId,
            AdmissionSeedIds.GraduationCertificateRequirementId,
            AdmissionSeedIds.MilitaryCertificateRequirementId,
            AdmissionSeedIds.OpenSourceApplicationsDevelopmentTrackId,
            AdmissionSeedIds.AiAndMachineLearningTrackId,
            AdmissionSeedIds.SystemsAdministrationTrackId,
            AdmissionSeedIds.ProfessionalDevelopmentCrmTrackId,
            AdmissionSeedIds.CloudPlatformDevelopmentTrackId,
            AdmissionSeedIds.CloudArchitectureTrackId,
            AdmissionSeedIds.IntegratedSoftwareArchitectureTrackId,
            AdmissionSeedIds.CyberSecurityTrackId,
            AdmissionSeedIds.EnterpriseJavaTrackId,
            AdmissionSeedIds.TelecomApplicationDevelopmentTrackId,
            AdmissionSeedIds.SmartVillageBranchId,
            AdmissionSeedIds.AlexandriaBranchId,
            AdmissionSeedIds.AssiutBranchId,
            AdmissionSeedIds.MansouraBranchId,
            AdmissionSeedIds.ZagazigBranchId
        ];

        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
