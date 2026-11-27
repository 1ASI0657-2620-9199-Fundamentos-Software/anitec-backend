namespace Anitec.Platform.Profiles.Domain.Model.Queries;

/// <summary>The profile that a user created for itself. It does not check who asks, so it is for internal use.</summary>
public record GetProfileByOwnerIdQuery(int OwnerId);
