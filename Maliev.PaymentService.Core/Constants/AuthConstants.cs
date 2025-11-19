namespace Maliev.PaymentService.Core.Constants;

/// <summary>
/// Authentication and authorization constants for service identity claims.
/// </summary>
public static class AuthConstants
{
    /// <summary>
    /// Claim types for service identity validation.
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Service identifier claim (identifies which internal service is making the request).
        /// </summary>
        public const string ServiceId = "service_id";

        /// <summary>
        /// Service name claim (human-readable service name).
        /// </summary>
        public const string ServiceName = "service_name";

        /// <summary>
        /// Service version claim (for tracking API versioning compatibility).
        /// </summary>
        public const string ServiceVersion = "service_version";

        /// <summary>
        /// Request context claim (additional context about the request origin).
        /// </summary>
        public const string RequestContext = "request_context";
    }

    /// <summary>
    /// Authorization policy names for different service access levels.
    /// </summary>
    public static class Policies
    {
        /// <summary>
        /// Policy requiring authenticated internal service.
        /// </summary>
        public const string InternalServicePolicy = "InternalServicePolicy";

        /// <summary>
        /// Policy requiring payment processing permissions.
        /// </summary>
        public const string PaymentProcessingPolicy = "PaymentProcessingPolicy";

        /// <summary>
        /// Policy requiring provider management permissions.
        /// </summary>
        public const string ProviderManagementPolicy = "ProviderManagementPolicy";

        /// <summary>
        /// Policy requiring refund permissions.
        /// </summary>
        public const string RefundPolicy = "RefundPolicy";
    }

    /// <summary>
    /// Known internal service identifiers.
    /// </summary>
    public static class Services
    {
        public const string OrderService = "order-service";
        public const string BookingService = "booking-service";
        public const string SubscriptionService = "subscription-service";
        public const string AdminService = "admin-service";
    }
}
