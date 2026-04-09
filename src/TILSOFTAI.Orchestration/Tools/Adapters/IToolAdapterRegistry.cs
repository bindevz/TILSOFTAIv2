namespace TILSOFTAI.Tools.Abstractions;

public interface IToolAdapterRegistry
{
    IToolAdapter Resolve(string adapterType);
    IToolAdapter ResolveForCapability(string capabilityKey, string systemId);
}
