using Anitec.Platform.Devices.Domain.Model.Commands;
using Anitec.Platform.Devices.Domain.Model;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Shared.Application.Model;

namespace Anitec.Platform.Devices.Application.CommandServices;

public interface IDeviceCommandService
{
    Task<Result<Device>> Handle(CreateDeviceCommand command, CancellationToken cancellationToken);
    Task<Result<Device>> Handle(UpdateDeviceCommand command, CancellationToken cancellationToken);
    Task<Result> Handle(DeleteDeviceCommand command, CancellationToken cancellationToken);

    /// <summary>
    ///     Issues (or rotates) the API key of a device and returns it in plain text; it cannot be read again.
    /// </summary>
    Task<Result<string>> Handle(IssueDeviceApiKeyCommand command, CancellationToken cancellationToken);
}
