using Anitec.Platform.Resources.Errors;
using Microsoft.Extensions.Localization;

namespace Anitec.Platform.Tests.Support;

public class FakeLocalizer : IStringLocalizer<ErrorMessages>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return Enumerable.Empty<LocalizedString>();
    }
}
