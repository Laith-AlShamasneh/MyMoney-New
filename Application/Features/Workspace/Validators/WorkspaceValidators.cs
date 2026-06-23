using Application.Features.Workspace.DTOs;
using FluentValidation;
using Shared.Constants;
using Shared.Enums.Workspace;

namespace Application.Features.Workspace.Validators;

public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Workspace.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Workspace.NameTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description is not null)
            .WithMessage(MessageKeys.Workspace.DescriptionTooLong);

        RuleFor(x => x.TypeId)
            .Must(v => Enum.IsDefined(typeof(WorkspaceType), v))
            .WithMessage(MessageKeys.Workspace.InvalidTypeId);

        RuleFor(x => x.CurrencyCode)
            .MaximumLength(10).When(x => x.CurrencyCode is not null)
            .WithMessage(MessageKeys.Workspace.CurrencyCodeTooLong);

        RuleFor(x => x.Timezone)
            .MaximumLength(50).When(x => x.Timezone is not null)
            .WithMessage(MessageKeys.Workspace.TimezoneTooLong);

        RuleFor(x => x.Color)
            .MaximumLength(10).When(x => x.Color is not null)
            .WithMessage(MessageKeys.Workspace.ColorTooLong);
    }
}

public sealed class UpdateWorkspaceValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Workspace.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Workspace.NameTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description is not null)
            .WithMessage(MessageKeys.Workspace.DescriptionTooLong);

        RuleFor(x => x.CurrencyCode)
            .MaximumLength(10).When(x => x.CurrencyCode is not null)
            .WithMessage(MessageKeys.Workspace.CurrencyCodeTooLong);

        RuleFor(x => x.Timezone)
            .MaximumLength(50).When(x => x.Timezone is not null)
            .WithMessage(MessageKeys.Workspace.TimezoneTooLong);

        RuleFor(x => x.Color)
            .MaximumLength(10).When(x => x.Color is not null)
            .WithMessage(MessageKeys.Workspace.ColorTooLong);
    }
}

public sealed class GetWorkspaceValidator : AbstractValidator<GetWorkspaceRequest>
{
    public GetWorkspaceValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class DeleteWorkspaceValidator : AbstractValidator<DeleteWorkspaceRequest>
{
    public DeleteWorkspaceValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.TargetUserId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidTargetUserId);

        RuleFor(x => x.NewRoleId)
            .Must(v => Enum.IsDefined(typeof(WorkspaceRoleId), v))
            .WithMessage(MessageKeys.Workspace.InvalidRoleId);
    }
}

public sealed class SuspendMemberValidator : AbstractValidator<SuspendMemberRequest>
{
    public SuspendMemberValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.TargetUserId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidTargetUserId);
    }
}

public sealed class ReinstateMemberValidator : AbstractValidator<ReinstateMemberRequest>
{
    public ReinstateMemberValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.TargetUserId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidTargetUserId);
    }
}

public sealed class RemoveMemberValidator : AbstractValidator<RemoveMemberRequest>
{
    public RemoveMemberValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.TargetUserId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidTargetUserId);
    }
}

public sealed class LeaveWorkspaceValidator : AbstractValidator<LeaveWorkspaceRequest>
{
    public LeaveWorkspaceValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class SendInvitationValidator : AbstractValidator<SendInvitationRequest>
{
    public SendInvitationValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(MessageKeys.Workspace.EmailRequired)
            .EmailAddress().WithMessage(MessageKeys.Workspace.InvalidEmail);

        RuleFor(x => x.RoleId)
            .Must(v => Enum.IsDefined(typeof(WorkspaceRoleId), v))
            .WithMessage(MessageKeys.Workspace.InvalidRoleId);
    }
}

public sealed class CancelInvitationValidator : AbstractValidator<CancelInvitationRequest>
{
    public CancelInvitationValidator()
    {
        RuleFor(x => x.InvitationId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidInvitationId);

        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(MessageKeys.Workspace.TokenRequired);
    }
}

public sealed class RejectInvitationValidator : AbstractValidator<RejectInvitationRequest>
{
    public RejectInvitationValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(MessageKeys.Workspace.TokenRequired);
    }
}

public sealed class GetInvitationsByTokenValidator : AbstractValidator<GetInvitationsByTokenRequest>
{
    public GetInvitationsByTokenValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(MessageKeys.Workspace.TokenRequired);
    }
}

public sealed class GetPermissionsValidator : AbstractValidator<GetPermissionsRequest>
{
    public GetPermissionsValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class GetActivityValidator : AbstractValidator<GetActivityRequest>
{
    public GetActivityValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage(MessageKeys.Workspace.PageSizeInvalid);
    }
}

public sealed class GetMembersValidator : AbstractValidator<GetMembersRequest>
{
    public GetMembersValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}

public sealed class GetInvitationsValidator : AbstractValidator<GetInvitationsRequest>
{
    public GetInvitationsValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0).WithMessage(MessageKeys.Workspace.InvalidWorkspaceId);
    }
}
