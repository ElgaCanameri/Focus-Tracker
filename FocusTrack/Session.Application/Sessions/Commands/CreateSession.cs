using FluentValidation;
using MediatR;
using Session.Application.Common;
using Session.Domain.Enums;
using Session.Domain.Interfaces;
using MassTransit;

namespace Session.Application.Sessions.Commands;

public record CreateSessionCommand(
    string UserId,
    string Topic,
    DateTime StartTime,
    DateTime EndTime,
    SessionMode Mode) : IRequest<Guid>;

//fluent validator 
public class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.StartTime)
            .NotEmpty();

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be after StartTime.");

        RuleFor(x => x.Mode)
            .IsInEnum();
    }
}

public class CreateSessionHandler : IRequestHandler<CreateSessionCommand, Guid>
{
    private readonly ISessionRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateSessionHandler(
        ISessionRepository repository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _repository = repository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Guid> Handle(CreateSessionCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByExternalIdAsync(request.UserId, ct);

        if (user == null)
        {
            user = Domain.Entities.User.Create(request.UserId);
            await _userRepository.AddAsync(user, ct);
        }
        if (!user.CanAuthenticate())
        {
            throw new UnauthorizedAccessException("User account is not active.");
        }
        var session = Domain.Entities.Session.Create(
            request.UserId,
            request.Topic,
            request.StartTime,
            request.EndTime,
            request.Mode);

        await _repository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // publish to RabbitMQ → RewardWorker listens
        await _publishEndpoint.Publish(new Contracts.Events.SessionCreatedEvent(
            session.Id,
            session.UserId,
            session.DurationMin.Value,
            DateTime.UtcNow), ct);

        return session.Id;
    }
}