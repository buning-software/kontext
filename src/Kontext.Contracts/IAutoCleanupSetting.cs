namespace Kontext.Contracts;

/// <summary>Whether Kontext clears the sessions of closed issues in the background. It is on until the user turns it off.</summary>
public interface IAutoCleanupSetting
{
    bool IsEnabled { get; set; }
}
