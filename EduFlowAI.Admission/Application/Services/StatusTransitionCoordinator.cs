using EduFlowAI.Admission.Application.DTOs;
using EduFlowAI.Admission.Application.Interfaces.Repositories;
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Admission.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace EduFlowAI.Admission.Application.Services
{
    internal class StatusTransitionCoordinator : IStatusTransitionCoordinator
    {
        private readonly IApplicationRepository _applicationRepository;

        public StatusTransitionCoordinator(IApplicationRepository applicationRepository)
        {
            _applicationRepository = applicationRepository;
        }

        public async Task<(bool IsSuccess, string ErrorMessage, ApplicationStatusDto? Data)> WithdrawApplicationAsync(Guid applicationId, string applicantUserId)
        {
            // 1. Fetch the application from the database
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if (application == null)
            {
                return (false, "Application not found.", null);
            }

            // 2. Authorization check: Ensure the user owns this application
            if(application.ApplicantUserId != applicantUserId)
            {
                return (false, "You are not authorized to withdraw this application.", null);
            }

            // 3. State Validation: Prevent withdrawing if it's already in an invalid state
            if(application.Status == ApplicationStatus.Withdrawn)
            {
                return (false, "Application is already withdrawn.", null);
            }
            else if(application.Status == ApplicationStatus.NotSelected)
            {
                return (false, $"Cannot withdraw an application that is already {application.Status}.", null);
            }

            // 4. Update the state
            application.Status = ApplicationStatus.Withdrawn;
            application.UpdatedAt = DateTimeOffset.UtcNow;

            // 5. Save changes to the database
            _applicationRepository.Update(application);
            await _applicationRepository.SaveChangesAsync();

            // 6. Construct the DTO for successful state
            var statusDto = new ApplicationStatusDto
            {
                ApplicationId = application.Id,
                CurrentStatus = application.Status.ToString(),
                LastUpdatedAt = application.UpdatedAt,
                StatusMessage = "You have successfully withdrawn your application."
            };

            return (true, string.Empty, statusDto);
        }

        public async Task<(bool IsSuccess, string ErrorMessage, ApplicationStatusDto? Data)> ProcessDocumentReviewAsync(Guid applicationId, DocumentReviewResultDto reviewResult)
        {
            //1. Fetch the application
            var application = await _applicationRepository.GetFirstOrDefaultAsync(app => app.Id == applicationId);
            if(application == null)
            {
                return (false, "Application is not found", null);
            }

            // 2. State Validation: Check if the application is in a valid state for document review
            if(application.Status != ApplicationStatus.UnderDocumentVerification &&
                application.Status != ApplicationStatus.NeedsHumanReview)
            {
                return (false, $"Cannot process document review. Current status is {application.Status}.", null);
            }

            // 3. Apply the Business Rules based on the Reviewer Type and Decision
            if(reviewResult.ReviewerType == ReviewerType.AI && reviewResult.IsAgentUncertain)
            {
                // The AI Agent is not confident enough, escalate to a human reviewer
                application.Status = ApplicationStatus.NeedsHumanReview;
            }
            else if (reviewResult.IsAccepted)
            {
                // Both confident AI Agents and Human reviewers agreed the documents are valid
                application.Status = ApplicationStatus.AssessmentInProgress;   // Move to the next phase
            }
            else
            {
                // The documents were rejected
                if (string.IsNullOrWhiteSpace(reviewResult.RejectionReason))
                {
                    return (false, "A rejection reason must be provided when rejecting documents.", null);
                }

                application.Status = ApplicationStatus.DocumentRejected;
            }

            // 4. Update the audit timestamp
            application.UpdatedAt = DateTimeOffset.UtcNow;

            // 5. Save changes to the database
            _applicationRepository.Update(application);
            await _applicationRepository.SaveChangesAsync();

            // 6. Construct the successful DTO response
            var statusDto = new ApplicationStatusDto
            {
                ApplicationId = application.Id,
                CurrentStatus = application.Status.ToString(),
                LastUpdatedAt = application.UpdatedAt,
                StatusMessage = $"Document review processed successfully. New Status: {application.Status}"
            };

            return (true, string.Empty, statusDto);
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> MarkDocumentVerificationStartedAsync(Guid applicationId)
        {
            var application = await _applicationRepository
                .GetFirstOrDefaultAsync(item => item.Id == applicationId);

            if (application is null)
            {
                return (false, "Application not found.");
            }

            if (application.Status is not (
                ApplicationStatus.DocumentsRequired or
                ApplicationStatus.NeedsHumanReview or
                ApplicationStatus.Draft))
            {
                return (
                    false,
                    $"Cannot start document verification while application status is {application.Status}."
                );
            }

            application.Status =
                ApplicationStatus.UnderDocumentVerification;

            application.UpdatedAt = DateTimeOffset.UtcNow;

            _applicationRepository.Update(application);

            return (true, string.Empty);
        }

    }
}

