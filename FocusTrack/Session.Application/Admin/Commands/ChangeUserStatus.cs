using Contracts.Events;
using FluentValidation;
using MassTransit;
using MediatR;
using Session.Application.Common;
using Session.Domain.Entities;
using Session.Domain.Enums;
using Session.Domain.Interfaces;

namespace Session.Application.Admin.Commands;
//the command
//with mediatR, the caller doesn't need to know about the handler, it just sends the command and the handler executes the logic
public record ChangeUserStatusCommand(
    string UserId,
    UserStatus NewStatus,
    string PerformedBy) : IRequest;

//the handler is the actual logic that executes when the command is sent
public class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.PerformedBy).NotEmpty();
    }
}
public class ChangeUserStatusHandler : IRequestHandler<ChangeUserStatusCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ChangeUserStatusHandler(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(ChangeUserStatusCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);

        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        var oldStatus = user.Status;

        // update status
        user.ChangeStatus(request.NewStatus);
        _userRepository.Update(user);

        // record in audit table
        var auditLog = AuditLog.Create(
            action: "UserStatusChanged",
            targetId: user.Id.ToString(),
            targetType: "User",
            performedBy: request.PerformedBy,
            details: $"Status changed from {oldStatus} to {request.NewStatus}");

        await _auditLogRepository.AddAsync(auditLog, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        // publish event
        await _publishEndpoint.Publish(new UserStatusChangedEvent(
            user.Id.ToString(),
            oldStatus.ToString(),
            request.NewStatus.ToString(),
            DateTime.UtcNow), ct);
    }
}