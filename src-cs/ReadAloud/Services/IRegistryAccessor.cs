namespace ReadAloud.Services;

public interface IRegistryAccessor
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}
