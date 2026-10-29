using Anitec.Platform.Profiles.Domain.Model.Commands;
using Anitec.Platform.Profiles.Interfaces.Rest.Resources;

// Added for ArgumentNullException

namespace Anitec.Platform.Profiles.Interfaces.Rest.Transform;

/// <summary>
///     Assembler responsible for transforming a <see cref="CreateProfileResource" /> into a
///     <see cref="CreateProfileCommand" />.
/// </summary>
public static class CreateProfileCommandFromResourceAssembler
{
    /// <summary>
    ///     Converts a <see cref="CreateProfileResource" /> to a <see cref="CreateProfileCommand" />.
    /// </summary>
    /// <param name="resource">
    ///     The <see cref="CreateProfileResource" /> containing the data for creating a profile. Must not be null.
    /// </param>
    /// <returns>
    ///     A new <see cref="CreateProfileCommand" /> instance.
    /// </returns>
    /// <param name="ownerId">The id of the user that creates the profile.</param>
    /// <exception cref="ArgumentNullException">Thrown if the input <paramref name="resource" /> is null.</exception>
    public static CreateProfileCommand ToCommandFromResource(CreateProfileResource resource, int? ownerId = null)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource),
                "CreateProfileResource cannot be null when converting to command.");
        return new CreateProfileCommand(
            resource.FirstName,
            resource.LastName,
            resource.Email,
            resource.Street,
            resource.Number,
            resource.City,
            resource.PostalCode,
            resource.Country,
            ownerId
        );
    }
}
