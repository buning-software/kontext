using Kontext.Domain;

namespace Kontext.Contracts;

/// <summary>Registers Kontext to start when the user signs in to Windows. The real state lives in Windows, so it is always read back from there.</summary>
public interface IStartupRegistration
{
    Task<StartupState> GetStateAsync();

    Task<StartupState> SetEnabledAsync(bool enabled);
}
