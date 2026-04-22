using FluentValidation;
using MassTransit;
using MediatR;
using Session.Application.Common;
using Session.Domain.Enums;
using Session.Domain.Interfaces;

namespace Session.Application.Sessions.Commands;

public record UpdateSessionCommand(
    Guid Id,
    string UserId,
    string Topic,
    DateTime StartTime,
    DateTime EndTime,
    SessionMode Mode) : IRequest;

public class UpdateSessionValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be after StartTime.");

        RuleFor(x => x.Mode)
            .IsInEnum();
    }
}

public class UpdateSessionHandler : IRequestHandler<UpdateSessionCommand>
{
    private readonly ISessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint; 

    public UpdateSessionHandler(
        ISessionRepository repository,
        IUnitOfWork unitOfWork, IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(UpdateSessionCommand request, CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.Id, ct);

        if (session is null)
            throw new NotFoundException(nameof(Session), request.Id);

        if (session.UserId != request.UserId)
            throw new ForbiddenException();

        session.Update(
            request.Topic,
            request.StartTime,
            request.EndTime,
            request.Mode);

        _repository.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new Contracts.Events.SessionUpdatedEvent(
           session.Id,
           session.UserId,
           session.DurationMin.Value,
           DateTime.UtcNow), ct);
    }
}