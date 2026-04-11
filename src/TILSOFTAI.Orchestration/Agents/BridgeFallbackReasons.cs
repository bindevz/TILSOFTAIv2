namespace TILSOFTAI.Agents;

public static class BridgeFallbackReasons
{
    public const string NoCapabilityMatch = "no_capability_match";
    public const string NoAdapterRegistry = "no_adapter_registry";
    public const string ExplicitLegacyFallback = "explicit_legacy_fallback";
    public const string UnsupportedGeneralRequest = "unsupported_general_request";
}
