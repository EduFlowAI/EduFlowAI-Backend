using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.AI.Application.DTOs;
using EduFlowAI.AI.Application.Interfaces;

namespace EduFlowAI.AI.Application.Services;

// Answers a "status" chat intent. Speaks as ITI, in the applicant's language, from a fixed set of
// replies - no model call, so it costs nothing and reads the same every time.
public class StatusAnswerService : IStatusAnswerService
{
    private readonly IApplicationStatusReader _statusReader;

    public StatusAnswerService(IApplicationStatusReader statusReader)
    {
        _statusReader = statusReader ?? throw new ArgumentNullException(nameof(statusReader));
    }

    public async Task<StatusAnswerDto> AnswerStatusAsync(
        string? userId,
        string language,
        CancellationToken cancellationToken = default)
    {
        // Guests can't have a status - they must register to apply. The reader is never touched.
        if (string.IsNullOrWhiteSpace(userId))
            return new StatusAnswerDto { RequiresSignIn = true, Answer = Localize(SignIn, language) };

        var status = await _statusReader.GetCurrentStatusAsync(userId, cancellationToken);

        if (status is null)
            return new StatusAnswerDto { Answer = Localize(NoApplication, language) };

        var message = Messages.TryGetValue(status.Status, out var known) ? known : Default;
        return new StatusAnswerDto { Answer = Localize(message, language) };
    }

    private static string Localize((string En, string Ar) message, string language) =>
        language == "ar" ? message.Ar : message.En;

    private static readonly (string En, string Ar) SignIn = (
        "You'll need to sign in to your account to check your application status.",
        "يلزم تسجيل الدخول إلى حسابك لمعرفة حالة طلبك.");

    private static readonly (string En, string Ar) NoApplication = (
        "You don't have an application yet. You can apply to a track to start one.",
        "لا يوجد لديك طلب حتى الآن. يمكنك التقديم على أحد المسارات لبدء طلب.");

    // Shown for None or any status value this module doesn't recognise, so a new/renamed status
    // never throws - it just gets a safe, neutral reply.
    private static readonly (string En, string Ar) Default = (
        "Your application is being processed. For more details, please contact the admissions office.",
        "طلبك قيد المعالجة. لمزيد من التفاصيل، يرجى التواصل مع مكتب القبول.");

    // Keyed by the Admission ApplicationStatus enum name (case-insensitive).
    private static readonly Dictionary<string, (string En, string Ar)> Messages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Draft"] = (
            "Your application is still a draft and has not been submitted. Please complete and submit it to be considered.",
            "طلبك ما زال مسودة ولم يتم تقديمه. يرجى استكماله وتقديمه ليتم النظر فيه."),
        ["DocumentsRequired"] = (
            "Some required documents are still missing. You can upload them from your account.",
            "لا تزال بعض المستندات المطلوبة ناقصة. يمكنك رفعها من حسابك."),
        ["EligibilityFailed"] = (
            "Your application did not meet the eligibility requirements for this cycle. You are welcome to apply again in a future cycle.",
            "لم يستوفِ طلبك شروط الأهلية لهذه الدورة. يمكنك التقديم مرة أخرى في دورة قادمة."),
        ["UnderDocumentVerification"] = (
            "Your documents are currently being verified. We will notify you once the review is complete.",
            "يتم حالياً التحقق من مستنداتك. سنبلغك عند اكتمال المراجعة."),
        ["NeedsHumanReview"] = (
            "Your application is under review by the admissions team. We will contact you once the review is complete.",
            "طلبك قيد المراجعة من قبل فريق القبول. سنتواصل معك عند انتهاء المراجعة."),
        ["DocumentRejected"] = (
            "One or more of your documents were not accepted. Please review the notes and re-upload them.",
            "لم يتم قبول واحد أو أكثر من مستنداتك. يرجى مراجعة الملاحظات وإعادة رفعها."),
        ["AssessmentInProgress"] = (
            "Your assessment is in progress. We will share the result once it is finalized.",
            "تقييم طلبك قيد التنفيذ. سنبلغك بالنتيجة عند الانتهاء منها."),
        ["Admitted"] = (
            "You have been admitted. Congratulations, and welcome to ITI.",
            "تم قبولك. مبروك، ومرحباً بك في معهد تكنولوجيا المعلومات."),
        ["Waitlisted"] = (
            "You are currently on the waiting list. We will contact you if a place becomes available.",
            "أنت حالياً على قائمة الانتظار. سنتواصل معك إذا توفر مكان."),
        ["NotSelected"] = (
            "You were not selected in this cycle. Thank you for applying, and you are welcome to apply again in a future cycle.",
            "لم يتم اختيارك في هذه الدورة. نشكر لك تقديمك، ويمكنك التقديم مرة أخرى في دورة قادمة."),
        ["Withdrawn"] = (
            "Your application has been withdrawn. If this was not intended, please contact the admissions office.",
            "تم سحب طلبك. إذا لم يكن ذلك مقصوداً، يرجى التواصل مع مكتب القبول."),
        ["Expired"] = (
            "Your application has expired. Please contact the admissions office, or apply again in the next cycle.",
            "انتهت صلاحية طلبك. يرجى التواصل مع مكتب القبول، أو التقديم مرة أخرى في الدورة القادمة."),
    };
}
