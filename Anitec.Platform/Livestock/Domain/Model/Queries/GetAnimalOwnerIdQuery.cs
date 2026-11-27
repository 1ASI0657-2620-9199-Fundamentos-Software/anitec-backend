namespace Anitec.Platform.Livestock.Domain.Model.Queries;

/// <summary>Finds the rancher that owns an animal through its herd. It does not check who asks.</summary>
public record GetAnimalOwnerIdQuery(int AnimalId);
