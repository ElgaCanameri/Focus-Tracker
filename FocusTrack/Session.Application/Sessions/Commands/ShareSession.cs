using FluentValidation;
using MassTransit;
using MediatR;
using Session.Application.Common;
using Session.Domain.Entities;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Commands;

public record ShareSessionCommand(
    Guid SessionId,
    string RequestingUserId,
    List<string> RecipientUserIds) : IRequest;

public class ShareSessionValidator : AbstractValidator<ShareSessionCommand>
{
    public ShareSessionValidator()
    {
        RuleFor(x => x.RecipientUserIds)
            .NotEmpty()
            .WithMessage("At least one recipient is required.");
    }
}

public class ShareSessionHandler : IRequestHandler<ShareSessionCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionShareRepository _shareRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ShareSessionHandler(
        ISessionRepository sessionRepository,
        ISessionShareRepository shareRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _sessionRepository = sessionRepository;
        _shareRepository = shareRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(ShareSessionCommand request, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.SessionId);

        if (session.UserId != request.RequestingUserId)
            throw new ForbiddenException();

        foreach (var recipientId in request.RecipientUserIds)
        {
            // avoid duplicate shares
            var alreadyShared = await _shareRepository.ExistsAsync(
                request.SessionId, recipientId, ct);

            if (alreadyShared)
                continue;

            var share = SessionShare.Create(request.SessionId, recipientId);
            await _shareRepository.AddAsync(share, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // publish to RabbitMQ → Notification Service listens
        await _publishEndpoint.Publish(new Contracts.Events.SessionSharedEvent(
            request.SessionId,
            request.RequestingUserId,
            request.RecipientUserIds,
            DateTime.UtcNow), ct);
    }
}