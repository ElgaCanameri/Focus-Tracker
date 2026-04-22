namespace Session.Application.Common
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string name, object id)
            : base($"{name} with id {id} was not found.") { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException()
            : base("You do not have permission to access this resource.") { }
    }

    public class RevokedException : Exception
    {
        public RevokedException()
            : base("This link has been revoked.") { }
    }
}
