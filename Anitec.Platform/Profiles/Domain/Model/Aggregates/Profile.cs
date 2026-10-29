using Anitec.Platform.Profiles.Domain.Model.Commands;
using Anitec.Platform.Profiles.Domain.Model.ValueObjects;

namespace Anitec.Platform.Profiles.Domain.Model.Aggregates;

/// <summary>
///     Profile Aggregate Root
/// </summary>
/// <remarks>
///     This class represents the Profile aggregate root.
///     It contains the properties and methods to manage the profile information.
/// </remarks>
public partial class Profile
{
    public Profile()
    {
        Name = new PersonName();
        Email = new EmailAddress();
        Address = new StreetAddress();
    }

    public Profile(string firstName, string lastName, string email, string street, string city, string state,
        string postalCode, string country)
    {
        Name = new PersonName(firstName, lastName);
        Email = new EmailAddress(email);
        Address = new StreetAddress(street, city, state, postalCode, country);
    }

    public Profile(CreateProfileCommand command)
    {
        Name = new PersonName(command.FirstName, command.LastName);
        Email = new EmailAddress(command.Email);
        Address = new StreetAddress(command.Street, command.Number, command.City, command.PostalCode, command.Country);
        OwnerId = command.OwnerId;
    }

    public int Id { get; }

    /// <summary>
    ///     Id of the user that created the profile. A profile without an owner is visible to nobody through the API.
    /// </summary>
    public int? OwnerId { get; private set; }

    public PersonName Name { get; }
    public EmailAddress Email { get; }
    public StreetAddress Address { get; }

    public string FullName => Name.FullName;
    public string EmailAddress => Email.Address;
    public string StreetAddress => Address.FullAddress;
}
